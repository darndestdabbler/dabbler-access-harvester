using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>Coordinates the cleanup detectors and assigns confidence tiers per §4.</summary>
public static class CleanupAnalyser
{
    /// <summary>Runs all detectors and returns the combined findings list.</summary>
    /// <param name="db">Parsed IR for the harvested database.</param>
    /// <param name="dateSampler">Fallback (DAO-metadata) sampler. Required; never null.</param>
    /// <param name="harvestDate">Calendar date anchoring the date-signal threshold.</param>
    /// <param name="thresholdDays">Date-signal threshold in days, &gt;= 1.</param>
    /// <param name="comAvailable">Whether the COM path was attempted overall.</param>
    /// <param name="primaryDateSampler">
    /// Optional primary (COM) sampler. When supplied, per-table COM samples are
    /// preferred for tables that have at least one DateTime column; the fallback
    /// sampler is consulted per-table when COM produced no sample (no DateTime
    /// column, or COM failed for that specific table). Strategy doc §3.2 / §5.
    /// </param>
    public static CleanupFindings Run(IrDatabase db, IDateMaximaSampler dateSampler,
        DateOnly harvestDate, int thresholdDays, bool comAvailable,
        IDateMaximaSampler? primaryDateSampler = null)
    {
        if (thresholdDays < 1)
            throw new ArgumentOutOfRangeException(nameof(thresholdDays), "thresholdDays must be >= 1");

        // 1. Collect signals per object.
        var nameSignalsByKey = new Dictionary<(string Type, string Name), List<NamePatternSignal>>();
        var dateSignalsByKey = new Dictionary<(string Type, string Name), DateSignal>();
        var allObjects = EnumerateAllObjects(db).ToList();
        var peers = allObjects.Select(o => (o.Name, o.Type)).ToArray();

        foreach (var (objType, objName) in allObjects)
        {
            if (NamePatternDetector.IsExcludedSystemObject(objType, objName)) continue;
            var sigs = NamePatternDetector.Detect(objName, objType, peers);
            if (sigs.Count > 0)
                nameSignalsByKey[(objType, objName)] = sigs.ToList();
        }

        // Date signals (tables only). Per §3.2 / §5, the source decision is per-table:
        // prefer COM when the table has at least one DateTime column AND COM produced a
        // sample for it; otherwise fall back to DAO metadata (IR LastUpdated). This
        // yields mixed-source findings where top-level dateSignalSource may be 'com'
        // while individual signals report 'dao-metadata'.
        IReadOnlyList<SampledDateMaximum> primarySamples = primaryDateSampler is null
            ? []
            : primaryDateSampler.Sample(db);
        var primaryByTable = primarySamples.ToDictionary(s => s.TableName, s => s,
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SampledDateMaximum> fallbackSamples = dateSampler.Sample(db);
        var fallbackByTable = fallbackSamples.ToDictionary(s => s.TableName, s => s,
            StringComparer.OrdinalIgnoreCase);

        var harvestUtc = new DateTimeOffset(harvestDate.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);
        var allSamplesForResolve = new List<SampledDateMaximum>();
        allSamplesForResolve.AddRange(primarySamples);
        allSamplesForResolve.AddRange(fallbackSamples);

        foreach (var t in db.Tables)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Table", t.Name)) continue;
            if (!string.IsNullOrWhiteSpace(t.Connect)) continue;

            // Per-table source selection: prefer primary (COM) when the table has at
            // least one DateTime column AND COM produced a sample; otherwise fall back
            // to DAO LastUpdated.
            SampledDateMaximum? chosen = null;
            if (primaryDateSampler is not null
                && t.DateTimeColumnNames.Count > 0
                && primaryByTable.TryGetValue(t.Name, out var primary))
            {
                chosen = primary;
            }
            else if (fallbackByTable.TryGetValue(t.Name, out var fallback))
            {
                chosen = fallback;
            }
            if (chosen is null) continue;

            int days = (int)Math.Floor((harvestUtc - chosen.MaxValue).TotalDays);
            if (days <= thresholdDays) continue;
            string summary = $"Max DateTime value {chosen.MaxValue:yyyy-MM-dd} is {days} days before harvest date {harvestDate:yyyy-MM-dd}";
            dateSignalsByKey[("Table", t.Name)] = new DateSignal
            {
                Summary = summary,
                Source = chosen.Source,
                MaxValue = chosen.MaxValue,
                DaysSinceMax = days,
                ThresholdDays = thresholdDays,
            };
        }

        // 2. Build per-object findings with tier assignment. Cleanup findings are
        // positive stale-like signals only; lack of inbound references is too
        // ambiguous in Access to stand on its own as cleanup evidence.
        var findings = new List<CleanupFinding>();
        var keys = new HashSet<(string Type, string Name)>();
        foreach (var k in nameSignalsByKey.Keys) keys.Add(k);
        foreach (var k in dateSignalsByKey.Keys) keys.Add(k);

        foreach (var key in keys)
        {
            bool hasName = nameSignalsByKey.TryGetValue(key, out var nameSigs);
            bool hasDate = dateSignalsByKey.TryGetValue(key, out var dateSig);

            string tier;
            if (hasName && hasDate) tier = ConfidenceTiers.High;
            else if (hasName || hasDate) tier = ConfidenceTiers.Medium;
            else continue;

            var fired = new List<CleanupSignal>();
            if (hasName) fired.AddRange(nameSigs!);
            if (hasDate) fired.Add(dateSig!);

            string explanation = BuildExplanation(key.Type, key.Name, tier, nameSigs, dateSig, thresholdDays);

            findings.Add(new CleanupFinding
            {
                ObjectType = key.Type,
                ObjectName = key.Name,
                ConfidenceTier = tier,
                Explanation = explanation,
                FiredSignals = fired,
            });
        }

        // 3. Stable sort: tier desc, type asc, name asc.
        findings = findings
            .OrderByDescending(f => ConfidenceTiers.Rank(f.ConfidenceTier))
            .ThenBy(f => f.ObjectType, StringComparer.Ordinal)
            .ThenBy(f => f.ObjectName, StringComparer.Ordinal)
            .ToList();

        // 4. Top-level dateSignalSource.
        string dateSignalSource = ResolveDateSignalSource(allSamplesForResolve, comAvailable);

        // 5. Degradation notes.
        var notes = new List<string>();
        if (dateSignalSource == "unavailable")
        {
            notes.Add("Date sampling unavailable: COM declined AND no IR LastUpdated values present. Date-signal path produced no findings; only NamePattern applied.");
        }

        return new CleanupFindings
        {
            Database = db.Name,
            GeneratedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            HarvestDate = harvestDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateSignalSource = dateSignalSource,
            ThresholdDays = thresholdDays,
            DegradationNotes = notes,
            Findings = findings,
        };
    }

    private static IEnumerable<(string Type, string Name)> EnumerateAllObjects(IrDatabase db)
    {
        foreach (var f in db.Forms) yield return ("Form", f.Name);
        foreach (var r in db.Reports) yield return ("Report", r.Name);
        foreach (var q in db.Queries) yield return ("Query", q.Name);
        foreach (var t in db.Tables) yield return ("Table", t.Name);
        foreach (var m in db.Modules)
        {
            if (NamePatternDetector.IsCodeBehindModuleName(m.Name)) continue;
            yield return ("Module", m.Name);
        }
        foreach (var m in db.Macros) yield return ("Macro", m.Name);
    }

    private static string BuildExplanation(string objectType, string name, string tier,
        IReadOnlyList<NamePatternSignal>? nameSigs, DateSignal? dateSig, int thresholdDays)
    {
        if (tier == ConfidenceTiers.High && dateSig is not null)
            return $"{objectType} '{name}' has staleness-pattern name AND no data updated in {dateSig.DaysSinceMax} days — likely archived/superseded.";
        if (tier == ConfidenceTiers.Medium && nameSigs is not null && nameSigs.Count > 0)
            return $"{objectType} '{name}' name matches staleness pattern '{nameSigs[0].MatchedSubstring}' — likely archived/superseded.";
        if (tier == ConfidenceTiers.Medium && dateSig is not null)
            return $"Table '{name}' has had no data updated in {dateSig.DaysSinceMax} days (threshold: {thresholdDays}) — possibly stale.";

        throw new InvalidOperationException(
            $"Unexpected cleanup tier '{tier}' for {objectType} '{name}'.");
    }

    private static string ResolveDateSignalSource(IReadOnlyList<SampledDateMaximum> samples, bool comAvailable)
    {
        if (samples.Count == 0) return "unavailable";
        if (samples.Any(s => s.Source == "com")) return "com";
        return "dao-metadata";
    }
}
