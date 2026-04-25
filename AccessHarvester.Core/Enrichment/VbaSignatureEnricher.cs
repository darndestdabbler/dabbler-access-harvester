using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Enrichment.Vba;

namespace AccessHarvester.Core.Enrichment;

/// <summary>
/// Reads the owning node's <c>VbaCode</c> field, splits it into procedures, parses
/// each signature, and writes a sibling <c>Procedures</c> array. Bodies are not
/// emitted — they remain only in <c>VbaCode</c> so consumers can slice by
/// <c>StartLine</c>. Each procedure carries an <c>InferredFunctionality: null</c>
/// slot reserved for the downstream AI design-doc stage to fill.
/// </summary>
/// <remarks>
/// One instance per VBA-bearing category. For Forms / Reports the IR root has a
/// <c>VbaCode</c> populated by <see cref="Translation.AccessTextParser"/> when the
/// source contained <c>CodeBehindForm</c> / <c>Attribute VB_</c> headers. For
/// ModulesAndClasses the kb file is itself a <c>{Name, Type, VbaCode}</c> shape
/// produced by <see cref="Translation.SaveAsTextTranslator"/>.
/// <para>
/// Always emits an array — empty when the node has no VBA — so consumers don't
/// special-case absence (mirrors the <c>Label: { source: "unresolved" }</c>
/// pattern). Reads only the root's <c>VbaCode</c>; child nodes' nullable
/// <c>VbaCode</c> fields are ignored.
/// </para>
/// </remarks>
public sealed class VbaSignatureEnricher : IObjectEnricher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private VbaSignatureEnricher(string category) { Category = category; }

    public string Category { get; }

    public static VbaSignatureEnricher Forms { get; } = new("Forms");
    public static VbaSignatureEnricher Reports { get; } = new("Reports");
    public static VbaSignatureEnricher ModulesAndClasses { get; } = new("ModulesAndClasses");

    public void EnrichFile(string inFile, string outFile)
    {
        string json = File.ReadAllText(inFile);
        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidDataException($"Root must be a JSON object: {inFile}");

        string? vba = (root["VbaCode"] as JsonValue)?.GetValue<string>();
        var procedures = BuildProcedures(vba);

        var arr = new JsonArray();
        foreach (var p in procedures) arr.Add(ToJsonNode(p));
        root["Procedures"] = arr;

        File.WriteAllText(outFile, root.ToJsonString(JsonOptions));
    }

    private static List<VbaProcedureInfo> BuildProcedures(string? vba)
    {
        var result = new List<VbaProcedureInfo>();
        if (string.IsNullOrWhiteSpace(vba)) return result;

        foreach (var block in VbaModuleSplitter.Split(vba))
        {
            var info = VbaSignatureParser.ParseSignature(block.SignatureLine);
            if (info is null) continue;
            info.StartLine = block.StartLine;
            result.Add(info);
        }
        return result;
    }

    private static JsonNode ToJsonNode(VbaProcedureInfo p)
    {
        // Hand-build the JsonObject so we control ordering and force null fields
        // (InferredFunctionality / Notes / Lib / Alias / ReturnType) to appear.
        var obj = new JsonObject
        {
            ["Name"] = p.Name,
            ["ProcedureType"] = p.ProcedureType,
            ["AccessModifier"] = p.AccessModifier,
            ["IsStatic"] = p.IsStatic,
            ["ReturnType"] = p.ReturnType,
        };

        var paramArr = new JsonArray();
        foreach (var param in p.Parameters)
        {
            paramArr.Add(new JsonObject
            {
                ["Name"] = param.Name,
                ["DataType"] = param.DataType,
                ["PassingMechanism"] = param.PassingMechanism,
                ["IsOptional"] = param.IsOptional,
                ["IsParamArray"] = param.IsParamArray,
                ["IsArray"] = param.IsArray,
                ["DefaultValue"] = param.DefaultValue,
            });
        }
        obj["Parameters"] = paramArr;
        obj["StartLine"] = p.StartLine;
        obj["Lib"] = p.Lib;
        obj["Alias"] = p.Alias;
        obj["InferredFunctionality"] = null;
        obj["Notes"] = null;
        return obj;
    }
}
