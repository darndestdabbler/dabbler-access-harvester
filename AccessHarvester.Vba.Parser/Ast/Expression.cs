using System.Collections.Generic;

namespace AccessHarvester.Vba.Parser.Ast;

// Expression is abstract — see ast-shape.md §4 for the variant list
// and which grammar alternatives collapse via the BinaryOperator enum.
public abstract record Expression(Provenance Provenance) : VbaSyntaxNode(Provenance);

// ── Literals & special constants ──────────────────────────────────

public sealed record LiteralExpression(
    Provenance Provenance,
    LiteralKind Kind,
    string Text)                                        // source spelling; canonical form is downstream
    : Expression(Provenance);

public enum LiteralKind { Integer, Float, String, Date, Hex, Octal }

public sealed record TrueExpression(Provenance Provenance) : Expression(Provenance);
public sealed record FalseExpression(Provenance Provenance) : Expression(Provenance);
public sealed record NothingExpression(Provenance Provenance) : Expression(Provenance);
public sealed record NullExpression(Provenance Provenance) : Expression(Provenance);
public sealed record EmptyExpression(Provenance Provenance) : Expression(Provenance);
public sealed record MeExpression(Provenance Provenance) : Expression(Provenance);
public sealed record DebugExpression(Provenance Provenance) : Expression(Provenance);

// ── Identifiers ───────────────────────────────────────────────────

public sealed record IdentifierExpression(
    Provenance Provenance,
    string Name,
    string? TypeSuffix)                                 // '$' / '%' / '&' / '!' / '#' / '@' from TYPED_IDENTIFIER, else null
    : Expression(Provenance);

// ── Composition ───────────────────────────────────────────────────

public sealed record ParenthesizedExpression(
    Provenance Provenance,
    Expression Inner)
    : Expression(Provenance);

public sealed record MemberAccessExpression(
    Provenance Provenance,
    Expression Target,
    string MemberName)
    : Expression(Provenance);

public sealed record BangAccessExpression(
    Provenance Provenance,
    Expression Target,
    string MemberName)
    : Expression(Provenance);

public sealed record ImplicitWithMemberExpression(
    Provenance Provenance,
    string MemberName)
    : Expression(Provenance);

public sealed record ImplicitWithBangExpression(
    Provenance Provenance,
    string MemberName)
    : Expression(Provenance);

public sealed record IndexOrCallExpression(
    Provenance Provenance,
    Expression Target,
    IReadOnlyList<Argument> Arguments)
    : Expression(Provenance);

// ── Operators ─────────────────────────────────────────────────────

public sealed record BinaryExpression(
    Provenance Provenance,
    BinaryOperator Operator,
    Expression Left,
    Expression Right)
    : Expression(Provenance);

public sealed record UnaryExpression(
    Provenance Provenance,
    UnaryOperator Operator,
    Expression Operand)
    : Expression(Provenance);

public enum BinaryOperator
{
    // Arithmetic & string
    Power,
    Multiply,
    Divide,
    IntDivide,
    Modulo,
    Add,
    Subtract,
    Concat,
    // Comparison
    Equal,
    NotEqual,
    Less,
    Greater,
    LessOrEqual,
    GreaterOrEqual,
    Like,
    Is,
    // Logical / bitwise
    And,
    Or,
    Xor,
    Eqv,
    Imp,
}

public enum UnaryOperator { Plus, Minus, Not }

// ── Object-creation & introspection ───────────────────────────────

public sealed record NewExpression(
    Provenance Provenance,
    TypeReference Type)
    : Expression(Provenance);

public sealed record AddressOfExpression(
    Provenance Provenance,
    Expression ProcedureReference)                      // bare identifier or member-access chain per grammar's procedureReference
    : Expression(Provenance);

public sealed record TypeOfExpression(
    Provenance Provenance,
    Expression Operand,
    TypeReference Type)
    : Expression(Provenance);

// ── Recovery sentinel ─────────────────────────────────────────────

// Emitted when a parse-tree subtree at expression position contains
// an IErrorNode the builder cannot meaningfully descend into. Always
// carries RecoveredProvenance.
public sealed record UnknownExpression(Provenance Provenance) : Expression(Provenance);
