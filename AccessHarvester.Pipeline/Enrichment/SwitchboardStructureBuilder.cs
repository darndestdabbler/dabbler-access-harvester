using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds a <see cref="SwitchboardStructure"/> from the faithful raw row
/// dump (<see cref="SwitchboardItemsArtifact"/>). Pure and deterministic:
/// no I/O, no environment; identical input yields an identical structure.
/// </summary>
/// <remarks>
/// <para>
/// The grouping is <b>generic</b> — it does not assume the canonical
/// Access Switchboard Manager layout. The page key is:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>ParentID</c> when the table carries that column
///     (<see cref="SwitchboardTableRows.HasParentId"/>) — the hand-rolled
///     hierarchy convention.
///   </description></item>
///   <item><description>
///     <c>SwitchboardID</c> otherwise — the wizard-generated convention,
///     where multiple rows share one (non-unique) page id.
///   </description></item>
/// </list>
/// <para>
/// Each page's display name is the <c>ItemText</c> of its lowest-ordered
/// row (lowest <c>ItemNumber</c>, ties broken by <c>SwitchboardID</c>) —
/// which is the <c>ItemNumber = 0</c> header on a canonical table and the
/// title row on a custom one. Every row is preserved as an item
/// (including that header row); command codes are kept raw and undecoded.
/// Rows whose page key is null (orphan <c>ParentID</c> rows) belong to no
/// page and are counted in <see cref="SwitchboardStructure.UnlinkedItemCount"/>
/// rather than dropped silently.
/// </para>
/// </remarks>
public static class SwitchboardStructureBuilder
{
    /// <summary>
    /// Builds the structure, or returns null when the artifact carries no
    /// detected switchboard tables (so the caller leaves
    /// <c>NavigationGraphInputs.Switchboard</c> null).
    /// </summary>
    public static SwitchboardStructure? Build(SwitchboardItemsArtifact? artifact)
    {
        if (artifact is null || artifact.Tables.Count == 0) return null;

        var pages = new List<SwitchboardPage>();
        int unlinked = 0;

        foreach (var table in artifact.Tables)
        {
            var rows = table.Rows ?? Array.Empty<SwitchboardRawRow>();

            int? PageKey(SwitchboardRawRow r) => table.HasParentId ? r.ParentID : r.SwitchboardID;

            // Orphan rows (null page key) carry no page linkage.
            unlinked += rows.Count(r => PageKey(r) is null);

            var groups = rows
                .Where(r => PageKey(r) is not null)
                .GroupBy(r => PageKey(r)!.Value)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var ordered = group
                    .OrderBy(r => r.ItemNumber ?? int.MaxValue)
                    .ThenBy(r => r.SwitchboardID ?? int.MaxValue)
                    .ToList();

                // Page name = the lowest-ordered row's text (canonical
                // ItemNumber=0 header, or the custom title row).
                var header = ordered[0];

                var items = ordered.Select(r => new SwitchboardItem
                {
                    Text = r.ItemText ?? string.Empty,
                    Command = r.Command,                 // raw, nullable — no synthetic 0
                    Argument = Normalize(r.Argument),
                    Provenance = new SwitchboardItemProvenance
                    {
                        Source = "switchboard-row",
                        Table = table.Table,
                        SwitchboardId = r.SwitchboardID,
                        ItemNumber = r.ItemNumber,
                        ParentId = table.HasParentId ? r.ParentID : null,
                    },
                }).ToList();

                pages.Add(new SwitchboardPage
                {
                    SwitchboardId = group.Key,
                    Table = table.Table,
                    Name = header.ItemText ?? string.Empty,
                    Items = items,
                });
            }
        }

        return new SwitchboardStructure
        {
            Pages = pages,
            UnlinkedItemCount = unlinked,
        };
    }

    // An empty / whitespace argument is "no argument" — surface it as
    // null per the output convention (do not emit a synthesised "").
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
