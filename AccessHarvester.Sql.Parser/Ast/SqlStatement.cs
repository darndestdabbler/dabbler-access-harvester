using System.Collections.Generic;

namespace AccessHarvester.Sql.Parser.Ast;

// Top-level statement node. A JET SQL source may have an optional
// PARAMETERS declaration followed by one DML statement.
public sealed record SqlStatement(
    Provenance Provenance,
    ParametersDeclaration? Parameters,
    DmlStatement Body)
    : SqlSyntaxNode(Provenance);

// ── PARAMETERS declaration ────────────────────────────────────────────

public sealed record ParametersDeclaration(
    Provenance Provenance,
    IReadOnlyList<ParameterDeclaration> Parameters)
    : SqlSyntaxNode(Provenance);

public sealed record ParameterDeclaration(
    Provenance Provenance,
    ParameterName Name,
    SqlTypeName TypeName)
    : SqlSyntaxNode(Provenance);

// Parameter name: one of three forms — a form reference path
// ([Forms]![frmX]![ctlY]), a bracketed identifier ([Enter ID]), or a
// bare identifier.
public abstract record ParameterName(Provenance Provenance) : SqlSyntaxNode(Provenance);

public sealed record FormReferenceParameterName(
    Provenance Provenance,
    FormReference Reference)
    : ParameterName(Provenance);

public sealed record BracketedParameterName(
    Provenance Provenance,
    string Name)
    : ParameterName(Provenance);

public sealed record SimpleParameterName(
    Provenance Provenance,
    string Name)
    : ParameterName(Provenance);

// SQL data type name as it appears after a parameter or column declaration.
public sealed record SqlTypeName(
    Provenance Provenance,
    string TypeKeyword,
    int? Precision,
    int? Scale)
    : SqlSyntaxNode(Provenance);

// ── DML base ─────────────────────────────────────────────────────────

public abstract record DmlStatement(Provenance Provenance) : SqlSyntaxNode(Provenance);

// ── SELECT / UNION ────────────────────────────────────────────────────

public sealed record UnionStatement(
    Provenance Provenance,
    IReadOnlyList<SelectStatement> Selects,
    IReadOnlyList<bool> AllFlags)              // AllFlags[i] = true means UNION ALL before Selects[i+1]
    : DmlStatement(Provenance);

// Parenthesized union counts as a SelectStatement for the grammar.
public abstract record SelectStatement(Provenance Provenance) : DmlStatement(Provenance);

public sealed record ParenthesizedSelect(
    Provenance Provenance,
    UnionStatement Inner)
    : SelectStatement(Provenance);

public sealed record SelectCore(
    Provenance Provenance,
    SelectQuantifier Quantifier,
    TopClause? Top,
    SelectList SelectList,
    FromClause? From,
    Expression? Where,
    IReadOnlyList<Expression>? GroupBy,
    Expression? Having,
    IReadOnlyList<OrderItem>? OrderBy)
    : SelectStatement(Provenance);

public enum SelectQuantifier { None, All, Distinct, DistinctRow }

public sealed record TopClause(
    Provenance Provenance,
    int N,
    bool Percent)
    : SqlSyntaxNode(Provenance);

// ── SELECT list ───────────────────────────────────────────────────────

public abstract record SelectList(Provenance Provenance) : SqlSyntaxNode(Provenance);

public sealed record StarSelectList(Provenance Provenance) : SelectList(Provenance);

public sealed record ColumnSelectList(
    Provenance Provenance,
    IReadOnlyList<SelectItem> Items)
    : SelectList(Provenance);

public abstract record SelectItem(Provenance Provenance) : SqlSyntaxNode(Provenance);

public sealed record QualifiedStarSelectItem(
    Provenance Provenance,
    string TableQualifier)
    : SelectItem(Provenance);

public sealed record ExpressionSelectItem(
    Provenance Provenance,
    Expression Value,
    string? Alias)
    : SelectItem(Provenance);

// ── FROM / JOINs ─────────────────────────────────────────────────────

public sealed record FromClause(
    Provenance Provenance,
    IReadOnlyList<TableSource> Sources)
    : SqlSyntaxNode(Provenance);

public abstract record TableSource(Provenance Provenance) : SqlSyntaxNode(Provenance);

public sealed record SimpleTableSource(
    Provenance Provenance,
    string TableName,
    string? Alias,
    IReadOnlyList<JoinClause> Joins)
    : TableSource(Provenance);

public sealed record SubqueryTableSource(
    Provenance Provenance,
    UnionStatement Subquery,
    string? Alias,
    IReadOnlyList<JoinClause> Joins)
    : TableSource(Provenance);

public sealed record ParenthesizedTableSource(
    Provenance Provenance,
    TableSource Inner,
    IReadOnlyList<JoinClause> Joins)
    : TableSource(Provenance);

public sealed record JoinClause(
    Provenance Provenance,
    JoinKind Kind,
    TableSource Right,
    Expression On)
    : SqlSyntaxNode(Provenance);

public enum JoinKind { Inner, Left, Right }

// ── ORDER BY ─────────────────────────────────────────────────────────

public sealed record OrderItem(
    Provenance Provenance,
    Expression Expression,
    OrderDirection Direction)
    : SqlSyntaxNode(Provenance);

public enum OrderDirection { Ascending, Descending }

// ── TRANSFORM ... PIVOT ───────────────────────────────────────────────

public sealed record TransformStatement(
    Provenance Provenance,
    Expression AggregateExpression,
    string? AggregateAlias,
    UnionStatement SelectBody,
    Expression PivotExpression,
    IReadOnlyList<Expression>? PivotInValues)
    : DmlStatement(Provenance);

// ── UPDATE ────────────────────────────────────────────────────────────

public sealed record UpdateStatement(
    Provenance Provenance,
    TableSource Target,
    IReadOnlyList<Assignment> Assignments,
    Expression? Where)
    : DmlStatement(Provenance);

public sealed record Assignment(
    Provenance Provenance,
    ColumnReference Column,
    Expression Value)
    : SqlSyntaxNode(Provenance);

// ── INSERT ────────────────────────────────────────────────────────────

public sealed record InsertStatement(
    Provenance Provenance,
    string TableName,
    IReadOnlyList<ColumnReference>? Columns,
    InsertSource Source)
    : DmlStatement(Provenance);

public abstract record InsertSource(Provenance Provenance) : SqlSyntaxNode(Provenance);

public sealed record ValuesSource(
    Provenance Provenance,
    IReadOnlyList<Expression> Values)
    : InsertSource(Provenance);

public sealed record SelectSource(
    Provenance Provenance,
    UnionStatement Select)
    : InsertSource(Provenance);

// ── DELETE ────────────────────────────────────────────────────────────

public sealed record DeleteStatement(
    Provenance Provenance,
    SelectList? SelectList,
    FromClause? From,
    Expression? Where)
    : DmlStatement(Provenance);
