using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Composite output of one <see cref="EnrichmentPipelineRunner.Run"/> call.
/// Bundles the enriched form specification (with <c>Classification</c> slots
/// populated), the extracted validation rules, the row-source specs, and
/// the raw event-handler classifications that were used to produce them.
/// Implements the pipeline-visitor stitching from Phase 6.3–6.5 described
/// in <see cref="ValidationRuleExtractor"/>'s remarks as a Session 4 deliverable.
/// </summary>
public sealed record EnrichmentResult
{
    /// <summary>
    /// The <see cref="FormComponentSpec"/> with every
    /// <see cref="EventHandlerReference.Classification"/> and
    /// <see cref="ValidationRuleReference.Classification"/> slot
    /// populated from the extracted classifications.
    /// </summary>
    public required FormComponentSpec EnrichedSpec { get; init; }

    /// <summary>
    /// Structured validation rules extracted from the form's design-time
    /// property bags and any <c>BeforeUpdate</c> event-handler bodies
    /// that were supplied in the procedures list.
    /// </summary>
    public required IReadOnlyList<ValidationRule> ValidationRules { get; init; }

    /// <summary>
    /// Row-source specifications for every combo / list control on the form,
    /// annotated with cascading-driver cross-references from
    /// <see cref="EventHandlerClassifications"/>.
    /// </summary>
    public required IReadOnlyList<RowSourceSpec> RowSourceSpecs { get; init; }

    /// <summary>
    /// Per-procedure event-handler classifications produced by
    /// <see cref="EventHandlerClassifier"/>. Includes only the procedures
    /// that were matched to an <see cref="EventHandlerReference"/> in
    /// <see cref="EnrichedSpec"/>.
    /// </summary>
    public required IReadOnlyList<EventHandlerClassification> EventHandlerClassifications { get; init; }
}
