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
/// fields (name, bound data source, filter / order, property bag, design
/// tree, code-behind) and adds the two report-only deliverables:
/// <see cref="ReportMetadata.GroupLevels"/> and
/// <see cref="ReportMetadata.PageSetup"/>.
/// <para>
/// <b>GroupLevels.</b> Built by intersecting two evidence sources in the
/// IR JSON:
/// <list type="number">
///   <item>Sections in the report root's <c>Children</c> array whose
///   <c>Type</c> matches <c>GroupLevel&lt;N&gt;Header</c> or
///   <c>GroupLevel&lt;N&gt;Footer</c> — these set
///   <see cref="GroupLevel.HasHeader"/> / <see cref="GroupLevel.HasFooter"/>.</item>
///   <item>The root <c>Properties</c> key <c>GroupLevel&lt;N&gt;</c> — the
///   field name or expression Access groups on, lifted into
///   <see cref="GroupLevel.GroupOn"/>.</item>
/// </list>
/// Each unique <c>N</c> observed in either source yields one
/// <see cref="GroupLevel"/>; the result is ordered by level number ascending
/// (outermost first, matching <see cref="GroupLevel.Level"/>'s 0 = outermost
/// convention). The per-level mode / interval / sort-order / keep-together
/// fields (<see cref="GroupLevel.GroupingMode"/>,
/// <see cref="GroupLevel.GroupInterval"/>, <see cref="GroupLevel.SortOrder"/>,
/// <see cref="GroupLevel.KeepTogether"/>) need a decoding table for Access's
/// numeric codes (e.g. <c>GroupOn0 = "0"</c> → <c>EachValue</c>); the table
/// and a real-fixture confirmation of the raw shape are out of scope for
/// this session — they remain null and a follow-on session can fill them
/// without breaking the contract.
/// </para>
/// <para>
/// <b>PageSetup.</b> Decoding the <c>PrtMip</c> / <c>PrtDevMode</c> binary
/// blobs is deferred per the spec (the typed schema documents
/// <see cref="ReportMetadata.PageSetup"/> as best-effort / nullable). This
/// extractor returns <c>null</c>.
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

    internal static readonly Regex GroupLevelSectionRegex = new(
        @"^GroupLevel(?<n>\d+)(?<role>Header|Footer)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static readonly Regex GroupLevelPropertyRegex = new(
        @"^GroupLevel(?<n>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        var consumedGroupLevelKeys = new HashSet<string>(StringComparer.Ordinal);
        var groupLevels = BuildGroupLevels(root, rawProperties, consumedGroupLevelKeys);

        HashSet<string> promotedKeys;
        if (consumedGroupLevelKeys.Count == 0)
        {
            promotedKeys = FormMetadataExtractor.PromotedRootKeys;
        }
        else
        {
            promotedKeys = new HashSet<string>(FormMetadataExtractor.PromotedRootKeys, StringComparer.Ordinal);
            foreach (var k in consumedGroupLevelKeys) promotedKeys.Add(k);
        }

        return new ReportMetadata
        {
            Name = name,
            ModuleName = null,
            RecordSource = AccessObjectReader.ReadString(rawProperties, "RecordSource"),
            Filter = AccessObjectReader.ReadString(rawProperties, "Filter"),
            OrderBy = AccessObjectReader.ReadString(rawProperties, "OrderBy"),
            DefaultView = AccessObjectReader.DecodeDefaultView(rawProperties, DefaultViewLabels),
            Properties = AccessObjectReader.BuildPromotedPropertiesBag(rawProperties, promotedKeys),
            GeometryIn = AccessObjectReader.ReadGeometry(root),
            NonDefaultProperties = AccessObjectReader.ReadStringArray(root, "NonDefaultProperties"),
            Children = AccessObjectReader.ReadChildren(root),
            GroupLevels = groupLevels,
            PageSetup = null,
            VbaCode = AccessObjectReader.ReadString(root, "VbaCode"),
        };
    }

    // NOTE: Assumes the report's root Properties bag carries the group-on
    // expression under a flat key literally named "GroupLevel0" / "GroupLevel1" /
    // etc. (per the Session 1 audit recommendation). SaveAsText's alternative is
    // nested `Begin GroupLevel <N>` sub-blocks under the report root, which
    // AccessTextParser would translate into a different shape. No committed real
    // grouped-report IR JSON exists yet to confirm which form Access emits;
    // Session 3's hermetic unit tests cover both interpretations via fabricated
    // input and the SkippableFact integration test will fail loudly if the
    // assumption is wrong against a live extract.
    private static IReadOnlyList<GroupLevel>? BuildGroupLevels(
        JsonElement root,
        JsonElement? rawProperties,
        HashSet<string> consumedGroupLevelKeys)
    {
        var levels = new SortedDictionary<int, (bool HasHeader, bool HasFooter)>();

        if (root.TryGetProperty("Children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                if (child.ValueKind != JsonValueKind.Object) continue;
                if (!child.TryGetProperty("Type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) continue;
                var match = GroupLevelSectionRegex.Match(typeEl.GetString() ?? string.Empty);
                if (!match.Success) continue;
                if (!int.TryParse(match.Groups["n"].Value, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int n))
                    continue;
                bool isHeader = match.Groups["role"].Value == "Header";
                levels.TryGetValue(n, out var state);
                levels[n] = isHeader ? (true, state.HasFooter) : (state.HasHeader, true);
            }
        }

        var groupOnByLevel = new Dictionary<int, string>();
        if (rawProperties is not null)
        {
            foreach (var prop in rawProperties.Value.EnumerateObject())
            {
                var match = GroupLevelPropertyRegex.Match(prop.Name);
                if (!match.Success) continue;
                if (!int.TryParse(match.Groups["n"].Value, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int n))
                    continue;
                var groupOn = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                if (!string.IsNullOrEmpty(groupOn))
                {
                    groupOnByLevel[n] = groupOn!;
                    if (!levels.ContainsKey(n)) levels[n] = (false, false);
                    consumedGroupLevelKeys.Add(prop.Name);
                }
            }
        }

        if (levels.Count == 0) return null;

        var result = new List<GroupLevel>(levels.Count);
        foreach (var (n, state) in levels)
        {
            result.Add(new GroupLevel
            {
                Level = n,
                GroupOn = groupOnByLevel.TryGetValue(n, out var groupOn) ? groupOn : null,
                HasHeader = state.HasHeader,
                HasFooter = state.HasFooter,
            });
        }
        return result;
    }
}
