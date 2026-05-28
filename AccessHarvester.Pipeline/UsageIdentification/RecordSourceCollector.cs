using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Collects RecordSource bindings from form and report IR files.
/// Produces a map from object name (form or report) to its RecordSource
/// (a table or query name). Only non-SQL RecordSources are retained;
/// inline SQL strings (starting with SELECT) are omitted.
/// </summary>
public static class RecordSourceCollector
{
    /// <summary>
    /// Returns a dictionary mapping each form/report name to its RecordSource.
    /// Objects with no RecordSource or with an inline SQL RecordSource are absent.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Collect(IrDatabase db)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var form in db.Forms)
            AddIfNamedSource(map, form.Name, form.RecordSource);
        foreach (var report in db.Reports)
            AddIfNamedSource(map, report.Name, report.RecordSource);
        return map;
    }

    private static void AddIfNamedSource(Dictionary<string, string> map, string name, string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        string trimmed = source.Trim();
        if (SqlDetection.IsInlineSql(trimmed)) return;
        map[name] = trimmed;
    }
}
