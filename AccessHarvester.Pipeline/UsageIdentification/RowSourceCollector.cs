using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Collects RowSource references from form IR files.
/// Reads both design-time RowSource from control nodes in the form's
/// Children tree (ComboBox/ListBox with RowSource property) and
/// static VBA assignments of the form Me.cboXxx.RowSource = "tableName".
/// Inline SQL and Value List RowSources are excluded — only table/query
/// name references are collected.
/// </summary>
public static class RowSourceCollector
{
    // Static VBA RowSource assignment: .RowSource = "SomeName"
    private static readonly System.Text.RegularExpressions.Regex _vbaRowSourcePattern =
        new(@"\.RowSource\s*=\s*""([A-Za-z_][A-Za-z0-9_\s]*)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns triples of (formName, controlName, rowSourceName) for all
    /// design-time and statically-assigned RowSources.
    /// </summary>
    public static IReadOnlyList<(string FormName, string ControlName, string RowSourceName)> Collect(IrDatabase db)
    {
        var results = new List<(string, string, string)>();
        foreach (var form in db.Forms)
        {
            // Design-time control nodes (populated when the control tree is in the IR)
            foreach (var ctl in form.Controls)
            {
                if (IsNamedTableOrQuery(ctl.RowSource, ctl.RowSourceType))
                    results.Add((form.Name, ctl.Name ?? "", ctl.RowSource!));
            }

            // VBA static assignments
            if (form.VbaCode is not null)
            {
                foreach (System.Text.RegularExpressions.Match m in _vbaRowSourcePattern.Matches(form.VbaCode))
                {
                    string rowSource = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(rowSource))
                        results.Add((form.Name, "<vba>", rowSource));
                }
            }
        }
        return results;
    }

    private static bool IsNamedTableOrQuery(string? rowSource, string? rowSourceType)
    {
        if (string.IsNullOrWhiteSpace(rowSource)) return false;
        string rs = rowSource.Trim();
        if (SqlDetection.IsInlineSql(rs)) return false;
        // Exclude value lists
        if (rowSourceType != null &&
            rowSourceType.Contains("Value List", StringComparison.OrdinalIgnoreCase)) return false;
        // Exclude semicolon-delimited value lists
        if (rs.Contains(';')) return false;
        return true;
    }
}
