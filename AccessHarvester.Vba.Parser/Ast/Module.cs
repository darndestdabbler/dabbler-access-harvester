using System.Collections.Generic;

namespace AccessHarvester.Vba.Parser.Ast;

// The top-level node. A VBA module (.bas, .cls, form/report module) is
// a list of module items. Per ast-shape.md §4.
//
// `Header` carries the typed `Attribute VB_*` / `VERSION` / `BEGIN..END`
// metadata stripped from Access-exported module text by
// `VbaModuleBodyLoader.LoadAndParse`. Null when the source carried no
// header or the caller went through `VbaParserPipeline.Parse` directly
// (the raw escape hatch for grammar / hand-built tests).
public sealed record Module(
    Provenance Provenance,
    IReadOnlyList<ModuleItem> Items)
    : VbaSyntaxNode(Provenance)
{
    public ModuleHeaderMetadata? Header { get; init; }
}

// One unit at module-statement position. Mirrors grammar moduleItem
// alternatives, with the trivial NEWLINE alt absorbed by the builder
// (blank lines do not become AST nodes).
public abstract record ModuleItem(Provenance Provenance) : VbaSyntaxNode(Provenance);

public sealed record OptionDirective(
    Provenance Provenance,
    OptionKind Kind,
    string? Argument)                                   // "Database" / "Binary" / "Text" for Compare; "0"/"1" for Base; "Module" for PrivateModule; null for Explicit
    : ModuleItem(Provenance);

public enum OptionKind { Explicit, Compare, Base, PrivateModule }

public sealed record ImplementsDirective(
    Provenance Provenance,
    TypeReference Interface)
    : ModuleItem(Provenance);

public sealed record ModuleVariableDeclaration(
    Provenance Provenance,
    AccessModifier Access,                              // Public | Private | Global | Dim
    IReadOnlyList<VariableDeclarator> Declarators)
    : ModuleItem(Provenance);

public sealed record ModuleConstantDeclaration(
    Provenance Provenance,
    AccessModifier Access,                              // Public | Private | Unspecified
    IReadOnlyList<ConstantDeclarator> Declarators)
    : ModuleItem(Provenance);

public sealed record TypeDeclaration(
    Provenance Provenance,
    AccessModifier Access,                              // Public | Private | Unspecified
    string Name,
    IReadOnlyList<TypeMember> Members)
    : ModuleItem(Provenance);

public sealed record TypeMember(
    Provenance Provenance,
    string Name,
    TypeReference? Type,                                // null = Variant
    IReadOnlyList<ArrayBound> ArrayBounds,              // empty = scalar; non-empty = array with bounds
    bool IsArray)                                       // true if the source had `()` even with empty bounds
    : VbaSyntaxNode(Provenance);

public sealed record EnumDeclaration(
    Provenance Provenance,
    AccessModifier Access,                              // Public | Private | Unspecified
    string Name,
    IReadOnlyList<EnumMember> Members)
    : ModuleItem(Provenance);

public sealed record EnumMember(
    Provenance Provenance,
    string Name,
    Expression? Value)                                  // null = auto-increment from previous + 1
    : VbaSyntaxNode(Provenance);

public sealed record DeclareStatement(
    Provenance Provenance,
    AccessModifier Access,                              // Public | Private | Unspecified
    bool IsPtrSafe,
    DeclareKind Kind,                                   // Sub | Function
    string Name,
    string LibraryName,                                 // contents of Lib "..."
    string? AliasName,                                  // contents of Alias "..." or null
    IReadOnlyList<Parameter> Parameters,
    TypeReference? ReturnType)                          // only on Function declares
    : ModuleItem(Provenance);

public enum DeclareKind { Sub, Function }

public sealed record EventDeclaration(
    Provenance Provenance,
    AccessModifier Access,                              // Public | Unspecified — Private Event is not legal
    string Name,
    IReadOnlyList<Parameter> Parameters)
    : ModuleItem(Provenance);

// Conditional-compilation directive line. Body is the raw text after
// the directive head (line-trimmed, line-continuation atoms collapsed
// per the lexer's LINE_CONTINUATION skip rule). Session 3 may
// decompose Body into structured fields without removing it.
public sealed record PreprocessorDirective(
    Provenance Provenance,
    PreprocessorKind Kind,
    string Body)
    : ModuleItem(Provenance);

public enum PreprocessorKind { If, ElseIf, Else, EndIf, Const }

// One declared variable inside a module-level or local declaration.
// `WithEvents` is module-only and is therefore tracked via the boolean
// flag on the declarator rather than a separate declarator type.
public sealed record VariableDeclarator(
    Provenance Provenance,
    string Name,
    string? TypeSuffix,                                 // '$' / '%' / '&' / '!' / '#' / '@' from TYPED_IDENTIFIER, else null
    bool WithEvents,
    bool IsAutoInstantiated,                            // true on `As New T`
    TypeReference? Type,                                // null = Variant
    IReadOnlyList<ArrayBound> ArrayBounds,
    bool IsArray)                                       // true if `()` present even with empty bounds
    : VbaSyntaxNode(Provenance);

public sealed record ConstantDeclarator(
    Provenance Provenance,
    string Name,
    string? TypeSuffix,
    TypeReference? Type,                                // null = inferred from value
    Expression Value)
    : VbaSyntaxNode(Provenance);

public sealed record RedimDeclarator(
    Provenance Provenance,
    string Name,
    string? TypeSuffix,
    IReadOnlyList<ArrayBound> ArrayBounds,
    TypeReference? Type)
    : VbaSyntaxNode(Provenance);

public sealed record ArrayBound(
    Provenance Provenance,
    Expression? Lower,                                  // null = lower bound omitted (defaults per Option Base)
    Expression Upper)
    : VbaSyntaxNode(Provenance);

public enum AccessModifier { Unspecified, Public, Private, Friend, Global, Dim }
