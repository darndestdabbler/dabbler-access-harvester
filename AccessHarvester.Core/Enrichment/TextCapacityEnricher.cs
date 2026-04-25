using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AccessHarvester.Core.Abstractions;

namespace AccessHarvester.Core.Enrichment;

/// <summary>
/// Estimates how much text fits inside each text-bearing control (TextBox,
/// ComboBox, ListBox, non-growing Label) and writes the estimate into a
/// <c>TextCapacity</c> object on the control node.
/// </summary>
/// <remarks>
/// The estimate is a heuristic — proportional fonts make exact character counts
/// impossible without knowing the actual text. To make that explicit to the
/// downstream AI stage, the block carries a <c>metric</c> tag of
/// <c>"avgWidth"</c> for proportional fonts and <c>"exact"</c> for fixed-width
/// faces (Courier New, Consolas, ...).
/// <para>
/// Math (twips throughout — 1 inch = 1440 twips, 1 point = 20 twips):
/// <code>
/// inner_w = Width  - LeftPadding - RightPadding
/// inner_h = Height - TopPadding  - BottomPadding
/// avg_char_w_twips = font_avgCharWidthEm * fontSize * 20
/// line_h_twips     = font_lineHeightEm   * fontSize * 20
/// charsPerLine = max(0, floor(inner_w / avg_char_w_twips))
/// visibleLines = max(1, floor(inner_h / line_h_twips))
/// </code>
/// Border widths are intentionally ignored — most Access controls use
/// BorderWidth=0 at default and the math is dominated by font + bbox. Add
/// border subtraction later only if real-world cases demand it.
/// </para>
/// <para>
/// Form-level <c>FontName</c> / <c>FontSize</c> (or <c>DatasheetFontHeight</c>)
/// is read once at the root and inherited by every control that doesn't set its
/// own FontName / FontSize. Controls' resolution is tagged <c>"explicit"</c> if
/// they had their own FontName, <c>"inherited"</c> if they picked up the form's,
/// or <c>"fallback"</c> when the resolved font name isn't in
/// <c>font-metrics.json</c> and the fallback metric is used.
/// </para>
/// <para>
/// Order in <see cref="Pipeline.Default"/>: runs after <see cref="PropertyDecoder"/>
/// so that <c>CanGrow = "NotDefault"</c> on Labels has already been resolved.
/// LabelResolver's <c>Label</c> output is independent and the order between this
/// enricher and LabelResolver doesn't matter.
/// </para>
/// </remarks>
public sealed class TextCapacityEnricher : IObjectEnricher
{
    private const int DefaultPaddingTwips = 30;
    private const string DefaultFontName = "Aptos";
    private const int DefaultFontSize = 11;
    private const double TwipsPerPoint = 20.0;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> EligibleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextBox", "ComboBox", "ListBox", "Label",
    };

    private static readonly Lazy<FontMetricsTable> Metrics =
        new(LoadEmbeddedMetrics, isThreadSafe: true);

    private TextCapacityEnricher(string category) { Category = category; }

    public string Category { get; }

    public static TextCapacityEnricher Forms { get; } = new("Forms");
    public static TextCapacityEnricher Reports { get; } = new("Reports");

    public void EnrichFile(string inFile, string outFile)
    {
        string json = File.ReadAllText(inFile);
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidDataException($"Root must be a JSON object: {inFile}");

        // Form/Report-level font context — controls without an explicit FontName
        // inherit this. DatasheetFontHeight covers the size-only inheritance case
        // for forms whose only font property is the datasheet height.
        string? rootFont = GetStringProperty(root, "FontName");
        int? rootSize = GetIntProperty(root, "FontSize")
                        ?? GetIntProperty(root, "DatasheetFontHeight");

        Walk(root, rootFont, rootSize, Metrics.Value);

        File.WriteAllText(outFile, root.ToJsonString(JsonOptions));
    }

    private static void Walk(JsonObject node, string? ambientFont, int? ambientSize, FontMetricsTable metrics)
    {
        if (TryComputeCapacity(node, ambientFont, ambientSize, metrics, out var capacity))
        {
            node["TextCapacity"] = capacity;
        }

        if (node["Children"] is JsonArray arr)
        {
            foreach (var child in arr)
            {
                if (child is JsonObject co) Walk(co, ambientFont, ambientSize, metrics);
            }
        }
    }

    private static bool TryComputeCapacity(
        JsonObject node, string? ambientFont, int? ambientSize,
        FontMetricsTable metrics, out JsonObject capacity)
    {
        capacity = new JsonObject();

        string type = TypeOf(node);
        if (!EligibleTypes.Contains(type)) return false;

        // Labels with CanGrow are free to wrap; capacity makes no sense.
        if (type.Equals("Label", StringComparison.OrdinalIgnoreCase) && IsCanGrowEffective(node))
            return false;

        if (node["Properties"] is not JsonObject props) return false;

        bool hasGeometry = node["GeometryIn"] is JsonObject;
        bool hasWidth = TryGetTwips(props, "Width", out long widthTwips);
        bool hasHeight = TryGetTwips(props, "Height", out long heightTwips);

        if (!hasGeometry || !hasWidth || !hasHeight || widthTwips <= 0 || heightTwips <= 0)
        {
            capacity = new JsonObject
            {
                ["unavailable"] = true,
                ["reason"] = "no geometry",
            };
            return true;
        }

        // Resolve font name + size (control's own value wins; otherwise inherit).
        string fontName;
        string resolution;
        if (GetStringProperty(node, "FontName") is { Length: > 0 } own)
        {
            fontName = own;
            resolution = "explicit";
        }
        else if (ambientFont is { Length: > 0 })
        {
            fontName = ambientFont;
            resolution = "inherited";
        }
        else
        {
            fontName = DefaultFontName;
            resolution = "inherited";
        }

        int fontSize = GetIntProperty(node, "FontSize")
                       ?? ambientSize
                       ?? DefaultFontSize;

        var metric = metrics.Lookup(fontName, out bool foundInTable);
        if (!foundInTable) resolution = "fallback";

        long leftPad = GetTwipsOrDefault(props, "LeftPadding", DefaultPaddingTwips);
        long rightPad = GetTwipsOrDefault(props, "RightPadding", DefaultPaddingTwips);
        long topPad = GetTwipsOrDefault(props, "TopPadding", DefaultPaddingTwips);
        long bottomPad = GetTwipsOrDefault(props, "BottomPadding", DefaultPaddingTwips);

        long innerW = widthTwips - leftPad - rightPad;
        long innerH = heightTwips - topPad - bottomPad;

        double avgCharWidthTwips = metric.AvgCharWidthEm * fontSize * TwipsPerPoint;
        double lineHeightTwips = metric.LineHeightEm * fontSize * TwipsPerPoint;

        int charsPerLine = avgCharWidthTwips > 0
            ? Math.Max(0, (int)Math.Floor(innerW / avgCharWidthTwips))
            : 0;
        int visibleLines = lineHeightTwips > 0
            ? Math.Max(1, (int)Math.Floor(innerH / lineHeightTwips))
            : 1;

        capacity = new JsonObject
        {
            ["charsPerLine"] = charsPerLine,
            ["visibleLines"] = visibleLines,
            ["font"] = new JsonObject
            {
                ["name"] = fontName,
                ["size"] = fontSize,
                ["resolution"] = resolution,
            },
            ["metric"] = metric.FixedWidth ? "exact" : "avgWidth",
        };
        return true;
    }

    /// <summary>True iff CanGrow is effectively true on this node — either decoded
    /// to <c>"True"</c> by PropertyDecoder, still raw as <c>"NotDefault"</c> (Label
    /// doesn't have CanGrow in the boolean defaults table, so PropertyDecoder
    /// leaves it alone — but a NotDefault marker against a False default still
    /// means True), or listed in NonDefaultProperties.</summary>
    private static bool IsCanGrowEffective(JsonObject node)
    {
        var v = GetStringProperty(node, "CanGrow");
        if (v is "True" or "NotDefault") return true;
        if (node["NonDefaultProperties"] is JsonArray nd)
        {
            foreach (var x in nd)
            {
                if (x is JsonValue jv && jv.TryGetValue<string>(out var s) && s == "CanGrow")
                    return true;
            }
        }
        return false;
    }

    // ---------- helpers over the JsonNode shape ----------

    private static string TypeOf(JsonObject n)
        => (n["Type"] as JsonValue)?.GetValue<string>() ?? string.Empty;

    private static string? GetStringProperty(JsonObject node, string key)
    {
        if (node["Properties"] is not JsonObject props) return null;
        if (props[key] is not JsonValue v) return null;
        return v.TryGetValue<string>(out var s) ? s : null;
    }

    private static int? GetIntProperty(JsonObject node, string key)
    {
        var s = GetStringProperty(node, key);
        if (s is null) return null;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i : null;
    }

    private static bool TryGetTwips(JsonObject props, string key, out long twips)
    {
        twips = 0;
        if (props[key] is not JsonValue v) return false;
        if (!v.TryGetValue<string>(out var s)) return false;
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out twips);
    }

    private static long GetTwipsOrDefault(JsonObject props, string key, long fallback)
        => TryGetTwips(props, key, out var t) ? t : fallback;

    // ---------- embedded font metrics ----------

    private static FontMetricsTable LoadEmbeddedMetrics()
    {
        var asm = typeof(TextCapacityEnricher).Assembly;
        const string resourceName = "AccessHarvester.Core.Resources.font-metrics.json";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource not found: {resourceName}. Ensure csproj includes " +
                "<EmbeddedResource Include=\"Resources\\font-metrics.json\"/>.");
        using var reader = new StreamReader(stream);
        var doc = JsonNode.Parse(reader.ReadToEnd()) as JsonObject
                  ?? throw new InvalidDataException("font-metrics.json is not a JSON object.");

        var fallback = ParseMetric(doc["fallback"] as JsonObject)
                       ?? new FontMetric(0.50, 1.20, false);

        var byName = new Dictionary<string, FontMetric>(StringComparer.OrdinalIgnoreCase);
        if (doc["fonts"] is JsonObject fontsObj)
        {
            foreach (var entry in fontsObj)
            {
                if (entry.Value is JsonObject obj && ParseMetric(obj) is { } m)
                    byName[entry.Key] = m;
            }
        }
        return new FontMetricsTable(byName, fallback);
    }

    private static FontMetric? ParseMetric(JsonObject? obj)
    {
        if (obj is null) return null;
        double avg = (obj["avgCharWidthEm"] as JsonValue)?.GetValue<double>() ?? 0.50;
        double lh = (obj["lineHeightEm"] as JsonValue)?.GetValue<double>() ?? 1.20;
        bool fixedWidth = obj["fixedWidth"] is JsonValue fv
                          && fv.TryGetValue<bool>(out var b) && b;
        return new FontMetric(avg, lh, fixedWidth);
    }

    private readonly record struct FontMetric(double AvgCharWidthEm, double LineHeightEm, bool FixedWidth);

    private sealed class FontMetricsTable
    {
        private readonly Dictionary<string, FontMetric> _byName;
        private readonly FontMetric _fallback;

        public FontMetricsTable(Dictionary<string, FontMetric> byName, FontMetric fallback)
        {
            _byName = byName;
            _fallback = fallback;
        }

        public FontMetric Lookup(string fontName, out bool found)
        {
            if (_byName.TryGetValue(fontName, out var m)) { found = true; return m; }
            found = false;
            return _fallback;
        }
    }
}
