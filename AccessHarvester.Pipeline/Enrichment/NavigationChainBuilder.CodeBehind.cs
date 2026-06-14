using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.UsageIdentification;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using ParserProcedure = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// S3 (R2-2 part 2, DW-10) code-behind producer for
/// <see cref="NavigationChainBuilder"/>. Derives the chain edges that need the
/// resolved VBA AST — the hops the pure-IR S2 producers cannot see. Kept in a
/// separate partial file so the <c>AccessHarvester.Vba.Parser.Ast</c> dependency
/// (which shares type names like <c>Expression</c> / <c>LiteralExpression</c>
/// with the SQL AST the S2 producers use) stays isolated from the SQL-AST
/// producers in <c>NavigationChainBuilder.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only modules whose name matches a harvested form or report are walked: a
/// chain edge is anchored to an application object, and a form/report
/// code-behind module's <see cref="ModuleScope.ModuleName"/> is the bare
/// object name (e.g. <c>rptServicePlanAndShortfall</c>) — the same seam
/// <see cref="NavigationGraphBuilder"/> uses for its VBA edges. Standard /
/// class modules have no form/report owner, so their <c>DoCmd</c> dispatches
/// stay in the VBA-edge layer (<c>NavigationGraph.Edges</c>) and are not
/// promoted to chain edges here.
/// </para>
/// <para>
/// <c>Forms!</c>/<c>Reports!</c> control references are recognised
/// syntactically (the bang-access root identifier is <c>Forms</c> or
/// <c>Reports</c>), not via the resolver's symbol resolution — the same
/// textual-recognition posture <see cref="NavigationGraphBuilder"/> already
/// uses for the <c>acForm</c>/<c>acReport</c> enum constants. The resolver
/// integration adds no precision for this literal-pattern detection and would
/// couple the builder to the symbol table for no gain.
/// </para>
/// </remarks>
public static partial class NavigationChainBuilder
{
    private static void CollectCodeBehindEdges(
        IReadOnlyList<ResolvedModule> modules,
        List<ChainEdge> edges,
        List<UnresolvedChainTarget> unresolved)
    {
        // The code-behind producer can fabricate the same structural edge from
        // two dispatch sites (e.g. a form that opens the same detail form from
        // both a button click and a double-click). A chain edge is a structural
        // relationship, not a per-call-site record, so collapse exact duplicates.
        var emittedEdges = new HashSet<string>(StringComparer.Ordinal);
        var emittedUnresolved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var module in modules)
        {
            if (module is null) continue;
            // The owner kind comes from the module's OWN resolved kind, NOT a
            // name → kind lookup: Access permits a form and a report to share a
            // name, and a form/report code-behind module's name is the bare
            // object name, so a name-keyed map would let one overwrite the other
            // and misattribute the colliding module's edges (devil's-advocate C3).
            var ownerKind = module.ModuleScope.Kind switch
            {
                ModuleKind.Form => ChainNodeKind.Form,
                ModuleKind.Report => ChainNodeKind.Report,
                _ => (ChainNodeKind?)null,   // standard / class module — no chain owner
            };
            if (ownerKind is null) continue;
            var moduleName = module.ModuleScope.ModuleName;

            var visitor = new CodeBehindVisitor(ownerKind.Value, moduleName);
            foreach (var item in module.Ast.Items)
            {
                if (item is not ParserProcedure proc) continue;
                foreach (var statement in proc.Body)
                    visitor.Visit(statement);
            }

            foreach (var edge in visitor.Edges)
                if (emittedEdges.Add(EdgeKey(edge)))
                    edges.Add(edge);
            foreach (var u in visitor.Unresolved)
                if (emittedUnresolved.Add(UnresolvedKey(u)))
                    unresolved.Add(u);
        }
    }

    // The set of every TempVar name assigned (`TempVars!x = …`) anywhere in
    // the harvested VBA — across ALL modules, not just form/report code-behind.
    // Used as the "supplied" universe for the unmatched-TempVar check so a
    // setter in a standard/class module (which produces no chain supplier edge,
    // because it has no form/report owner) still counts as supplying the value.
    private static IReadOnlySet<string> CollectAssignedTempVars(IReadOnlyList<ResolvedModule> modules)
    {
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var collector = new TempVarSetterCollector(assigned);
        foreach (var module in modules)
        {
            if (module is null) continue;
            foreach (var item in module.Ast.Items)
            {
                if (item is not ParserProcedure proc) continue;
                foreach (var statement in proc.Body)
                    collector.Visit(statement);
            }
        }
        return assigned;
    }

    // Collects TempVar names set by ANY of the common Access setter forms — so
    // the unmatched-supplier check does not false-flag a value that IS supplied,
    // just not via the canonical bang form. Recognised:
    //   TempVars!x = …            (bang assignment target)
    //   TempVars("x") = …         (indexer assignment target)
    //   TempVars.Add "x", value   (method call, name = first string-literal arg)
    private sealed class TempVarSetterCollector : VbaSyntaxWalker
    {
        private readonly HashSet<string> _names;
        public TempVarSetterCollector(HashSet<string> names) => _names = names;

        public override void VisitLetStatement(LetStatement n) { RecordTarget(n.Target); base.VisitLetStatement(n); }
        public override void VisitSetStatement(SetStatement n) { RecordTarget(n.Target); base.VisitSetStatement(n); }

        public override void VisitCallStatement(CallStatement n)
        {
            // TempVars.Add "name", value  (bare or parenthesised). Unwrap one
            // IndexOrCall layer so the parenthesised form surfaces the same.
            Expression target = n.Target;
            IReadOnlyList<Argument> args = n.BareArguments;
            if (args.Count == 0 && target is IndexOrCallExpression ioc)
            {
                target = ioc.Target;
                args = ioc.Arguments;
            }
            if (target is MemberAccessExpression m
                && string.Equals(m.MemberName, "Add", StringComparison.OrdinalIgnoreCase)
                && IsTempVarsRoot(m.Target))
                RecordNameSlot(args);
            base.VisitCallStatement(n);
        }

        private void RecordTarget(Expression target)
        {
            // TempVars!x
            if (target is BangAccessExpression bang && IsTempVarsRoot(bang.Target))
            {
                _names.Add(bang.MemberName);
                return;
            }
            // TempVars("x")  or  TempVars.Item("x")
            if (target is IndexOrCallExpression ioc && IsTempVarsCollection(ioc.Target))
                RecordNameSlot(ioc.Arguments);
        }

        // The TempVar name is the FIRST positional slot, or an explicit `Name:=`
        // named argument, for `TempVars(name)` / `TempVars.Item(name)` /
        // `TempVars.Add name, value`. Inspect ONLY the name slot — never scan
        // positional args forward — so `TempVars.Add dynamicName, "value"` does
        // not mistake the VALUE literal for the name. A dynamic/non-literal name
        // slot records nothing (the supplied name is not statically known).
        private void RecordNameSlot(IReadOnlyList<Argument> args)
        {
            // An explicit `Name:=` named argument wins wherever it appears.
            foreach (var arg in args)
                if (arg is NamedArgument na
                    && string.Equals(na.Name, "Name", StringComparison.OrdinalIgnoreCase)
                    && TryAsStringLiteral(na.Value, out var named) && named.Length > 0)
                {
                    _names.Add(named);
                    return;
                }
            // Otherwise the first positional slot is the name.
            if (args.Count > 0 && args[0] is PositionalArgument p
                && TryAsStringLiteral(p.Value, out var name) && name.Length > 0)
                _names.Add(name);
        }

        private static bool IsTempVarsRoot(Expression e) =>
            e is IdentifierExpression id
            && string.Equals(id.Name, "TempVars", StringComparison.OrdinalIgnoreCase);

        // The TempVars collection itself: bare `TempVars` or the explicit
        // default member `TempVars.Item` (so `TempVars.Item("x") = …` is
        // recognised the same as the `TempVars("x") = …` shorthand).
        private static bool IsTempVarsCollection(Expression e) =>
            IsTempVarsRoot(e)
            || (e is MemberAccessExpression m
                && string.Equals(m.MemberName, "Item", StringComparison.OrdinalIgnoreCase)
                && IsTempVarsRoot(m.Target));
    }

    private static string EdgeKey(ChainEdge e) =>
        $"{(int)e.Kind}|{(int)e.From.Kind}|{e.From.Name}|{e.From.Member}|{(int)e.To.Kind}|{e.To.Name}|{e.To.Member}|{e.Provenance}";

    private static string UnresolvedKey(UnresolvedChainTarget u) =>
        $"{(int)u.Kind}|{(int)u.Consumer.Kind}|{u.Consumer.Name}|{u.Consumer.Member}|{u.Provenance}|{u.Reason}|{u.Detail}";

    // ── Code-behind body walker ─────────────────────────────────────────

    // Walks one form/report code-behind module's procedure bodies. The owner
    // endpoint is the form/report the module belongs to; all edges originate
    // from (Opens*, tempvar-flow setter) or terminate at (control reads) that
    // owner. Pure: no I/O, no environment, no resolver state consulted.
    private sealed class CodeBehindVisitor : VbaSyntaxWalker
    {
        public List<ChainEdge> Edges { get; } = new();
        public List<UnresolvedChainTarget> Unresolved { get; } = new();

        private readonly ChainNodeKind _ownerKind;
        private readonly string _ownerName;

        // BangAccess nodes that are the direct write-target (LHS) of an
        // assignment — a control/TempVar WRITE, not a READ. Tracked by
        // reference so VisitBangAccessExpression does not promote a write into
        // a (wrong-direction) SuppliesParameter read edge.
        private readonly HashSet<BangAccessExpression> _writeTargets =
            new(ReferenceEqualityComparer.Instance);

        public CodeBehindVisitor(ChainNodeKind ownerKind, string ownerName)
        {
            _ownerKind = ownerKind;
            _ownerName = ownerName;
        }

        private ChainEndpoint Owner => new() { Kind = _ownerKind, Name = _ownerName };

        // ── Assignments: TempVars!x = … setters; suppress write-target reads ──

        public override void VisitLetStatement(LetStatement n)
        {
            HandleAssignmentTarget(n.Target);
            base.VisitLetStatement(n);
        }

        public override void VisitSetStatement(SetStatement n)
        {
            HandleAssignmentTarget(n.Target);
            base.VisitSetStatement(n);
        }

        private void HandleAssignmentTarget(Expression target)
        {
            // `TempVars!x = …` (direct bang) — the owner supplies a value into
            // TempVar x (the supplier side of the tempvar-flow the
            // record-source producer surfaces as the consumer side).
            if (target is BangAccessExpression directBang
                && directBang.Target is IdentifierExpression rootId
                && string.Equals(rootId.Name, "TempVars", StringComparison.OrdinalIgnoreCase))
            {
                _writeTargets.Add(directBang);   // not a control read
                Edges.Add(new ChainEdge
                {
                    From = Owner,
                    To = new ChainEndpoint { Kind = ChainNodeKind.TempVar, Name = directBang.MemberName },
                    Kind = ChainEdgeKind.SuppliesParameter,
                    Provenance = ProvTempVarFlow,
                });
                return;
            }

            // A WRITE whose target is (or postfix-wraps) a `Forms!frm!ctl`
            // reference — `Forms!frm!ctl = …` OR `Forms!frm!ctl.Value = …` —
            // is the owner pushing INTO the dialog, the opposite direction of a
            // parameter read. Peel any `.Member` / `(index)` postfix layers to
            // the controlling bang and mark it so the bang walker does not emit
            // a (wrong-direction) read edge for it.
            var controllingBang = UnwrapToControllingBang(target);
            if (controllingBang is not null && IsFormsControlRef(controllingBang, out _, out _, out _))
                _writeTargets.Add(controllingBang);
        }

        // Peels a postfix chain of `.Member` (MemberAccess) and `(index)`
        // (IndexOrCall) layers down to the innermost BangAccessExpression, so a
        // member/index-wrapped assignment LHS over a control reference is
        // recognised as the same write the bare bang would be. Returns null when
        // the chain does not bottom out in a bang.
        private static BangAccessExpression? UnwrapToControllingBang(Expression e)
        {
            while (true)
            {
                switch (e)
                {
                    case BangAccessExpression b: return b;
                    case MemberAccessExpression m: e = m.Target; break;
                    case IndexOrCallExpression ioc: e = ioc.Target; break;
                    default: return null;
                }
            }
        }

        // ── Control reads: Forms!dlg!ctl / Reports!rpt!ctl ──────────────────

        public override void VisitBangAccessExpression(BangAccessExpression n)
        {
            if (!_writeTargets.Contains(n)
                && IsFormsControlRef(n, out var refKind, out var objName, out var member))
            {
                // The dialog/object control supplies a parameter value the
                // owner reads in its code-behind.
                Edges.Add(new ChainEdge
                {
                    From = new ChainEndpoint { Kind = refKind, Name = objName, Member = member },
                    To = Owner,
                    Kind = ChainEdgeKind.SuppliesParameter,
                    Provenance = ProvVbaCall,
                });
            }
            base.VisitBangAccessExpression(n);
        }

        // True when `bang` is the two-segment `Forms!obj!member` /
        // `Reports!obj!member` shape (root identifier Forms/Reports, one
        // intermediate bang for the object, the outer bang for the member).
        private static bool IsFormsControlRef(
            BangAccessExpression bang, out ChainNodeKind kind, out string objName, out string member)
        {
            kind = default; objName = string.Empty; member = string.Empty;
            if (bang.Target is not BangAccessExpression inner) return false;
            if (inner.Target is not IdentifierExpression root) return false;

            if (string.Equals(root.Name, "Forms", StringComparison.OrdinalIgnoreCase))
                kind = ChainNodeKind.Form;
            else if (string.Equals(root.Name, "Reports", StringComparison.OrdinalIgnoreCase))
                kind = ChainNodeKind.Report;
            else
                return false;

            objName = inner.MemberName;
            member = bang.MemberName;
            return objName.Length > 0 && member.Length > 0;
        }

        // ── DoCmd.OpenForm / OpenReport dispatches ──────────────────────────

        public override void VisitCallStatement(CallStatement n)
        {
            HandleDispatch(n.Target, n.BareArguments);
            base.VisitCallStatement(n);
        }

        private void HandleDispatch(Expression target, IReadOnlyList<Argument> bareArguments)
        {
            // Unwrap one IndexOrCall layer so `DoCmd.OpenForm("frm")` surfaces
            // the same as `DoCmd.OpenForm "frm"` (the grammar puts
            // parenthesised args on the IndexOrCall, not on BareArguments).
            Expression effectiveTarget = target;
            IReadOnlyList<Argument> effectiveArgs = bareArguments;
            if (bareArguments.Count == 0 && target is IndexOrCallExpression ioc)
            {
                effectiveTarget = ioc.Target;
                effectiveArgs = ioc.Arguments;
            }

            if (effectiveTarget is not MemberAccessExpression member) return;
            if (!IsDoCmdTarget(member.Target)) return;

            bool isOpenForm = string.Equals(member.MemberName, "OpenForm", StringComparison.OrdinalIgnoreCase);
            bool isOpenReport = string.Equals(member.MemberName, "OpenReport", StringComparison.OrdinalIgnoreCase);
            if (!isOpenForm && !isOpenReport) return;   // Close/BrowseTo are not chain-open kinds.

            var positional = PositionalValues(effectiveArgs);
            // No positional target at all — the dispatch named no form/report.
            // The VBA-edge layer already records this as a missing-target
            // unresolved; the chain layer only models a resolvable or
            // explicitly-dynamic *named* target, so a no-target dispatch is
            // left to that layer rather than double-reported here.
            var nameExpr = positional.Count > 0 ? positional[0] : null;
            if (nameExpr is null) return;

            if (TryAsStringLiteral(nameExpr, out var literal))
            {
                ChainEdgeKind kind;
                ChainNodeKind toKind;
                if (isOpenReport)
                {
                    kind = ChainEdgeKind.OpensReport;
                    toKind = ChainNodeKind.Report;
                }
                else
                {
                    // acDialog (positional WindowMode slot 5, or a WindowMode:=
                    // named arg) distinguishes a modal parameter dialog from a
                    // plain form open.
                    kind = OpensWithDialog(positional, effectiveArgs)
                        ? ChainEdgeKind.OpensDialog
                        : ChainEdgeKind.OpensForm;
                    toKind = ChainNodeKind.Form;
                }

                Edges.Add(new ChainEdge
                {
                    From = Owner,
                    To = new ChainEndpoint { Kind = toKind, Name = literal },
                    Kind = kind,
                    Provenance = ProvVbaCall,
                });
            }
            else
            {
                // Dynamic target (`DoCmd.OpenReport strReport`): a chain exists
                // here but its target is not statically resolvable — surface it
                // explicitly rather than silently dropping the hop.
                Unresolved.Add(new UnresolvedChainTarget
                {
                    Kind = isOpenReport ? ChainEdgeKind.OpensReport : ChainEdgeKind.OpensForm,
                    Consumer = Owner,
                    Provenance = ProvVbaCall,
                    Reason = ReasonNonLiteralTarget,
                    Detail = null,
                });
            }
        }

        // OpenForm WindowMode is the sixth positional argument (index 5):
        //   OpenForm(FormName, View, FilterName, WhereCondition, DataMode,
        //            WindowMode, OpenArgs)
        // acDialog is also expressible as the named argument `WindowMode:=`.
        private const int OpenFormWindowModeIndex = 5;

        private static bool OpensWithDialog(
            IReadOnlyList<Expression?> positional, IReadOnlyList<Argument> args)
        {
            if (OpenFormWindowModeIndex < positional.Count
                && IsAcDialog(positional[OpenFormWindowModeIndex]))
                return true;

            foreach (var arg in args)
                if (arg is NamedArgument named
                    && string.Equals(named.Name, "WindowMode", StringComparison.OrdinalIgnoreCase)
                    && IsAcDialog(named.Value))
                    return true;

            return false;
        }

        private static bool IsAcDialog(Expression? expr) =>
            expr is IdentifierExpression id
            && string.Equals(id.Name, "acDialog", StringComparison.OrdinalIgnoreCase);
    }

    // ── DoCmd recognition + argument helpers (code-behind) ──────────────────
    // Local minimal copies of the NavigationGraphBuilder helpers; kept here so
    // the chain-edge producer is self-contained and the two builders do not
    // couple through a shared internal surface.

    private static bool IsDoCmdTarget(Expression target) => target switch
    {
        IdentifierExpression id => string.Equals(id.Name, "DoCmd", StringComparison.OrdinalIgnoreCase),
        MemberAccessExpression m => string.Equals(m.MemberName, "DoCmd", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private static IReadOnlyList<Expression?> PositionalValues(IReadOnlyList<Argument> args)
    {
        var result = new List<Expression?>();
        foreach (var arg in args)
        {
            switch (arg)
            {
                case PositionalArgument p: result.Add(p.Value); break;
                case MissingArgument: result.Add(null); break;
                case NamedArgument: break;   // named args do not fill positional slots
            }
        }
        return result;
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

    private static string UnquoteString(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            return raw.Substring(1, raw.Length - 2).Replace("\"\"", "\"");
        return raw;
    }
}
