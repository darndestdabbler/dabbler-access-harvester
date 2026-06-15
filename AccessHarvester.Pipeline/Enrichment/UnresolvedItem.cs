using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// One run-wide manual-review record per Phase 6.8 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>: every construct
/// the harvester can identify but cannot fully process produces an
/// <see cref="UnresolvedItem"/> so the downstream Blazor-migration AI
/// consumer (or a human reviewer) sees a single deterministic queue
/// rather than re-walking every prior phase's per-subsystem unresolved
/// shapes.
/// </summary>
/// <remarks>
/// <para>
/// The shape unifies provenance from every prior child set without
/// information loss:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="AccessHarvester.Vba.Resolver.References.ResolverDiagnostic"/>
///   (resolver, Phases 3.x) — Warning-severity diagnostics surface as
///   <see cref="UnresolvedSource.Resolver"/> items. Info-severity entries
///   are deferred-by-design (a downstream resolver phase will close them)
///   and the aggregator drops them from the manual-review queue.</description></item>
///   <item><description><see cref="AccessHarvester.Sql.Parser.FormReferences.UnresolvedFormReference"/>
///   (SQL form-reference resolver, Phase 5.3) — surfaces as
///   <see cref="UnresolvedSource.SqlFormReference"/>.</description></item>
///   <item><description><see cref="AccessHarvester.Sql.Parser.DynamicSql.DynamicSqlExecutionSite"/>
///   with <c>IsAnalyzable == false</c> (dynamic-SQL analyzer,
///   Phase 5.4) — surfaces as <see cref="UnresolvedSource.DynamicSqlSite"/>.</description></item>
///   <item><description><see cref="UnresolvedNavigationTarget"/>
///   (Session 2's navigation-graph builder, Phase 6.7) — surfaces as
///   <see cref="UnresolvedSource.NavigationTarget"/>.</description></item>
///   <item><description><see cref="UnresolvedReferenceFact"/>
///   (Session 1's function classifier, Phase 6.6) — surfaces as
///   <see cref="UnresolvedSource.FunctionClassification"/>. These mirror
///   the resolver's reason codes but are sourced from the classifier's
///   per-procedure walk so they ride alongside the procedure-level
///   classification record.</description></item>
/// </list>
/// <para>
/// The shape is intentionally narrow: enough provenance for a reviewer
/// (or downstream AI) to find the source location and decide what to
/// do, no more. Free-form provenance not captured by the named slots
/// goes in <see cref="AdditionalContext"/> as a string-string map so
/// future producers can extend without a schema break.
/// </para>
/// <para>
/// Per <c>docs/planning/project-guidance.md</c> -> VBA Parser Strategy
/// "unresolvable constructs are explicit, not silent" — every entry in
/// <see cref="AdditionalContext"/> uses stable, additive keys; future
/// producers append rather than rename. Reason codes are likewise
/// stable strings sourced from the producer's own closed reason set
/// (e.g., <see cref="AccessHarvester.Vba.Resolver.References.UnresolvedReasons"/>
/// for resolver diagnostics, <see cref="AccessHarvester.Sql.Parser.FormReferences.UnresolvedReason"/>
/// for SQL form references, <see cref="AccessHarvester.Sql.Parser.DynamicSql.UnanalyzableReason"/>
/// for dynamic-SQL sites, and <see cref="UnresolvedNavigationTarget.Reason"/>'s
/// closed set for navigation targets).
/// </para>
/// <para>
/// All <see cref="UnresolvedItem"/> instances surface from the aggregator
/// in a deterministic order (see <see cref="UnresolvedItemAggregator"/>)
/// so identical input always produces the same <c>UnresolvedItems</c>
/// array — the determinism standard the project guidance pins for
/// parser-and-enrichment outputs.
/// </para>
/// </remarks>
public sealed record UnresolvedItem
{
    /// <summary>
    /// Discriminates which subsystem produced the record so consumers
    /// that want to filter (e.g., "show me only the navigation gaps")
    /// can do so without parsing <see cref="Reason"/>.
    /// </summary>
    public required UnresolvedSource Source { get; init; }

    /// <summary>
    /// Stable reason code from the producer's own closed reason set.
    /// Future producer revisions append to the set rather than renaming
    /// existing codes, so consumers may match on this string exactly.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// One-sentence human-readable description of the construct that
    /// did not fully process (e.g., <c>"DoCmd.OpenForm dispatch with
    /// non-literal target"</c>, <c>"Bare identifier reference whose
    /// declaration was not in scope"</c>). The exact wording is not
    /// part of the contract — consumers that need a stable
    /// classification key match on <see cref="Reason"/> instead.
    /// </summary>
    public required string ConstructDescription { get; init; }

    /// <summary>
    /// Where the construct lives. Carries module / procedure / line /
    /// column when available; some sources (top-level macro entries,
    /// switchboard items extracted from metadata) only carry the
    /// module slot.
    /// </summary>
    public required UnresolvedLocation Location { get; init; }

    /// <summary>
    /// The surrounding source text the producer captured (typically
    /// the original VBA expression, the unresolved chain text, or the
    /// SQL fragment). Null when the producer had no useful surface
    /// text to attach (e.g., a resolver diagnostic whose location
    /// already pins the reference unambiguously).
    /// </summary>
    public string? SurroundingText { get; init; }

    /// <summary>
    /// Mechanically-derived next-step hints the consumer can render
    /// directly. The list is computed from <see cref="Reason"/> via a
    /// closed lookup in <see cref="UnresolvedItemAggregator"/>; it is
    /// not a free-form recommendation, so two items with the same
    /// reason carry the same hint list verbatim. Always non-empty.
    /// </summary>
    public required IReadOnlyList<string> SuggestedNextSteps { get; init; }

    /// <summary>
    /// Producer-specific context that does not fit the named slots.
    /// Keys are stable, additive strings (the producer-level
    /// documentation lists each key it emits). Null when the producer
    /// had no extra context to attach. Sorted by key on construction
    /// so identical input always yields the same key order.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalContext { get; init; }
}

/// <summary>
/// Discriminates which subsystem produced an
/// <see cref="UnresolvedItem"/>. Closed set; future producers append
/// rather than reuse an existing value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnresolvedSource
{
    /// <summary>
    /// VBA name resolver
    /// (<see cref="AccessHarvester.Vba.Resolver.References.ResolverDiagnostic"/>
    /// with Warning severity). Info severity is deferred-by-design and
    /// is not surfaced as a manual-review item.
    /// </summary>
    Resolver,

    /// <summary>
    /// SQL form-reference resolver
    /// (<see cref="AccessHarvester.Sql.Parser.FormReferences.UnresolvedFormReference"/>).
    /// </summary>
    SqlFormReference,

    /// <summary>
    /// Dynamic SQL analyzer execution site whose symbolic value did not
    /// reduce to a usable template
    /// (<see cref="AccessHarvester.Sql.Parser.DynamicSql.DynamicSqlExecutionSite"/>
    /// with <c>IsAnalyzable == false</c>).
    /// </summary>
    DynamicSqlSite,

    /// <summary>
    /// Navigation-graph builder dispatch with a non-literal target
    /// (<see cref="UnresolvedNavigationTarget"/>).
    /// </summary>
    NavigationTarget,

    /// <summary>
    /// Per-procedure unresolved-reference fact attached to a
    /// <see cref="FunctionClassification"/>. Sourced from the same
    /// resolver outcomes as <see cref="Resolver"/> items but emitted
    /// via the classifier's procedure-scoped walk so consumers that
    /// process the per-procedure record set see them in the same place.
    /// </summary>
    FunctionClassification,

    /// <summary>
    /// A MODULE-LEVEL VBA reference (a <c>Const</c>/<c>Dim</c> initializer,
    /// an <c>Implements &lt;unresolved&gt;</c>, a module-scope
    /// <c>As New &lt;unresolved&gt;</c>) that has no enclosing procedure, so
    /// it carries a <see cref="UnresolvedLocation.ModuleName"/> but a null
    /// <see cref="UnresolvedLocation.ProcedureName"/>. DW-11 Layer B (015 S2):
    /// such references live on <c>ResolvedModule.References</c> but were
    /// dropped by <c>ParserOutputProjector</c> (no procedure <c>Body</c> to
    /// surface in) and so had no manifest signal at all; this source gives
    /// them the run-wide index as their home. Sourced via the resolver's
    /// procedure-less references, disjoint from
    /// <see cref="FunctionClassification"/> (which is procedure-scoped), so
    /// the two never double-count.
    /// </summary>
    ModuleLevelReference,
}

/// <summary>
/// Where an <see cref="UnresolvedItem"/> lives. All slots are optional
/// because different producers populate different subsets — a resolver
/// diagnostic carries module / procedure / line / column; a
/// switchboard-item extraction carries only an external-source label.
/// </summary>
public sealed record UnresolvedLocation
{
    /// <summary>
    /// Source-file path the producer associated with the construct.
    /// Empty-string-or-null when no file path was supplied to the
    /// producer (typical for COM-extracted metadata).
    /// </summary>
    public string? File { get; init; }

    /// <summary>
    /// VBA module the construct lives in. Null when the producer is a
    /// non-VBA source (e.g., a switchboard-table item).
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>
    /// Procedure the construct lives in. Null when the producer is at
    /// module-scope or comes from outside the VBA AST.
    /// </summary>
    public string? ProcedureName { get; init; }

    /// <summary>
    /// 1-based source line. Null when the producer did not capture a
    /// line position (rare — only top-level metadata extractions
    /// without text positions).
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Column position as the producer recorded it. The convention
    /// follows the producer's own emission: navigation-graph and
    /// function-classification facts use 1-based columns; resolver
    /// diagnostics and SQL form-reference annotations use the parser
    /// layer's 0-based ANTLR convention. The aggregator preserves the
    /// producer's value verbatim rather than normalising — round-trip
    /// fidelity is more useful to consumers that cross-reference back
    /// to the upstream record than a uniform numbering.
    /// </summary>
    public int? Column { get; init; }
}
