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
// session-1-resolution-algorithm.md. Future sessions extend the
// algorithm by replacing specific Unresolved branches:
//   Session 2 → Unresolved("with-block-flattening-deferred") replaced
//               by WithBlockScope flattening
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
            => Emit(n, new ResolutionOutcome.Unresolved(
                UnresolvedReasons.WithBlockFlatteningDeferred), CurrentScope);

        public override void VisitImplicitWithBangExpression(ImplicitWithBangExpression n)
            => Emit(n, new ResolutionOutcome.Unresolved(
                UnresolvedReasons.WithBlockFlatteningDeferred), CurrentScope);

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

            // Step 2: Access object model registry top-level.
            var topLevel = _registry.TryResolveTopLevelMember(name);
            if (topLevel is not null)
                return new ResolutionOutcome.ResolvedToObjectModel(topLevel);

            // Step 3: built-in functions.
            var builtIn = BuiltInFunctionRegistry.TryGet(name);
            if (builtIn is not null)
                return new ResolutionOutcome.ResolvedToBuiltIn(builtIn);

            // Step 4: explicit unresolved.
            return new ResolutionOutcome.Unresolved(UnresolvedReasons.NotInScope);
        }

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
                // Subform/subreport navigation. When the resolved
                // target is a control declaration and the member is
                // ".Form" / ".Report", the registry should expose a
                // synthetic declaration for the contained form. The
                // exact protocol is #11's responsibility; Session 1
                // wires the call, the registry stub returns null.
                _ = parentDecl;
                _ = member;
                return null;
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
