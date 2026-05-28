using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Collects navigation edges from macro IR files and VBA code.
/// A navigation edge is a directed (source, target, edgeType) triple where
/// the source object opens the target via DoCmd.OpenForm / DoCmd.OpenReport
/// or the equivalent macro action.
/// </summary>
public static class NavigationEdgeCollector
{
    // DoCmd.OpenForm "FormName" or DoCmd.OpenReport "ReportName"
    private static readonly Regex _doCmdPattern =
        new(@"DoCmd\.Open(?<verb>Form|Report|Query)\s*\(?\s*""(?<name>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // basListHelpers.OpenDetailFromList ..., "FormName" — common helper in HomeHealthCare
    private static readonly Regex _helperOpenPattern =
        new(@"OpenDetailFromList\s+\w+\s*,\s*""(?<name>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record NavigationEdge(string Source, string Target, string EdgeType);

    /// <summary>
    /// Returns all directed navigation edges found in macros and VBA code.
    /// </summary>
    public static IReadOnlyList<NavigationEdge> Collect(IrDatabase db)
    {
        var edges = new List<NavigationEdge>();

        // Macro-level navigation edges
        foreach (var macro in db.Macros)
            AddMacroEdges(macro.Name, macro.Actions, edges);

        // Form-level VBA navigation edges (form code-behind)
        foreach (var form in db.Forms)
        {
            if (form.VbaCode is not null)
                AddVbaEdges(form.Name, form.VbaCode, edges);
            // Embedded macro action nodes in the form's Children
            AddMacroEdges(form.Name, form.MacroActions, edges);
        }

        // Report-level VBA
        foreach (var report in db.Reports)
            if (report.VbaCode is not null)
                AddVbaEdges(report.Name, report.VbaCode, edges);

        // Standalone module VBA
        foreach (var module in db.Modules)
            if (module.VbaCode is not null)
                AddVbaEdges(module.Name, module.VbaCode, edges);

        return edges;
    }

    private static void AddMacroEdges(string source, IReadOnlyList<IrMacroAction> actions,
                                      List<NavigationEdge> edges)
    {
        foreach (var action in actions)
        {
            string? edgeType = action.Action switch
            {
                var a when string.Equals(a, "OpenForm",   StringComparison.OrdinalIgnoreCase) => "OpenForm",
                var a when string.Equals(a, "OpenReport", StringComparison.OrdinalIgnoreCase) => "OpenReport",
                var a when string.Equals(a, "OpenQuery",  StringComparison.OrdinalIgnoreCase) => "OpenQuery",
                _ => null
            };
            if (edgeType == null) continue;
            string? target = action.Arguments.Count > 0 ? action.Arguments[0] : null;
            if (!string.IsNullOrWhiteSpace(target))
                edges.Add(new NavigationEdge(source, target.Trim(), edgeType));
        }
    }

    private static void AddVbaEdges(string source, string vbaCode, List<NavigationEdge> edges)
    {
        foreach (Match m in _doCmdPattern.Matches(vbaCode))
        {
            string verb   = m.Groups["verb"].Value;
            string target = m.Groups["name"].Value.Trim();
            if (!string.IsNullOrEmpty(target))
                edges.Add(new NavigationEdge(source, target, "Open" + verb));
        }
        foreach (Match m in _helperOpenPattern.Matches(vbaCode))
        {
            string target = m.Groups["name"].Value.Trim();
            if (!string.IsNullOrEmpty(target))
                edges.Add(new NavigationEdge(source, target, "OpenForm"));
        }
    }
}
