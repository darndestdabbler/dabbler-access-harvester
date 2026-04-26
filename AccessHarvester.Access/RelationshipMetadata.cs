using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Structured metadata for a single Access cross-table relationship — the
/// "relationship graph" deliverable from Plan Phase 4.3. Each
/// <see cref="RelationshipMetadata"/> record corresponds 1:1 with a DAO
/// <c>Relation</c> object on <c>CurrentDb().Relations</c>.
/// </summary>
/// <remarks>
/// DAO labels <c>Relation.Table</c> as the primary (parent) and
/// <c>Relation.ForeignTable</c> as the foreign (child). The per-field DAO
/// shape is similarly directional — <c>Field.Name</c> on the parent,
/// <c>Field.ForeignName</c> on the child. This typed schema preserves that
/// direction with the unambiguous names <see cref="ParentTable"/> /
/// <see cref="ChildTable"/> and <see cref="RelationshipFieldPair.ParentField"/>
/// / <see cref="RelationshipFieldPair.ChildField"/>, matching the labelling
/// the imported raw <c>RelationshipExporter</c> already uses in its IR JSON.
/// <para>
/// The raw DAO <c>Attributes</c> bitmask drives several derived flags
/// (<see cref="IsEnforced"/>, <see cref="CascadeUpdate"/>,
/// <see cref="CascadeDelete"/>, <see cref="IsInherited"/>,
/// <see cref="IsUnique"/>, <see cref="JoinType"/>). The raw bitmask is also
/// preserved on <see cref="Attributes"/> so downstream consumers can decode
/// bits this schema does not promote without reparsing the IR JSON.
/// </para>
/// <para>
/// Output JSON conventions (<c>docs/planning/project-guidance.md</c> -> Output
/// JSON Conventions) apply: every field except <see cref="Name"/>,
/// <see cref="ParentTable"/>, and <see cref="ChildTable"/> is nullable;
/// serializer should configure <c>JsonIgnoreCondition.WhenWritingNull</c>.
/// </para>
/// </remarks>
public sealed record RelationshipMetadata
{
    /// <summary>
    /// The relationship's name. Access typically auto-generates names like
    /// <c>{Parent}{Child}</c> but they can be edited in the Relationships
    /// window; the name uniquely identifies the relationship within the
    /// database.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The parent (primary-key-side) table name.</summary>
    public required string ParentTable { get; init; }

    /// <summary>The child (foreign-key-side) table name.</summary>
    public required string ChildTable { get; init; }

    /// <summary>
    /// The column pairs that join the two tables, in the order DAO returned
    /// them. Composite keys produce a multi-element list; null only when DAO
    /// returned no field collection (rare; corruption case).
    /// </summary>
    public IReadOnlyList<RelationshipFieldPair>? Fields { get; init; }

    /// <summary>
    /// True when referential integrity is enforced (the absence of the DAO
    /// <c>dbRelationDontEnforce</c> bit in <see cref="Attributes"/>). Null
    /// when <see cref="Attributes"/> could not be read.
    /// </summary>
    public bool? IsEnforced { get; init; }

    /// <summary>
    /// True when the relationship cascades updates (DAO
    /// <c>dbRelationUpdateCascade</c>, <c>0x00000100</c>). Null when
    /// <see cref="Attributes"/> could not be read.
    /// </summary>
    public bool? CascadeUpdate { get; init; }

    /// <summary>
    /// True when the relationship cascades deletes (DAO
    /// <c>dbRelationDeleteCascade</c>, <c>0x00001000</c>). Null when
    /// <see cref="Attributes"/> could not be read.
    /// </summary>
    public bool? CascadeDelete { get; init; }

    /// <summary>
    /// True when the relationship was inherited from a linked database
    /// (DAO <c>dbRelationInherited</c>, <c>0x00000004</c>). Null when
    /// <see cref="Attributes"/> could not be read.
    /// </summary>
    public bool? IsInherited { get; init; }

    /// <summary>
    /// True when the relationship is one-to-one (DAO
    /// <c>dbRelationUnique</c>, <c>0x00000001</c>); false implies
    /// one-to-many. Null when <see cref="Attributes"/> could not be read.
    /// </summary>
    public bool? IsUnique { get; init; }

    /// <summary>
    /// The default JOIN flavour Access uses when this relationship is
    /// dragged into the query designer — one of <c>Inner</c>,
    /// <c>LeftOuter</c>, or <c>RightOuter</c>. Decoded from the
    /// <c>dbRelationLeftJoin</c> (<c>0x01000000</c>) and
    /// <c>dbRelationRightJoin</c> (<c>0x02000000</c>) bits in
    /// <see cref="Attributes"/>; absent bits resolve to <c>Inner</c>.
    /// Null when <see cref="Attributes"/> could not be read.
    /// </summary>
    public string? JoinType { get; init; }

    /// <summary>
    /// The raw DAO <c>Attributes</c> bitmask. Preserved so consumers can
    /// decode bits this schema does not promote without reparsing.
    /// </summary>
    public int? Attributes { get; init; }
}

/// <summary>
/// One pair of joined columns in a <see cref="RelationshipMetadata"/>. For a
/// simple one-column FK this list has one entry; composite keys produce
/// multi-entry lists.
/// </summary>
public sealed record RelationshipFieldPair
{
    /// <summary>
    /// The column name on <see cref="RelationshipMetadata.ParentTable"/>
    /// (primary-key side).
    /// </summary>
    public required string ParentField { get; init; }

    /// <summary>
    /// The column name on <see cref="RelationshipMetadata.ChildTable"/>
    /// (foreign-key side) that references <see cref="ParentField"/>.
    /// </summary>
    public required string ChildField { get; init; }
}
