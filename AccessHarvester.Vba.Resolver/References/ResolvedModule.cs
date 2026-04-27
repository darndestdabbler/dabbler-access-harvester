using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.Scopes;

namespace AccessHarvester.Vba.Resolver.References;

// The per-module resolver result. Bundles the parser AST, the scope
// tree root, the flat list of ResolvedReferences, the unresolved
// subset, and the diagnostics list — plus O(1) lookups from an AST
// Expression node to its ResolvedReference and to its bound
// Declaration (when one exists).
//
// Constructed by ReferenceResolver.Resolve; not meant to be
// instantiated directly by consumers (the constructor is public for
// testability and future extension only). All public surfaces are
// immutable views over fields materialised at construction time.
//
// Algorithm and stability commitments documented in
// docs/session-sets/resolver-references-and-with-blocks/synthesis/
// session-3-resolved-module.md.
public sealed class ResolvedModule
{
    private readonly Dictionary<Expression, ResolvedReference> _byExpression;

    public ResolvedModule(
        Module ast,
        ModuleScope moduleScope,
        IReadOnlyList<ResolvedReference> references)
    {
        Ast = ast ?? throw new ArgumentNullException(nameof(ast));
        ModuleScope = moduleScope ?? throw new ArgumentNullException(nameof(moduleScope));
        if (references is null) throw new ArgumentNullException(nameof(references));

        // Defensive copy: the input list may be a mutable List<T> the
        // caller could later mutate, which would desynchronise the
        // public surfaces from the lookup map. Materialising once here
        // freezes the resolver result into immutable views.
        var frozen = new ResolvedReference[references.Count];
        for (int i = 0; i < references.Count; i++) frozen[i] = references[i];

        var unresolved = new List<ResolvedReference>();
        _byExpression = new Dictionary<Expression, ResolvedReference>(
            ReferenceEqualityComparer.Instance);
        foreach (var r in frozen)
        {
            // Last-write-wins for the lookup map per §3.1; the resolver
            // walker emits one entry per AST node under normal operation
            // so this only matters defensively.
            _byExpression[r.Expression] = r;
            if (r.Outcome is ResolutionOutcome.Unresolved)
                unresolved.Add(r);
        }

        var diagnostics = new List<ResolverDiagnostic>(unresolved.Count);
        foreach (var r in unresolved)
        {
            var reason = ((ResolutionOutcome.Unresolved)r.Outcome).Reason;
            diagnostics.Add(new ResolverDiagnostic(
                Severity: SeverityFor(reason),
                Code: reason,
                Message: MessageFor(reason, r),
                Location: r.Location));
        }

        References = new ReadOnlyCollection<ResolvedReference>(frozen);
        UnresolvedReferences = new ReadOnlyCollection<ResolvedReference>(unresolved);
        Diagnostics = new ReadOnlyCollection<ResolverDiagnostic>(diagnostics);
    }

    public Module Ast { get; }

    public ModuleScope ModuleScope { get; }

    public IReadOnlyList<ResolvedReference> References { get; }

    public IReadOnlyList<ResolvedReference> UnresolvedReferences { get; }

    public IReadOnlyList<ResolverDiagnostic> Diagnostics { get; }

    public ResolvedReference? TryGetReference(Expression expression)
    {
        if (expression is null) throw new ArgumentNullException(nameof(expression));
        return _byExpression.TryGetValue(expression, out var r) ? r : null;
    }

    // Collapses the outcome to a bound Declaration when one exists, per
    // session-3 §3.2. Returns null for ResolvedToBuiltIn, ResolvedToMe,
    // Unresolved, or any expression with no recorded reference.
    public Declaration? TryGetDeclaration(Expression expression)
    {
        var reference = TryGetReference(expression);
        if (reference is null) return null;
        return DeclarationFromOutcome(reference.Outcome);
    }

    private static Declaration? DeclarationFromOutcome(ResolutionOutcome outcome)
    {
        // S2 §3 invariant: ResolvedViaWithFlattening.InnerOutcome is
        // never another ResolvedViaWithFlattening, so the recursion is
        // bounded at one level. The recursive form keeps the code
        // robust if the invariant ever changes.
        return outcome switch
        {
            ResolutionOutcome.Resolved r => r.Declaration,
            ResolutionOutcome.ResolvedToObjectModel r => r.Declaration,
            ResolutionOutcome.ResolvedViaWithFlattening f => DeclarationFromOutcome(f.InnerOutcome),
            _ => null,
        };
    }

    private static ResolverDiagnosticSeverity SeverityFor(string reason) => reason switch
    {
        UnresolvedReasons.NotInScope => ResolverDiagnosticSeverity.Warning,
        UnresolvedReasons.MeInStandardModule => ResolverDiagnosticSeverity.Warning,
        UnresolvedReasons.DefaultMemberNotYetResolved => ResolverDiagnosticSeverity.Info,
        UnresolvedReasons.WithBlockFlatteningDeferred => ResolverDiagnosticSeverity.Info,
        UnresolvedReasons.MemberAccessOnUnresolvedTarget => ResolverDiagnosticSeverity.Info,
        // Unknown reason strings would mean a future session added a
        // reason without updating this mapping; keep them visible at
        // Warning rather than silently dropping to Info.
        _ => ResolverDiagnosticSeverity.Warning,
    };

    private static string MessageFor(string reason, ResolvedReference r) => reason switch
    {
        UnresolvedReasons.NotInScope =>
            $"Name '{NameOf(r.Expression)}' is not in scope.",
        UnresolvedReasons.MeInStandardModule =>
            "'Me' is not valid in a standard module.",
        UnresolvedReasons.DefaultMemberNotYetResolved =>
            $"Member '{MemberOf(r.Expression)}' depends on default-member resolution (deferred).",
        UnresolvedReasons.WithBlockFlatteningDeferred =>
            "Implicit With-block member appeared outside any With block.",
        UnresolvedReasons.MemberAccessOnUnresolvedTarget =>
            $"Cannot resolve member '{MemberOf(r.Expression)}' on an unresolved target.",
        _ => reason,
    };

    private static string NameOf(Expression e) => e switch
    {
        IdentifierExpression i => i.Name,
        MemberAccessExpression m => m.MemberName,
        BangAccessExpression b => b.MemberName,
        ImplicitWithMemberExpression w => w.MemberName,
        ImplicitWithBangExpression w => w.MemberName,
        _ => "<expression>",
    };

    private static string MemberOf(Expression e) => e switch
    {
        MemberAccessExpression m => m.MemberName,
        BangAccessExpression b => b.MemberName,
        ImplicitWithMemberExpression w => w.MemberName,
        ImplicitWithBangExpression w => w.MemberName,
        _ => NameOf(e),
    };
}
