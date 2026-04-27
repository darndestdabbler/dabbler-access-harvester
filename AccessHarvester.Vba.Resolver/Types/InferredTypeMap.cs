using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.Types;

// The result of running TypeInferencePass over a ResolvedModule.
// Maps each candidate declaration that gathered any evidence to an
// InferredType record. Declarations that gathered zero evidence have
// no entry — consumers should not equate map-absence with "Variant"
// because the original declaration is still authoritative for that
// case (see session-2-type-inference.md §4).
//
// Backing storage is wrapped in ReadOnlyDictionary so a caller cannot
// cast `All` back to a mutable Dictionary (mirrors the
// DefaultMemberRegistry defense from Session 1).
//
// Map keys use ReferenceEqualityComparer.Instance: each Declaration
// is a unique entity in the resolver graph; two distinct declarations
// with the same name in different scopes must register independently.
public sealed class InferredTypeMap
{
    private readonly ReadOnlyDictionary<Declaration, InferredType> _byDeclaration;

    internal InferredTypeMap(IDictionary<Declaration, InferredType> source)
    {
        // Defensive copy: the caller's dictionary may be mutable. We
        // freeze the snapshot here so the public surface is genuinely
        // immutable rather than just an IReadOnlyDictionary view.
        var copy = new Dictionary<Declaration, InferredType>(
            source, ReferenceEqualityComparer.Instance);
        _byDeclaration = new ReadOnlyDictionary<Declaration, InferredType>(copy);
    }

    public IReadOnlyDictionary<Declaration, InferredType> All => _byDeclaration;

    public int Count => _byDeclaration.Count;

    public InferredType? TryGet(Declaration declaration)
    {
        if (declaration is null) throw new ArgumentNullException(nameof(declaration));
        return _byDeclaration.TryGetValue(declaration, out var entry) ? entry : null;
    }
}
