using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Pure-logic transform from the enriched <c>ir/Forms/&lt;Name&gt;.ir.json</c>
/// AccessNode tree (produced by <c>AccessHarvester.Core.Translation.SaveAsTextTranslator</c>
/// plus the Forms enricher chain — <c>GeometryEnricher</c>, <c>LabelResolver</c>,
/// <c>PropertyDecoder</c>, <c>TextCapacityEnricher</c>, <c>VbaSignatureEnricher</c>,
/// <c>MacroActionsEnricher</c>) into a typed <see cref="FormMetadata"/> record.
/// </summary>
/// <remarks>
/// The IR JSON for a Form has no top-level <c>Name</c> field — Access's
/// SaveAsText omits it because the file name carries the form name. The
/// extractor therefore takes the form name as an explicit argument; callers
/// derive it from the source file name (e.g.
/// <c>Path.GetFileNameWithoutExtension</c> stripped of the trailing <c>.ir</c>
/// segment).
/// <para>
/// Promotion rules (see <see cref="FormMetadata"/> remarks):
/// <list type="bullet">
///   <item>Exact-value lifts — <c>RecordSource</c>, <c>Filter</c>,
///   <c>OrderBy</c> are read from the IR root <c>Properties</c> bag, copied
///   into the typed fields, and the corresponding keys are removed from the
///   serialized <see cref="FormMetadata.Properties"/>.</item>
///   <item>Decoded lift — <c>DefaultView</c> numeric code (0-5) is mapped to
///   the documented label; the raw numeric code remains in
///   <see cref="FormMetadata.Properties"/> for round-trip fidelity.</item>
///   <item><c>Name</c> is lifted out of each control's <c>Properties</c> bag
///   when present (see <see cref="AccessControl"/>).</item>
/// </list>
/// </para>
/// <para>
/// Top-level enricher outputs that are NOT re-modeled on
/// <see cref="FormMetadata"/> stay accessible from the underlying IR JSON and
/// are excluded from the typed properties bag here:
/// <c>Procedures</c> (<c>VbaSignatureEnricher</c>) and <c>EventMacros</c>
/// (<c>MacroActionsEnricher</c>) per <see cref="FormMetadata.VbaCode"/>'s
/// remarks. Per-control <c>EventMacros</c> blocks attached inside the
/// <see cref="FormMetadata.Children"/> tree are also dropped from each
/// control's <see cref="AccessControl.Properties"/> bag — they live on the
/// IR JSON, not on the typed schema.
/// </para>
/// </remarks>
public static class FormMetadataExtractor
{
    internal static readonly HashSet<string> PromotedRootKeys = new(StringComparer.Ordinal)
    {
        "RecordSource",
        "Filter",
        "OrderBy",
    };

    internal static readonly Dictionary<string, string> DefaultViewLabels = new(StringComparer.Ordinal)
    {
        ["0"] = "Single Form",
        ["1"] = "Continuous Forms",
        ["2"] = "Datasheet",
        ["3"] = "PivotTable",
        ["4"] = "PivotChart",
        ["5"] = "Split Form",
    };

    public static FormMetadata Extract(string name, string irJson)
    {
        if (irJson is null) throw new ArgumentNullException(nameof(irJson));
        using var doc = JsonDocument.Parse(irJson);
        return Extract(name, doc.RootElement);
    }

    public static FormMetadata Extract(string name, JsonElement root)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Form name is required.", nameof(name));
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"Expected JSON object at the root of a Form payload but got {root.ValueKind}.",
                nameof(root));

        var rawProperties = AccessObjectReader.GetObject(root, "Properties");

        return new FormMetadata
        {
            Name = name,
            ModuleName = null,
            RecordSource = AccessObjectReader.ReadString(rawProperties, "RecordSource"),
            Filter = AccessObjectReader.ReadString(rawProperties, "Filter"),
            OrderBy = AccessObjectReader.ReadString(rawProperties, "OrderBy"),
            DefaultView = AccessObjectReader.DecodeDefaultView(rawProperties, DefaultViewLabels),
            Properties = AccessObjectReader.BuildPromotedPropertiesBag(rawProperties, PromotedRootKeys),
            GeometryIn = AccessObjectReader.ReadGeometry(root),
            NonDefaultProperties = AccessObjectReader.ReadStringArray(root, "NonDefaultProperties"),
            Children = AccessObjectReader.ReadChildren(root),
            VbaCode = AccessObjectReader.ReadString(root, "VbaCode"),
        };
    }
}
