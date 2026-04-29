using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.Scopes;

namespace AccessHarvester.Vba.Resolver.References;

// Reference-resolution algorithm per Phase 3.3 of the parser-migration
// plan. Inputs: the ModuleScope built by #7's ScopeBuildingVisitor and
// the parser AST for the same module. Output: one ResolvedReference
// per name-use site.
//
// Side effect: when a use resolves to a Declaration, the declaration's
// AddReference is called so #7's reverse-lookup (`References` list) is
// populated. This lets Phase 3.7's cross-reference index drop in
// without a re-pass. ResolvedToObjectModel hits also populate the
// declaration (a synthetic one from the registry); ResolvedToBuiltIn,
// ResolvedToMe, and Unresolved have no Declaration to update.
//
// Algorithm shape, scope, and explicit non-goals are documented in
// docs/session-sets/resolver-references-and-with-blocks/synthesis/
// session-1-resolution-algorithm.md. Session 2 extended the resolver
// with a With-block flattening pass — see
// session-2-with-block-flattening.md for the algorithm. Future
// sessions extend further:
//   Session 3 → ResolvedModule wrapper around the returned list
//   #9 / Phase 3.5 → Unresolved("default-member-not-yet-resolved")
//                    replaced by default-member resolver
public sealed class ReferenceResolver
{
    private readonly IAccessObjectModelRegistry _accessRegistry;

    public ReferenceResolver(IAccessObjectModelRegistry? accessRegistry = null)
    {
        _accessRegistry = accessRegistry ?? NullAccessObjectModelRegistry.Instance;
    }

    public IReadOnlyList<ResolvedReference> ResolveModule(ModuleScope moduleScope, Module module)
    {
        if (moduleScope is null) throw new ArgumentNullException(nameof(moduleScope));
        if (module is null) throw new ArgumentNullException(nameof(module));

        var collector = new List<ResolvedReference>();
        var walker = new ResolutionWalker(_accessRegistry, moduleScope, collector);
        walker.Visit(module);
        return collector;
    }

    // Session 3 wrapper. Returns a ResolvedModule bundling the AST,
    // scope, references, unresolved subset, diagnostics, and lookup
    // helpers. Internally calls ResolveModule and then constructs the
    // wrapper — the walker is unchanged. See
    // session-3-resolved-module.md §5.
    public ResolvedModule Resolve(ModuleScope moduleScope, Module module)
    {
        var references = ResolveModule(moduleScope, module);
        return new ResolvedModule(module, moduleScope, references);
    }

    // The walker is a private nested class so the public surface stays
    // tiny (one method) and consumers cannot reach its mutable state.
    private sealed class ResolutionWalker : VbaSyntaxWalker
    {
        private readonly IAccessObjectModelRegistry _registry;
        private readonly ModuleScope _module;
        private readonly List<ResolvedReference> _collector;
        // Reference-keyed map of expression-node → outcome. The
        // member-access dispatch consults this to determine the
        // target's outcome rather than scanning _collector, which
        // would give the wrong answer when the target is a
        // non-emitting wrapper (e.g., IndexOrCallExpression).
        private readonly Dictionary<Expression, ResolutionOutcome> _outcomesByExpression
            = new(ReferenceEqualityComparer.Instance);
        private readonly Stack<Scope> _scopeStack = new();

        public ResolutionWalker(
            IAccessObjectModelRegistry registry,
            ModuleScope module,
            List<ResolvedReference> collector)
        {
            _registry = registry;
            _module = module;
            _collector = collector;
            _scopeStack.Push(module);
        }

        private Scope CurrentScope => _scopeStack.Peek();

        // ── Module entry: walk items in source order. We rely on
        // VbaSyntaxWalker.DefaultVisit to recurse into Module children;
        // overriding leaf nodes lets us skip OptionDirective /
        // ImplementsDirective / PreprocessorDirective (no name uses
        // at this layer) without affecting the rest.
        public override void VisitOptionDirective(OptionDirective n) { }
        public override void VisitImplementsDirective(ImplementsDirective n) { }
        public override void VisitPreprocessorDirective(PreprocessorDirective n) { }
        public override void VisitPreprocessorStatement(PreprocessorStatement n) { }

        // ── Procedures: push the matching ProcedureScope, walk
        // parameters (their default-value expressions may name things)
        // and the body, pop.
        public override void VisitSubProcedure(SubProcedure n)
            => WalkProcedure(n.Provenance, n.Parameters, n.Body);

        public override void VisitFunctionProcedure(FunctionProcedure n)
            => WalkProcedure(n.Provenance, n.Parameters, n.Body);

        public override void VisitPropertyProcedure(PropertyProcedure n)
            => WalkProcedure(n.Provenance, n.Parameters, n.Body);

        private void WalkProcedure(
            Provenance procProvenance,
            IReadOnlyList<Parameter> parameters,
            IReadOnlyList<Statement> body)
        {
            var procScope = MatchChildScope<ProcedureScope>(_module, procProvenance);
            // Best-effort fallback: if the structural match fails (a
            // bug in scope construction or a Provenance mismatch in
            // future parser-AST changes), continue walking with the
            // module scope as enclosing context.
            var enclosing = (Scope?)procScope ?? _module;
            _scopeStack.Push(enclosing);
            try
            {
                foreach (var p in parameters)
                    Visit(p);
                foreach (var s in body)
                    Visit(s);
            }
            finally
            {
                _scopeStack.Pop();
            }
        }

        // ── With and ForEach scopes: push the matching scope-tree
        // entry, walk the body, pop. The Target / Collection
        // expression of the loop construct is resolved AT the
        // enclosing (outer) scope, not inside the new scope — those
        // expressions don't see their own loop scope as a binding
        // context.
        public override void VisitWithStatement(WithStatement n)
        {
            // Target is resolved against the enclosing scope.
            Visit(n.Target);
            var withScope = MatchChildScope<WithBlockScope>(CurrentScope, n.Provenance);
            var enclosing = (Scope?)withScope ?? CurrentScope;
            _scopeStack.Push(enclosing);
            try
            {
                foreach (var s in n.Body) Visit(s);
            }
            finally
            {
                _scopeStack.Pop();
            }
        }

        public override void VisitForEachStatement(ForEachStatement n)
        {
            // The collection expression is evaluated at the enclosing
            // scope. Visit it BEFORE pushing ForEachScope so any names
            // it references resolve against the outer chain.
            Visit(n.Collection);

            var forScope = MatchChildScope<ForEachScope>(CurrentScope, n.Provenance);
            var enclosing = (Scope?)forScope ?? CurrentScope;
            _scopeStack.Push(enclosing);
            try
            {
                // The loop variable itself is recorded as
                // ForEachScope.LoopVariable (not a Declaration), so
                // identifier-resolution for the loop variable's uses
                // inside the body falls through the enclosing
                // ProcedureScope chain via the normal §4.3 path.
                foreach (var s in n.Body) Visit(s);
            }
            finally
            {
                _scopeStack.Pop();
            }
        }

        // ── Reference sites ─────────────────────────────────────────

        public override void VisitIdentifierExpression(IdentifierExpression n)
            => Emit(n, ResolveIdentifier(n.Name), CurrentScope);

        public override void VisitMeExpression(MeExpression n)
        {
            ResolutionOutcome outcome = _module.OwningClass is { } cls
                ? new ResolutionOutcome.ResolvedToMe(cls)
                : new ResolutionOutcome.Unresolved(UnresolvedReasons.MeInStandardModule);
            Emit(n, outcome, CurrentScope);
        }

        public override void VisitBangAccessExpression(BangAccessExpression n)
        {
            Visit(n.Target);
            Emit(n, DecideMemberOutcome(n.Target, n.MemberName, isBang: true), CurrentScope);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpression n)
        {
            Visit(n.Target);
            Emit(n, DecideMemberOutcome(n.Target, n.MemberName, isBang: false), CurrentScope);
        }

        public override void VisitImplicitWithMemberExpression(ImplicitWithMemberExpression n)
            => EmitImplicitWithFlattening(n, n.MemberName, isBang: false);

        public override void VisitImplicitWithBangExpression(ImplicitWithBangExpression n)
            => EmitImplicitWithFlattening(n, n.MemberName, isBang: true);

        // Phase 3.4 With-block flattening. Synthesises the full
        // reference (`withTarget.member` or `withTarget!member`),
        // resolves it against the With target's recorded outcome, and
        // emits the original implicit-with node with a
        // ResolvedViaWithFlattening outcome carrying both the
        // synthesised AST and the inner outcome. Algorithm details:
        // session-2-with-block-flattening.md §4.
        private void EmitImplicitWithFlattening(
            Expression site, string memberName, bool isBang)
        {
            var withScope = FindEnclosingWith(CurrentScope);
            if (withScope is null)
            {
                // Defensive: parser does not produce ImplicitWith*
                // outside a With body. Falling back to Session 1's
                // deferred reason keeps the closed reason set populated
                // for any future grammar/scope-construction defect.
                Emit(site, new ResolutionOutcome.Unresolved(
                    UnresolvedReasons.WithBlockFlatteningDeferred), CurrentScope);
                return;
            }

            Expression withTarget = withScope.Target;

            // When the With target is itself an implicit-with member
            // (e.g., `With .Foo` inside an outer `With`), its recorded
            // outcome is already ResolvedViaWithFlattening. The
            // SynthesizedTarget on the outcome is the fully-expanded
            // reference; substitute it so the synthesised target we
            // build below does not still contain an implicit node.
            // Round-1 verifier finding (Major).
            //
            // For outcome dispatch we still pass the ORIGINAL withTarget
            // — DecideMemberOutcome's peek-through reads the recorded
            // outcome for the original AST node and unwraps the
            // ResolvedViaWithFlattening to its inner outcome.
            Expression effectiveTarget = ExpandFlattenedTarget(withTarget);
            Expression synthesized = isBang
                ? new BangAccessExpression(site.Provenance, effectiveTarget, memberName)
                : new MemberAccessExpression(site.Provenance, effectiveTarget, memberName);

            var innerOutcome = DecideMemberOutcome(withTarget, memberName, isBang);
            Emit(site,
                new ResolutionOutcome.ResolvedViaWithFlattening(synthesized, innerOutcome),
                CurrentScope);
        }

        private Expression ExpandFlattenedTarget(Expression target)
        {
            var unwrapped = UnwrapTransparent(target);
            if (_outcomesByExpression.TryGetValue(unwrapped, out var outcome)
                && outcome is ResolutionOutcome.ResolvedViaWithFlattening f)
            {
                // Recursive in case f.SynthesizedTarget itself wraps
                // another flattenable node. By the source-order pass's
                // construction, f.SynthesizedTarget was fully expanded
                // when f was first emitted — the recursion is defensive.
                return ExpandFlattenedTarget(f.SynthesizedTarget);
            }
            return unwrapped;
        }

        private static WithBlockScope? FindEnclosingWith(Scope from)
        {
            for (Scope? s = from; s is not null; s = s.Parent)
                if (s is WithBlockScope w) return w;
            return null;
        }

        // Index-or-call: walk target and arguments. Not a reference
        // site itself — the target's emission covers the callable name.
        // No outcome is recorded for the IndexOrCall node, so an
        // outer member-access dispatch that asks for this node's
        // outcome will see "no recorded outcome" and fall back
        // appropriately (DecideMemberOutcome handles that case).
        public override void VisitIndexOrCallExpression(IndexOrCallExpression n)
        {
            Visit(n.Target);
            foreach (var arg in n.Arguments) Visit(arg);
        }

        // ── Resolution helpers ──────────────────────────────────────

        private ResolutionOutcome ResolveIdentifier(string name)
        {
            // Step 1: scope chain walk.
            for (Scope? s = CurrentScope; s is not null; s = s.Parent)
            {
                var hit = s.TryFindLocal(name);
                if (hit is not null)
                    return new ResolutionOutcome.Resolved(hit);
            }

            // Step 1.5: cross-module Public lookup. The scope-chain walk
            // bottoms out at ProjectScope, which by design owns no
            // declarations of its own (see Scopes/Scope.cs ProjectScope
            // comment). Public declarations in other Standard modules of
            // the same project are visible bare from any module in the
            // project; this step closes the gap surfaced by the
            // vba-symbol-db Session 7 comprehension probe (e.g.,
            // `GetString` and `StringFormat` calls in a Form module
            // referencing modStrings.GetString / modStrings.StringFormat).
            // Public-enum members spill into their owning module's Public
            // namespace under VBA's enum visibility rule, so a bare use
            // of an enum value (e.g., `sCompanyDelete_CompanyTypeChange`)
            // also binds here when the enum is Public in a sibling module.
            //
            // Restricted to Standard modules: Form/Report/Class modules
            // host class members, not bare-callable functions. Public
            // procedures in those modules are reached via the class type
            // (`Forms!frmFoo.Bar`), not as bare names from other modules.
            // Option Private Module is intentionally NOT filtered here —
            // it suppresses visibility to host-VBA consumers (Excel/Access
            // calling into the project) but leaves intra-project Public
            // declarations visible to other modules in the same project.
            var crossModule = TryResolveAcrossModules(name);
            if (crossModule is not null)
                return new ResolutionOutcome.Resolved(crossModule);

            // Step 2: form / report class-context lookup. VBA allows
            // bare control name access from a form / report module's
            // code-behind (e.g. `txtFirstName = ""` rather than the
            // explicit `Me.txtFirstName = ""`). The control resolution
            // is the same one MeRegistryLookup performs in
            // DecideMemberOutcome — but reached through the bare-name
            // path rather than `Me.<name>`. Locals and module-level
            // declarations (Step 1) shadow controls per VBA rules.
            if (_module.OwningClass is { } owningClass)
            {
                Declaration? ctlHit = _module.Kind switch
                {
                    ModuleKind.Form   => _registry.TryResolveControlOnForm(owningClass.Name, name),
                    ModuleKind.Report => _registry.TryResolveControlOnReport(owningClass.Name, name),
                    _ => null,
                };
                if (ctlHit is not null)
                    return new ResolutionOutcome.ResolvedToObjectModel(ctlHit);
            }

            // Step 3: Access object model registry top-level.
            var topLevel = _registry.TryResolveTopLevelMember(name);
            if (topLevel is not null)
                return new ResolutionOutcome.ResolvedToObjectModel(topLevel);

            // Step 4: built-in functions.
            var builtIn = BuiltInFunctionRegistry.TryGet(name);
            if (builtIn is not null)
                return new ResolutionOutcome.ResolvedToBuiltIn(builtIn);

            // Step 5: explicit unresolved.
            return new ResolutionOutcome.Unresolved(UnresolvedReasons.NotInScope);
        }

        // Cross-module Public lookup helper for Step 1.5. Returns the
        // first matching Public declaration found in any sibling
        // Standard module of the same ProjectScope, or a Public-enum
        // member of such a module's Public enum, or null when no match
        // exists. Match is case-insensitive per VBA name comparison.
        private Declaration? TryResolveAcrossModules(string name)
        {
            // _module is the resolved module; its Parent is the project root
            // when the scope tree was built normally. Defensive: if the
            // module has no ProjectScope parent (a hand-constructed test
            // module, or a future scope-tree shape change), fall through.
            if (_module.Parent is not ProjectScope project)
                return null;

            foreach (var child in project.Children)
            {
                if (child is not ModuleScope sibling) continue;
                if (ReferenceEquals(sibling, _module)) continue;
                if (sibling.Kind != ModuleKind.Standard) continue;

                foreach (var decl in sibling.Declarations)
                {
                    if (IsPublicVisible(decl)
                        && Declaration.NameComparer.Equals(decl.Name, name))
                    {
                        return decl;
                    }

                    // Public-enum member spillover: a Public Enum's members
                    // are visible bare in any module of the same project.
                    if (decl is Declarations.EnumDeclaration enumDecl
                        && enumDecl.Access == AccessLevel.Public)
                    {
                        foreach (var member in enumDecl.Members)
                        {
                            if (Declaration.NameComparer.Equals(member.Name, name))
                                return member;
                        }
                    }
                }
            }
            return null;
        }

        // True when the declaration is Public at module level — i.e.,
        // visible to other modules in the same project. Returns false
        // for declarations that have no module-level Access concept
        // (parameters, locals, type-members, implicit declarations).
        private static bool IsPublicVisible(Declaration decl) => decl switch
        {
            Declarations.ProcedureDeclaration p => p.Access == AccessLevel.Public,
            DeclareDeclaration d   => d.Access == AccessLevel.Public,
            Declarations.EnumDeclaration e => e.Access == AccessLevel.Public,
            Declarations.EventDeclaration ev => ev.Access == AccessLevel.Public,
            UserDefinedTypeDeclaration t => t.Access == AccessLevel.Public,
            VariableDeclaration v  => v.Access == AccessLevel.Public,
            _ => false,
        };

        private ResolutionOutcome DecideMemberOutcome(
            Expression target, string memberName, bool isBang)
        {
            // Unwrap transparent wrappers (parentheses, recursively)
            // before looking up the target's outcome. Without this,
            // `(Me)!ctl` would lose the inner Me's outcome and fall
            // through to default-member-not-yet-resolved instead of
            // routing through the registry. Round-2 verifier finding.
            //
            // Non-transparent wrappers (IndexOrCall, BinaryExpression,
            // etc.) intentionally are NOT unwrapped: their result type
            // differs from the inner target's, so dispatching on the
            // inner outcome would be wrong. Those fall through to
            // default-member-not-yet-resolved as before.
            _ = isBang; // bang vs. dot does not change Session 1 dispatch
            var logicalTarget = UnwrapTransparent(target);
            ResolutionOutcome? targetOutcome = null;
            if (_outcomesByExpression.TryGetValue(logicalTarget, out var recorded))
                targetOutcome = recorded;

            // Peek through Session 2's With-flattening wrapper so a
            // chained `.Foo.Bar` whose target is itself an implicit-with
            // member dispatches on the inner outcome. The peek is
            // structurally bounded: ResolvedViaWithFlattening.InnerOutcome
            // is never another ResolvedViaWithFlattening (session-2
            // synthesis §3 invariant), so the loop runs at most once.
            while (targetOutcome is ResolutionOutcome.ResolvedViaWithFlattening flattened)
                targetOutcome = flattened.InnerOutcome;

            switch (targetOutcome)
            {
                case ResolutionOutcome.ResolvedToMe me when MeRegistryLookup(me.OwningClass, memberName) is { } registryHit:
                    return new ResolutionOutcome.ResolvedToObjectModel(registryHit);

                case ResolutionOutcome.ResolvedToObjectModel objModel when SubChildLookup(objModel.Declaration, memberName) is { } sub:
                    return new ResolutionOutcome.ResolvedToObjectModel(sub);

                case ResolutionOutcome.Resolved resolved when SubChildLookup(resolved.Declaration, memberName) is { } sub:
                    return new ResolutionOutcome.ResolvedToObjectModel(sub);

                case ResolutionOutcome.Unresolved:
                    return new ResolutionOutcome.Unresolved(
                        UnresolvedReasons.MemberAccessOnUnresolvedTarget);

                default:
                    // Resolved-with-no-registry-hit, ResolvedToBuiltIn,
                    // ResolvedToMe-without-registry-hit, or no recorded
                    // target outcome (non-emitting wrapper). All fall
                    // back to default-member-not-yet-resolved pending
                    // Phase 3.5 work.
                    return new ResolutionOutcome.Unresolved(
                        UnresolvedReasons.DefaultMemberNotYetResolved);
            }

            // Local helpers ----------------------------------------------------

            Declaration? MeRegistryLookup(ClassDeclaration owningClass, string member)
            {
                // Form/Report owning classes: ask the registry for the
                // named control. The class declaration's name is the
                // module name (form/report name) per #7's class-decl
                // construction.
                return _module.Kind switch
                {
                    ModuleKind.Form   => _registry.TryResolveControlOnForm(owningClass.Name, member),
                    ModuleKind.Report => _registry.TryResolveControlOnReport(owningClass.Name, member),
                    _ => null,
                };
            }

            Declaration? SubChildLookup(Declaration parentDecl, string member)
            {
                // Member resolution for synthetic declarations
                // (controls, forms, reports, host collections, type-
                // library members) and for subform / subreport
                // navigation. Source-defined parents fall through to
                // null and the existing default-member-not-yet-resolved
                // path. The Access-backed implementation lives in
                // AccessIntegration.AccessObjectModelRegistry; the
                // NullAccessObjectModelRegistry default returns null,
                // preserving the original Session-1 semantics for
                // resolver runs that do not supply Access metadata.
                return _registry.TryResolveMemberOf(parentDecl, member);
            }
        }

        private void Emit(Expression expr, ResolutionOutcome outcome, Scope enclosing)
        {
            var location = MaterializeRange(expr.Provenance);
            var entry = new ResolvedReference(expr, location, enclosing, outcome);
            _collector.Add(entry);
            _outcomesByExpression[expr] = outcome;

            // Side effect: when the outcome is Resolved or
            // ResolvedToObjectModel, populate the declaration's
            // reverse-lookup. ResolvedToMe and ResolvedToBuiltIn
            // have no Declaration to attach to. Unresolved likewise.
            // For ResolvedViaWithFlattening, peek through to the inner
            // outcome and back-populate from it — flattened sites
            // contribute to the declaration's References list just like
            // ordinary resolved sites do.
            BackPopulate(outcome, location);
        }

        private static void BackPopulate(ResolutionOutcome outcome, SourceRange location)
        {
            switch (outcome)
            {
                case ResolutionOutcome.Resolved r:
                    r.Declaration.AddReference(new DeclarationReference(
                        Location: location, IsImplicit: false));
                    break;
                case ResolutionOutcome.ResolvedToObjectModel r:
                    r.Declaration.AddReference(new DeclarationReference(
                        Location: location, IsImplicit: false));
                    break;
                case ResolutionOutcome.ResolvedViaWithFlattening f:
                    BackPopulate(f.InnerOutcome, location);
                    break;
            }
        }

        // Recursively unwrap transparent wrappers so the logical
        // target of a member access can be looked up in
        // `_outcomesByExpression`. Currently only ParenthesizedExpression
        // qualifies as transparent — the parser has no other "preserve
        // type, just group" expression node. If future grammar work
        // adds more (e.g., explicit type-annotation expressions), they
        // belong here.
        private static Expression UnwrapTransparent(Expression expr)
        {
            while (expr is ParenthesizedExpression p)
                expr = p.Inner;
            return expr;
        }

        // Match a child scope of `parent` whose Range starts at the
        // same line+column as the AST node's provenance. SourceRange
        // equality would be simpler but tightens coupling to whether
        // ScopeBuildingVisitor's range fallback synthesizes the same
        // end-column as the AST's provenance does — start-only
        // matching is robust to that.
        private static T? MatchChildScope<T>(Scope parent, Provenance astProvenance)
            where T : Scope
        {
            int astLine = astProvenance.Range?.Start.Line ?? astProvenance.Line;
            int astColumn = astProvenance.Range?.Start.Column ?? astProvenance.Column;
            foreach (var c in parent.Children)
            {
                if (c is T t && c.Range is { } range
                    && range.Start.Line == astLine
                    && range.Start.Column == astColumn)
                    return t;
            }
            return null;
        }

        // Materialize a SourceRange from an expression's Provenance.
        // Falls back to a single-character range derived from
        // Line+Column when Provenance.Range is still null in this
        // transitional parser-infrastructure state. Mirrors
        // ScopeBuildingVisitor's ToSourceRange logic so the two
        // pipelines see consistent locations.
        private static SourceRange MaterializeRange(Provenance p)
            => p.Range ?? new SourceRange(
                new SourceLocation("", p.Line, p.Column),
                new SourceLocation("", p.Line, p.Column + 1));
    }
}
