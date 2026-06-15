using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using ParserProcedure = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds <see cref="NavigationGraph"/> per Phase 6.7 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>: walks every
/// <see cref="ResolvedModule"/>'s procedure bodies looking for
/// <c>DoCmd.&lt;NavigationVerb&gt;</c> dispatch calls, extracts
/// statically-resolvable target form/report names from string-literal
/// arguments, and surfaces dynamic targets as
/// <see cref="UnresolvedNavigationTarget"/> entries. Top-level
/// surfaces (ribbon, switchboard, macros, default form) come in via
/// <see cref="NavigationGraphInputs"/> from the metadata extractor.
/// </summary>
/// <remarks>
/// Mirrors the structural / deterministic posture of
/// <see cref="FunctionClassificationBuilder"/>: the inputs are the
/// only state consulted (no environment, no globals), and every output
/// collection is sorted before return so identical inputs yield
/// byte-identical graphs across runs. The
/// <see cref="CrossReferenceIndex"/> is accepted today for parity
/// with the FunctionClassification API and to keep the door open for
/// reverse-edge enrichment (who else navigates to this form?) without
/// rewriting call-sites; the current builder does not consult it.
/// </remarks>
public static class NavigationGraphBuilder
{
    /// <summary>
    /// Reconstructs the navigation graph across the supplied modules.
    /// </summary>
    /// <param name="modules">
    /// Resolved modules to walk. Each module's procedure bodies are
    /// scanned for <c>DoCmd.OpenForm</c> / <c>DoCmd.OpenReport</c> /
    /// <c>DoCmd.Close</c> / <c>DoCmd.BrowseTo</c> dispatches.
    /// </param>
    /// <param name="crossReferences">
    /// Project-wide cross-reference index. Currently unused by the
    /// builder; accepted for API parity with
    /// <see cref="FunctionClassificationBuilder.Build"/> and for
    /// future reverse-edge enrichment.
    /// </param>
    /// <param name="inputs">
    /// Optional ribbon / switchboard / macro / default-form inputs
    /// from the metadata extractor. When null, the resulting graph
    /// has no top-level surfaces and no entry points — the VBA-side
    /// edges are still produced.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modules"/> or
    /// <paramref name="crossReferences"/> is null, or
    /// <paramref name="modules"/> contains a null entry.
    /// </exception>
    public static NavigationGraph Build(
        IReadOnlyList<ResolvedModule> modules,
        CrossReferenceIndex crossReferences,
        NavigationGraphInputs? inputs = null)
    {
        if (modules is null) throw new ArgumentNullException(nameof(modules));
        if (crossReferences is null) throw new ArgumentNullException(nameof(crossReferences));
        // crossReferences is reserved for future reverse-edge work;
        // the underscore-discard quiets the analyzer without losing
        // the parameter from the signature contract.
        _ = crossReferences;

        var edges = new List<NavigationEdge>();
        var unresolved = new List<UnresolvedNavigationTarget>();

        foreach (var module in modules)
        {
            if (module is null)
                throw new ArgumentNullException(nameof(modules), "Module list contains a null entry.");
            CollectFromModule(module, edges, unresolved);
        }

        var sortedEdges = SortEdges(edges);
        var sortedUnresolved = SortUnresolved(unresolved);

        var macros = inputs?.Macros is { } m ? (IReadOnlyList<MacroChain>)m.ToArray() : Array.Empty<MacroChain>();
        var entryPoints = BuildEntryPoints(inputs, macros);
        var ribbonEntryPoints = BuildRibbonEntryPoints(inputs?.Ribbon, modules);

        return new NavigationGraph
        {
            EntryPoints = entryPoints,
            Edges = sortedEdges,
            UnresolvedTargets = sortedUnresolved,
            Ribbon = inputs?.Ribbon,
            RibbonEntryPoints = ribbonEntryPoints,
            Switchboard = inputs?.Switchboard,
            Macros = macros,
        };
    }

    // ── Per-module walk ────────────────────────────────────────────

    private static void CollectFromModule(
        ResolvedModule module,
        List<NavigationEdge> edges,
        List<UnresolvedNavigationTarget> unresolved)
    {
        var moduleName = module.ModuleScope.ModuleName;
        foreach (var item in module.Ast.Items)
        {
            if (item is not ParserProcedure proc) continue;
            var kind = KindOf(proc);
            var visitor = new NavigationVisitor(moduleName, proc.Name, kind);
            foreach (var statement in proc.Body)
                visitor.Visit(statement);
            edges.AddRange(visitor.Edges);
            unresolved.AddRange(visitor.Unresolved);
        }
    }

    // ── Body walker ────────────────────────────────────────────────

    // Walks one procedure body looking for DoCmd.<verb>(...) dispatch
    // sites. Both the bare-call form (`DoCmd.OpenForm "frmFoo"`) and
    // the parenthesised form (`DoCmd.OpenForm("frmFoo")`) reach this
    // visitor as CallStatement nodes; the difference is whether the
    // arguments live on CallStatement.BareArguments or are nested
    // inside an IndexOrCallExpression target. Both shapes are
    // dispatched through ExtractArguments so the rest of the rule
    // engine sees a uniform argument list.
    private sealed class NavigationVisitor : VbaSyntaxWalker
    {
        public List<NavigationEdge> Edges { get; } = new();
        public List<UnresolvedNavigationTarget> Unresolved { get; } = new();

        private readonly string _moduleName;
        private readonly string _procedureName;
        private readonly ProcedureKind _procedureKind;

        public NavigationVisitor(string moduleName, string procedureName, ProcedureKind procedureKind)
        {
            _moduleName = moduleName;
            _procedureName = procedureName;
            _procedureKind = procedureKind;
        }

        public override void VisitCallStatement(CallStatement n)
        {
            HandleDispatch(n.Target, n.BareArguments, n.Provenance);
            base.VisitCallStatement(n);
        }

        // Some dispatches surface as ExpressionStatement-equivalents
        // (e.g., a function-style use whose result is discarded). The
        // grammar models those uses as CallStatement too — there is no
        // separate ExpressionStatement node — so VisitCallStatement is
        // the only entry we need.

        private void HandleDispatch(
            Expression target,
            IReadOnlyList<Argument> bareArguments,
            Provenance provenance)
        {
            // Unwrap one IndexOrCall layer so `DoCmd.OpenForm("frmFoo")`
            // surfaces the same as `DoCmd.OpenForm "frmFoo"`. The
            // grammar puts parenthesised args on the IndexOrCall, not on
            // the CallStatement's BareArguments list.
            Expression effectiveTarget = target;
            IReadOnlyList<Argument> effectiveArgs = bareArguments;
            if (bareArguments.Count == 0 && target is IndexOrCallExpression ioc)
            {
                effectiveTarget = ioc.Target;
                effectiveArgs = ioc.Arguments;
            }

            if (effectiveTarget is not MemberAccessExpression member) return;
            if (!IsDoCmdTarget(member.Target)) return;

            if (!TryParseAction(member.MemberName, out var action)) return;

            var (line, column) = LocationOf(provenance);

            // Close has the form `DoCmd.Close [acForm|acReport, name]`.
            // The first argument is the object kind; the second is the
            // name. With zero or one positional args the object being
            // closed is the active form/report — there is no statically
            // resolvable target name and we suppress the edge entirely
            // rather than emit a no-target unresolved record (which
            // would confuse the manual-review aggregator about whether
            // the *target* was unresolvable or simply absent).
            if (action == NavigationAction.Close)
            {
                HandleClose(effectiveArgs, line, column);
                return;
            }

            // OpenForm / OpenReport / BrowseTo all take the target
            // name as the first positional argument.
            var positional = PositionalValues(effectiveArgs);
            if (positional.Count == 0)
            {
                Unresolved.Add(new UnresolvedNavigationTarget
                {
                    SourceModule = _moduleName,
                    SourceProcedure = _procedureName,
                    SourceProcedureKind = _procedureKind,
                    SourceLine = line,
                    SourceColumn = column,
                    Action = action,
                    Reason = MissingTargetReason,
                    TargetExpressionText = null,
                });
                return;
            }

            var nameExpr = positional[0];
            // A null entry in the positional list is a MissingArgument
            // — `DoCmd.OpenForm , "filter"` — which is the same kind
            // of "the dispatch named no target" failure as zero
            // arguments above.
            if (nameExpr is null)
            {
                Unresolved.Add(new UnresolvedNavigationTarget
                {
                    SourceModule = _moduleName,
                    SourceProcedure = _procedureName,
                    SourceProcedureKind = _procedureKind,
                    SourceLine = line,
                    SourceColumn = column,
                    Action = action,
                    Reason = MissingTargetReason,
                    TargetExpressionText = null,
                });
                return;
            }
            if (TryAsStringLiteral(nameExpr, out var literalValue))
            {
                Edges.Add(new NavigationEdge
                {
                    SourceModule = _moduleName,
                    SourceProcedure = _procedureName,
                    SourceProcedureKind = _procedureKind,
                    SourceLine = line,
                    SourceColumn = column,
                    Action = action,
                    TargetKind = action == NavigationAction.OpenReport
                        ? NavigationTargetKind.Report
                        : NavigationTargetKind.Form,
                    TargetName = literalValue,
                    WhereCondition = ExtractOptionalArgText(positional, WhereConditionIndex(action)),
                    OpenArgs = action == NavigationAction.OpenForm
                        ? ExtractOptionalArgText(positional, OpenArgsIndex)
                        : null,
                });
            }
            else
            {
                Unresolved.Add(new UnresolvedNavigationTarget
                {
                    SourceModule = _moduleName,
                    SourceProcedure = _procedureName,
                    SourceProcedureKind = _procedureKind,
                    SourceLine = line,
                    SourceColumn = column,
                    Action = action,
                    Reason = NonLiteralTargetReason,
                    TargetExpressionText = ExpressionTextFormatter.Format(nameExpr),
                });
            }
        }

        // DoCmd.Close handling — the first positional argument is the
        // object kind enum (acForm / acReport / acQuery / etc.) which
        // we recognise structurally to decide TargetKind.
        private void HandleClose(IReadOnlyList<Argument> args, int line, int column)
        {
            var positional = PositionalValues(args);
            // No args at all: closes the active object — not a
            // navigation event we model as an edge.
            if (positional.Count < 2) return;

            var kindExpr = positional[0];
            var nameExpr = positional[1];

            // Either positional slot may be a MissingArgument hole
            // (`DoCmd.Close , "frmFoo"` or `DoCmd.Close acForm,`).
            // Treat either as missing-target — the dispatch carries
            // no actionable form/report identity.
            if (nameExpr is null)
            {
                Unresolved.Add(new UnresolvedNavigationTarget
                {
                    SourceModule = _moduleName,
                    SourceProcedure = _procedureName,
                    SourceProcedureKind = _procedureKind,
                    SourceLine = line,
                    SourceColumn = column,
                    Action = NavigationAction.Close,
                    Reason = MissingTargetReason,
                    TargetExpressionText = null,
                });
                return;
            }

            var targetKind = kindExpr is null
                ? NavigationTargetKind.Unknown
                : ResolveCloseTargetKind(kindExpr);
            if (TryAsStringLiteral(nameExpr, out var literalValue))
            {
                Edges.Add(new NavigationEdge
                {
                    SourceModule = _moduleName,
                    SourceProcedure = _procedureName,
                    SourceProcedureKind = _procedureKind,
                    SourceLine = line,
                    SourceColumn = column,
                    Action = NavigationAction.Close,
                    TargetKind = targetKind,
                    TargetName = literalValue,
                });
            }
            else
            {
                Unresolved.Add(new UnresolvedNavigationTarget
                {
                    SourceModule = _moduleName,
                    SourceProcedure = _procedureName,
                    SourceProcedureKind = _procedureKind,
                    SourceLine = line,
                    SourceColumn = column,
                    Action = NavigationAction.Close,
                    Reason = NonLiteralTargetReason,
                    TargetExpressionText = ExpressionTextFormatter.Format(nameExpr),
                });
            }
        }

        private static NavigationTargetKind ResolveCloseTargetKind(Expression kindExpr)
        {
            // The kind argument is almost always a bare identifier
            // referring to an Access enum constant — `acForm`,
            // `acReport`, etc. We recognise it textually rather than
            // via the resolver because the Access enum constants are
            // known stable names and the cost of plumbing the resolver
            // into this path for one identifier is not worth the
            // marginal precision.
            if (kindExpr is not IdentifierExpression id) return NavigationTargetKind.Unknown;
            return id.Name switch
            {
                var n when string.Equals(n, "acForm", StringComparison.OrdinalIgnoreCase)
                    => NavigationTargetKind.Form,
                var n when string.Equals(n, "acReport", StringComparison.OrdinalIgnoreCase)
                    => NavigationTargetKind.Report,
                _ => NavigationTargetKind.Unknown,
            };
        }
    }

    // ── DoCmd recognition ─────────────────────────────────────────

    // True when the target expression names DoCmd (case-insensitive).
    // Both `DoCmd.OpenForm` and `Application.DoCmd.OpenForm` are
    // recognised by checking for an identifier named DoCmd directly,
    // or a member-access whose final name is DoCmd.
    private static bool IsDoCmdTarget(Expression target) => target switch
    {
        IdentifierExpression id => string.Equals(id.Name, "DoCmd", StringComparison.OrdinalIgnoreCase),
        MemberAccessExpression m => string.Equals(m.MemberName, "DoCmd", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static bool TryParseAction(string memberName, out NavigationAction action)
    {
        // Case-insensitive comparisons because VBA names are case-
        // insensitive — `docmd.openform` is the same dispatch as
        // `DoCmd.OpenForm` and consumers should not see a
        // false-negative on lower-case sources.
        if (string.Equals(memberName, "OpenForm", StringComparison.OrdinalIgnoreCase))
        { action = NavigationAction.OpenForm; return true; }
        if (string.Equals(memberName, "OpenReport", StringComparison.OrdinalIgnoreCase))
        { action = NavigationAction.OpenReport; return true; }
        if (string.Equals(memberName, "Close", StringComparison.OrdinalIgnoreCase))
        { action = NavigationAction.Close; return true; }
        if (string.Equals(memberName, "BrowseTo", StringComparison.OrdinalIgnoreCase))
        { action = NavigationAction.BrowseTo; return true; }
        action = default;
        return false;
    }

    // ── Argument helpers ──────────────────────────────────────────

    private const string MissingTargetReason = "missing-target";
    private const string NonLiteralTargetReason = "non-literal-target";

    // Both OpenForm and OpenReport place WhereCondition at the
    // fourth positional slot (index 3) per the Access docs:
    //   OpenForm(FormName, View, FilterName, WhereCondition, ...)
    //   OpenReport(ReportName, View, FilterName, WhereCondition, ...)
    // BrowseTo does not carry a where-condition. Returns -1 to mean
    // "no where-condition slot".
    private static int WhereConditionIndex(NavigationAction action) => action switch
    {
        NavigationAction.OpenForm => 3,
        NavigationAction.OpenReport => 3,
        _ => -1,
    };

    // The OpenArgs slot on OpenForm — seventh positional argument.
    private const int OpenArgsIndex = 6;

    private static IReadOnlyList<Expression?> PositionalValues(IReadOnlyList<Argument> args)
    {
        // Map the argument list to a positional slot list, following
        // VBA's "missing argument leaves a hole" semantics. Named
        // arguments are out-of-order extras and do not fill positional
        // slots. The positional list is what the Access docs refer to
        // when they say "the fourth argument is WhereCondition".
        var result = new List<Expression?>();
        foreach (var arg in args)
        {
            switch (arg)
            {
                case PositionalArgument p:
                    result.Add(p.Value);
                    break;
                case MissingArgument:
                    result.Add(null);
                    break;
                case NamedArgument:
                    // Named arguments do not advance the positional
                    // counter; they are tagged extras the consumer can
                    // pull out separately if needed. We do not currently
                    // support resolving WhereCondition supplied as a
                    // named arg — adding that is a Lean By Default
                    // future-work cut.
                    break;
            }
        }
        return result;
    }

    private static string? ExtractOptionalArgText(IReadOnlyList<Expression?> positional, int index)
    {
        if (index < 0 || index >= positional.Count) return null;
        var expr = positional[index];
        if (expr is null) return null;
        return ExpressionTextFormatter.Format(expr);
    }

    private static bool TryAsStringLiteral(Expression? expr, out string value)
    {
        if (expr is LiteralExpression lit && lit.Kind == AccessHarvester.Vba.Parser.Ast.LiteralKind.String)
        {
            value = UnquoteString(lit.Text);
            return true;
        }
        value = string.Empty;
        return false;
    }

    // VBA string literals carry their quotes in `Text` per
    // AstBuilder.BuildLiteral. Strip the surrounding quotes and
    // un-escape doubled quotes (`""` inside a literal is one quote
    // character) so consumers see the canonical content.
    private static string UnquoteString(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            var inner = raw.Substring(1, raw.Length - 2);
            return inner.Replace("\"\"", "\"");
        }
        return raw;
    }

    // ── Expression-to-text formatter (small subset) ───────────────

    // Renders the unresolved navigation target's source expression
    // back to a single-line approximation of the original VBA so the
    // manual-review aggregator does not need to re-read the source.
    // Not a faithful pretty-printer: parens, operator spacing, and
    // exotic shapes are normalised to a readable form. Unknown node
    // types collapse to "<expr>" — explicit so a future grammar
    // addition surfaces as a "missing case" rather than producing
    // misleading text.
    private static class ExpressionTextFormatter
    {
        public static string Format(Expression expr)
        {
            var sb = new StringBuilder();
            Write(sb, expr);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, Expression expr)
        {
            switch (expr)
            {
                case LiteralExpression lit:
                    sb.Append(lit.Text);
                    break;
                case IdentifierExpression id:
                    sb.Append(id.Name);
                    if (id.TypeSuffix is not null) sb.Append(id.TypeSuffix);
                    break;
                case MeExpression:
                    sb.Append("Me");
                    break;
                case MemberAccessExpression m:
                    Write(sb, m.Target);
                    sb.Append('.').Append(m.MemberName);
                    break;
                case BangAccessExpression b:
                    Write(sb, b.Target);
                    sb.Append('!').Append(b.MemberName);
                    break;
                case ImplicitWithMemberExpression w:
                    sb.Append('.').Append(w.MemberName);
                    break;
                case ImplicitWithBangExpression w:
                    sb.Append('!').Append(w.MemberName);
                    break;
                case ParenthesizedExpression p:
                    sb.Append('(');
                    Write(sb, p.Inner);
                    sb.Append(')');
                    break;
                case BinaryExpression bin:
                    Write(sb, bin.Left);
                    sb.Append(' ').Append(OperatorText(bin.Operator)).Append(' ');
                    Write(sb, bin.Right);
                    break;
                case UnaryExpression un:
                    sb.Append(UnaryText(un.Operator));
                    Write(sb, un.Operand);
                    break;
                case IndexOrCallExpression ioc:
                    Write(sb, ioc.Target);
                    sb.Append('(');
                    for (int i = 0; i < ioc.Arguments.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        WriteArgument(sb, ioc.Arguments[i]);
                    }
                    sb.Append(')');
                    break;
                case TrueExpression:
                    sb.Append("True");
                    break;
                case FalseExpression:
                    sb.Append("False");
                    break;
                case NothingExpression:
                    sb.Append("Nothing");
                    break;
                case NullExpression:
                    sb.Append("Null");
                    break;
                case EmptyExpression:
                    sb.Append("Empty");
                    break;
                default:
                    // Unknown / NewExpression / AddressOfExpression /
                    // TypeOfExpression / DebugExpression — none are
                    // realistic first-arguments to DoCmd dispatches,
                    // and emitting a placeholder keeps the formatter
                    // total without inventing text.
                    sb.Append("<expr>");
                    break;
            }
        }

        private static void WriteArgument(StringBuilder sb, Argument arg)
        {
            switch (arg)
            {
                case PositionalArgument p:
                    Write(sb, p.Value);
                    break;
                case NamedArgument n:
                    sb.Append(n.Name).Append(":=");
                    Write(sb, n.Value);
                    break;
                case MissingArgument:
                    // VBA's `f(a, , b)` — the empty slot renders as
                    // empty between commas to match source spelling.
                    break;
            }
        }

        private static string OperatorText(BinaryOperator op) => op switch
        {
            BinaryOperator.Power => "^",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.IntDivide => "\\",
            BinaryOperator.Modulo => "Mod",
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Concat => "&",
            BinaryOperator.Equal => "=",
            BinaryOperator.NotEqual => "<>",
            BinaryOperator.Less => "<",
            BinaryOperator.Greater => ">",
            BinaryOperator.LessOrEqual => "<=",
            BinaryOperator.GreaterOrEqual => ">=",
            BinaryOperator.Like => "Like",
            BinaryOperator.Is => "Is",
            BinaryOperator.And => "And",
            BinaryOperator.Or => "Or",
            BinaryOperator.Xor => "Xor",
            BinaryOperator.Eqv => "Eqv",
            BinaryOperator.Imp => "Imp",
            _ => "?",
        };

        private static string UnaryText(UnaryOperator op) => op switch
        {
            UnaryOperator.Plus => "+",
            UnaryOperator.Minus => "-",
            UnaryOperator.Not => "Not ",
            _ => "?",
        };
    }

    // ── Procedure-kind discrimination (mirrors FunctionClassificationBuilder) ──

    private static ProcedureKind KindOf(ParserProcedure proc) => proc switch
    {
        SubProcedure => ProcedureKind.Sub,
        FunctionProcedure => ProcedureKind.Function,
        PropertyProcedure { Kind: PropertyKind.Get } => ProcedureKind.PropertyGet,
        PropertyProcedure { Kind: PropertyKind.Let } => ProcedureKind.PropertyLet,
        PropertyProcedure { Kind: PropertyKind.Set } => ProcedureKind.PropertySet,
        _ => throw new InvalidOperationException(
            $"Unknown parser ProcedureDeclaration subtype: {proc.GetType().FullName}"),
    };

    // ── Provenance helpers ────────────────────────────────────────

    private static (int line, int column) LocationOf(Provenance p)
    {
        // Prefer the Range start when present (post-Range population);
        // fall back to the bare Line/Column scalars during the parser's
        // mid-transition window. Same pattern used by ComplexityCollector
        // to remain forward-compatible with both shapes.
        if (p.Range is { } range)
            return (range.Start.Line, range.Start.Column);
        return (p.Line, p.Column);
    }

    // ── Sorting (determinism guards) ──────────────────────────────

    private static IReadOnlyList<NavigationEdge> SortEdges(List<NavigationEdge> edges)
    {
        edges.Sort(static (a, b) =>
        {
            int c = StringComparer.OrdinalIgnoreCase.Compare(a.SourceModule, b.SourceModule);
            if (c != 0) return c;
            c = StringComparer.OrdinalIgnoreCase.Compare(a.SourceProcedure, b.SourceProcedure);
            if (c != 0) return c;
            c = a.SourceLine.CompareTo(b.SourceLine);
            if (c != 0) return c;
            c = a.SourceColumn.CompareTo(b.SourceColumn);
            if (c != 0) return c;
            // Stable secondary on action / target so two dispatches
            // sharing an exact location (theoretically possible if the
            // parser ever emits multiple statements at one position)
            // still produce a deterministic ordering.
            c = ((int)a.Action).CompareTo((int)b.Action);
            if (c != 0) return c;
            return StringComparer.OrdinalIgnoreCase.Compare(a.TargetName, b.TargetName);
        });
        return edges;
    }

    private static IReadOnlyList<UnresolvedNavigationTarget> SortUnresolved(List<UnresolvedNavigationTarget> items)
    {
        items.Sort(static (a, b) =>
        {
            int c = StringComparer.OrdinalIgnoreCase.Compare(a.SourceModule, b.SourceModule);
            if (c != 0) return c;
            c = StringComparer.OrdinalIgnoreCase.Compare(a.SourceProcedure, b.SourceProcedure);
            if (c != 0) return c;
            c = a.SourceLine.CompareTo(b.SourceLine);
            if (c != 0) return c;
            c = a.SourceColumn.CompareTo(b.SourceColumn);
            if (c != 0) return c;
            c = ((int)a.Action).CompareTo((int)b.Action);
            if (c != 0) return c;
            return StringComparer.Ordinal.Compare(a.Reason, b.Reason);
        });
        return items;
    }

    // ── Entry-point assembly ──────────────────────────────────────

    private static IReadOnlyList<NavigationEntryPoint> BuildEntryPoints(
        NavigationGraphInputs? inputs,
        IReadOnlyList<MacroChain> macros)
    {
        // Order per the session-set Standards:
        //   AutoExec → default form → switchboard root(s).
        var result = new List<NavigationEntryPoint>();

        var autoExec = FindAutoExec(macros);
        if (autoExec is not null)
        {
            // Effective entry: the procedure RunCode hands control to,
            // when present. Otherwise the macro itself is the entry.
            var firstRunCode = autoExec.Actions.FirstOrDefault(a => a.CallsProcedureName is not null);
            result.Add(new NavigationEntryPoint
            {
                Kind = EntryPointKind.AutoExec,
                Name = autoExec.Name,
                OpensFormName = ExtractFirstOpenFormFromMacro(autoExec),
                CallsProcedure = firstRunCode is not null
                    ? new ProcedureReference { ProcedureName = firstRunCode.CallsProcedureName! }
                    : null,
            });
        }

        if (!string.IsNullOrEmpty(inputs?.DefaultFormName))
        {
            result.Add(new NavigationEntryPoint
            {
                Kind = EntryPointKind.DefaultForm,
                Name = inputs!.DefaultFormName!,
                OpensFormName = inputs.DefaultFormName,
                CallsProcedure = null,
            });
        }

        if (inputs?.Switchboard is { } sb)
        {
            foreach (var page in sb.Pages)
            {
                result.Add(new NavigationEntryPoint
                {
                    Kind = EntryPointKind.SwitchboardRoot,
                    Name = page.Name,
                    OpensFormName = null,
                    CallsProcedure = null,
                });
            }
        }

        return result;
    }

    // ── Ribbon entry-point derivation (016 S2 / DW-13) ────────────

    // For each custom-ribbon callback — every control onAction plus the root
    // onLoad — resolve the named procedure against the harvested modules and
    // surface a RibbonEntryPoint. Returns null when there is no ribbon (the
    // facet is omitted, exactly like NavigationGraph.Ribbon, so a non-ribbon
    // corpus stays byte-neutral); a present-but-callback-less ribbon yields an
    // empty list. Deterministic source order: root onLoad first, then a
    // depth-first walk of tabs -> groups -> controls -> children, then the
    // contextual tab sets. The resolution outcome itself is order-independent
    // (a unique match resolves regardless of module iteration order; multiple
    // matches are Ambiguous with no guessed module), so no extra sort is
    // needed and the authored order is preserved.
    private static IReadOnlyList<RibbonEntryPoint>? BuildRibbonEntryPoints(
        RibbonStructure? ribbon,
        IReadOnlyList<ResolvedModule> modules)
    {
        if (ribbon is null) return null;

        // name (case-insensitive) -> the distinct (module, declared-name)
        // procedures that carry it. A callback resolves only when exactly one
        // procedure carries the name.
        var byName = BuildProcedureIndex(modules);

        var result = new List<RibbonEntryPoint>();

        if (!string.IsNullOrEmpty(ribbon.OnLoad))
            result.Add(ResolveCallback(RibbonCallbackKind.OnLoad, ribbon.OnLoad!, control: null, byName));

        foreach (var tab in ribbon.Tabs)
            CollectFromTab(tab, byName, result);
        foreach (var set in ribbon.ContextualTabSets)
            foreach (var tab in set.Tabs)
                CollectFromTab(tab, byName, result);

        return result;
    }

    private static void CollectFromTab(
        RibbonTab tab,
        IReadOnlyDictionary<string, List<ProcedureReference>> byName,
        List<RibbonEntryPoint> result)
    {
        foreach (var group in tab.Groups)
            foreach (var control in group.Controls)
                CollectFromControl(control, byName, result);
    }

    // Depth-first, source order: a control's own onAction (if any) before its
    // children's, so a splitButton/menu surfaces ahead of its nested items.
    private static void CollectFromControl(
        RibbonControl control,
        IReadOnlyDictionary<string, List<ProcedureReference>> byName,
        List<RibbonEntryPoint> result)
    {
        if (!string.IsNullOrEmpty(control.OnAction))
            result.Add(ResolveCallback(RibbonCallbackKind.OnAction, control.OnAction!, control, byName));
        foreach (var child in control.Children)
            CollectFromControl(child, byName, result);
    }

    private static RibbonEntryPoint ResolveCallback(
        RibbonCallbackKind kind,
        string callbackName,
        RibbonControl? control,
        IReadOnlyDictionary<string, List<ProcedureReference>> byName)
    {
        RibbonCallbackResolution resolution;
        ProcedureReference? resolved = null;
        if (byName.TryGetValue(callbackName, out var matches) && matches.Count > 0)
        {
            if (matches.Count == 1)
            {
                resolution = RibbonCallbackResolution.Resolved;
                resolved = matches[0];
            }
            else
            {
                // Found in more than one module — surface the ambiguity, never
                // pick a module (faithful-mechanical).
                resolution = RibbonCallbackResolution.Ambiguous;
            }
        }
        else
        {
            resolution = RibbonCallbackResolution.Unresolved;
        }

        return new RibbonEntryPoint
        {
            Kind = kind,
            CallbackName = callbackName,
            ControlType = control?.ControlType,
            ControlId = control?.Id,
            ControlIdMso = control?.IdMso,
            ControlLabel = control?.Label,
            Resolution = resolution,
            ResolvedProcedure = resolved,
        };
    }

    // Index the harvested procedures a ribbon callback can actually bind to,
    // keyed by name (case-insensitive, VBA semantics). Two bindable procedures
    // sharing a name across modules both land under the key, which is what
    // makes the callback Ambiguous. A name declared twice in the SAME module
    // (illegal VBA, but the parser does not enforce it) is de-duplicated to one
    // (module, name) entry so it does not falsely read as ambiguous.
    //
    // Bindability gate (016 S2 set-wide review, gpt-5-4 + in-IDE pair, Major):
    // Access dispatches a ribbon `onAction`/`onLoad` callback only to a
    // procedure that is callable as a project-global entry point — a Public
    // (or modifier-less, which is Public by default) procedure in a STANDARD
    // module. A `Private`/`Friend` procedure, or any procedure in a class /
    // form / report module, cannot be the runtime target. Indexing those too
    // (the original name-only pass) let a same-named private helper or
    // code-behind sub force a false `Ambiguous` that HID the one real,
    // uniquely-bindable target. Filtering to bindable procedures applies
    // Access's STATIC binding rule (not dynamic interpretation): a name whose
    // only matches are non-bindable now resolves to `Unresolved` — faithful,
    // since Access could not bind it either — and the `CallbackName` is still
    // retained verbatim, so nothing is lost.
    private static IReadOnlyDictionary<string, List<ProcedureReference>> BuildProcedureIndex(
        IReadOnlyList<ResolvedModule> modules)
    {
        var byName = new Dictionary<string, List<ProcedureReference>>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            // Only standard modules expose project-global ribbon-callback targets.
            if (module.ModuleScope.Kind != ModuleKind.Standard) continue;
            var moduleName = module.ModuleScope.ModuleName;
            foreach (var item in module.Ast.Items)
            {
                if (item is not ParserProcedure proc) continue;
                if (!IsBindableCallbackTarget(proc.Access)) continue;
                if (!byName.TryGetValue(proc.Name, out var list))
                {
                    list = new List<ProcedureReference>();
                    byName[proc.Name] = list;
                }
                // De-dup on (module, procedure) so a same-module redeclaration
                // does not inflate the match count into a false Ambiguous.
                bool alreadyPresent = list.Any(p =>
                    string.Equals(p.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.ProcedureName, proc.Name, StringComparison.OrdinalIgnoreCase));
                if (!alreadyPresent)
                    list.Add(new ProcedureReference { ModuleName = moduleName, ProcedureName = proc.Name });
            }
        }
        return byName;
    }

    // A ribbon callback binds only to a Public-visible standard-module
    // procedure. A modifier-less Sub/Function is Public by default in a
    // standard module (Unspecified), and Global is project-wide public; both
    // are bindable. Private and Friend are not; Dim is not a procedure modifier.
    private static bool IsBindableCallbackTarget(AccessModifier access) => access switch
    {
        AccessModifier.Public => true,
        AccessModifier.Unspecified => true,
        AccessModifier.Global => true,
        _ => false,
    };

    private static MacroChain? FindAutoExec(IReadOnlyList<MacroChain> macros)
    {
        foreach (var m in macros)
        {
            if (string.Equals(m.Name, "AutoExec", StringComparison.OrdinalIgnoreCase))
                return m;
        }
        return null;
    }

    private static string? ExtractFirstOpenFormFromMacro(MacroChain macro)
    {
        // If the macro contains an OpenForm action with a FormName arg,
        // surface it as the macro's terminal form; the Access AutoExec
        // pattern of "RunCode -> Initialize -> DoCmd.OpenForm <role>"
        // means OpenForm-via-macro is rarer than OpenForm-via-VBA, but
        // the macro extractor surfaces both shapes uniformly so we
        // cover them at the entry-point layer for symmetry.
        foreach (var a in macro.Actions)
        {
            if (string.Equals(a.ActionName, "OpenForm", StringComparison.OrdinalIgnoreCase)
                && a.Arguments.TryGetValue("FormName", out var formName))
            {
                return formName;
            }
        }
        return null;
    }
}
