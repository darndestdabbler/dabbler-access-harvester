using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Enrichment.Macros;

namespace AccessHarvester.Core.Enrichment;

/// <summary>
/// Emits a canonical <c>Actions: [...]</c> array for standalone macros (under
/// the <c>Macros</c> category) and per-event <c>EventMacros</c> blocks for
/// embedded macros on Form / Report controls. Both use the same
/// <c>_AXL:</c> XML representation as the source of truth, parsed by
/// <see cref="MacroXmlParser"/>; the legacy positional-argument SaveAsText
/// form is ignored when XML is present (the XML has named arguments and
/// canonical-text values, which the AI design-doc stage prefers).
/// </summary>
/// <remarks>
/// Each emitted action carries an <c>InferredFunctionality: null</c> slot —
/// the same contract <see cref="VbaSignatureEnricher"/> uses — reserved for
/// the downstream AI agent to fill. Per-event blocks also expose an
/// <c>InferredFunctionality: null</c> slot so the AI can describe the event
/// handler as a whole.
/// </remarks>
public sealed class MacroActionsEnricher : IObjectEnricher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private MacroActionsEnricher(string category) { Category = category; }

    public string Category { get; }

    public static MacroActionsEnricher Macros { get; } = new("Macros");
    public static MacroActionsEnricher Forms { get; } = new("Forms");
    public static MacroActionsEnricher Reports { get; } = new("Reports");

    public void EnrichFile(string inFile, string outFile)
    {
        string json = File.ReadAllText(inFile);
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidDataException($"Root must be a JSON object: {inFile}");

        if (Category == "Macros")
        {
            EnrichStandalone(root);
        }
        else
        {
            EnrichEmbedded(root);
        }

        File.WriteAllText(outFile, root.ToJsonString(JsonOptions));
    }

    private static void EnrichStandalone(JsonObject root)
    {
        string? xml = MacroXmlParser.AssembleAxlChunks(root);
        var actions = xml is null ? new JsonArray() : MacroXmlParser.ParseToActions(xml);
        root["Actions"] = actions;
        root["InferredFunctionality"] = null;
    }

    private static void EnrichEmbedded(JsonObject root)
    {
        Walk(root);

        static void Walk(JsonObject node)
        {
            // Each control may carry one or more *EmMacro properties — keys
            // ending in "EmMacro" identify which events are wired up. We
            // assemble a single XML doc from the control's child _AXL: chunks
            // and split it into per-event entries.
            AddEventMacrosIfPresent(node);

            if (node["Children"] is JsonArray children)
            {
                foreach (var c in children)
                {
                    if (c is JsonObject obj) Walk(obj);
                }
            }
        }
    }

    private static void AddEventMacrosIfPresent(JsonObject node)
    {
        if (node["Properties"] is not JsonObject props) return;

        // Collect event names in document order — the order matters when the
        // assembled XML contains multiple <UserInterfaceMacro> elements (one
        // per event) and we need to associate them positionally.
        var eventNames = new List<string>();
        foreach (var kvp in props)
        {
            string key = kvp.Key;
            if (key.EndsWith("EmMacro", StringComparison.Ordinal) && key.Length > "EmMacro".Length)
            {
                eventNames.Add(key.Substring(0, key.Length - "EmMacro".Length));
            }
        }
        if (eventNames.Count == 0) return;

        // The XML chunks live as Begin children of the control. Reassemble
        // and split into per-event action lists.
        string? xml = MacroXmlParser.AssembleAxlChunks(node);
        var perEventActions = SplitXmlByEvent(xml, eventNames.Count);

        var eventBlock = new JsonObject();
        for (int i = 0; i < eventNames.Count; i++)
        {
            var actions = i < perEventActions.Count ? perEventActions[i] : new JsonArray();
            eventBlock[eventNames[i]] = new JsonObject
            {
                ["Actions"] = actions,
                ["InferredFunctionality"] = null,
            };
        }
        node["EventMacros"] = eventBlock;
    }

    /// <summary>
    /// Splits a reassembled embedded-macro XML doc into one action list per
    /// event by extracting each top-level <c>&lt;UserInterfaceMacro&gt;</c>
    /// element and parsing it independently. Falls back to one combined list
    /// (assigned to event 0) if the structure doesn't match — better to emit
    /// merged-but-correct actions than to drop them.
    /// </summary>
    private static List<JsonArray> SplitXmlByEvent(string? xml, int eventCount)
    {
        var lists = new List<JsonArray>();
        if (xml is null) return lists;

        // Naive split by top-level <UserInterfaceMacro …> markers. Each event
        // is its own UIM element; we slice the string between the openers.
        const string opener = "<UserInterfaceMacro";
        var starts = new List<int>();
        int idx = xml.IndexOf(opener, StringComparison.Ordinal);
        while (idx >= 0)
        {
            starts.Add(idx);
            idx = xml.IndexOf(opener, idx + opener.Length, StringComparison.Ordinal);
        }

        if (starts.Count == 0)
        {
            lists.Add(MacroXmlParser.ParseToActions(xml));
            return lists;
        }

        for (int i = 0; i < starts.Count; i++)
        {
            int s = starts[i];
            int e = i + 1 < starts.Count ? starts[i + 1] : xml.Length;
            string slice = xml.Substring(s, e - s);
            lists.Add(MacroXmlParser.ParseToActions(slice));
        }

        return lists;
    }
}
