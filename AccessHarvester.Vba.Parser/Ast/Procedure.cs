using System.Collections.Generic;

namespace AccessHarvester.Vba.Parser.Ast;

// Procedure declarations: Sub, Function, Property Get/Let/Set. Each
// kind shares the same structural surface (modifiers, parameters,
// optional return type, body); per ast-shape.md §4 the only kind-
// specific surface is PropertyKind on PropertyProcedure.
public abstract record ProcedureDeclaration(
    Provenance Provenance,
    string Name,
    AccessModifier Access,                              // Public | Private | Friend | Unspecified
    bool IsStatic,
    IReadOnlyList<Parameter> Parameters,
    TypeReference? ReturnType,                          // null on Sub / PropertyLet / PropertySet
    IReadOnlyList<Statement> Body)
    : ModuleItem(Provenance);

public sealed record SubProcedure(
    Provenance Provenance,
    string Name,
    AccessModifier Access,
    bool IsStatic,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyList<Statement> Body)
    : ProcedureDeclaration(Provenance, Name, Access, IsStatic, Parameters, null, Body);

public sealed record FunctionProcedure(
    Provenance Provenance,
    string Name,
    AccessModifier Access,
    bool IsStatic,
    IReadOnlyList<Parameter> Parameters,
    TypeReference? ReturnType,
    IReadOnlyList<Statement> Body)
    : ProcedureDeclaration(Provenance, Name, Access, IsStatic, Parameters, ReturnType, Body);

public sealed record PropertyProcedure(
    Provenance Provenance,
    string Name,
    AccessModifier Access,
    bool IsStatic,
    PropertyKind Kind,
    IReadOnlyList<Parameter> Parameters,
    TypeReference? ReturnType,                          // only on Get
    IReadOnlyList<Statement> Body)
    : ProcedureDeclaration(Provenance, Name, Access, IsStatic, Parameters, ReturnType, Body);

public enum PropertyKind { Get, Let, Set }
