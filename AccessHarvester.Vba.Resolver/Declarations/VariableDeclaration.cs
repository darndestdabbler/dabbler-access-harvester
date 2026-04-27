using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// Where the variable is declared. Module-level (one of the
// AccessModifier-bearing module items) vs. local (inside a procedure
// body via Dim or Static). The kind distinction matters for default
// access semantics and for which scope owns the declaration:
// ModuleScope vs. ProcedureScope, populated in Session 2.
public enum VariableScope
{
    Module,
    Local,
}

// Dim / Public / Private / Static / Global / Const declaration. Per the
// spec's Feature 1, constants are part of this taxonomy; ConstantDeclaration
// (below) is a subclass that adds the initializing expression. Treating
// constants as a kind of variable lets later resolver stages dispatch on
// `is VariableDeclaration` and pick up both kinds uniformly.
public class VariableDeclaration : Declaration
{
    public VariableScope DeclarationScope { get; }

    // Module-level only — Public / Private / Friend after canonicalisation.
    // Defaults to Private for locals (where access is meaningless but the
    // field is non-nullable).
    public AccessLevel Access { get; }
    public bool IsDefaultedAccess { get; }

    // True when the source used the `Static` storage-class keyword
    // (module-level Static variables, or `Static` locals inside a
    // procedure). Distinct from the `Static` modifier on a procedure
    // declaration, which lives on ProcedureDeclaration.IsStatic.
    public bool IsStatic { get; }

    public bool IsWithEvents { get; }

    public bool IsAutoInstantiated { get; }

    public TypeReference? Type { get; }

    public string? TypeSuffix { get; }

    public bool IsArray { get; }

    public IReadOnlyList<ArrayBound> ArrayBounds { get; }

    public VariableDeclaration(
        string name,
        DeclarationProvenance provenance,
        VariableScope declarationScope,
        AccessLevel access,
        bool isDefaultedAccess,
        bool isStatic,
        bool isWithEvents,
        bool isAutoInstantiated,
        TypeReference? type,
        string? typeSuffix,
        bool isArray,
        IReadOnlyList<ArrayBound> arrayBounds)
        : base(name, provenance)
    {
        DeclarationScope = declarationScope;
        Access = access;
        IsDefaultedAccess = isDefaultedAccess;
        IsStatic = isStatic;
        IsWithEvents = isWithEvents;
        IsAutoInstantiated = isAutoInstantiated;
        Type = type;
        TypeSuffix = typeSuffix;
        IsArray = isArray;
        ArrayBounds = arrayBounds;
    }
}

// Module-level Const or local Const. Subclass of VariableDeclaration
// (per spec Feature 1's taxonomy that bundles Dim / Public / Private /
// Static / Const into one declaration kind), with the additional
// constraint that Value is non-null and the storage-class fields
// (IsStatic, IsWithEvents, IsAutoInstantiated, ArrayBounds) are
// pinned to defaults that are never set on a Const in source.
public sealed class ConstantDeclaration : VariableDeclaration
{
    // The initializer expression. The resolver does not evaluate it at
    // this layer — type inference and folding belong to #9. Stored as
    // the parser-AST Expression node so the source form is preserved.
    public Expression Value { get; }

    public ConstantDeclaration(
        string name,
        DeclarationProvenance provenance,
        VariableScope declarationScope,
        AccessLevel access,
        bool isDefaultedAccess,
        TypeReference? type,
        string? typeSuffix,
        Expression value)
        : base(
            name: name,
            provenance: provenance,
            declarationScope: declarationScope,
            access: access,
            isDefaultedAccess: isDefaultedAccess,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: type,
            typeSuffix: typeSuffix,
            isArray: false,
            arrayBounds: System.Array.Empty<ArrayBound>())
    {
        Value = value;
    }
}

// Synthesised at first use of an undeclared variable in a module that
// is not Option Explicit (and at preprocessor #Const expansion sites
// per the same rules). Always Variant-typed at this layer (Type is
// null, matching the parser-AST convention "null TypeReference =
// Variant"); #9 may refine the type via inference and write through
// to Type.
public sealed class ImplicitDeclaration : Declaration
{
    // Where the implicit declaration is logically anchored. The owning
    // ProcedureScope decides this; ModuleScope is the fallback when
    // the originating use is outside any procedure (rare but legal at
    // module-level initializers in a non-Option-Explicit module).
    public VariableScope DeclarationScope { get; }

    // null = Variant per the parser-AST TypeReference-null convention.
    // Settable internally so #9 (default-members + inference) can
    // refine an inferred concrete type without reshaping the surface.
    public TypeReference? Type { get; internal set; }

    // The TYPED_IDENTIFIER suffix character ('$', '%', '&', '!', '#',
    // '@') that VBA allows on a use site to assert a primitive type
    // for an implicitly-declared name. Null when the source had no
    // suffix — the most common case.
    public string? TypeSuffix { get; }

    public ImplicitDeclaration(
        string name,
        ImplicitDeclarationProvenance provenance,
        VariableScope declarationScope,
        TypeReference? type = null,
        string? typeSuffix = null)
        : base(name, provenance)
    {
        DeclarationScope = declarationScope;
        Type = type;
        TypeSuffix = typeSuffix;
    }
}
