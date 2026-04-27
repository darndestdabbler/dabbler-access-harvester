using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// Semantic declaration node. Distinct from the parser's AST records of
// the same names (Parser.Ast.ProcedureDeclaration / TypeDeclaration /
// EnumDeclaration / EventDeclaration are syntax records); this hierarchy
// adds back-pointers (Scope), reference-to-this-declaration tracking
// populated during reference resolution (#8), and canonicalised modifiers.
//
// Per docs/session-sets/resolver-declarations-and-scopes/spec.md
// Feature 1, this is the input shape that #8 (reference-resolution)
// consumes. Bias toward shape stability; later sessions populate the
// mutable backreferences but should not reshape these record-like
// surfaces without coordination.
//
// Names are stored case-preserved for round-tripping back to source
// and compared case-insensitively. Equality on Declaration uses
// reference identity (each declaration is a unique entity in the
// resolver graph), not value equality — that is why Declaration is a
// class rather than a record.
public abstract class Declaration
{
    // The single comparer scope-construction (#7 Session 2) and
    // reference-resolution (#8) must use when matching VBA names
    // against declarations. VBA is case-insensitive (ordinal). Encoded
    // here so consumers cannot accidentally use a culture-sensitive
    // comparer or default Equals.
    public static readonly StringComparer NameComparer
        = StringComparer.OrdinalIgnoreCase;

    public string Name { get; }

    public DeclarationProvenance Provenance { get; }

    // Populated by the scope-construction visitor in #7 Session 2.
    // Null until the visitor has assigned the declaration to a scope.
    public IDeclarationScope? Scope { get; internal set; }

    // Populated by #8 reference-resolution. Append-only.
    public IReadOnlyList<DeclarationReference> References => _references;
    private readonly List<DeclarationReference> _references = new();

    protected Declaration(string name, DeclarationProvenance provenance)
    {
        Name = name;
        Provenance = provenance;
    }

    // Internal so only resolver-internal code can mutate the
    // references list. Public consumers see an immutable view.
    internal void AddReference(DeclarationReference reference)
        => _references.Add(reference);
}

// Forward-declared marker for the scope tree type, fleshed out in
// Session 2. Kept here so Declaration can hold a back-pointer without
// a circular-namespace dependency between Declarations/ and Scopes/.
// Session 2 implements concrete scope types in
// AccessHarvester.Vba.Resolver/Scopes/ that implement IDeclarationScope.
public interface IDeclarationScope
{
    // Intentionally empty in Session 1 — Session 2 elaborates members
    // (Parent, Children, Declarations, Range). Declarations only need
    // the interface as a back-pointer slot at this layer.
}

// Forward-declared marker for a reference site, fleshed out in #8
// (resolver-references-and-with-blocks). Captures the source location
// of a name use that resolved to a particular declaration. Kept as a
// minimal record so Declaration.AddReference has a concrete type to
// store; #8 may extend with resolution-quality metadata.
public sealed record DeclarationReference(
    SourceRange Location,
    bool IsImplicit);
