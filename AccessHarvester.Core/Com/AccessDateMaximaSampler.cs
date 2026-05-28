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
public sealed class AccessDateMaximaSampler : IDateMaximaSampler, IDisposable
{
    private readonly string _databasePath;
    private AccessComSession? _session;

    public AccessDateMaximaSampler(string databasePath)
    {
        _databasePath = databasePath;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SampledDateMaximum> Sample(IrDatabase db)
    {
        var results = new List<SampledDateMaximum>();
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

            DateTime utc = DateTime.SpecifyKind(maxAcrossCols.Value, DateTimeKind.Utc);
            results.Add(new SampledDateMaximum(t.Name,
                new DateTimeOffset(utc, TimeSpan.Zero), "com"));
        }

        return results;
    }

    private static DateTime? TrySampleMax(dynamic currentDb, string tableName, string colName)
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
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
