using System;
using System.Collections.Generic;
using AccessHarvester.Pipeline.Classification;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Pure-logic builder for <see cref="FkGraph"/>. Walks a reviewed
/// <see cref="ClassificationDocument"/> in two passes:
///
/// <para>
/// <b>Pass 1 — PK remap decision.</b> For every column with
/// <c>isPrimaryKey: true</c>, decide whether the sanitizer will remap its
/// values. A PK is remapped when its effective masking strategy resolves to
/// <see cref="MaskingStrategy.RemapId"/>. Default strategies per effective
/// category come from plan § 3.5 (<see cref="EffectiveCategoryResolver"/>).
/// A <c>surrogate-autonumber-pk</c> PK with the human
/// <c>preserveAutonumber</c> flag set is forced to <c>preserve</c>; a PK
/// whose effective category is <c>business-natural-key</c> or
/// <c>foreign-key</c> defaults to <c>preserve</c>.
/// </para>
///
/// <para>
/// <b>Pass 2 — FK effective strategy.</b> For every column with
/// <c>isForeignKey: true</c>, look up the referenced PK's decision from
/// Pass 1. If the PK is being remapped, the FK is forced to
/// <see cref="MaskingStrategy.RemapId"/> so the new values stay in lockstep
/// (FK-consistency invariant, plan § 5). If the PK is preserved, the FK is
/// preserved too. Self-referencing FKs use the same map.
/// </para>
///
/// <para>
/// An FK whose target table or column is missing from the document is
/// treated as <c>preserve</c> and flagged via
/// <see cref="MissingFkTargets"/>; the runner surfaces these as
/// degradations rather than aborting, because real-world classification
/// files may have been hand-edited or sourced from a partial scan.
/// </para>
/// </summary>
public static class FkGraphBuilder
{
    public sealed record BuildResult(FkGraph Graph, IReadOnlyList<TableColumn> MissingFkTargets);

    public static BuildResult Build(ClassificationDocument doc)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc));

        var pkDecisions = new Dictionary<TableColumn, bool>(TableColumn.CaseInsensitiveComparer);
        foreach (var table in doc.Tables)
        {
            foreach (var column in table.Columns)
            {
                if (!column.IsPrimaryKey) continue;
                var category = column.HumanOverride.Category ?? column.GuessedCategory;
                var strategy = EffectiveCategoryResolver.ResolveDefaultPkStrategy(category, column);
                if (column.HumanOverride.MaskingStrategy is { } explicitOverride)
                    strategy = explicitOverride;
                pkDecisions[new TableColumn(table.Name, column.Name)] =
                    strategy == MaskingStrategy.RemapId;
            }
        }

        var fkStrategies = new Dictionary<TableColumn, MaskingStrategy>(TableColumn.CaseInsensitiveComparer);
        var missing = new List<TableColumn>();
        foreach (var table in doc.Tables)
        {
            foreach (var column in table.Columns)
            {
                if (!column.IsForeignKey) continue;
                var target = column.FkTarget;
                if (target is null)
                {
                    // isForeignKey:true with fkTarget:null is a malformed
                    // document; default to preserve and surface.
                    missing.Add(new TableColumn(table.Name, column.Name));
                    fkStrategies[new TableColumn(table.Name, column.Name)] = MaskingStrategy.Preserve;
                    continue;
                }
                var targetKey = new TableColumn(target.Table, target.Column);
                if (!pkDecisions.TryGetValue(targetKey, out bool targetWillRemap))
                {
                    // FK references a column the document doesn't mark as a
                    // primary key. Default to preserve and surface.
                    missing.Add(targetKey);
                    fkStrategies[new TableColumn(table.Name, column.Name)] = MaskingStrategy.Preserve;
                    continue;
                }
                fkStrategies[new TableColumn(table.Name, column.Name)] =
                    targetWillRemap ? MaskingStrategy.RemapId : MaskingStrategy.Preserve;
            }
        }

        return new BuildResult(new FkGraph(pkDecisions, fkStrategies), missing);
    }
}
