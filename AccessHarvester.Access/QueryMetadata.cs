using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Structured metadata for a single Access saved query, covering the deliverables
/// of Plan Phase 4.3. Holds the query's name, decoded type, SQL text verbatim,
/// inferred parameters, pass-through connect string, and the raw property bag with
/// promoted keys removed.
/// </summary>
/// <remarks>
/// One designed-once schema covers every saved query — SELECT, MAKE TABLE,
/// APPEND, UPDATE, DELETE, UNION, CROSSTAB, DATA DEFINITION, and SQL pass-through.
/// Action-specific structural fields (CROSSTAB <c>PIVOT</c> columns, MAKE TABLE
/// destinations, JOIN trees) are intentionally not modelled here — see
/// <c>docs/session-sets/access-metadata-extraction/spec.md</c> -> Standards:
/// "SQL text is captured verbatim; structural parsing of SQL is the
/// <c>jet-sql-grammar-and-parser</c> set's work, not this set's."
/// <para>
/// Output JSON conventions (<c>docs/planning/project-guidance.md</c> -> Output
/// JSON Conventions) apply identically to <see cref="FormMetadata"/> /
/// <see cref="ReportMetadata"/>: only-emit-what-Access-wrote (every field except
/// <see cref="Name"/> is nullable; serializer should configure
/// <c>JsonIgnoreCondition.WhenWritingNull</c>); the <c>{ source, ... }</c>
/// provenance shape is reused on parameter origin via
/// <see cref="QueryParameter.Source"/>.
/// </para>
/// <para>
/// Phase 4.3 lifts a small number of bound-data-source properties out of the raw
/// <see cref="Properties"/> bag. The promotion rule is the same as for forms /
/// reports:
/// <list type="bullet">
///   <item><b>Exact-value lifts</b> — <see cref="Sql"/>, <see cref="Connect"/>,
///   <see cref="ReturnsRecords"/>, <see cref="OdbcTimeout"/>: the typed field
///   carries the same value Access wrote. Extractors remove the corresponding
///   key from <see cref="Properties"/> when promoting it, to avoid double-
///   emitting the same value through two paths.</item>
///   <item><b>Decoded lifts</b> — <see cref="QueryType"/>: the typed field
///   carries a resolved label (e.g. <c>Select</c>, <c>CrossTab</c>) decoded
///   from Access's <c>dbQueryType</c> numeric code. The raw numeric code is
///   preserved in <see cref="Properties"/> as <c>QueryTypeCode</c> so
///   round-trip fidelity to the IR JSON is not lost.</item>
/// </list>
/// </para>
/// </remarks>
public sealed record QueryMetadata
{
    /// <summary>
    /// The query's name as Access stores it. System and temporary queries (names
    /// beginning with <c>~</c>) are excluded from extraction upstream; any name
    /// that reaches this record is a user-authored query name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The decoded query-type label — one of <c>Select</c>, <c>MakeTable</c>,
    /// <c>Append</c>, <c>Update</c>, <c>Delete</c>, <c>Union</c>,
    /// <c>CrossTab</c>, <c>DataDefinition</c>, <c>SqlPassthrough</c>,
    /// <c>SetOperation</c>, <c>Xbase</c>, <c>SpMakeTable</c>, <c>SpAppend</c>,
    /// <c>SpUpdate</c>, <c>SpDelete</c>, or <c>Unknown(N)</c> when an unmapped
    /// numeric code is encountered. Decoded from DAO's <c>dbQueryType</c>; the
    /// raw numeric code is retained in <see cref="Properties"/> as
    /// <c>QueryTypeCode</c> per the decoded-lift rule above.
    /// </summary>
    public string? QueryType { get; init; }

    /// <summary>
    /// The query's SQL text exactly as Access wrote it, including any leading
    /// <c>PARAMETERS ... ;</c> clause. Null when the underlying DAO call could
    /// not retrieve the SQL (rare; typically a permission or corruption case).
    /// </summary>
    public string? Sql { get; init; }

    /// <summary>
    /// Parameters detected for this query. Order is: declared parameters
    /// first (in PARAMETERS-clause order), then implicit form-reference
    /// parameters (in left-to-right SQL order), then implicit report-
    /// reference parameters (in left-to-right SQL order). Two origins are
    /// recognised:
    /// <list type="bullet">
    ///   <item><b>Declared</b> — from a <c>PARAMETERS &lt;name&gt;
    ///   &lt;type&gt;[, ...];</c> clause at the head of <see cref="Sql"/>.
    ///   These carry <see cref="QueryParameter.Source"/> = <c>declared</c>
    ///   and a <see cref="QueryParameter.DataType"/> when one is given.</item>
    ///   <item><b>Implicit form / report references</b> —
    ///   <c>[Forms]![X]![Y]</c>, <c>[Forms]![X]</c>,
    ///   <c>[Reports]![X]![Y]</c>, etc. encountered anywhere in
    ///   <see cref="Sql"/>. These carry <see cref="QueryParameter.Source"/> =
    ///   <c>form-reference</c> or <c>report-reference</c> and the verbatim
    ///   <see cref="QueryParameter.Path"/> as written.</item>
    /// </list>
    /// Detection is deliberately shallow — a regex scan over <see cref="Sql"/>
    /// — because true SQL parsing is the <c>jet-sql-grammar-and-parser</c>
    /// set's work. Limitations: parameters inside string literals or comments
    /// are detected as if they were live references; consumers that need
    /// strictly-correct grammar-level analysis should re-extract from the
    /// JET SQL parse tree once Phase 5.1 lands. Null when no parameters were
    /// detected (not an empty list, to keep the only-emit-what-Access-wrote
    /// rule consistent — empty lists imply "examined and found none" but a
    /// downstream parser may disagree, and an absent field invites that
    /// re-extraction more naturally).
    /// </summary>
    public IReadOnlyList<QueryParameter>? Parameters { get; init; }

    /// <summary>
    /// The query's output columns when discoverable — i.e. the SELECT-list
    /// columns of a SELECT / UNION / CROSSTAB query, or the destination
    /// columns of an APPEND / MAKE TABLE. Null under the current raw IR
    /// shape because <c>AccessHarvester.Core.Extraction.QueryExporter</c>
    /// does not yet enumerate <c>QueryDef.Fields</c>; the typed slot is
    /// committed here so a future raw-exporter widening (or downstream
    /// JET SQL parse-tree consumer) can populate it without a schema
    /// contract change. See Plan Phase 4.3 — "output column metadata when
    /// available" — and the corresponding standard in the session-set
    /// spec.
    /// </summary>
    public IReadOnlyList<QueryOutputColumn>? OutputColumns { get; init; }

    /// <summary>
    /// True when the query returns a recordset (SELECT, CROSSTAB, UNION, and
    /// pass-through SELECTs); false for action queries that do not. Lifted
    /// verbatim from DAO's <c>QueryDef.ReturnsRecords</c>; null if the property
    /// could not be read.
    /// </summary>
    public bool? ReturnsRecords { get; init; }

    /// <summary>
    /// The ODBC connect string for SQL-passthrough queries. The raw IR
    /// preserves whatever DAO returned, including the empty string DAO
    /// emits for native JET queries — so a non-passthrough query yields
    /// <c>""</c>, not <c>null</c>. <c>null</c> specifically means the
    /// underlying property could not be read (e.g. permission /
    /// corruption case in <c>QueryExporter.SafeString</c>). Consumers
    /// that need a "is this passthrough" boolean should check for non-
    /// empty rather than non-null, or read the decoded
    /// <see cref="QueryType"/> for <c>SqlPassthrough</c>.
    /// </summary>
    public string? Connect { get; init; }

    /// <summary>
    /// ODBC timeout in seconds for pass-through queries; null when the
    /// underlying property was not set or could not be read.
    /// </summary>
    public int? OdbcTimeout { get; init; }

    /// <summary>
    /// The raw property bag from the <c>kb/Queries/</c> JSON, with exact-value
    /// lifts removed (<see cref="Sql"/>, <see cref="Connect"/>,
    /// <see cref="ReturnsRecords"/>, <see cref="OdbcTimeout"/>) but decoded
    /// lifts left intact (<c>QueryTypeCode</c> remains here alongside the
    /// decoded <see cref="QueryType"/> label). Values preserved as
    /// <see cref="JsonElement"/> for faithful round-trip — see
    /// <see cref="AccessControl.Properties"/> for the rationale shared with
    /// the form / report schema.
    /// <para>
    /// The discriminator <c>"Type": "Query"</c> emitted by the upstream raw
    /// exporter is also stripped; consumers should use the C# type
    /// (<see cref="QueryMetadata"/>) for that distinction.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Properties { get; init; }
}

/// <summary>
/// One parameter declared by or implicit in a <see cref="QueryMetadata.Sql"/>.
/// Provenance is on <see cref="Source"/> — reusing the
/// <c>{ source, ... }</c> shape that <see cref="LabelInfo"/> uses for resolved
/// labels — so consumers do not have to special-case missing keys to know
/// whether a parameter is declared or implicit.
/// </summary>
public sealed record QueryParameter
{
    /// <summary>
    /// The parameter name. For declared parameters this is the bracketed
    /// identifier from the <c>PARAMETERS</c> clause, with surrounding
    /// brackets stripped (e.g. <c>[Enter Customer ID]</c> -> <c>Enter
    /// Customer ID</c>). For form / report references this is the leaf
    /// segment of the bracketed path — e.g.
    /// <c>[Forms]![frmCustomers]![txtId]</c> -> <c>txtId</c>; bare
    /// <c>[Forms]![frmCustomers]</c> -> <c>frmCustomers</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// One of <c>declared</c>, <c>form-reference</c>, or
    /// <c>report-reference</c>. Mirrors <see cref="LabelInfo.Source"/> in
    /// shape so the provenance idiom is consistent across the schema
    /// family. The <c>unresolved</c> sentinel <see cref="LabelInfo"/>
    /// uses has no analogue here — parameter detection either finds a
    /// parameter or it does not, and a not-found result yields
    /// <c>Parameters = null</c> on the parent <see cref="QueryMetadata"/>
    /// rather than synthesising an unresolved record.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// The declared JET SQL type when <see cref="Source"/> is <c>declared</c>
    /// — verbatim text following the parameter name in the <c>PARAMETERS</c>
    /// clause (e.g. <c>Long</c>, <c>Text(50)</c>, <c>Date</c>). Null when the
    /// parameter is implicit or no type was declared.
    /// </summary>
    public string? DataType { get; init; }

    /// <summary>
    /// The verbatim bracketed path as it appears in <see cref="QueryMetadata.Sql"/>
    /// when <see cref="Source"/> is <c>form-reference</c> or
    /// <c>report-reference</c> (e.g. <c>[Forms]![frmCustomers]![txtId]</c>).
    /// Null for declared parameters. Form metadata in this schema family is
    /// keyed by form name — Plan Phase 4.4 (the next child set,
    /// <c>access-symbol-table-and-types</c>) consumes this <see cref="Path"/>
    /// to resolve the reference against the extracted
    /// <see cref="FormMetadata"/> tree.
    /// </summary>
    public string? Path { get; init; }
}

/// <summary>
/// One output column on a <see cref="QueryMetadata"/>. Reserved schema slot
/// — under the current raw IR shape no extractor populates these because
/// <c>AccessHarvester.Core.Extraction.QueryExporter</c> does not yet
/// enumerate <c>QueryDef.Fields</c>. Committed in this typed schema so
/// that future raw-exporter widening or downstream JET SQL parse-tree
/// consumers can populate it without a contract change. Per Plan Phase
/// 4.3: "output column metadata when available."
/// </summary>
public sealed record QueryOutputColumn
{
    /// <summary>The column name as it would appear in the result set.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The decoded data-type label when available (matches the labels in
    /// <see cref="TableField.DataType"/>); null when the column type
    /// cannot be inferred without parsing the SQL or running the query.
    /// </summary>
    public string? DataType { get; init; }

    /// <summary>
    /// The raw DAO <c>dbType*</c> numeric code paired with
    /// <see cref="DataType"/>; null when not available.
    /// </summary>
    public int? DataTypeCode { get; init; }

    /// <summary>
    /// Declared field size for variable-length columns (<c>Text</c>); null
    /// when not available or not applicable.
    /// </summary>
    public int? Size { get; init; }
}
