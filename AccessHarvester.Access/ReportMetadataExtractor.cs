using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessHarvester.Access;

/// <summary>
/// Pure-logic transform from the enriched <c>ir/Reports/&lt;Name&gt;.ir.json</c>
/// AccessNode tree (produced by <c>AccessHarvester.Core.Translation.SaveAsTextTranslator</c>
/// plus the Reports enricher chain) into a typed <see cref="ReportMetadata"/>
/// record.
/// </summary>
/// <remarks>
/// Mirrors <see cref="FormMetadataExtractor"/> for the shared form/report
/// fields (name, module, bound data source, filter / order, property bag,
/// design tree, code-behind) and adds the two report-only deliverables:
/// <see cref="ReportMetadata.GroupLevels"/> and
/// <see cref="ReportMetadata.PageSetup"/>.
/// <para>
/// <b>GroupLevels.</b> Built by scanning the IR JSON's <c>Children</c> array
/// for two node types:
/// <list type="number">
///   <item>Nodes with <c>Type</c> matching <c>GroupLevel N</c> (space-separated,
///   e.g. <c>"GroupLevel 0"</c>) — these are the configuration nodes produced
///   by <c>AccessTextParser</c> from Access's <c>Begin GroupLevel N</c> blocks.
///   They carry the grouping expression in <c>Properties["GroupOnCol"]</c>,
///   the grouping mode code in <c>Properties["GroupOn"]</c>, and the sort /
///   keep-together / interval values.</item>
///   <item>Nodes with <c>Type</c> matching <c>GroupLevel&lt;N&gt;Header</c> or
///   <c>GroupLevel&lt;N&gt;Footer</c> (no space, e.g. <c>"GroupLevel0Header"</c>)
///   — these are the design-band section nodes. They set
///   <see cref="GroupLevel.HasHeader"/> / <see cref="GroupLevel.HasFooter"/>.</item>
/// </list>
/// Each unique level index observed in either source yields one
/// <see cref="GroupLevel"/>; the result is ordered by level ascending
/// (outermost first).
/// </para>
/// <para>
/// <b>PageSetup.</b> Decoding the <c>PrtMip</c> / <c>PrtDevMode</c> binary
/// blobs is deferred per the spec. This extractor returns <c>null</c>.
/// </para>
/// </remarks>
public static class ReportMetadataExtractor
{
    /// <summary>
    /// Access reports use a different <c>DefaultView</c> enumeration than
    /// forms — sharing <see cref="FormMetadataExtractor.DefaultViewLabels"/>
    /// would silently decode <c>"1"</c> as <c>"Continuous Forms"</c> on a
    /// report whose actual view is <c>"Print Preview"</c>.
    /// </summary>
    internal static readonly Dictionary<string, string> DefaultViewLabels = new(StringComparer.Ordinal)
    {
        ["0"] = "Report View",
        ["1"] = "Print Preview",
        ["2"] = "Layout View",
    };

    // Matches section bands: GroupLevel0Header, GroupLevel0Footer, GroupLevel1Header, ...
    internal static readonly Regex GroupLevelSectionRegex = new(
        @"^GroupLevel(?<n>\d+)(?<role>Header|Footer)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches configuration nodes: "GroupLevel 0", "GroupLevel 1", ...
    // Note the space — this is how AccessTextParser emits "Begin GroupLevel 0".
    internal static readonly Regex GroupLevelConfigRegex = new(
        @"^GroupLevel (?<n>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // GroupOn (mode code) → label
    internal static readonly Dictionary<string, string> GroupingModeLabels = new(StringComparer.Ordinal)
    {
        ["0"] = "EachValue",
        ["1"] = "Prefix",
        ["2"] = "Year",
        ["3"] = "Quarter",
        ["4"] = "Month",
        ["5"] = "Week",
        ["6"] = "Day",
        ["7"] = "Hour",
        ["8"] = "Minute",
        ["9"] = "Interval",
    };

    internal static readonly Dictionary<string, string> SortOrderLabels = new(StringComparer.Ordinal)
    {
        ["0"] = "Ascending",
        ["1"] = "Descending",
    };

    internal static readonly Dictionary<string, string> KeepTogetherLabels = new(StringComparer.Ordinal)
    {
        ["0"] = "None",
        ["1"] = "WholeGroup",
        ["2"] = "WithFirstDetail",
    };

    public static ReportMetadata Extract(string name, string irJson)
    {
        if (irJson is null) throw new ArgumentNullException(nameof(irJson));
        using var doc = JsonDocument.Parse(irJson);
        return Extract(name, doc.RootElement);
    }

    public static ReportMetadata Extract(string name, JsonElement root)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Report name is required.", nameof(name));
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"Expected JSON object at the root of a Report payload but got {root.ValueKind}.",
                nameof(root));

        var rawProperties = AccessObjectReader.GetObject(root, "Properties");
        var vbaCode = AccessObjectReader.ReadString(root, "VbaCode");

        // Read and filter: "GroupLevel N" config nodes are metadata consumed by
        // BuildGroupLevels; they must not appear in Children alongside design-band sections.
        var allChildren = AccessObjectReader.ReadChildren(root);
        var designChildren = allChildren is null ? null
            : allChildren.Where(c => !GroupLevelConfigRegex.IsMatch(c.Type)).ToList() is { Count: > 0 } filtered
                ? (IReadOnlyList<AccessControl>)filtered
                : null;

        return new ReportMetadata
        {
            Name = name,
            ModuleName = AccessObjectReader.ReadModuleName(vbaCode),
            RecordSource = AccessObjectReader.ReadString(rawProperties, "RecordSource"),
            Filter = AccessObjectReader.ReadString(rawProperties, "Filter"),
            OrderBy = AccessObjectReader.ReadString(rawProperties, "OrderBy"),
            DefaultView = AccessObjectReader.DecodeDefaultView(rawProperties, DefaultViewLabels),
            Properties = AccessObjectReader.BuildPromotedPropertiesBag(rawProperties, FormMetadataExtractor.PromotedRootKeys),
            GeometryIn = AccessObjectReader.ReadGeometry(root),
            NonDefaultProperties = AccessObjectReader.ReadStringArray(root, "NonDefaultProperties"),
            Children = designChildren,
            GroupLevels = BuildGroupLevels(root),
            PageSetup = null,
            VbaCode = vbaCode,
        };
    }

    private static IReadOnlyList<GroupLevel>? BuildGroupLevels(JsonElement root)
    {
        if (!root.TryGetProperty("Children", out var children) || children.ValueKind != JsonValueKind.Array)
            return null;

        // Configuration nodes carry the grouping expression and per-level metadata.
        var configs = new Dictionary<int, (string? GroupOnCol, string? GroupOnCode, string? SortOrder, string? KeepTogether, string? GroupInterval)>();
        // Section nodes carry HasHeader / HasFooter.
        var sections = new Dictionary<int, (bool HasHeader, bool HasFooter)>();

        foreach (var child in children.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object) continue;
            if (!child.TryGetProperty("Type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
            var typeName = typeEl.GetString() ?? string.Empty;

            // Configuration node: "GroupLevel 0", "GroupLevel 1", ...
            var configMatch = GroupLevelConfigRegex.Match(typeName);
            if (configMatch.Success)
            {
                if (!int.TryParse(configMatch.Groups["n"].Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int n)) continue;
                var props = AccessObjectReader.GetObject(child, "Properties");
                configs[n] = (
                    GroupOnCol: AccessObjectReader.ReadString(props, "GroupOnCol"),
                    GroupOnCode: AccessObjectReader.ReadString(props, "GroupOn"),
                    SortOrder: AccessObjectReader.ReadString(props, "SortOrder"),
                    KeepTogether: AccessObjectReader.ReadString(props, "KeepTogether"),
                    GroupInterval: AccessObjectReader.ReadString(props, "GroupInterval")
                );
                continue;
            }

            // Section node: "GroupLevel0Header", "GroupLevel0Footer"
            var sectionMatch = GroupLevelSectionRegex.Match(typeName);
            if (!sectionMatch.Success) continue;
            if (!int.TryParse(sectionMatch.Groups["n"].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int sn)) continue;
            bool isHeader = sectionMatch.Groups["role"].Value == "Header";
            sections.TryGetValue(sn, out var state);
            sections[sn] = isHeader ? (true, state.HasFooter) : (state.HasHeader, true);
        }

        // Union of levels seen in either source, ordered ascending.
        var allLevels = new SortedSet<int>(configs.Keys);
        foreach (var k in sections.Keys) allLevels.Add(k);
        if (allLevels.Count == 0) return null;

        var result = new List<GroupLevel>(allLevels.Count);
        foreach (var n in allLevels)
        {
            configs.TryGetValue(n, out var cfg);
            sections.TryGetValue(n, out var sec);

            result.Add(new GroupLevel
            {
                Level = n,
                GroupOn = cfg.GroupOnCol,
                GroupingMode = DecodeLabel(cfg.GroupOnCode, GroupingModeLabels),
                SortOrder = DecodeLabel(cfg.SortOrder, SortOrderLabels),
                KeepTogether = DecodeLabel(cfg.KeepTogether, KeepTogetherLabels),
                GroupInterval = cfg.GroupInterval is not null
                    && int.TryParse(cfg.GroupInterval, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int gi) ? gi : null,
                HasHeader = sec.HasHeader,
                HasFooter = sec.HasFooter,
            });
        }
        return result;
    }

    private static string? DecodeLabel(string? code, Dictionary<string, string> table)
        => code is not null && table.TryGetValue(code, out var label) ? label : null;
}
