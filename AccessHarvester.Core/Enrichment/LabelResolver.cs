using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AccessHarvester.Core.Abstractions;

namespace AccessHarvester.Core.Enrichment;

/// <summary>
/// Resolves a visible label for each data-bound / interactive control in a Form or
/// Report and writes it into a <c>Label</c> object on the control node, along with
/// provenance describing which round produced it.
/// </summary>
/// <remarks>
/// Rounds run in this order; the first one that produces a label wins:
/// <list type="number">
///   <item><b>attached</b>    — control has a Label child; use its Caption.</item>
///   <item><b>proximity</b>   — a peer Label in the same Section satisfies a
///   conservative 2× uniqueness rule (see <see cref="ResolveProximity"/>).</item>
///   <item><b>from-name</b>   — control's Name isn't an Access auto-generated default
///   (Text1 / Label2 / Combo7 / ...); split the Name as camelCase.</item>
///   <item><b>from-binding</b> — control's ControlSource is a simple identifier; split
///   as camelCase.</item>
/// </list>
/// Controls that survive all four rounds receive <c>Label: { source: "unresolved" }</c>
/// so downstream consumers don't have to special-case absence.
///
/// Proximity works per-Section (controls' Left/Top are section-local in Access) and
/// on bounding-box edge gaps in twips — not center-to-center — because labels sit
/// immediately left-of or above their control, so edge gap matches human reading.
/// Since the 2× rule is dimensionless, the choice of units is immaterial.
///
/// Safe to run in chain with <see cref="GeometryEnricher"/>: reads only the raw twip
/// properties, not GeometryIn. Enricher order within a category is therefore free.
/// </remarks>
public sealed class LabelResolver : IObjectEnricher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Control types we attempt to label. Buttons are excluded — their
    /// Caption already serves as their visible text.</summary>
    private static readonly HashSet<string> EligibleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextBox", "ComboBox", "ListBox", "CheckBox",
        "OptionButton", "ToggleButton", "SubForm", "Subreport",
    };

    /// <summary>Access auto-assigns names of the form &lt;prefix&gt;&lt;digits&gt;
    /// (e.g., Text1, Combo7, Check3) when the developer doesn't override them. Those
    /// names carry no semantic value and should not seed a Round-3 label.</summary>
    private static readonly Regex GenericNameRegex = new(
        @"^(Text|Label|Combo|Command|Check|List|Option|Toggle|Frame|Line|Rectangle|Box|Image|Tab|Page|Subform|Child|Unbound|Bound)\d+$",
        RegexOptions.Compiled);

    /// <summary>Simple identifier — letters, digits, underscore; starts with a letter
    /// or underscore. Rejects expressions like <c>=Sum([Amt])</c>.</summary>
    private static readonly Regex SimpleIdentifierRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private LabelResolver(string category)
    {
        Category = category;
    }

    public string Category { get; }

    public static LabelResolver Forms { get; } = new("Forms");
    public static LabelResolver Reports { get; } = new("Reports");

    public void EnrichFile(string inFile, string outFile)
    {
        string json = File.ReadAllText(inFile);
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidDataException($"Root must be a JSON object: {inFile}");

        ResolveTree(root);

        File.WriteAllText(outFile, root.ToJsonString(JsonOptions));
    }

    private static void ResolveTree(JsonObject root)
    {
        foreach (var section in EnumerateSections(root))
        {
            ResolveSection(section);
        }
    }

    /// <summary>Yields every Section node reachable from <paramref name="root"/>.
    /// If the document has no explicit Section (rare but possible), the root itself
    /// is yielded so its direct controls still get labeled.</summary>
    private static IEnumerable<JsonObject> EnumerateSections(JsonObject root)
    {
        var sections = new List<JsonObject>();
        Walk(root);
        if (sections.Count == 0) sections.Add(root);
        return sections;

        void Walk(JsonObject n)
        {
            if (TypeOf(n) == "Section") sections.Add(n);
            foreach (var child in ChildrenOf(n))
            {
                if (child is JsonObject co) Walk(co);
            }
        }
    }

    private static void ResolveSection(JsonObject section)
    {
        var controls = new List<ControlInfo>();
        var peerLabels = new List<LabelInfo>();

        foreach (var peer in EnumeratePeers(section))
        {
            string type = TypeOf(peer);
            if (type == "Label")
            {
                if (TryMakeLabelInfo(peer, out var li)) peerLabels.Add(li);
            }
            else if (EligibleTypes.Contains(type))
            {
                if (TryMakeControlInfo(peer, out var ci)) controls.Add(ci);
            }
        }

        // Round 1 — attached.
        foreach (var c in controls)
        {
            if (c.Resolved) continue;
            var attached = FindAttachedLabelCaption(c.Node);
            if (attached is not null)
                c.Assign("Attached", attached);
        }

        // Round 2 — proximity (uses only controls still unlabeled and peer labels).
        ResolveProximity(controls, peerLabels);

        // Round 3 — non-generic Name.
        foreach (var c in controls)
        {
            if (c.Resolved) continue;
            if (c.Name is not null && !GenericNameRegex.IsMatch(c.Name))
                c.Assign("FromName", SplitCamelCase(c.Name));
        }

        // Round 4 — ControlSource.
        foreach (var c in controls)
        {
            if (c.Resolved) continue;
            if (c.ControlSource is not null && SimpleIdentifierRegex.IsMatch(c.ControlSource))
                c.Assign("FromBinding", SplitCamelCase(c.ControlSource));
        }

        // Fallback — unresolved.
        foreach (var c in controls)
        {
            if (!c.Resolved) c.Assign("Unresolved", null);
        }

        foreach (var c in controls) c.WriteBack();
    }

    /// <summary>Yields nodes that are peers within <paramref name="section"/> —
    /// its direct children, transparently descending through anonymous Begin
    /// wrappers but never crossing into a nested Section.</summary>
    private static IEnumerable<JsonObject> EnumeratePeers(JsonObject section)
    {
        var results = new List<JsonObject>();
        foreach (var child in ChildrenOf(section))
        {
            if (child is JsonObject co) Collect(co);
        }
        return results;

        void Collect(JsonObject n)
        {
            string type = TypeOf(n);
            if (type == "Begin")
            {
                foreach (var child in ChildrenOf(n))
                {
                    if (child is JsonObject co) Collect(co);
                }
            }
            else if (type != "Section")
            {
                results.Add(n);
            }
        }
    }

    private static string? FindAttachedLabelCaption(JsonObject control)
    {
        foreach (var child in ChildrenOf(control))
        {
            if (child is not JsonObject co) continue;
            string type = TypeOf(co);
            if (type == "Label")
            {
                // Access auto-creates attached labels for some controls; their
                // Caption is often empty unless the developer set one. Treat
                // whitespace-only captions as "no real label" so subsequent
                // rounds get a chance.
                var caption = GetStringProperty(co, "Caption");
                if (!string.IsNullOrWhiteSpace(caption)) return caption;
            }
            else if (type == "Begin")
            {
                var nested = FindAttachedLabelCaption(co);
                if (nested is not null) return nested;
            }
        }
        return null;
    }

    /// <summary>Applies the conservative 2× rule: a peer label L labels control C
    /// iff L is at least 2× closer to C than any other label is to C, AND L is at
    /// least 2× farther from any other eligible control than it is from C.</summary>
    private static void ResolveProximity(List<ControlInfo> controls, List<LabelInfo> peerLabels)
    {
        if (peerLabels.Count == 0) return;

        var unlabeledControls = controls.Where(c => !c.Resolved).ToList();
        if (unlabeledControls.Count == 0) return;

        var consumedLabels = new HashSet<LabelInfo>();

        foreach (var c in unlabeledControls)
        {
            LabelInfo? best = null;
            double bestGap = double.PositiveInfinity;
            double secondGap = double.PositiveInfinity;

            foreach (var L in peerLabels)
            {
                if (consumedLabels.Contains(L)) continue;
                double g = Gap(L.Box, c.Box);
                if (g < bestGap) { secondGap = bestGap; bestGap = g; best = L; }
                else if (g < secondGap) { secondGap = g; }
            }

            if (best is null) continue;

            // (a) Label uniqueness — best label is 2× closer to C than any other.
            if (secondGap < 2 * bestGap) continue;

            // (b) Control uniqueness — best label is 2× farther from any other
            // eligible control than from C. Compares against *all* eligible controls,
            // not just unlabeled ones: another control already assigned by Round 1
            // is still physically present and can veto an ambiguous proximity match.
            bool controlUnique = true;
            foreach (var other in controls)
            {
                if (ReferenceEquals(other, c)) continue;
                double g2 = Gap(best.Box, other.Box);
                if (g2 < 2 * bestGap) { controlUnique = false; break; }
            }
            if (!controlUnique) continue;

            c.Assign("Proximity", best.Caption);
            consumedLabels.Add(best);
        }
    }

    /// <summary>Axis-aligned bounding-box gap. Zero if the boxes overlap or touch.</summary>
    private static double Gap(BBox a, BBox b)
    {
        double dx = Math.Max(0, Math.Max(a.X, b.X) - Math.Min(a.X + a.W, b.X + b.W));
        double dy = Math.Max(0, Math.Max(a.Y, b.Y) - Math.Min(a.Y + a.H, b.Y + b.H));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static string SplitCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Insert a space before an uppercase letter preceded by a lowercase letter
        // or digit (customerID → customer ID; addr1Line → addr1 Line).
        s = Regex.Replace(s, "([a-z0-9])([A-Z])", "$1 $2");
        // Insert a space before an uppercase letter that begins a word — i.e. is
        // followed by a lowercase letter and preceded by another uppercase letter
        // (XMLParser → XML Parser).
        s = Regex.Replace(s, "([A-Z])([A-Z][a-z])", "$1 $2");
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    // -------------- helpers over the JsonNode shape --------------

    private static string TypeOf(JsonObject n)
        => (n["Type"] as JsonValue)?.GetValue<string>() ?? string.Empty;

    private static IEnumerable<JsonNode?> ChildrenOf(JsonObject n)
        => n["Children"] is JsonArray arr ? arr : Enumerable.Empty<JsonNode?>();

    private static string? GetStringProperty(JsonObject node, string key)
    {
        if (node["Properties"] is not JsonObject props) return null;
        if (props[key] is not JsonValue v) return null;
        return v.TryGetValue<string>(out var s) ? s : null;
    }

    private static bool TryGetTwips(JsonObject props, string key, out double twips)
    {
        twips = 0;
        if (props[key] is not JsonValue v) return false;
        if (!v.TryGetValue<string>(out var s)) return false;
        return double.TryParse(s, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out twips);
    }

    private static BBox BoxFrom(JsonObject props)
    {
        TryGetTwips(props, "Left", out var x);
        TryGetTwips(props, "Top", out var y);
        TryGetTwips(props, "Width", out var w);
        TryGetTwips(props, "Height", out var h);
        return new BBox(x, y, w, h);
    }

    private static bool TryMakeControlInfo(JsonObject node, out ControlInfo info)
    {
        info = null!;
        if (node["Properties"] is not JsonObject props) return false;
        info = new ControlInfo(
            node,
            GetStringProperty(node, "Name"),
            GetStringProperty(node, "ControlSource"),
            BoxFrom(props));
        return true;
    }

    private static bool TryMakeLabelInfo(JsonObject node, out LabelInfo info)
    {
        info = null!;
        if (node["Properties"] is not JsonObject props) return false;
        var caption = GetStringProperty(node, "Caption");
        if (string.IsNullOrWhiteSpace(caption)) return false;
        info = new LabelInfo(node, caption!, BoxFrom(props));
        return true;
    }

    private readonly record struct BBox(double X, double Y, double W, double H);

    private sealed class ControlInfo
    {
        public ControlInfo(JsonObject node, string? name, string? controlSource, BBox box)
        {
            Node = node; Name = name; ControlSource = controlSource; Box = box;
        }

        public JsonObject Node { get; }
        public string? Name { get; }
        public string? ControlSource { get; }
        public BBox Box { get; }

        public bool Resolved => _source is not null;
        private string? _source;
        private string? _text;

        public void Assign(string source, string? text)
        {
            if (Resolved) return;
            _source = source;
            _text = text;
        }

        public void WriteBack()
        {
            var label = new JsonObject { ["source"] = _source };
            if (_text is not null) label["text"] = _text;
            Node["Label"] = label;
        }
    }

    private sealed record LabelInfo(JsonObject Node, string Caption, BBox Box);
}
