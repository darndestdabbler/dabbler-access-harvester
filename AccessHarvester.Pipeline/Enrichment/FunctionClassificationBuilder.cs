using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using ResolverProcedure = AccessHarvester.Vba.Resolver.Declarations.ProcedureDeclaration;
using ParserProcedure = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds <see cref="FunctionClassification"/> records from one
/// <see cref="ResolvedModule"/> + one parser-AST procedure body +
/// (for callers and transitive deps) the project-wide
/// <see cref="CrossReferenceIndex"/>. See
/// <see cref="FunctionClassification"/>'s remarks for the rules driving
/// each facet; the implementation here is the deterministic encoding of
/// those rules and consults nothing outside the inputs (no ambient
/// configuration, no environment, no globals).
/// </summary>
/// <remarks>
/// Per the spec's Standards "classification is deterministic; metrics
/// are mechanical" — every facet is computed from the resolver's own
/// outcomes, the parser AST shape, and a small static mapping table
/// from <see cref="BuiltInFunction"/> names / categories to
/// <see cref="ClassificationCategory"/>. That mapping is intentionally
/// open-coded (rather than data-driven) so a reader can audit the rules
/// in one pass; the registry's name list is small and stable.
/// </remarks>
public static class FunctionClassificationBuilder
{
    /// <summary>
    /// Classifies one procedure. The caller supplies the
    /// <see cref="ResolvedModule"/> that owns the procedure (for
    /// reference lookups), the parser-AST procedure (for the body walk),
    /// and the project-wide <see cref="CrossReferenceIndex"/> (for
    /// callers and transitive dependency closure). When the procedure
    /// is the only one being classified, callers can pass the index
    /// built from a single module — the result is internally consistent
    /// even if the cross-module callers list happens to be empty.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Any argument is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="procedureAst"/> does not appear in
    /// <paramref name="module"/>'s AST, so reference lookups cannot
    /// observe its body. The check is mechanical (declared procedures
    /// inside the same Module record), which catches the common
    /// "wrong module" mistake without inspecting AST identity over the
    /// tree.
    /// </exception>
    public static FunctionClassification Build(
        ResolvedModule module,
        ParserProcedure procedureAst,
        CrossReferenceIndex crossReferences)
    {
        if (module is null) throw new ArgumentNullException(nameof(module));
        if (procedureAst is null) throw new ArgumentNullException(nameof(procedureAst));
        if (crossReferences is null) throw new ArgumentNullException(nameof(crossReferences));

        if (!ModuleContainsProcedure(module.Ast, procedureAst))
        {
            throw new ArgumentException(
                $"Procedure '{procedureAst.Name}' is not declared in module '{module.ModuleScope.ModuleName}'. " +
                "Pass the ResolvedModule whose AST owns the procedure.",
                nameof(procedureAst));
        }

        ResolverProcedure? resolverProc = FindResolverProcedure(module.ModuleScope, procedureAst.Name, KindOf(procedureAst));

        var visitor = new ClassificationVisitor(module);
        visitor.Visit(procedureAst);

        var metrics = ComplexityCollector.Collect(procedureAst);

        var directDeps = BuildDependencyEdges(visitor.CalledProcedures, seedProcedure: resolverProc);
        var callers = resolverProc is null
            ? Array.Empty<DependencyEdge>()
            : BuildCallerEdges(crossReferences.GetReferencesTo(resolverProc), seedProcedure: resolverProc);
        var transitive = ComputeTransitiveClosure(
            startProcedures: visitor.CalledProcedures,
            crossReferences: crossReferences,
            seedProcedure: resolverProc);

        var primary = PickPrimary(visitor.Categories);
        var isMixed = ComputeIsMixed(visitor.Categories);

        return new FunctionClassification
        {
            ModuleName = module.ModuleScope.ModuleName,
            ProcedureName = procedureAst.Name,
            Kind = KindOf(procedureAst),
            IsPublic = resolverProc is not null && resolverProc.Access == AccessLevel.Public,
            IsStatic = procedureAst.IsStatic,
            ParameterCount = procedureAst.Parameters.Count,
            HasReturnValue = HasReturnValue(procedureAst),
            Categories = SnapshotCategories(visitor.Categories),
            PrimaryCategory = primary,
            IsMixed = isMixed,
            Metrics = metrics,
            DirectDependencies = directDeps,
            TransitiveDependencies = transitive,
            Callers = callers,
            UnresolvedReferences = visitor.UnresolvedFacts.Count == 0
                ? null
                : visitor.UnresolvedFacts.AsReadOnly(),
        };
    }

    /// <summary>
    /// Convenience: classifies every body-bearing procedure across the
    /// supplied modules. Order is preserved per module (source order)
    /// and per module list (input order). Modules that contain no
    /// procedures contribute no entries.
    /// </summary>
    public static IReadOnlyList<FunctionClassification> BuildAll(
        IReadOnlyList<ResolvedModule> modules,
        CrossReferenceIndex crossReferences)
    {
        if (modules is null) throw new ArgumentNullException(nameof(modules));
        if (crossReferences is null) throw new ArgumentNullException(nameof(crossReferences));

        var results = new List<FunctionClassification>();
        foreach (var module in modules)
        {
            if (module is null)
                throw new ArgumentException("Module list contains a null entry.", nameof(modules));
            foreach (var item in module.Ast.Items)
            {
                if (item is ParserProcedure proc)
                    results.Add(Build(module, proc, crossReferences));
            }
        }
        return results;
    }

    // ── Body walker ────────────────────────────────────────────────

    // Walks one procedure's body, looking up every Expression node in
    // ResolvedModule.TryGetReference. Accumulates classification signals,
    // call edges, and unresolved-reference facts. Pure observational —
    // never mutates resolver state.
    private sealed class ClassificationVisitor : VbaSyntaxWalker
    {
        private readonly ResolvedModule _module;

        // Set semantics for categories so duplicate signals collapse;
        // ordered list mirrors first-encounter order so output stays
        // deterministic. The pair is the natural shape — a HashSet for
        // membership and a List for order — and is small enough that
        // O(n) Contains on the list would also be fine.
        private readonly HashSet<ClassificationCategory> _categorySet = new();
        public List<ClassificationCategory> Categories { get; } = new();

        // Set semantics again for called procedures so the same callee
        // counts once even when invoked from many sites. Reference
        // identity matches the resolver graph's "one Declaration per
        // declaration site" invariant.
        public HashSet<ResolverProcedure> CalledProcedures { get; }
            = new(ReferenceEqualityComparer.Instance);

        public List<UnresolvedReferenceFact> UnresolvedFacts { get; } = new();

        // Discriminate "I am visiting the procedure body" from "I am
        // visiting nested AST that should be skipped". VBA has no nested
        // procedures, so the only distinction we need is to start
        // walking from the body and ignore the procedure header
        // (parameters / return-type signatures contribute no references
        // we want to classify as call edges).
        public ClassificationVisitor(ResolvedModule module) => _module = module;

        public void Visit(ParserProcedure procedureAst)
        {
            // Walking the procedure node itself would visit Parameters
            // first, which would emit type-reference identifier hits we
            // do not want classified as runtime side effects. Walk only
            // the Body — the resolver still recorded references for
            // parameter type names, but those are header concerns, not
            // body behaviour.
            foreach (var statement in procedureAst.Body)
                Visit(statement);
        }

        public override void VisitIdentifierExpression(IdentifierExpression n)
        {
            ClassifyExpression(n);
            base.VisitIdentifierExpression(n);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpression n)
        {
            ClassifyExpression(n);
            base.VisitMemberAccessExpression(n);
        }

        public override void VisitBangAccessExpression(BangAccessExpression n)
        {
            ClassifyExpression(n);
            base.VisitBangAccessExpression(n);
        }

        public override void VisitImplicitWithMemberExpression(ImplicitWithMemberExpression n)
        {
            ClassifyExpression(n);
            base.VisitImplicitWithMemberExpression(n);
        }

        public override void VisitImplicitWithBangExpression(ImplicitWithBangExpression n)
        {
            ClassifyExpression(n);
            base.VisitImplicitWithBangExpression(n);
        }

        public override void VisitMeExpression(MeExpression n)
        {
            ClassifyExpression(n);
            base.VisitMeExpression(n);
        }

        // Bare `Debug.Print` — Debug is its own AST node. The resolver
        // does not currently emit a ResolvedReference for DebugExpression
        // (it is recognised structurally), so there is nothing to look
        // up. Debug output is a side effect, but not one the consumer
        // needs to classify on — it does not survive translation.
        // Intentionally no override; default base recursion is fine.

        private void ClassifyExpression(Expression expr)
        {
            var reference = _module.TryGetReference(expr);
            if (reference is null) return;
            DispatchOutcome(reference.Outcome, expr);
        }

        private void DispatchOutcome(ResolutionOutcome outcome, Expression expr)
        {
            switch (outcome)
            {
                case ResolutionOutcome.Resolved r:
                    HandleResolvedDeclaration(r.Declaration);
                    break;

                case ResolutionOutcome.ResolvedToObjectModel r:
                    HandleObjectModelDeclaration(r.Declaration);
                    break;

                case ResolutionOutcome.ResolvedToBuiltIn r:
                    HandleBuiltIn(r.Function);
                    break;

                case ResolutionOutcome.ResolvedToMe r:
                    HandleMe(r.OwningClass);
                    break;

                case ResolutionOutcome.ResolvedViaWithFlattening f:
                    // Per Session 2 invariant the InnerOutcome is never
                    // itself a flattening; the recursive call still
                    // bottoms out in one of the leaf outcomes above.
                    DispatchOutcome(f.InnerOutcome, expr);
                    break;

                case ResolutionOutcome.Unresolved u:
                    HandleUnresolved(u.Reason, expr);
                    break;
            }
        }

        private void HandleResolvedDeclaration(Declaration decl)
        {
            switch (decl)
            {
                case ResolverProcedure proc:
                    CalledProcedures.Add(proc);
                    break;

                case DeclareDeclaration:
                    // Declares are external library entry points;
                    // calling one is by definition an external-IO
                    // dependency. They do not contribute a
                    // FunctionClassification of their own (no body) but
                    // they do count as a call-edge target for the
                    // caller's classification.
                    AddCategory(ClassificationCategory.ExternalIO);
                    break;

                // Locals, parameters, module variables, types, enums,
                // constants — none contributes a side-effect signal.
                // The reference IS recorded by the resolver; we just
                // have nothing to classify on it.
            }
        }

        private void HandleObjectModelDeclaration(Declaration decl)
        {
            switch (decl)
            {
                case SyntheticFormDeclaration:
                case SyntheticReportDeclaration:
                case SyntheticControlDeclaration:
                case SyntheticFormsCollectionDeclaration:
                    AddCategory(ClassificationCategory.UIBound);
                    break;

                case SyntheticTypeLibraryMemberDeclaration m:
                    AddCategory(CategorizeTypeLibraryOwner(m.OwnerTypeName));
                    break;
            }
        }

        private void HandleBuiltIn(BuiltInFunction fn)
        {
            // Category-driven defaults first; then a few name-specific
            // overrides for AccessSpecific built-ins that mix UI and
            // data concerns.
            switch (fn.Category)
            {
                case BuiltInCategory.Interaction:
                    // MsgBox / InputBox = UI prompt; Shell = launch
                    // external process. The two flavours are bucketed
                    // differently because Shell is a process boundary
                    // the consumer adapts via .NET Process.Start, while
                    // MsgBox / InputBox are dialogs the consumer hosts
                    // inside the Blazor shell.
                    if (BuiltInNameSets.ShellLikeInteraction.Contains(fn.Name))
                        AddCategory(ClassificationCategory.ExternalIO);
                    else
                        AddCategory(ClassificationCategory.UIBound);
                    break;

                case BuiltInCategory.IO:
                    AddCategory(ClassificationCategory.ExternalIO);
                    break;

                case BuiltInCategory.AccessSpecific:
                    if (BuiltInNameSets.AccessUiNames.Contains(fn.Name))
                        AddCategory(ClassificationCategory.UIBound);
                    else if (BuiltInNameSets.AccessDataNames.Contains(fn.Name))
                        AddCategory(ClassificationCategory.DataAccess);
                    // The remaining AccessSpecific entries (Nz,
                    // CurrentProject, CurrentUser, Eval) carry no
                    // unambiguous side-effect signal at this layer:
                    //   - Nz is value coalescing (pure)
                    //   - CurrentProject / CurrentUser are environment
                    //     reads (informational; not data nor UI)
                    //   - Eval is metaprogramming and surfaces as a
                    //     manual-review item via Phase 6.8 rather than
                    //     being categorised here
                    break;

                // String / Math / DateTime / Information / Conversion
                // are pure functions; no signal to add.
            }
        }

        private void HandleMe(ClassDeclaration owningClass)
        {
            var moduleScope = _module.ModuleScope;
            // `Me` in a Form/Report module is the form/report instance —
            // any use of it is a UI binding even when no member is
            // accessed (e.g., passing `Me` to AttachToForm). Class
            // modules use `Me` for ordinary self-reference; no signal.
            if (moduleScope.Kind == ModuleKind.Form || moduleScope.Kind == ModuleKind.Report)
                AddCategory(ClassificationCategory.UIBound);
            // Suppress unused parameter warning without tampering with
            // the discriminated-union arm shape.
            _ = owningClass;
        }

        private void HandleUnresolved(string reason, Expression expr)
        {
            // Two reasons signal late-bound chains: the target was
            // resolved but the member could not be (default-member)
            // or the target itself was unresolved so the tail member
            // is necessarily unresolved too. Both leave the static
            // signal incomplete and force the procedure to mixed.
            if (reason == UnresolvedReasons.DefaultMemberNotYetResolved
                || reason == UnresolvedReasons.MemberAccessOnUnresolvedTarget)
            {
                AddCategory(ClassificationCategory.LateBound);
            }
            UnresolvedFacts.Add(new UnresolvedReferenceFact
            {
                Reason = reason,
                Line = expr.Provenance.Line,
                Column = expr.Provenance.Column,
                MemberName = ExtractName(expr),
            });
        }

        private void AddCategory(ClassificationCategory category)
        {
            if (_categorySet.Add(category))
                Categories.Add(category);
        }

        private static string? ExtractName(Expression e) => e switch
        {
            IdentifierExpression i => i.Name,
            MemberAccessExpression m => m.MemberName,
            BangAccessExpression b => b.MemberName,
            ImplicitWithMemberExpression w => w.MemberName,
            ImplicitWithBangExpression w => w.MemberName,
            _ => null,
        };
    }

    // ── Static name lookup tables ──────────────────────────────────

    // Curated subsets of BuiltInFunctionRegistry.AccessSpecific names
    // grouped by translation-side concern. Kept here (not on the
    // registry itself) because this grouping is a Pipeline concern, not
    // a resolver concern; the registry stays focused on "is this name a
    // built-in?" while the Pipeline owns "what does the built-in mean
    // for translation?". When the registry adds a new AccessSpecific
    // entry, this file is the single place that decides which bucket
    // it lands in — review at promotion time, not at use time.
    private static class BuiltInNameSets
    {
        // Members of the Access UI surface. DoCmd is the dispatch entry
        // point for OpenForm / OpenReport / Close / etc.; Forms /
        // Reports / Screen / Application reach forms, reports, focused
        // controls, and the running Application object respectively.
        // Every name here is one whose presence in a body should pin
        // the procedure's translation target to the Blazor component
        // layer.
        public static readonly HashSet<string> AccessUiNames
            = new(StringComparer.OrdinalIgnoreCase)
            {
                "DoCmd",
                "Forms",
                "Reports",
                "Screen",
                "Application",
            };

        // Domain-data aggregate functions and the canonical handle to
        // the current database. Every one reads or yields a database
        // object, so the procedure containing any of them must be
        // routed through the repository / data-access layer in
        // translation.
        public static readonly HashSet<string> AccessDataNames
            = new(StringComparer.OrdinalIgnoreCase)
            {
                "DLookup", "DCount", "DSum", "DAvg",
                "DMin", "DMax", "DFirst", "DLast",
                "CurrentDb",
            };

        // Interaction built-ins that actually launch an external
        // process rather than show an in-process dialog. Currently only
        // Shell — split out so the UI-vs-ExternalIO classification stays
        // honest as new entries arrive.
        public static readonly HashSet<string> ShellLikeInteraction
            = new(StringComparer.OrdinalIgnoreCase)
            {
                "Shell",
            };
    }

    // Maps a SyntheticTypeLibraryMember's owner-type name (e.g.,
    // "Access.Form", "DAO.Recordset") to a category. The leading
    // namespace is the discriminator: Access.* is UI, DAO.* / ADODB.*
    // are data, Scripting.* / Excel.* / Word.* / VBA filesystem types
    // are external I/O. Unknown libraries fall through with no signal —
    // adding a guess would be the kind of "interpretive heuristic" the
    // project guidance explicitly avoids.
    internal static ClassificationCategory CategorizeTypeLibraryOwner(string ownerTypeName)
    {
        if (ownerTypeName.StartsWith("Access.", StringComparison.OrdinalIgnoreCase))
            return ClassificationCategory.UIBound;
        if (ownerTypeName.StartsWith("DAO.", StringComparison.OrdinalIgnoreCase)
            || ownerTypeName.StartsWith("ADODB.", StringComparison.OrdinalIgnoreCase))
            return ClassificationCategory.DataAccess;
        if (ownerTypeName.StartsWith("Scripting.", StringComparison.OrdinalIgnoreCase)
            || ownerTypeName.StartsWith("Excel.", StringComparison.OrdinalIgnoreCase)
            || ownerTypeName.StartsWith("Word.", StringComparison.OrdinalIgnoreCase)
            || ownerTypeName.StartsWith("Outlook.", StringComparison.OrdinalIgnoreCase)
            || ownerTypeName.StartsWith("PowerPoint.", StringComparison.OrdinalIgnoreCase))
            return ClassificationCategory.ExternalIO;
        // No strong signal: leave the choice to the consumer rather
        // than guess. Returning Pure here would be wrong (the type-lib
        // member call IS a side effect); we return LateBound to flag
        // "we recognised the call but cannot bucket it without more
        // information."
        return ClassificationCategory.LateBound;
    }

    // ── Complexity collector ──────────────────────────────────────

    // Walks one procedure's body counting decision points, statements,
    // and the set of distinct source lines actually occupied by
    // statements. Pure structural — no resolver lookups.
    private sealed class ComplexityCollector : VbaSyntaxWalker
    {
        private int _decisions;
        private int _statements;
        private readonly HashSet<int> _lines = new();

        public static ComplexityMetrics Collect(ParserProcedure procedureAst)
        {
            var c = new ComplexityCollector();
            foreach (var statement in procedureAst.Body)
                c.Visit(statement);
            return new ComplexityMetrics
            {
                CyclomaticComplexity = 1 + c._decisions,
                LinesOfCode = c._lines.Count,
                StatementCount = c._statements,
            };
        }

        private void RecordStatement(Statement n)
        {
            // UnknownStatement is the parse-recovery sentinel; it is not
            // a real statement and would inflate counts misleadingly.
            if (n is UnknownStatement) return;
            _statements++;
            RecordLines(n.Provenance);
        }

        private void RecordLines(Provenance p)
        {
            if (p.Range is { } range)
            {
                for (int line = range.Start.Line; line <= range.End.Line; line++)
                    _lines.Add(line);
            }
            else if (p.Line > 0)
            {
                _lines.Add(p.Line);
            }
        }

        // ── Decision-point arms ──

        public override void VisitIfStatement(IfStatement n)
        {
            RecordStatement(n);
            // Standard cyclomatic: the `If` itself contributes one
            // decision (true vs. else); each ElseIf contributes one
            // additional decision. The `Else` branch is fall-through
            // and not a separate decision.
            _decisions += 1 + n.ElseIfClauses.Count;
            CountConditionLogicals(n.Condition);
            base.VisitIfStatement(n);
        }

        public override void VisitElseIfClause(ElseIfClause n)
        {
            // The ElseIf decision was counted on the IfStatement above.
            // Recurse into the body so nested decisions still register.
            CountConditionLogicals(n.Condition);
            base.VisitElseIfClause(n);
        }

        public override void VisitForStatement(ForStatement n)
        {
            RecordStatement(n);
            _decisions++;
            base.VisitForStatement(n);
        }

        public override void VisitForEachStatement(ForEachStatement n)
        {
            RecordStatement(n);
            _decisions++;
            base.VisitForEachStatement(n);
        }

        public override void VisitDoLoopStatement(DoLoopStatement n)
        {
            RecordStatement(n);
            // Infinite Do/Loop has no decision; the four conditional
            // forms each add one.
            if (n.Kind != DoLoopKind.Infinite)
            {
                _decisions++;
                if (n.Condition is { } cond)
                    CountConditionLogicals(cond);
            }
            base.VisitDoLoopStatement(n);
        }

        public override void VisitWhileWendStatement(WhileWendStatement n)
        {
            RecordStatement(n);
            _decisions++;
            CountConditionLogicals(n.Condition);
            base.VisitWhileWendStatement(n);
        }

        public override void VisitSelectCaseStatement(SelectCaseStatement n)
        {
            RecordStatement(n);
            // Each Case clause is one branch. Case Else is the
            // fall-through and is not a distinct decision (matches the
            // Else-branch rule on If).
            _decisions += n.Cases.Count;
            base.VisitSelectCaseStatement(n);
        }

        public override void VisitWithStatement(WithStatement n)
        {
            RecordStatement(n);
            base.VisitWithStatement(n);
        }

        // ── Plain statement arms ──

        public override void VisitOnErrorStatement(OnErrorStatement n) { RecordStatement(n); base.VisitOnErrorStatement(n); }
        public override void VisitResumeStatement(ResumeStatement n) { RecordStatement(n); base.VisitResumeStatement(n); }
        public override void VisitGotoStatement(GotoStatement n) { RecordStatement(n); base.VisitGotoStatement(n); }
        public override void VisitGosubStatement(GosubStatement n) { RecordStatement(n); base.VisitGosubStatement(n); }
        public override void VisitReturnStatement(ReturnStatement n) { RecordStatement(n); base.VisitReturnStatement(n); }
        public override void VisitExitStatement(ExitStatement n) { RecordStatement(n); base.VisitExitStatement(n); }
        public override void VisitStopStatement(StopStatement n) { RecordStatement(n); base.VisitStopStatement(n); }
        public override void VisitEndStatement(EndStatement n) { RecordStatement(n); base.VisitEndStatement(n); }
        public override void VisitRaiseEventStatement(RaiseEventStatement n) { RecordStatement(n); base.VisitRaiseEventStatement(n); }
        public override void VisitLocalDeclarationStatement(LocalDeclarationStatement n) { RecordStatement(n); base.VisitLocalDeclarationStatement(n); }
        public override void VisitRedimStatement(RedimStatement n) { RecordStatement(n); base.VisitRedimStatement(n); }
        public override void VisitLocalConstantStatement(LocalConstantStatement n) { RecordStatement(n); base.VisitLocalConstantStatement(n); }
        public override void VisitSetStatement(SetStatement n) { RecordStatement(n); base.VisitSetStatement(n); }
        public override void VisitLetStatement(LetStatement n) { RecordStatement(n); base.VisitLetStatement(n); }
        public override void VisitCallStatement(CallStatement n) { RecordStatement(n); base.VisitCallStatement(n); }
        public override void VisitLabelStatement(LabelStatement n) { RecordStatement(n); base.VisitLabelStatement(n); }
        public override void VisitPreprocessorStatement(PreprocessorStatement n) { RecordStatement(n); base.VisitPreprocessorStatement(n); }

        // ── Logical-operator counting in conditions ──

        // Count short-circuit-eligible logical operators (And / Or / Xor /
        // Eqv / Imp) inside one condition expression as additional
        // decision points. This is the "extended cyclomatic" form, which
        // is the common reading in modern static-analysis tools and
        // matches the spec's intent of "complexity should reflect each
        // independent path."
        private void CountConditionLogicals(Expression condition)
        {
            switch (condition)
            {
                case BinaryExpression bin
                    when bin.Operator is BinaryOperator.And
                                      or BinaryOperator.Or
                                      or BinaryOperator.Xor
                                      or BinaryOperator.Eqv
                                      or BinaryOperator.Imp:
                    _decisions++;
                    CountConditionLogicals(bin.Left);
                    CountConditionLogicals(bin.Right);
                    break;
                case BinaryExpression bin:
                    CountConditionLogicals(bin.Left);
                    CountConditionLogicals(bin.Right);
                    break;
                case UnaryExpression un:
                    CountConditionLogicals(un.Operand);
                    break;
                case ParenthesizedExpression p:
                    CountConditionLogicals(p.Inner);
                    break;
                // Other expression shapes carry no further logical
                // operators worth counting at this layer.
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static ProcedureKind KindOf(ParserProcedure proc) => proc switch
    {
        SubProcedure => ProcedureKind.Sub,
        FunctionProcedure => ProcedureKind.Function,
        PropertyProcedure { Kind: PropertyKind.Get } => ProcedureKind.PropertyGet,
        PropertyProcedure { Kind: PropertyKind.Let } => ProcedureKind.PropertyLet,
        PropertyProcedure { Kind: PropertyKind.Set } => ProcedureKind.PropertySet,
        // The abstract ProcedureDeclaration is only ever instantiated as
        // one of the three concrete subtypes above; throwing here makes
        // a future fourth subtype loud rather than silently mis-classified.
        _ => throw new InvalidOperationException(
            $"Unknown parser ProcedureDeclaration subtype: {proc.GetType().FullName}"),
    };

    private static ProcedureKind KindOfResolver(ResolverProcedure proc) => proc switch
    {
        SubDeclaration => ProcedureKind.Sub,
        FunctionDeclaration => ProcedureKind.Function,
        PropertyDeclaration { Kind: PropertyKind.Get } => ProcedureKind.PropertyGet,
        PropertyDeclaration { Kind: PropertyKind.Let } => ProcedureKind.PropertyLet,
        PropertyDeclaration { Kind: PropertyKind.Set } => ProcedureKind.PropertySet,
        _ => throw new InvalidOperationException(
            $"Unknown resolver ProcedureDeclaration subtype: {proc.GetType().FullName}"),
    };

    private static bool HasReturnValue(ParserProcedure proc) => proc switch
    {
        FunctionProcedure => true,
        PropertyProcedure { Kind: PropertyKind.Get } => true,
        _ => false,
    };

    private static bool ModuleContainsProcedure(Module ast, ParserProcedure procedureAst)
    {
        foreach (var item in ast.Items)
        {
            if (ReferenceEquals(item, procedureAst))
                return true;
        }
        return false;
    }

    private static ResolverProcedure? FindResolverProcedure(
        ModuleScope moduleScope, string procedureName, ProcedureKind kind)
    {
        // The resolver-side procedure declarations live as Declarations
        // on the ModuleScope (one per source procedure). Match by name +
        // kind so an overloaded property triplet resolves to the right
        // accessor; VBA forbids same-name procedures of the same kind in
        // one module so the (name, kind) pair is unique.
        foreach (var decl in moduleScope.Declarations)
        {
            if (decl is not ResolverProcedure proc) continue;
            if (!Declaration.NameComparer.Equals(proc.Name, procedureName)) continue;
            if (KindOfResolver(proc) != kind) continue;
            return proc;
        }
        return null;
    }

    private static IReadOnlyList<DependencyEdge> BuildDependencyEdges(
        IEnumerable<ResolverProcedure> procedures,
        ResolverProcedure? seedProcedure)
    {
        // Skip self-references. The VBA return-via-name idiom
        // (`MyFunc = expr` inside `Function MyFunc`) resolves the LHS
        // identifier to the function's own declaration; treating it as
        // a call edge inflates DirectDependencies with a degenerate
        // self-edge for every Function / PropertyGet body. True
        // recursive calls also resolve to self, but a procedure
        // calling itself is a structural fact already evident from the
        // procedure's existence — recording it as a dependency adds no
        // information for the downstream migration consumer.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<DependencyEdge>();
        foreach (var proc in procedures)
        {
            if (seedProcedure is not null && ReferenceEquals(proc, seedProcedure)) continue;
            var module = OwningModuleName(proc);
            var key = module + "::" + proc.Name + "::" + KindOfResolver(proc);
            if (!seen.Add(key)) continue;
            edges.Add(new DependencyEdge
            {
                ModuleName = module,
                ProcedureName = proc.Name,
                Kind = KindOfResolver(proc),
            });
        }
        // Determinism: the input is a HashSet whose iteration order is
        // not specified, so the output must be sorted before return.
        // Same input -> same output is a project-wide standard
        // (project-guidance.md -> VBA Parser Strategy).
        return SortEdges(edges);
    }

    private static IReadOnlyList<DependencyEdge> BuildCallerEdges(
        IEnumerable<ResolvedReference> references,
        ResolverProcedure? seedProcedure)
    {
        // Walk each reference up to its enclosing ProcedureScope; that
        // scope's Procedure is the caller. References emitted from
        // module-level positions (e.g., a Const initializer that names a
        // function) have no enclosing procedure scope and are skipped —
        // they are not "callers" in the call-graph sense.
        // Symmetric self-skip with BuildDependencyEdges: the
        // return-via-name use sites resolve to this procedure, so
        // GetReferencesTo(seed) sees them and would otherwise list the
        // seed as its own caller.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<DependencyEdge>();
        foreach (var reference in references)
        {
            var callerProc = FindEnclosingProcedure(reference.EnclosingScope);
            if (callerProc is null) continue;
            if (seedProcedure is not null && ReferenceEquals(callerProc, seedProcedure)) continue;
            var module = OwningModuleName(callerProc);
            var key = module + "::" + callerProc.Name + "::" + KindOfResolver(callerProc);
            if (!seen.Add(key)) continue;
            edges.Add(new DependencyEdge
            {
                ModuleName = module,
                ProcedureName = callerProc.Name,
                Kind = KindOfResolver(callerProc),
            });
        }
        // The reference list itself is in source order, but two
        // different references can share a caller and the dedup pass
        // above stabilises which one wins; pin the final edge order
        // explicitly so external observers do not depend on the
        // dedup-implementation tie-breaker.
        return SortEdges(edges);
    }

    private static ResolverProcedure? FindEnclosingProcedure(Scope scope)
    {
        for (Scope? s = scope; s is not null; s = s.Parent)
        {
            if (s is ProcedureScope ps)
                return ps.Procedure;
        }
        return null;
    }

    private static string OwningModuleName(ResolverProcedure proc)
    {
        // Walk the scope chain up from the procedure's own scope to the
        // ModuleScope; ModuleScope.ModuleName is the canonical answer.
        // Procedures whose Scope back-pointer is null (impossible after
        // ScopeBuildingVisitor finishes, but guarded here for forward
        // safety) report "<unknown>" — the empty string would be more
        // brittle to consumers searching by exact name.
        for (IDeclarationScope? s = proc.Scope; s is not null; )
        {
            if (s is ModuleScope ms) return ms.ModuleName;
            s = (s as Scope)?.Parent;
        }
        return "<unknown>";
    }

    private static IReadOnlyList<DependencyEdge> ComputeTransitiveClosure(
        HashSet<ResolverProcedure> startProcedures,
        CrossReferenceIndex crossReferences,
        ResolverProcedure? seedProcedure)
    {
        // BFS over the "called procedures of X" relation. Seed: the
        // direct callees. We need a per-procedure callee map; the index
        // tells us "who references this declaration", but we need the
        // inverse for the body we are walking. We rebuild the callee
        // set on the fly per procedure by walking that procedure's body
        // expressions (same trick the per-procedure classifier uses) —
        // but that is expensive across N procedures. Instead, derive
        // callees from the index: a procedure X calls Y iff X appears in
        // GetReferencesTo(Y) as the enclosing-scope procedure. So invert
        // the index once per BFS run, scoped to the procedures we visit.
        //
        // The cost is O(R) where R is the total reference count across
        // the indexed modules; typical projects have R in the low
        // thousands. Acceptable at the per-classification call site for
        // the foreseeable workload — the alternative (per-procedure body
        // walks) would be O(R) per BFS step.
        var callGraph = BuildCalleeMap(crossReferences);

        var result = new List<DependencyEdge>();
        var visited = new HashSet<ResolverProcedure>(ReferenceEqualityComparer.Instance);
        // Seed the BFS in deterministic order so the visit sequence —
        // and therefore any tie-breaking ordering downstream — does not
        // depend on HashSet enumeration. Same for the per-step callee
        // expansion below.
        var queue = new Queue<ResolverProcedure>(SortProcedures(startProcedures));
        // The seed procedure (the one being classified) is the root of
        // the BFS; do not add it to the result, but DO mark it visited
        // so a recursive call self-loop is not re-emitted.
        if (seedProcedure is not null)
            visited.Add(seedProcedure);

        while (queue.Count > 0)
        {
            var next = queue.Dequeue();
            if (!visited.Add(next)) continue;
            result.Add(new DependencyEdge
            {
                ModuleName = OwningModuleName(next),
                ProcedureName = next.Name,
                Kind = KindOfResolver(next),
            });
            if (callGraph.TryGetValue(next, out var callees))
            {
                foreach (var c in SortProcedures(callees))
                    if (!visited.Contains(c))
                        queue.Enqueue(c);
            }
        }
        // Sort the final result for the same reason BuildDependencyEdges
        // sorts: callers should observe the same edge sequence on every
        // run regardless of the BFS step-internal HashSet enumeration.
        return SortEdges(result);
    }

    // Sort by (ModuleName, ProcedureName, Kind) using the resolver's
    // ordinal-case-insensitive name comparer for consistency with the
    // rest of the resolver graph (VBA names are case-insensitive). Kind
    // is broken by enum value for stability.
    private static IReadOnlyList<DependencyEdge> SortEdges(List<DependencyEdge> edges)
    {
        edges.Sort(static (a, b) =>
        {
            int c = StringComparer.OrdinalIgnoreCase.Compare(a.ModuleName, b.ModuleName);
            if (c != 0) return c;
            c = StringComparer.OrdinalIgnoreCase.Compare(a.ProcedureName, b.ProcedureName);
            if (c != 0) return c;
            return ((int)a.Kind).CompareTo((int)b.Kind);
        });
        return edges;
    }

    // Procedure-level sort with the same comparator semantics as
    // SortEdges, used for ordering BFS enqueues so the visit sequence
    // is independent of HashSet enumeration order.
    private static IEnumerable<ResolverProcedure> SortProcedures(IEnumerable<ResolverProcedure> source)
    {
        return source
            .OrderBy(p => OwningModuleName(p), StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => (int)KindOfResolver(p));
    }

    private static Dictionary<ResolverProcedure, HashSet<ResolverProcedure>> BuildCalleeMap(
        CrossReferenceIndex crossReferences)
    {
        // Invert the cross-reference index for procedure-to-procedure
        // calls only: for every indexed Declaration that is a procedure,
        // and every reference site whose enclosing ProcedureScope's
        // Procedure is itself a procedure, record (caller -> callee).
        var map = new Dictionary<ResolverProcedure, HashSet<ResolverProcedure>>(
            ReferenceEqualityComparer.Instance);
        foreach (var decl in crossReferences.Declarations)
        {
            if (decl is not ResolverProcedure callee) continue;
            foreach (var reference in crossReferences.GetReferencesTo(callee))
            {
                var caller = FindEnclosingProcedure(reference.EnclosingScope);
                if (caller is null) continue;
                if (!map.TryGetValue(caller, out var set))
                {
                    set = new HashSet<ResolverProcedure>(ReferenceEqualityComparer.Instance);
                    map[caller] = set;
                }
                set.Add(callee);
            }
        }
        return map;
    }

    private static IReadOnlyList<ClassificationCategory> SnapshotCategories(
        List<ClassificationCategory> source)
    {
        // Pure is "absence of signals", not its own signal among others —
        // collapse to a singleton Pure list when nothing else fired.
        if (source.Count == 0) return new[] { ClassificationCategory.Pure };
        return source.ToArray();
    }

    private static ClassificationCategory PickPrimary(List<ClassificationCategory> categories)
    {
        if (categories.Count == 0) return ClassificationCategory.Pure;
        // Precedence per FunctionClassification.PrimaryCategory's doc.
        if (categories.Contains(ClassificationCategory.UIBound))
            return ClassificationCategory.UIBound;
        if (categories.Contains(ClassificationCategory.DataAccess))
            return ClassificationCategory.DataAccess;
        if (categories.Contains(ClassificationCategory.ExternalIO))
            return ClassificationCategory.ExternalIO;
        if (categories.Contains(ClassificationCategory.LateBound))
            return ClassificationCategory.LateBound;
        return ClassificationCategory.Pure;
    }

    private static bool ComputeIsMixed(List<ClassificationCategory> categories)
    {
        // Mixed iff the body emitted more than one non-Pure category, OR
        // emitted LateBound alongside any other signal (LateBound by
        // itself just means "we don't know"; combined with any other
        // signal it means the static picture is incomplete in a way the
        // consumer must reconcile).
        if (categories.Count <= 1) return false;
        return true;
    }
}
