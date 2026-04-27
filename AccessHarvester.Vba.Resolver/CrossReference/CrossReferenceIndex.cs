using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;

namespace AccessHarvester.Vba.Resolver.CrossReference;

// Phase 3.7 cross-reference index: an inverse of the
// reference→declaration map exposed by ResolvedModule. Given a
// Declaration, returns every ResolvedReference site that resolved to
// it across the supplied set of modules.
//
// Construction is a single pass over the input modules' References
// lists (per spec Feature 3 Standards). Downstream enrichment
// (#16 enrichment-functions-navigation-review) queries via
// GetReferencesTo and never reconstructs the index itself.
//
// Outcome handling mirrors ResolvedModule.TryGetDeclaration semantics
// — only outcomes that ResolvedModule treats as having a bound
// declaration are indexed:
//
//   • Resolved                → keyed by .Declaration
//   • ResolvedToObjectModel   → keyed by .Declaration
//   • ResolvedViaWithFlattening → recurses into InnerOutcome (S2 §3
//                                 invariant: at most one wrap level,
//                                 but the recursive form is robust if
//                                 that ever changes)
//   • ResolvedToMe / ResolvedToBuiltIn / Unresolved → not indexed
//
// Per Lean By Default: the index returns ResolvedReference values
// (not a separate Reference DTO). Consumers already receive the AST
// node, source range, enclosing scope, and resolution outcome — the
// information call-graph and impact-analysis enrichment needs.
//
// Equality model: declarations are compared by reference identity (the
// resolver graph holds one Declaration instance per declaration site),
// matching Declaration's class-not-record design noted in
// Declaration.cs. Using ReferenceEqualityComparer for the dictionary
// avoids any accidental dependence on a future Declaration.Equals
// override.
public sealed class CrossReferenceIndex
{
    private static readonly ReadOnlyCollection<ResolvedReference> EmptyReferences
        = new(Array.Empty<ResolvedReference>());

    private readonly Dictionary<Declaration, ReadOnlyCollection<ResolvedReference>> _byDeclaration;

    // Materialised once at construction; exposed read-only so callers
    // can enumerate the indexed declarations without paying the
    // dictionary's hash-iteration cost on each access.
    private readonly ReadOnlyCollection<Declaration> _declarations;

    private readonly ReadOnlyCollection<ResolvedModule> _modules;

    // Static factory keeps the IEnumerable<ResolvedModule> ctor as
    // the canonical entry while letting one-module call sites stay
    // terse: CrossReferenceIndex.Build(resolved).
    public static CrossReferenceIndex Build(params ResolvedModule[] modules)
    {
        if (modules is null) throw new ArgumentNullException(nameof(modules));
        return new CrossReferenceIndex((IEnumerable<ResolvedModule>)modules);
    }

    public CrossReferenceIndex(IEnumerable<ResolvedModule> modules)
    {
        if (modules is null) throw new ArgumentNullException(nameof(modules));

        // Snapshot the input so subsequent caller mutation cannot
        // desynchronise Modules from _byDeclaration.
        var moduleList = modules.ToList();
        for (int i = 0; i < moduleList.Count; i++)
        {
            if (moduleList[i] is null)
                throw new ArgumentException(
                    "Module list contains a null entry.", nameof(modules));
        }
        _modules = new ReadOnlyCollection<ResolvedModule>(moduleList);

        // Build the inverted map. Use ReferenceEqualityComparer so
        // grouping is by Declaration identity (see class doc).
        var working = new Dictionary<Declaration, List<ResolvedReference>>(
            ReferenceEqualityComparer.Instance);

        foreach (var module in moduleList)
        {
            foreach (var reference in module.References)
            {
                var decl = DeclarationFromOutcome(reference.Outcome);
                if (decl is null) continue;

                if (!working.TryGetValue(decl, out var list))
                {
                    list = new List<ResolvedReference>();
                    working[decl] = list;
                }
                list.Add(reference);
            }
        }

        _byDeclaration = new Dictionary<Declaration, ReadOnlyCollection<ResolvedReference>>(
            working.Count, ReferenceEqualityComparer.Instance);
        var declList = new List<Declaration>(working.Count);
        foreach (var kvp in working)
        {
            _byDeclaration[kvp.Key] = new ReadOnlyCollection<ResolvedReference>(kvp.Value);
            declList.Add(kvp.Key);
        }
        _declarations = new ReadOnlyCollection<Declaration>(declList);
    }

    public IReadOnlyList<ResolvedModule> Modules => _modules;

    // The set of declarations with at least one indexed reference.
    // Declarations that exist in the resolver graph but have no
    // resolved references are NOT included — the index is by
    // construction only populated by reference sites it observed.
    // Callers that need "all declarations" should walk ModuleScope
    // directly.
    public IReadOnlyCollection<Declaration> Declarations => _declarations;

    // O(1) lookup. Returns an empty (immutable) list when the
    // declaration has no recorded references rather than null, so
    // consumers can iterate without a presence check.
    public IReadOnlyList<ResolvedReference> GetReferencesTo(Declaration declaration)
    {
        if (declaration is null) throw new ArgumentNullException(nameof(declaration));
        return _byDeclaration.TryGetValue(declaration, out var list)
            ? list
            : EmptyReferences;
    }

    private static Declaration? DeclarationFromOutcome(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Resolved r => r.Declaration,
        ResolutionOutcome.ResolvedToObjectModel r => r.Declaration,
        ResolutionOutcome.ResolvedViaWithFlattening f => DeclarationFromOutcome(f.InnerOutcome),
        _ => null,
    };
}
