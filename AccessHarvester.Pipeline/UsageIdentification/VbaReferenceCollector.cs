using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Collects table/query references found in VBA code via domain aggregate
/// functions (DCount, DLookup, DSum, DMin, DMax, DFirst, DLast, DAvg) and
/// CurrentDb().OpenRecordset calls. These patterns reveal tables that are
/// used programmatically without a formal RecordSource binding.
/// </summary>
public static class VbaReferenceCollector
{
    // DCount/DLookup/etc. with a quoted table/query name as the second argument
    private static readonly Regex _domainFunctionPattern =
        new(@"\bD(?:Count|Lookup|Sum|Min|Max|First|Last|Avg)\s*\(\s*[^,]+,\s*""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // CurrentDb.OpenRecordset("tableName") or OpenRecordset("tableName")
    private static readonly Regex _openRecordsetPattern =
        new(@"OpenRecordset\s*\(\s*""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record VbaReference(string SourceObject, string ReferencedObject);

    /// <summary>
    /// Returns all VBA-level references to table/query names discovered in
    /// form, report, and module VBA code.
    /// </summary>
    public static IReadOnlyList<VbaReference> Collect(IrDatabase db)
    {
        var results = new List<VbaReference>();
        foreach (var form in db.Forms)
            if (form.VbaCode is not null)
                ScanVba(form.Name, form.VbaCode, results);
        foreach (var report in db.Reports)
            if (report.VbaCode is not null)
                ScanVba(report.Name, report.VbaCode, results);
        foreach (var module in db.Modules)
            if (module.VbaCode is not null)
                ScanVba(module.Name, module.VbaCode, results);
        return results;
    }

    private static void ScanVba(string sourceObject, string vbaCode, List<VbaReference> results)
    {
        foreach (Match m in _domainFunctionPattern.Matches(vbaCode))
        {
            string name = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(name))
                results.Add(new VbaReference(sourceObject, name));
        }
        foreach (Match m in _openRecordsetPattern.Matches(vbaCode))
        {
            string name = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(name) && !SqlDetection.IsInlineSql(name))
                results.Add(new VbaReference(sourceObject, name));
        }
    }
}
