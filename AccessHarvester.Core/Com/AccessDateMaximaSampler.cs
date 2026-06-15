// COM-using implementation of Pipeline.Cleanup.IDateMaximaSampler. Lives in Core because Core→Pipeline is the existing project-reference direction.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AccessHarvester.Pipeline.Cleanup;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Core.Com;

/// <summary>Live Access COM SELECT-MAX sampler — see strategy doc §3.2.</summary>
[SupportedOSPlatform("windows")]
public sealed class AccessDateMaximaSampler : IDateMaximaSampler, ISamplerDegradationSource, IDisposable
{
    private readonly string _databasePath;
    private AccessComSession? _session;
    private readonly List<string> _degradations = new();

    public AccessDateMaximaSampler(string databasePath)
    {
        _databasePath = databasePath;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Degradations => _degradations;

    /// <inheritdoc/>
    public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db)
    {
        var results = new List<SampledDateMaximum>();
        _degradations.Clear();
        _session ??= new AccessComSession(_databasePath);
        dynamic app = _session.Application;
        dynamic currentDb = app.CurrentDb();

        foreach (var t in db.Tables)
        {
            if (NamePatternDetector.IsExcludedSystemObject("Table", t.Name)) continue;
            if (!string.IsNullOrWhiteSpace(t.Connect)) continue;
            if (t.DateTimeColumnNames.Count == 0) continue;

            DateTime? maxAcrossCols = null;
            foreach (var col in t.DateTimeColumnNames)
            {
                DateTime? val = TrySampleMax(currentDb, t.Name, col);
                if (val is null) continue;
                if (maxAcrossCols is null || val.Value > maxAcrossCols.Value)
                    maxAcrossCols = val.Value;
            }
            if (maxAcrossCols is null) continue;

            // O-1-5 (Set 014): the sampled value is an Access *local wall-clock*
            // DateTime. Anchor it at offset 0 for the day-difference arithmetic
            // (the analyser anchors the harvest date the same way, so the comparison
            // stays consistent) but do NOT mislabel it as UTC — SpecifyKind(Utc) was
            // a false zone claim, not a conversion. Keep the kind Unspecified.
            DateTime naive = DateTime.SpecifyKind(maxAcrossCols.Value, DateTimeKind.Unspecified);
            results.Add(new SampledDateMaximum(t.Name,
                new DateTimeOffset(naive, TimeSpan.Zero), "com"));
        }

        return results;
    }

    private DateTime? TrySampleMax(dynamic currentDb, string tableName, string colName)
    {
        try
        {
            string sql = $"SELECT MAX([{colName}]) FROM [{tableName}]";
            dynamic rs = currentDb.OpenRecordset(sql, 2 /* dbOpenDynaset */);
            try
            {
                if (rs.EOF) return null;
                var f0 = rs.Fields[0];
                object? v = f0.Value;
                if (v is null || v is DBNull) return null;
                if (v is DateTime dt) return dt;
                return Convert.ToDateTime(v, System.Globalization.CultureInfo.InvariantCulture);
            }
            finally
            {
                try { rs.Close(); } catch { }
                try { Marshal.ReleaseComObject(rs); } catch { }
            }
        }
        catch (Exception ex)
        {
            // O-1-5 (Set 014): record the swallowed per-column failure instead of a
            // silent null. A null return still means "no usable max for this column"
            // (so sampling continues for the table's other columns), but the failure
            // is now visible as a degradation note rather than indistinguishable from
            // "column has no date data".
            _degradations.Add(
                $"Date sampling failed for [{tableName}].[{colName}]: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
