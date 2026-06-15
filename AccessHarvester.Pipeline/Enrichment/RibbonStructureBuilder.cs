using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds the singular <see cref="RibbonStructure"/> surfaced as
/// <c>NavigationGraph.Ribbon</c> from the faithful raw row dump
/// (<see cref="RibbonXmlArtifact"/>) by parsing the selected row's customUI
/// XML through <see cref="RibbonXmlParser"/>. Pure and deterministic: no I/O,
/// no environment; identical input yields an identical structure.
/// </summary>
/// <remarks>
/// A <c>USysRibbons</c> table can define more than one named ribbon row.
/// <c>NavigationGraph.Ribbon</c> is a singular convenience view, so this
/// builder surfaces exactly one — the first row (ordered by
/// <c>RibbonName</c> then <c>Table</c>, ordinal) whose <c>RibbonXML</c> parses
/// to a non-null structure. Every row is preserved losslessly in the raw dump
/// regardless, so the additional rows are surfaced (not dropped); when more
/// than one parseable ribbon exists, the caller is told via the
/// <see cref="BuildOutcome.AdditionalRibbonCount"/> so it can emit a
/// diagnostic. Choosing the database's <i>active</i> ribbon (Access's
/// <c>CustomRibbonID</c> property) is a separate concern recorded as deferred
/// work, not decided here.
/// </remarks>
public static class RibbonStructureBuilder
{
    /// <summary>The result of a build: the surfaced ribbon plus multiplicity signal.</summary>
    public readonly record struct BuildOutcome(
        RibbonStructure? Ribbon,
        int AdditionalRibbonCount,
        IReadOnlyList<string> MalformedRows);

    /// <summary>
    /// Parses the artifact. Returns an outcome whose <c>Ribbon</c> is null when
    /// the artifact carries no parseable ribbon row. A row whose XML is
    /// malformed is recorded in <see cref="BuildOutcome.MalformedRows"/> (by
    /// <c>table/RibbonName</c>) rather than aborting — the remaining rows are
    /// still considered.
    /// </summary>
    public static BuildOutcome Build(RibbonXmlArtifact? artifact)
    {
        if (artifact is null || artifact.Rows.Count == 0)
            return new BuildOutcome(null, 0, Array.Empty<string>());

        // Deterministic order, independent of DAO row order. RibbonXml is the
        // final tiebreaker so two rows sharing (RibbonName, Table) — RibbonName
        // is not unique (the table PK is a separate autonumber) — still sort
        // stably rather than falling back to non-deterministic DAO row order.
        var ordered = artifact.Rows
            .OrderBy(r => r.RibbonName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(r => r.Table, StringComparer.Ordinal)
            .ThenBy(r => r.RibbonXml ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var parsed = new List<RibbonStructure>();
        var malformed = new List<string>();
        foreach (var row in ordered)
        {
            RibbonStructure? structure;
            try
            {
                structure = RibbonXmlParser.Parse(row.RibbonXml);
            }
            catch (FormatException)
            {
                malformed.Add($"{row.Table}/{row.RibbonName ?? "(unnamed)"}");
                continue;
            }
            if (structure is null) continue; // empty/whitespace XML -> no ribbon
            parsed.Add(structure with { Name = row.RibbonName });
        }

        if (parsed.Count == 0)
            return new BuildOutcome(null, 0, malformed);

        return new BuildOutcome(parsed[0], parsed.Count - 1, malformed);
    }
}
