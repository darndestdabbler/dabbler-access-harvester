using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Declarations;

// Sub / Function / Property Get/Let/Set declarations. One declaration
// per source procedure; property triplets (Get + Let, Get + Set) are
// pairing-deferred per the spec's Feature 2 → Standards — each
// Property procedure is its own PropertyDeclaration in the module
// scope, and #9 (resolver-default-members-and-inference) introduces
// the property-group concept that pairs them.
public abstract class ProcedureDeclaration : Declaration
{
    public AccessLevel Access { get; }

    // True when the source omitted the Public/Private/Friend keyword
    // and the resolver inferred Private (VBA's module-level default).
    public bool IsDefaultedAccess { get; }

    public bool IsStatic { get; }

    public IReadOnlyList<ParameterDeclaration> Parameters { get; }

    // Null on Sub / Property Let / Property Set.
    public TypeReference? ReturnType { get; }

    protected ProcedureDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        bool isStatic,
        IReadOnlyList<ParameterDeclaration> parameters,
        TypeReference? returnType)
        : base(name, provenance)
    {
        Access = access;
        IsDefaultedAccess = isDefaultedAccess;
        IsStatic = isStatic;
        Parameters = parameters;
        ReturnType = returnType;
    }
}

public sealed class SubDeclaration : ProcedureDeclaration
{
    public SubDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        bool isStatic,
        IReadOnlyList<ParameterDeclaration> parameters)
        : base(name, provenance, access, isDefaultedAccess, isStatic, parameters, returnType: null) { }
}

public sealed class FunctionDeclaration : ProcedureDeclaration
{
    public FunctionDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        bool isStatic,
        IReadOnlyList<ParameterDeclaration> parameters,
        TypeReference? returnType)
        : base(name, provenance, access, isDefaultedAccess, isStatic, parameters, returnType) { }
}

public sealed class PropertyDeclaration : ProcedureDeclaration
{
    // Reuses the parser-AST PropertyKind enum (Get | Let | Set) rather
    // than redefining a parallel one, because the resolver's notion of
    // property-kind is identical to the source spelling.
    public PropertyKind Kind { get; }

    public PropertyDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        bool isStatic,
        PropertyKind kind,
        IReadOnlyList<ParameterDeclaration> parameters,
        TypeReference? returnType)
        : base(name, provenance, access, isDefaultedAccess, isStatic, parameters, returnType)
    {
        Kind = kind;
    }
}

// Externally-declared library entry point (`Declare [PtrSafe] Sub/Function`).
// Kept as a sibling of ProcedureDeclaration rather than a subclass
// because Declares cannot have a body and carry library-binding fields
// (LibraryName, AliasName, IsPtrSafe) that no ordinary procedure has.
// Reference resolution treats them as call targets identically to
// procedures.
public sealed class DeclareDeclaration : Declaration
{
    public AccessLevel Access { get; }
    public bool IsDefaultedAccess { get; }
    public bool IsPtrSafe { get; }
    public DeclareKind Kind { get; }
    public string LibraryName { get; }
    public string? AliasName { get; }
    public IReadOnlyList<ParameterDeclaration> Parameters { get; }
    public TypeReference? ReturnType { get; }

    public DeclareDeclaration(
        string name,
        DeclarationProvenance provenance,
        AccessLevel access,
        bool isDefaultedAccess,
        bool isPtrSafe,
        DeclareKind kind,
        string libraryName,
        string? aliasName,
        IReadOnlyList<ParameterDeclaration> parameters,
        TypeReference? returnType)
        : base(name, provenance)
    {
        Access = access;
        IsDefaultedAccess = isDefaultedAccess;
        IsPtrSafe = isPtrSafe;
        Kind = kind;
        LibraryName = libraryName;
        AliasName = aliasName;
        Parameters = parameters;
        ReturnType = returnType;
    }
}
