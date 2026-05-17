using System.Text;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace AccessHarvester.IrCodegen;

internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "usage: AccessHarvester.IrCodegen <ir-file.yaml|json> <output-dir> [<namespace>]");
            return 2;
        }

        var inputPath = args[0];
        var outputDir = args[1];
        var ns = args.Length >= 3 ? args[2] : "BlazorAddAnOrder.Ui.Pages.Generated";

        var ir = LoadIr(inputPath);

        var pageType = ir.GetProperty("pageType").GetString();
        if (pageType != "ModalDetailForm")
        {
            Console.Error.WriteLine($"unsupported pageType: {pageType}");
            return 3;
        }

        var id = ir.GetProperty("id").GetString()!;
        var className = ToPascalCase(id);

        var gen = new ModalDetailFormGenerator(ir, className, ns);
        var razor = gen.EmitRazor();
        var razorCs = gen.EmitRazorCs();

        Directory.CreateDirectory(outputDir);
        var razorPath = Path.Combine(outputDir, $"{className}.razor");
        var razorCsPath = Path.Combine(outputDir, $"{className}.razor.cs");
        File.WriteAllText(razorPath, razor);
        File.WriteAllText(razorCsPath, razorCs);

        Console.WriteLine($"wrote {razorPath}");
        Console.WriteLine($"wrote {razorCsPath}");
        return 0;
    }

    static JsonElement LoadIr(string path)
    {
        var text = File.ReadAllText(path);
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonDocument.Parse(text).RootElement.Clone();
        }
        // Use YamlDotNet's representation model so we can inspect each scalar's
        // style (plain vs quoted) and apply YAML 1.2 Core-schema coercion --
        // `false`, `42`, `null`, etc. become real JSON bools/numbers/null only
        // when the source author wrote them unquoted.
        var stream = new YamlStream();
        using var reader = new StringReader(text);
        stream.Load(reader);
        var root = stream.Documents[0].RootNode;
        var jsonBuf = new StringBuilder();
        YamlToJson(root, jsonBuf);
        return JsonDocument.Parse(jsonBuf.ToString()).RootElement.Clone();
    }

    static void YamlToJson(YamlNode node, StringBuilder sb)
    {
        switch (node)
        {
            case YamlScalarNode s:
                EmitScalar(s, sb);
                break;
            case YamlMappingNode m:
                sb.Append('{');
                bool first = true;
                foreach (var kv in m.Children)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(JsonQuote(((YamlScalarNode)kv.Key).Value ?? string.Empty));
                    sb.Append(':');
                    YamlToJson(kv.Value, sb);
                }
                sb.Append('}');
                break;
            case YamlSequenceNode seq:
                sb.Append('[');
                bool sfirst = true;
                foreach (var c in seq.Children)
                {
                    if (!sfirst) sb.Append(',');
                    sfirst = false;
                    YamlToJson(c, sb);
                }
                sb.Append(']');
                break;
        }
    }

    static void EmitScalar(YamlScalarNode s, StringBuilder sb)
    {
        var v = s.Value ?? string.Empty;
        // Quoted scalars stay strings even if their text looks numeric/bool.
        if (s.Style != YamlDotNet.Core.ScalarStyle.Plain)
        {
            sb.Append(JsonQuote(v));
            return;
        }
        // YAML 1.2 Core schema plain-scalar inference.
        if (v.Length == 0 || v == "~" || v == "null" || v == "Null" || v == "NULL")
        {
            sb.Append("null");
            return;
        }
        if (v == "true" || v == "True" || v == "TRUE") { sb.Append("true"); return; }
        if (v == "false" || v == "False" || v == "FALSE") { sb.Append("false"); return; }
        if (long.TryParse(v, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var i))
        { sb.Append(i); return; }
        if (double.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
        { sb.Append(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); return; }
        sb.Append(JsonQuote(v));
    }

    static string JsonQuote(string s)
    {
        var b = new StringBuilder("\"");
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\b': b.Append("\\b"); break;
                case '\f': b.Append("\\f"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                default:
                    if (ch < 0x20) b.Append($"\\u{(int)ch:x4}");
                    else b.Append(ch);
                    break;
            }
        }
        b.Append('"');
        return b.ToString();
    }

    static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
