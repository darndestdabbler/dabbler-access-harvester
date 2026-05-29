using System;
using AccessHarvester.Pipeline.Classification;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Pure-logic resolver for the effective <c>(category, maskingStrategy)</c>
/// pair per column. The rule (plan § 5 and the schema's
/// <c>ColumnHumanOverride</c> definition):
///
/// <para>
/// 1. Effective category = <c>humanOverride.category ?? guessedCategory</c>.
/// 2. Effective strategy = <c>humanOverride.maskingStrategy ?? DefaultFor(effectiveCategory, column, fkGraph)</c>.
/// </para>
///
/// FK and PK columns get their strategy from the <see cref="FkGraph"/>
/// rather than from a per-category default — the schema's <c>allOf</c>
/// rule constrains FK overrides to <c>preserve</c> or <c>remap-id</c>, and
/// PKs of remapped surrogates must remap in lockstep. The resolver respects
/// any human override that satisfies the schema constraint; values rejected
/// by the schema would not reach the sanitizer at all (the editor enforces
/// the allOf rule at save time, and the sanitizer re-validates).
/// </summary>
public static class EffectiveCategoryResolver
{
    public sealed record EffectiveDecision(
        ColumnCategory Category,
        MaskingStrategy Strategy);

    public static EffectiveDecision Resolve(
        ClassifiedColumn column,
        TableColumn key,
        FkGraph fkGraph)
    {
        if (column  is null) throw new ArgumentNullException(nameof(column));
        if (fkGraph is null) throw new ArgumentNullException(nameof(fkGraph));

        var effectiveCategory = column.HumanOverride.Category ?? column.GuessedCategory;

        if (column.IsPrimaryKey
            && fkGraph.PkRemapDecisions.TryGetValue(key, out bool willRemap))
        {
            return new EffectiveDecision(
                effectiveCategory,
                willRemap ? MaskingStrategy.RemapId : MaskingStrategy.Preserve);
        }

        if (column.IsForeignKey
            && fkGraph.FkEffectiveStrategies.TryGetValue(key, out var fkStrategy))
        {
            return new EffectiveDecision(effectiveCategory, fkStrategy);
        }

        var strategy = column.HumanOverride.MaskingStrategy
            ?? DefaultStrategyFor(effectiveCategory, column);
        return new EffectiveDecision(effectiveCategory, strategy);
    }

    /// <summary>
    /// Per-category default masking strategy from plan § 3.5. PK + FK
    /// columns are routed through <see cref="FkGraph"/> instead and never
    /// hit this method during <see cref="Resolve"/>; the public surface is
    /// preserved so <see cref="FkGraphBuilder"/> can ask "what would the PK
    /// default be?" while building Pass 1.
    /// </summary>
    public static MaskingStrategy DefaultStrategyFor(
        ColumnCategory category,
        ClassifiedColumn column)
    {
        if (column is null) throw new ArgumentNullException(nameof(column));
        return category switch
        {
            ColumnCategory.DirectIdentifierPii    => MaskingStrategy.MockReplace,
            ColumnCategory.QuasiIdentifier        => MaskingStrategy.Generalize,
            ColumnCategory.SensitiveNonPii        => MaskingStrategy.Redact,
            ColumnCategory.FreeTextMemo           => MaskingStrategy.ScrubFreeText,
            ColumnCategory.LookupReference        => MaskingStrategy.Preserve,
            ColumnCategory.SystemAudit            => MaskingStrategy.Regenerate,
            ColumnCategory.ForeignKey             => MaskingStrategy.Preserve,
            ColumnCategory.SurrogateAutonumberPk  =>
                column.HumanOverride.PreserveAutonumber ? MaskingStrategy.Preserve : MaskingStrategy.RemapId,
            ColumnCategory.BusinessNaturalKey     => MaskingStrategy.Preserve,
            ColumnCategory.GenericNonSensitive    => MaskingStrategy.Preserve,
            _ => MaskingStrategy.Preserve,
        };
    }

    /// <summary>
    /// PK-only default-strategy helper used by <see cref="FkGraphBuilder"/>'s
    /// Pass 1. Surrogate-autonumber-PKs remap by default; everything else a
    /// PK could plausibly be (business-natural-key, generic-non-sensitive,
    /// even a misclassified PII column) defaults to preserve. The human can
    /// still override the strategy via <c>humanOverride.maskingStrategy</c>.
    /// </summary>
    public static MaskingStrategy ResolveDefaultPkStrategy(
        ColumnCategory category,
        ClassifiedColumn column) =>
        category switch
        {
            ColumnCategory.SurrogateAutonumberPk =>
                column.HumanOverride.PreserveAutonumber ? MaskingStrategy.Preserve : MaskingStrategy.RemapId,
            _ => MaskingStrategy.Preserve,
        };
}
