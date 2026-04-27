using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Translation-ready specification for a single Jet/Access SQL query.
/// Implements Phase 6.1 of <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>:
/// the parsed JET SQL AST + form-reference annotations + symbol-table
/// information are flattened into named facets a downstream Blazor-migration
/// AI consumer can read directly without re-walking SQL grammar trees.
/// </summary>
/// <remarks>
/// <para>
/// The abstraction is deliberately *not* an opinion about translation —
/// per the session-set spec's Standards: "Translation lives downstream."
/// Each facet captures structure (predicates as a tree, joins as a list of
/// descriptors, sort order as an ordered list) so the consumer can decide
/// how to render it. Facets the underlying SQL did not exercise (e.g.,
/// <see cref="GroupBy"/> on a non-aggregate query, <see cref="Having"/>
/// without <see cref="GroupBy"/>) are left null rather than emitted as
/// empty lists, matching the only-emit-what-the-source-wrote rule from
/// <c>docs/planning/project-guidance.md</c> -> Output JSON Conventions.
/// </para>
/// <para>
/// Interpretive slots (<see cref="Purpose"/>, <see cref="Intent"/>,
/// <see cref="Notes"/>) are reserved for the AI consumer to fill
/// downstream and remain null at the <c>ir/</c> tier per
/// <c>project-guidance.md</c> -> Output Architecture.
/// </para>
/// <para>
/// Builders for this record live in
/// <see cref="QueryAbstractionBuilder"/>, which walks an
/// <see cref="AccessHarvester.Sql.Parser.Ast.SqlStatement"/> and the
/// associated form-reference annotations to populate as many facets as
/// the input unambiguously determines. Facets that require a resolved
/// symbol table (e.g., parameter type inference from underlying table
/// columns, return-column nullability from declared column types) are
/// left null until Session 3's end-to-end pipeline run wires them up.
/// </para>
/// </remarks>
public sealed record QueryAbstraction
{
    /// <summary>
    /// The query's source name as extracted by
    /// <c>AccessHarvester.Access.QueryMetadata</c> for saved queries, or a
    /// synthetic identifier for inline (RecordSource / RowSource) SQL.
    /// </summary>
    public required string SourceQueryName { get; init; }

    /// <summary>
    /// Discriminates the underlying DML shape so the consumer can pick the
    /// correct method-emission strategy without re-parsing.
    /// </summary>
    public required QueryShape Shape { get; init; }

    /// <summary>
    /// PascalCase method name suggestion derived from <see cref="SourceQueryName"/>
    /// (e.g., <c>qryActiveCasesByDistrict</c> -> <c>GetActiveCasesByDistrict</c>).
    /// Null when the source name yields no usable suggestion (e.g., empty,
    /// punctuation-only). The suggestion is a hint; downstream consumers may
    /// override.
    /// </summary>
    public string? SuggestedMethodName { get; init; }

    /// <summary>
    /// Parameters consumed by the query, in the order Access scans them:
    /// declared <c>PARAMETERS</c> clause first, then implicit form-reference
    /// parameters in left-to-right SQL order, then implicit report-reference
    /// parameters. Null when no parameters were detected.
    /// </summary>
    public IReadOnlyList<QueryParameterAbstraction>? Parameters { get; init; }

    /// <summary>
    /// SELECT-list columns of a <see cref="QueryShape.Select"/>,
    /// <see cref="QueryShape.Union"/>, or <see cref="QueryShape.Transform"/>
    /// query, in source order. Null for action queries that do not return a
    /// recordset (<see cref="QueryShape.Insert"/>, <see cref="QueryShape.Update"/>,
    /// <see cref="QueryShape.Delete"/>) and when the SELECT list is
    /// <c>SELECT *</c> — in the star case the column list is unknown until
    /// the symbol table is consulted, and the visitor leaves the slot null
    /// rather than fabricate a wildcard sentinel.
    /// </summary>
    public IReadOnlyList<ReturnColumn>? ReturnShape { get; init; }

    /// <summary>
    /// The primary table source (the leftmost entry of the FROM clause), or
    /// null when the query has no FROM (e.g., <c>SELECT 1 + 1</c>) or the
    /// FROM is itself a subquery / parenthesised group whose primary table
    /// is ambiguous without resolution.
    /// </summary>
    public TableSourceDescriptor? PrimaryFrom { get; init; }

    /// <summary>
    /// Joins applied to <see cref="PrimaryFrom"/> in source order. Null when
    /// the FROM clause has no joins.
    /// </summary>
    public IReadOnlyList<JoinDescriptor>? Joins { get; init; }

    /// <summary>
    /// The WHERE clause as a structured predicate tree, or null when the
    /// query has no WHERE.
    /// </summary>
    public PredicateNode? Where { get; init; }

    /// <summary>
    /// GROUP BY expressions in source order, or null when the query has no
    /// GROUP BY.
    /// </summary>
    public IReadOnlyList<ExpressionDescriptor>? GroupBy { get; init; }

    /// <summary>
    /// HAVING clause as a structured predicate tree, or null when the query
    /// has no HAVING.
    /// </summary>
    public PredicateNode? Having { get; init; }

    /// <summary>
    /// ORDER BY items in source order, or null when the query has no
    /// ORDER BY.
    /// </summary>
    public IReadOnlyList<OrderByDescriptor>? OrderBy { get; init; }

    /// <summary>
    /// Special markers (DISTINCT, TOP N, aggregate-presence) the consumer
    /// may need to render the method differently. Null when none apply.
    /// </summary>
    public AggregationMarkers? Aggregation { get; init; }

    /// <summary>
    /// Reserved interpretive slot — null at the <c>ir/</c> tier; populated
    /// downstream by the consumer AI per
    /// <c>project-guidance.md</c> -> Output Architecture.
    /// </summary>
    public string? Purpose { get; init; }

    /// <summary>Reserved interpretive slot. See <see cref="Purpose"/>.</summary>
    public string? Intent { get; init; }

    /// <summary>Reserved interpretive slot. See <see cref="Purpose"/>.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Discriminates the underlying DML shape of a <see cref="QueryAbstraction"/>.
/// Mirrors <c>AccessHarvester.Sql.Parser.Ast.DmlStatement</c> subclasses
/// flattened into a single enum so the consumer does not have to walk the
/// AST hierarchy to know the query's kind.
/// </summary>
public enum QueryShape
{
    /// <summary>Plain <c>SELECT</c> (the <see cref="ReturnShape"/>-bearing case).</summary>
    Select,

    /// <summary><c>SELECT ... UNION [ALL] SELECT ...</c></summary>
    Union,

    /// <summary><c>TRANSFORM ... PIVOT ...</c> (CROSSTAB).</summary>
    Transform,

    /// <summary><c>INSERT INTO ... VALUES (...)</c> or <c>INSERT INTO ... SELECT ...</c>.</summary>
    Insert,

    /// <summary><c>UPDATE ... SET ...</c></summary>
    Update,

    /// <summary><c>DELETE FROM ...</c></summary>
    Delete,

    /// <summary>Shape could not be determined (e.g., parse recovery left a hole).</summary>
    Unknown,
}

/// <summary>
/// One parameter on a <see cref="QueryAbstraction"/>. The shape mirrors the
/// fixture-04 <c>abstraction.suggestedParameters</c> contract while adding
/// <see cref="Origin"/> and <see cref="UsageCount"/> for downstream
/// rendering decisions.
/// </summary>
public sealed record QueryParameterAbstraction
{
    /// <summary>
    /// The C#-style camelCase parameter name suggested for the generated
    /// method signature (e.g., <c>districtId</c>). Derived from the source
    /// parameter name with bracket stripping and casing normalisation.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The source-side identifier exactly as Jet recorded it: the bracketed
    /// declared name (e.g., <c>[Enter Customer ID]</c>) or the leaf path
    /// segment of a form/report reference (e.g., <c>cboDistrict</c>).
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Declared or inferred type label, normalised to a C#-style identifier
    /// (e.g., <c>long</c>, <c>DateTime</c>, <c>string</c>). Null when the
    /// type cannot be inferred without a resolved symbol table — the
    /// downstream pipeline session fills this in.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// True for parameters that should be emitted as <c>ref</c>-style
    /// arguments. Reserved for future use; defaults to false.
    /// </summary>
    public bool IsByRef { get; init; }

    /// <summary>
    /// True when the parameter has a sensible default and the generated
    /// method should make it optional. Reserved for future use; defaults
    /// to false.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Discriminates how the parameter is bound at the source level —
    /// declared in a <c>PARAMETERS</c> clause, or implicit via a
    /// <c>[Forms]!...</c> / <c>[Reports]!...</c> reference appearing inline
    /// in the query body.
    /// </summary>
    public required ParameterOrigin Origin { get; init; }

    /// <summary>
    /// Free-form provenance string for human / AI readers — e.g.,
    /// <c>"Forms!frmDistrictReport!cboDistrict (declared parameter)"</c>.
    /// Distinct from <see cref="SourceName"/>: <c>SourceName</c> is the
    /// machine identifier, <see cref="DerivedFrom"/> is the human caption.
    /// </summary>
    public string? DerivedFrom { get; init; }

    /// <summary>
    /// Number of times the parameter is referenced in the SQL body. Useful
    /// to surface "consider single computed binding rather than repeated
    /// expression evaluation" hints downstream. Null when usage scanning
    /// has not been performed.
    /// </summary>
    public int? UsageCount { get; init; }
}

/// <summary>Provenance discriminator for <see cref="QueryParameterAbstraction.Origin"/>.</summary>
public enum ParameterOrigin
{
    /// <summary>Declared via a <c>PARAMETERS &lt;name&gt; &lt;type&gt;;</c> clause.</summary>
    Declared,

    /// <summary>Implicit from a <c>[Forms]![X]![Y]</c> reference inline in the body.</summary>
    FormReference,

    /// <summary>Implicit from a <c>[Reports]![X]![Y]</c> reference inline in the body.</summary>
    ReportReference,

    /// <summary>Origin could not be determined.</summary>
    Unknown,
}

/// <summary>
/// One column on the <see cref="QueryAbstraction.ReturnShape"/>. Mirrors the
/// fixture-04 <c>abstraction.returnShape</c> contract.
/// </summary>
public sealed record ReturnColumn
{
    /// <summary>
    /// The column name as it would appear in the result set — the SELECT
    /// alias when present, otherwise the source column name. Synthetic
    /// names are used (e.g., <c>Expr1</c>) when no alias is available and
    /// the SELECT item is an expression with no obvious source-derived
    /// name; the visitor records that case in <see cref="HasSyntheticName"/>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Inferred type label (e.g., <c>long</c>, <c>string</c>, <c>DateTime</c>).
    /// Null when type inference requires the symbol table — populated by
    /// Session 3's end-to-end pass.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Nullability of the column. Null when nullability cannot be determined
    /// without consulting the underlying table column metadata.
    /// </summary>
    public bool? Nullable { get; init; }

    /// <summary>
    /// Discriminates how the column was produced (a direct table column,
    /// a computed expression, an aggregate, or a literal constant).
    /// </summary>
    public required ReturnColumnOrigin Origin { get; init; }

    /// <summary>
    /// Verbatim text of the source SELECT-list expression, suitable for
    /// surfacing in a generated comment so the reader can compare the
    /// generated method to the original SQL.
    /// </summary>
    public required string SourceExpression { get; init; }

    /// <summary>
    /// True when <see cref="Name"/> was synthesised because the source
    /// SELECT item had no alias and no obvious derived name. Downstream
    /// consumers may surface this as a "name this column" prompt.
    /// </summary>
    public bool HasSyntheticName { get; init; }
}

/// <summary>Discriminator for <see cref="ReturnColumn.Origin"/>.</summary>
public enum ReturnColumnOrigin
{
    /// <summary>Direct reference to a table column (e.g., <c>c.CaseID</c>).</summary>
    Direct,

    /// <summary>Computed scalar expression (e.g., <c>IIf(...)</c>, concatenation).</summary>
    Computed,

    /// <summary>Aggregate function (e.g., <c>COUNT(*)</c>, <c>SUM(x)</c>).</summary>
    Aggregate,

    /// <summary>Literal constant (e.g., <c>SELECT 1 AS One</c>).</summary>
    Constant,

    /// <summary>Origin could not be classified.</summary>
    Unknown,
}

/// <summary>
/// Description of a FROM-clause table source. Subqueries and parenthesised
/// groups are normalised to the same record so the consumer does not have
/// to switch on AST node kind to read the alias.
/// </summary>
public sealed record TableSourceDescriptor
{
    /// <summary>
    /// The table name, as written in the source. Null when the source is a
    /// subquery (in which case <see cref="IsSubquery"/> is true) or a
    /// parenthesised compound the visitor could not flatten.
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// The FROM-clause alias (e.g., <c>c</c> in <c>tblCases AS c</c>), or
    /// null when no alias was given.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>True when the source is a subquery rather than a real table.</summary>
    public bool IsSubquery { get; init; }
}

/// <summary>
/// Description of a JOIN clause: the kind, the right-hand table source,
/// and the ON predicate as a structured tree.
/// </summary>
public sealed record JoinDescriptor
{
    /// <summary>The join kind (Inner / Left / Right). Mirrors <c>JoinKind</c> in the SQL AST.</summary>
    public required JoinDescriptorKind Kind { get; init; }

    /// <summary>The right-hand table source.</summary>
    public required TableSourceDescriptor RightSource { get; init; }

    /// <summary>The ON-condition predicate.</summary>
    public required PredicateNode On { get; init; }
}

/// <summary>Discriminator for <see cref="JoinDescriptor.Kind"/>.</summary>
public enum JoinDescriptorKind
{
    /// <summary><c>INNER JOIN</c>.</summary>
    Inner,

    /// <summary><c>LEFT [OUTER] JOIN</c>.</summary>
    Left,

    /// <summary><c>RIGHT [OUTER] JOIN</c>.</summary>
    Right,
}

/// <summary>
/// Sort key on the ORDER BY list: the expression to sort by and the
/// direction (ASC / DESC).
/// </summary>
public sealed record OrderByDescriptor
{
    /// <summary>The sort expression.</summary>
    public required ExpressionDescriptor Expression { get; init; }

    /// <summary>The sort direction.</summary>
    public required OrderDirection Direction { get; init; }
}

/// <summary>Discriminator for <see cref="OrderByDescriptor.Direction"/>.</summary>
public enum OrderDirection
{
    /// <summary>Ascending — the source default when no direction is written.</summary>
    Ascending,

    /// <summary>Descending — explicitly written <c>DESC</c>.</summary>
    Descending,
}

/// <summary>
/// Aggregation-related markers on a <see cref="QueryAbstraction"/>:
/// DISTINCT modifier, TOP N modifier, and presence of aggregate functions
/// in the SELECT list. The consumer reads these to decide whether the
/// generated method should return <c>IEnumerable&lt;T&gt;</c>, a single
/// scalar, or use a <c>.Distinct()</c> / <c>.Take(N)</c> combinator.
/// </summary>
public sealed record AggregationMarkers
{
    /// <summary>
    /// True when the SELECT carried <c>DISTINCT</c> or <c>DISTINCTROW</c>.
    /// </summary>
    public bool IsDistinct { get; init; }

    /// <summary>
    /// True when DISTINCTROW (the JET row-level distinct) was specified
    /// rather than the standard DISTINCT.
    /// </summary>
    public bool IsDistinctRow { get; init; }

    /// <summary>
    /// The <c>TOP N</c> count when the SELECT carried a <c>TOP</c> clause,
    /// null otherwise.
    /// </summary>
    public int? TopN { get; init; }

    /// <summary>
    /// True when <c>TOP N PERCENT</c> was specified (i.e.,
    /// <see cref="TopN"/> is a percentage, not a row count).
    /// </summary>
    public bool TopIsPercent { get; init; }

    /// <summary>
    /// True when at least one SELECT-list item is an aggregate function
    /// (<c>COUNT</c>, <c>SUM</c>, <c>AVG</c>, <c>MIN</c>, <c>MAX</c>,
    /// <c>FIRST</c>, <c>LAST</c>, <c>STDEV</c>, <c>VAR</c>, etc.).
    /// </summary>
    public bool HasAggregates { get; init; }

    /// <summary>True when the query has a GROUP BY clause.</summary>
    public bool HasGrouping { get; init; }
}

// ── Predicate tree ─────────────────────────────────────────────────────
//
// A simplified predicate tree the consumer can walk without re-importing
// the SQL AST. Predicates that don't fit one of the named shapes fall back
// to OpaquePredicate, whose Expression carries the verbatim source text.

/// <summary>
/// A node in the structured predicate tree used for WHERE / HAVING / JOIN
/// ON clauses. The hierarchy is closed (sealed records inheriting from a
/// sealed-friendly abstract base) so consumers can switch exhaustively.
/// </summary>
public abstract record PredicateNode;

/// <summary>Logical conjunction: <c>A AND B [AND ...]</c>.</summary>
public sealed record AndPredicate(IReadOnlyList<PredicateNode> Operands) : PredicateNode;

/// <summary>Logical disjunction: <c>A OR B [OR ...]</c>.</summary>
public sealed record OrPredicate(IReadOnlyList<PredicateNode> Operands) : PredicateNode;

/// <summary>Logical negation: <c>NOT P</c>.</summary>
public sealed record NotPredicate(PredicateNode Operand) : PredicateNode;

/// <summary>
/// Comparison: <c>Left &lt;op&gt; Right</c>. The operator is one of
/// <c>=</c>, <c>&lt;&gt;</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>,
/// <c>&gt;=</c>. <c>LIKE</c>, <c>IN</c>, <c>BETWEEN</c>, and <c>IS NULL</c>
/// have dedicated record types below.
/// </summary>
public sealed record ComparisonPredicate(
    string Operator,
    ExpressionDescriptor Left,
    ExpressionDescriptor Right) : PredicateNode;

/// <summary><c>Expr [NOT] IN (Value1, Value2, ...)</c>.</summary>
public sealed record InPredicate(
    ExpressionDescriptor Expression,
    IReadOnlyList<ExpressionDescriptor> Values,
    bool Negated) : PredicateNode;

/// <summary><c>Expr IS [NOT] NULL</c>.</summary>
public sealed record IsNullPredicate(
    ExpressionDescriptor Expression,
    bool Negated) : PredicateNode;

/// <summary><c>Expr [NOT] BETWEEN Low AND High</c>.</summary>
public sealed record BetweenPredicate(
    ExpressionDescriptor Expression,
    ExpressionDescriptor Low,
    ExpressionDescriptor High,
    bool Negated) : PredicateNode;

/// <summary><c>Expr [NOT] LIKE Pattern</c>.</summary>
public sealed record LikePredicate(
    ExpressionDescriptor Expression,
    ExpressionDescriptor Pattern,
    bool Negated) : PredicateNode;

/// <summary>
/// Fallback for predicates the visitor could not map to a named shape
/// (e.g., a function call returning a Boolean, an EXISTS subquery, a
/// recovery-reconstructed node). The verbatim source text is preserved
/// so consumers can surface or re-parse it.
/// </summary>
public sealed record OpaquePredicate(ExpressionDescriptor Expression) : PredicateNode;

// ── Expression descriptor ──────────────────────────────────────────────
//
// A flat description of a SQL expression. The visitor classifies the
// expression into a Kind and lifts a few common structural fields; the
// Display string carries the verbatim source text for fallback rendering.

/// <summary>
/// Description of a SQL expression as it appears in a predicate, ON
/// clause, ORDER BY, GROUP BY, or SELECT-list item. The visitor populates
/// <see cref="Display"/> always; structural fields
/// (<see cref="ColumnName"/>, <see cref="LiteralValue"/>,
/// <see cref="FormReferencePath"/>) are populated only for the matching
/// <see cref="Kind"/>.
/// </summary>
public sealed record ExpressionDescriptor
{
    /// <summary>
    /// Verbatim source text of the expression. Always populated. Consumers
    /// that cannot interpret a particular <see cref="Kind"/> fall back to
    /// rendering this string.
    /// </summary>
    public required string Display { get; init; }

    /// <summary>The structural classification of this expression.</summary>
    public required ExpressionKind Kind { get; init; }

    /// <summary>
    /// The column name when <see cref="Kind"/> is <see cref="ExpressionKind.Column"/>,
    /// null otherwise.
    /// </summary>
    public string? ColumnName { get; init; }

    /// <summary>
    /// The table qualifier (alias or table name) when
    /// <see cref="Kind"/> is <see cref="ExpressionKind.Column"/> and a
    /// qualifier is present in the source (e.g., <c>c</c> in <c>c.CaseID</c>).
    /// Null otherwise.
    /// </summary>
    public string? TableQualifier { get; init; }

    /// <summary>
    /// The literal kind (string / number / date / boolean / null) when
    /// <see cref="Kind"/> is <see cref="ExpressionKind.Literal"/>, null
    /// otherwise. The raw value text is in <see cref="Display"/>.
    /// </summary>
    public LiteralKind? LiteralKind { get; init; }

    /// <summary>
    /// The full bracketed path when <see cref="Kind"/> is
    /// <see cref="ExpressionKind.FormReference"/> or
    /// <see cref="ExpressionKind.ReportReference"/> (e.g.,
    /// <c>[Forms]![frmDistrictReport]![cboDistrict]</c>). Null otherwise.
    /// </summary>
    public string? FormReferencePath { get; init; }

    /// <summary>
    /// The function name when <see cref="Kind"/> is
    /// <see cref="ExpressionKind.FunctionCall"/> (e.g., <c>IIf</c>,
    /// <c>Nz</c>, <c>DateDiff</c>). Null otherwise.
    /// </summary>
    public string? FunctionName { get; init; }
}

/// <summary>Discriminator for <see cref="ExpressionDescriptor.Kind"/>.</summary>
public enum ExpressionKind
{
    /// <summary>Direct column reference (qualified or unqualified).</summary>
    Column,

    /// <summary>Literal constant — see <see cref="ExpressionDescriptor.LiteralKind"/>.</summary>
    Literal,

    /// <summary><c>[Forms]![X]![Y]</c> reference.</summary>
    FormReference,

    /// <summary><c>[Reports]![X]![Y]</c> reference.</summary>
    ReportReference,

    /// <summary><c>?</c> placeholder for a parameterised query.</summary>
    Parameter,

    /// <summary>Function call — see <see cref="ExpressionDescriptor.FunctionName"/>.</summary>
    FunctionCall,

    /// <summary>Compound (binary / unary / arithmetic / concatenation) expression.</summary>
    Compound,

    /// <summary>Subquery expression.</summary>
    Subquery,

    /// <summary>Kind could not be classified — consumers should fall back to <see cref="ExpressionDescriptor.Display"/>.</summary>
    Unknown,
}

/// <summary>Discriminator for <see cref="ExpressionDescriptor.LiteralKind"/>.</summary>
public enum LiteralKind
{
    /// <summary>Quoted string literal.</summary>
    String,

    /// <summary>Numeric literal — integer or decimal.</summary>
    Number,

    /// <summary>Date literal (<c>#...#</c> in JET).</summary>
    Date,

    /// <summary><c>TRUE</c> or <c>FALSE</c>.</summary>
    Boolean,

    /// <summary><c>NULL</c>.</summary>
    Null,
}
