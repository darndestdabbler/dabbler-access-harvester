using System.Collections.Generic;

namespace AccessHarvester.Vba.Parser.Ast;

// AST-layer type reference. Wraps the grammar's typeName/typeSpec/
// variableTypeSpec rules. Per ast-shape.md §4.
public sealed record TypeReference(
    Provenance Provenance,
    IReadOnlyList<string> QualifiedName,                // e.g., ["DAO", "Recordset"] or ["String"]
    bool IsAutoInstantiated,                            // true on the `As New T` form
    Expression? FixedStringSize,                        // only on `String * <const-expr>`
    bool IsArray)                                       // true on array-element-type form e.g. parameter `x() As Long`
    : VbaSyntaxNode(Provenance);
