using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Structured metadata for a single Access table, covering the deliverables of
/// Plan Phase 4.3. Holds the table's name, link metadata for attached tables,
/// the field set with decoded data types, indexes (including the primary key
/// as a derived <see cref="PrimaryKey"/> column list), and the raw property
/// bag with promoted keys removed.
/// <para>
/// Table-level validation rules (<see cref="ValidationRule"/> /
/// <see cref="ValidationText"/>) are typed slots in this schema but the
/// current raw IR shape does not populate them — see the per-field remarks
/// for the disclosure and the planned widening. Per-field validation
/// (<see cref="TableField.ValidationRule"/>) is fully covered by the
/// current pipeline.
/// </para>
/// </summary>
/// <remarks>
/// Inter-table relationships (foreign keys) live in their own
/// <see cref="RelationshipMetadata"/> records keyed by relationship name,
/// not inlined here, so the relationship graph stays single-sourced across
/// the database. The per-field <c>ForeignName</c> property remains available
/// in <see cref="TableField.ForeignName"/> as a hint, but downstream
/// enrichment should consume the dedicated <see cref="RelationshipMetadata"/>
/// graph rather than reconstructing it from per-field strings — see
/// <c>docs/session-sets/access-metadata-extraction/spec.md</c> -> Standards:
/// "Relationships emit a structured graph; downstream enrichment consumes
/// that, not raw FK strings."
/// <para>
/// Output JSON conventions (<c>docs/planning/project-guidance.md</c> -> Output
/// JSON Conventions) apply identically to <see cref="FormMetadata"/> /
/// <see cref="ReportMetadata"/> / <see cref="QueryMetadata"/>: only-emit-what-
/// Access-wrote (every field except <see cref="Name"/> is nullable; serializer
/// should configure <c>JsonIgnoreCondition.WhenWritingNull</c>).
/// </para>
/// <para>
/// Phase 4.3 promotes a small number of structural keys out of the raw
/// <see cref="Properties"/> bag. The promotion rule mirrors forms / reports /
/// queries:
/// <list type="bullet">
///   <item><b>Exact-value lifts</b> — <see cref="Connect"/>,
///   <see cref="SourceTableName"/>, <see cref="ValidationRule"/>,
///   <see cref="ValidationText"/>: the typed field carries the same value
///   Access wrote. Extractors remove the corresponding key from
///   <see cref="Properties"/> when promoting it.</item>
///   <item><b>Derived flags</b> — <see cref="IsLinked"/>: derived from the
///   raw <c>Attributes</c> bitmask (DAO <c>dbAttachedTable</c> /
///   <c>dbAttachedODBC</c> bits) but kept alongside it; the raw
///   <c>Attributes</c> integer is preserved in <see cref="Properties"/>
///   so other consumers can decode further bits without reparsing.</item>
/// </list>
/// </para>
/// </remarks>
public sealed record TableMetadata
{
    /// <summary>
    /// The table's name as Access stores it. System tables (names beginning
    /// with <c>MSys</c>) and temporary tables (names beginning with <c>~</c>)
    /// are excluded from extraction upstream; any name that reaches this
    /// record is a user-authored or migration-imported table.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// True when the table is a linked attachment to an external source
    /// (another Access database, ODBC server, Excel, SharePoint, etc.) rather
    /// than a native local table. Derived from the raw <c>Attributes</c>
    /// bitmask: any of the <c>dbAttached*</c> bits being set yields true. Null
    /// when <c>Attributes</c> could not be read.
    /// </summary>
    public bool? IsLinked { get; init; }

    /// <summary>
    /// The ODBC / JET connect string for linked tables. The raw IR
    /// preserves whatever DAO returned, including the empty string DAO
    /// emits for native local tables — so a non-linked table yields
    /// <c>""</c>, not <c>null</c>. <c>null</c> specifically means the
    /// underlying property could not be read. Consumers that need a
    /// "is this linked" boolean should use <see cref="IsLinked"/>
    /// rather than checking <see cref="Connect"/> for null.
    /// </summary>
    public string? Connect { get; init; }

    /// <summary>
    /// The remote table's original name when this is a linked table — i.e.
    /// the name on the foreign data source, which may differ from
    /// <see cref="Name"/> (the local alias). Like <see cref="Connect"/>,
    /// the raw IR preserves DAO's empty string for native local tables; a
    /// <c>null</c> specifically means the property could not be read.
    /// </summary>
    public string? SourceTableName { get; init; }

    /// <summary>
    /// The table's columns in <see cref="TableField.OrdinalPosition"/> order
    /// — i.e. the order Access stores them, which is also the order
    /// schema-aware consumers should preserve when emitting CREATE TABLE
    /// statements. Null when the underlying DAO call returned no field
    /// collection (rare; typically a corruption or permission case).
    /// </summary>
    public IReadOnlyList<TableField>? Fields { get; init; }

    /// <summary>
    /// The names of the columns making up the primary key, in key order.
    /// Derived from the unique <see cref="TableIndex"/> with
    /// <see cref="TableIndex.IsPrimary"/> = true (Access writes at most one).
    /// Null when no primary-key index was present — common for staging /
    /// import tables. Consumers preferring the index form can still read it
    /// out of <see cref="Indexes"/>; this field is the convenience promotion
    /// because PK lookup is the dominant downstream query.
    /// </summary>
    public IReadOnlyList<string>? PrimaryKey { get; init; }

    /// <summary>
    /// All indexes on the table, including the primary key. Order is the
    /// order DAO returned them, which Access does not guarantee but tends to
    /// preserve. Null if no indexes were present.
    /// </summary>
    public IReadOnlyList<TableIndex>? Indexes { get; init; }

    /// <summary>
    /// The table-level validation rule (a JET SQL boolean expression evaluated
    /// over the row about to be written) — distinct from per-field rules in
    /// <see cref="TableField.ValidationRule"/>. Captured verbatim; structural
    /// parsing is the <c>jet-sql-grammar-and-parser</c> / VBA-expression-
    /// grammar set's work.
    /// <para>
    /// <b>Raw-IR coverage gap (acknowledged):</b> the current
    /// <c>AccessHarvester.Core.Extraction.TableExporter</c> imported from
    /// <c>accessexportertemp1-import</c> does not yet emit table-level
    /// <c>ValidationRule</c> / <c>ValidationText</c> at the JSON root, so
    /// <see cref="TableMetadataExtractor"/> currently always reads
    /// <c>null</c> here. The typed slot is committed now so that the
    /// minor raw-exporter widening this needs (added under a follow-up
    /// session in this set or in the broader plan) can populate it
    /// without a schema contract change. Per-field rules in
    /// <see cref="TableField.ValidationRule"/> are unaffected — those
    /// the raw exporter already emits.
    /// </para>
    /// </summary>
    public string? ValidationRule { get; init; }

    /// <summary>
    /// The user-visible message Access shows when <see cref="ValidationRule"/>
    /// rejects a row. Subject to the same raw-IR coverage gap noted on
    /// <see cref="ValidationRule"/>: the typed slot is committed but the
    /// current raw exporter does not populate it.
    /// </summary>
    public string? ValidationText { get; init; }

    /// <summary>
    /// The raw property bag from the <c>kb/Tables/</c> JSON, with exact-value
    /// lifts removed (<see cref="Connect"/>, <see cref="SourceTableName"/>,
    /// <see cref="ValidationRule"/>, <see cref="ValidationText"/>) but
    /// retained values like the raw <c>Attributes</c> bitmask kept intact for
    /// downstream consumers that need finer-grained flag decoding. Values
    /// preserved as <see cref="JsonElement"/> per <see cref="AccessControl.Properties"/>.
    /// <para>
    /// The discriminator <c>"Type": "Table"</c> emitted by the upstream raw
    /// exporter is stripped; the C# type (<see cref="TableMetadata"/>) carries
    /// that distinction. The structural collections <c>Fields</c> and
    /// <c>Indexes</c> are also stripped — they live on the typed
    /// <see cref="Fields"/> / <see cref="Indexes"/> properties.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Properties { get; init; }
}

/// <summary>
/// One column in a <see cref="TableMetadata"/>. Field-level validation rules
/// and defaults are first-class typed fields; the rest of DAO's per-field
/// property surface (custom Format, Caption, InputMask, Description, etc.) is
/// not modelled here because those are presentation properties downstream
/// migration tooling treats as advisory rather than structural. If a future
/// session needs them, promote on demand rather than scaling the schema
/// pre-emptively.
/// </summary>
public sealed record TableField
{
    /// <summary>The column name as Access stores it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The decoded data-type label — one of <c>Boolean</c>, <c>Byte</c>,
    /// <c>Integer</c>, <c>Long</c>, <c>Currency</c>, <c>Single</c>,
    /// <c>Double</c>, <c>DateTime</c>, <c>Binary</c>, <c>Text</c>,
    /// <c>LongBinary</c>, <c>Memo</c>, <c>GUID</c>, <c>BigInt</c>,
    /// <c>VarBinary</c>, <c>Char</c>, <c>Numeric</c>, <c>Decimal</c>,
    /// <c>Float</c>, <c>Time</c>, <c>TimeStamp</c>, <c>AttachmentField</c>,
    /// or one of the <c>Complex*</c> / multi-value variants — or
    /// <c>Unknown(N)</c> when an unmapped numeric code appears. Decoded from
    /// DAO's <c>dbType*</c>; the raw numeric code is also exposed as
    /// <see cref="DataTypeCode"/>.
    /// </summary>
    public string? DataType { get; init; }

    /// <summary>
    /// The raw DAO <c>dbType*</c> numeric code corresponding to
    /// <see cref="DataType"/>. Kept as a first-class field rather than
    /// hidden in a properties bag because per-field properties bags are not
    /// part of this typed schema (see remarks on <see cref="TableField"/>).
    /// </summary>
    public int? DataTypeCode { get; init; }

    /// <summary>
    /// Declared field size — the max length for <c>Text</c> columns, the byte
    /// width for fixed-width numeric types, etc. Null when not applicable
    /// (e.g. variable-length <c>Memo</c>) or not read.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// True when <c>NULL</c> is rejected; equivalent to a <c>NOT NULL</c>
    /// constraint in standard SQL. Null when the property was not read.
    /// </summary>
    public bool? Required { get; init; }

    /// <summary>
    /// True when zero-length strings are allowed alongside <c>NULL</c>; an
    /// Access-specific distinction with no SQL-standard equivalent. Null
    /// when not applicable (non-text types) or not read.
    /// </summary>
    public bool? AllowZeroLength { get; init; }

    /// <summary>
    /// The default value as Access wrote it — typically a literal
    /// (<c>0</c>, <c>"="</c>-prefixed expression, <c>Now()</c>, etc.).
    /// Captured verbatim; structural parsing of expression defaults is
    /// VBA-expression-grammar territory. Null when no default is set.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// The per-field validation rule (a JET SQL boolean expression evaluated
    /// over the candidate value). Distinct from <see cref="TableMetadata.ValidationRule"/>
    /// (the table-level rule). Null when no per-field rule is set.
    /// </summary>
    public string? ValidationRule { get; init; }

    /// <summary>
    /// The user-visible message Access shows when <see cref="ValidationRule"/>
    /// rejects a value. Null when not set or empty.
    /// </summary>
    public string? ValidationText { get; init; }

    /// <summary>
    /// Zero-based ordinal position; matches the order in
    /// <see cref="TableMetadata.Fields"/> for native tables but is preserved
    /// independently because linked tables can present a non-contiguous set.
    /// </summary>
    public int? OrdinalPosition { get; init; }

    /// <summary>
    /// True when the column is an auto-increment <c>AutoNumber</c> column
    /// (DAO <c>dbAutoIncrField</c> bit set in <see cref="Attributes"/>).
    /// Null when <see cref="Attributes"/> could not be read.
    /// </summary>
    public bool? IsAutoIncrement { get; init; }

    /// <summary>
    /// For columns participating in a relationship as the local end (typical
    /// of foreign-key columns), the name of the referenced column on the
    /// other table. DAO populates this from per-relationship <c>Field</c>
    /// records during <c>TableDef</c> enumeration, so it is preserved here as
    /// a hint. The authoritative relationship graph is
    /// <see cref="RelationshipMetadata"/>; consumers wanting cross-table
    /// joins should consume that, not this hint.
    /// </summary>
    public string? ForeignName { get; init; }

    /// <summary>
    /// The raw DAO <c>Attributes</c> bitmask. Preserved so consumers can
    /// decode bits this schema does not promote (e.g. <c>dbHyperlinkField</c>,
    /// <c>dbDescending</c>) without reparsing.
    /// </summary>
    public int? Attributes { get; init; }
}

/// <summary>
/// One index on a <see cref="TableMetadata"/>. Includes the primary key (which
/// is also surfaced as the convenience <see cref="TableMetadata.PrimaryKey"/>
/// column-name list).
/// </summary>
public sealed record TableIndex
{
    /// <summary>
    /// The index's name. Access auto-generates names for indexes derived from
    /// relationships (e.g. <c>FK_Orders_Customers</c>) and uses
    /// <c>PrimaryKey</c> for the primary-key index by default.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>True when this index is the table's primary key.</summary>
    public bool? IsPrimary { get; init; }

    /// <summary>
    /// True when the index enforces uniqueness across its column tuple.
    /// Implied by <see cref="IsPrimary"/> being true but tracked
    /// independently because Access writes both flags and downstream
    /// consumers may rely on either.
    /// </summary>
    public bool? IsUnique { get; init; }

    /// <summary>
    /// True when the index requires every covered column to be non-null.
    /// </summary>
    public bool? IsRequired { get; init; }

    /// <summary>
    /// The ordered columns covered by this index, with per-column
    /// <see cref="IndexField.IsDescending"/> flags. Null only when the DAO
    /// fields collection could not be read.
    /// </summary>
    public IReadOnlyList<IndexField>? Fields { get; init; }
}

/// <summary>
/// One column reference inside a <see cref="TableIndex"/>. The column name
/// resolves into the parent <see cref="TableMetadata.Fields"/> list by name
/// — Access does not allow indexing on cross-table columns within a single
/// index.
/// </summary>
public sealed record IndexField
{
    /// <summary>The column name (matches a <see cref="TableField.Name"/>).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// True when this column is sorted descending in the index. Derived from
    /// the per-field <c>Attributes</c> bitmask (DAO <c>dbDescending</c>, value
    /// <c>0x1</c>). Null when <see cref="Attributes"/> could not be read.
    /// </summary>
    public bool? IsDescending { get; init; }

    /// <summary>
    /// The raw per-index-column <c>Attributes</c> bitmask. Preserved so
    /// consumers can decode bits this schema does not promote without
    /// reparsing.
    /// </summary>
    public int? Attributes { get; init; }
}
