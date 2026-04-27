using System.Collections.Generic;

namespace AccessHarvester.Sql.Parser.Ast;

// ── Expression hierarchy ──────────────────────────────────────────────
//
// Expression precedence (low → high) mirrors the grammar:
//   OR/EQV/IMP/XOR → AND → NOT → comparison → concat(&) →
//   additive(+,-) → multiplicative(*,/,\,MOD) → exponent(^) → unary

public abstract record Expression(Provenance Provenance) : SqlSyntaxNode(Provenance);

// ── Binary and unary operators ────────────────────────────────────────

public sealed record BinaryExpression(
    Provenance Provenance,
    Expression Left,
    BinaryOperator Operator,
    Expression Right)
    : Expression(Provenance);

public enum BinaryOperator
{
    Or, Eqv, Imp, Xor,
    And,
    Equal, NotEqual, LessThan, LessOrEqual, GreaterThan, GreaterOrEqual,
    Like, NotLike,
    In, NotIn,
    Concat,
    Add, Subtract,
    Multiply, Divide, IntDivide, Modulo,
    Exponent,
}

public sealed record UnaryExpression(
    Provenance Provenance,
    UnaryOperator Operator,
    Expression Operand)
    : Expression(Provenance);

public enum UnaryOperator { Not, Plus, Minus }

// ── Comparison expressions with complex right-hand sides ──────────────

public sealed record BetweenExpression(
    Provenance Provenance,
    Expression Operand,
    Expression Low,
    Expression High)
    : Expression(Provenance);

public sealed record NotBetweenExpression(
    Provenance Provenance,
    Expression Operand,
    Expression Low,
    Expression High)
    : Expression(Provenance);

public sealed record IsNullExpression(
    Provenance Provenance,
    Expression Operand)
    : Expression(Provenance);

public sealed record IsNotNullExpression(
    Provenance Provenance,
    Expression Operand)
    : Expression(Provenance);

// IN (...) with an expression list
public sealed record InListExpression(
    Provenance Provenance,
    Expression Operand,
    bool Negated,
    IReadOnlyList<Expression> Values)
    : Expression(Provenance);

// IN (SELECT ...) with a subquery
public sealed record InSubqueryExpression(
    Provenance Provenance,
    Expression Operand,
    bool Negated,
    UnionStatement Subquery)
    : Expression(Provenance);

public sealed record ExistsExpression(
    Provenance Provenance,
    UnionStatement Subquery)
    : Expression(Provenance);

public sealed record SubqueryExpression(
    Provenance Provenance,
    UnionStatement Subquery)
    : Expression(Provenance);

public sealed record ParenthesizedExpression(
    Provenance Provenance,
    Expression Inner)
    : Expression(Provenance);

// ── Literals ──────────────────────────────────────────────────────────

public sealed record IntegerLiteralExpression(
    Provenance Provenance,
    string RawText)
    : Expression(Provenance);

public sealed record DecimalLiteralExpression(
    Provenance Provenance,
    string RawText)
    : Expression(Provenance);

public sealed record StringLiteralExpression(
    Provenance Provenance,
    string RawText)
    : Expression(Provenance);

// Date literals: #...# fenced. RawText includes the # delimiters.
public sealed record DateLiteralExpression(
    Provenance Provenance,
    string RawText)
    : Expression(Provenance);

public sealed record TrueLiteralExpression(Provenance Provenance) : Expression(Provenance);
public sealed record FalseLiteralExpression(Provenance Provenance) : Expression(Provenance);
public sealed record NullLiteralExpression(Provenance Provenance) : Expression(Provenance);

// ── Name references ───────────────────────────────────────────────────

// Forms![frmX]![ctlY] — JET-specific form/report control reference.
public sealed record FormReference(
    Provenance Provenance,
    FormReferenceRoot Root,
    IReadOnlyList<RefSegment> Segments,
    IReadOnlyList<RefAccessOperator> Operators)   // BANG or DOT between root and each segment
    : Expression(Provenance);

public abstract record FormReferenceRoot(Provenance Provenance) : SqlSyntaxNode(Provenance);
public sealed record FormsRoot(Provenance Provenance) : FormReferenceRoot(Provenance);
public sealed record ReportsRoot(Provenance Provenance) : FormReferenceRoot(Provenance);

public enum RefAccessOperator { Bang, Dot }

public sealed record RefSegment(
    Provenance Provenance,
    string Name,
    bool IsBracketed)
    : SqlSyntaxNode(Provenance);

// column or table.column reference
public sealed record ColumnReference(
    Provenance Provenance,
    string? TableQualifier,
    string ColumnName,
    bool ColumnIsBracketed)
    : Expression(Provenance);

// ? placeholder for parameterized queries
public sealed record ParameterPlaceholderExpression(Provenance Provenance) : Expression(Provenance);

// ── Function calls ────────────────────────────────────────────────────

public sealed record IIfExpression(
    Provenance Provenance,
    Expression Condition,
    Expression ThenExpr,
    Expression ElseExpr)
    : Expression(Provenance);

public sealed record SwitchExpression(
    Provenance Provenance,
    IReadOnlyList<SwitchPair> Pairs)
    : Expression(Provenance);

public sealed record SwitchPair(
    Provenance Provenance,
    Expression Condition,
    Expression Value)
    : SqlSyntaxNode(Provenance);

public sealed record ChooseExpression(
    Provenance Provenance,
    Expression Index,
    IReadOnlyList<Expression> Choices)
    : Expression(Provenance);

public sealed record DomainFunctionExpression(
    Provenance Provenance,
    string FunctionName,
    Expression FieldExpr,
    Expression DomainExpr,
    Expression? CriteriaExpr)
    : Expression(Provenance);

// COUNT(*) — distinguished from COUNT(expr) to preserve source semantics
public sealed record CountStarExpression(Provenance Provenance) : Expression(Provenance);

public sealed record FunctionCallExpression(
    Provenance Provenance,
    string FunctionName,
    IReadOnlyList<Expression> Arguments)
    : Expression(Provenance);
