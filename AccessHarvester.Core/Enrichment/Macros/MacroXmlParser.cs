using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace AccessHarvester.Core.Enrichment.Macros;

/// <summary>
/// Parses an Access macro's <c>_AXL:</c> XML representation into a flat,
/// canonical list of actions. The XML root is <c>&lt;UserInterfaceMacro&gt;</c>
/// (one or more) containing <c>&lt;Statements&gt;</c>; statement children are
/// <c>&lt;Action&gt;</c>, <c>&lt;ConditionalBlock&gt;</c>, <c>&lt;Comment&gt;</c>,
/// and <c>&lt;SubMacro&gt;</c>. We flatten the tree, propagating each action's
/// effective condition string (and its branch label — If / ElseIf / Else) so
/// downstream consumers don't have to walk the conditional structure.
/// </summary>
/// <remarks>
/// Two callers — the standalone-macro enricher reassembles a chunked
/// <c>_AXL:</c> string from the SaveAsText representation and calls
/// <see cref="ParseToActions"/>; the embedded-macro enricher hands it the
/// XML stored in a control's <c>*EmMacro</c> property (or reassembled from
/// child <c>_AXL:</c> Comment chunks). One parser, two sources, by design.
/// </remarks>
public static class MacroXmlParser
{
    /// <summary>
    /// Walks <paramref name="root"/> in document order, collects every
    /// <c>Begin</c> child whose <c>Comment</c> property starts with
    /// <c>"_AXL:"</c>, and concatenates the chunks (stripping the prefix) into
    /// the assembled XML string. Returns null if no chunks were found.
    /// </summary>
    public static string? AssembleAxlChunks(JsonObject root)
    {
        var sb = new StringBuilder();
        Walk(root, sb);
        return sb.Length == 0 ? null : sb.ToString();

        static void Walk(JsonObject node, StringBuilder sb)
        {
            // Each Begin child holds its own _AXL: chunk in Properties.Comment.
            // We intentionally don't recurse — the standalone macro file's
            // _AXL: chunks live at the document root, and embedded-macro
            // chunks live as direct children of the owning control.
            if (node["Children"] is not JsonArray children) return;
            foreach (var c in children)
            {
                if (c is not JsonObject obj) continue;
                if (obj["Properties"] is not JsonObject props) continue;
                if (props["Comment"] is not JsonValue v) continue;
                if (!v.TryGetValue<string>(out var text)) continue;
                if (!text.StartsWith("_AXL:", StringComparison.Ordinal)) continue;
                sb.Append(text.AsSpan(5));
            }
        }
    }

    /// <summary>
    /// Parses <paramref name="xml"/> into a JSON array of action objects.
    /// Handles malformed input by returning a one-element array containing a
    /// <c>ParseError</c> sentinel — the enricher emits this verbatim so a
    /// failure is observable in the IR without crashing the pipeline.
    /// </summary>
    public static JsonArray ParseToActions(string xml)
    {
        var result = new JsonArray();
        XDocument doc;
        try
        {
            // SaveAsText writes the XML with backslash escapes (\" for inner
            // quotes, \015\012 for newlines, \\ for literal backslash). Undo
            // those before XML parsing — and strip the default namespace so
            // descendant queries can use unqualified names.
            string unescaped = UnescapeSaveAsText(xml);
            string stripped = StripDefaultNamespace(unescaped);
            doc = XDocument.Parse(stripped);
        }
        catch (XmlException ex)
        {
            result.Add(BuildError(ex.Message));
            return result;
        }

        if (doc.Root is null)
        {
            result.Add(BuildError("XML has no root element."));
            return result;
        }

        // Top-level Statements lives as a direct child of UserInterfaceMacro
        // (the doc root). Walking descendants would double-count actions — every
        // ConditionalBlock branch has its own nested <Statements>.
        foreach (var stmts in doc.Root.Elements("Statements"))
        {
            EmitStatements(stmts, condition: null, branch: null, subMacro: null, result);
        }

        // SubMacros (named action groups) are sibling elements under the root.
        // For v1 we lump their actions into the same Actions array with a
        // SubMacro="<name>" marker — keeps the output flat and easy for the
        // AI stage to consume.
        foreach (var sub in doc.Root.Descendants("SubMacro"))
        {
            string subName = sub.Attribute("Name")?.Value ?? "";
            foreach (var stmts in sub.Elements("Statements"))
            {
                EmitStatements(stmts, condition: null, branch: null, subMacro: subName, result);
            }
        }

        return result;
    }

    private static void EmitStatements(
        XElement statements,
        string? condition,
        string? branch,
        string? subMacro,
        JsonArray result)
    {
        foreach (var child in statements.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Action":
                    result.Add(BuildAction(child, condition, branch, subMacro));
                    break;
                case "Comment":
                    result.Add(BuildComment(child.Value, condition, branch, subMacro));
                    break;
                case "ConditionalBlock":
                    EmitConditionalBlock(child, subMacro, result);
                    break;
                // Other statement kinds (SubMacro, RunMenuCommand variants,
                // OnError, etc.) — actions are the only kind we've seen that
                // matter; everything else falls through and gets logged as
                // an Action-shaped entry with no Arguments.
                default:
                    result.Add(BuildGenericStatement(child, condition, branch, subMacro));
                    break;
            }
        }
    }

    private static void EmitConditionalBlock(XElement block, string? subMacro, JsonArray result)
    {
        var ifConditions = new List<string>();   // running list of preceding conditions for Else negation

        foreach (var branchEl in block.Elements())
        {
            string branchName = branchEl.Name.LocalName;   // If / ElseIf / Else
            string? effective;

            if (branchName == "Else")
            {
                effective = ifConditions.Count switch
                {
                    0 => null,
                    1 => $"NOT ({ifConditions[0]})",
                    _ => string.Join(" AND ", ifConditions.Select(c => $"NOT ({c})")),
                };
            }
            else
            {
                string? cond = branchEl.Element("Condition")?.Value;
                if (cond is null)
                {
                    effective = null;
                }
                else if (branchName == "ElseIf" && ifConditions.Count > 0)
                {
                    string negPrev = string.Join(" AND ", ifConditions.Select(c => $"NOT ({c})"));
                    effective = $"{negPrev} AND {cond}";
                }
                else
                {
                    effective = cond;
                }
                if (cond is not null) ifConditions.Add(cond);
            }

            foreach (var stmts in branchEl.Elements("Statements"))
            {
                EmitStatements(stmts, effective, branchName, subMacro, result);
            }
        }
    }

    private static JsonObject BuildAction(
        XElement actionEl, string? condition, string? branch, string? subMacro)
    {
        string name = actionEl.Attribute("Name")?.Value ?? "";
        var args = new JsonObject();
        string? comment = null;

        foreach (var child in actionEl.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Argument":
                    string argName = child.Attribute("Name")?.Value ?? "";
                    if (string.IsNullOrEmpty(argName)) continue;
                    args[argName] = child.Value;
                    break;
                case "Comment":
                    comment = child.Value;
                    break;
            }
        }

        var obj = new JsonObject
        {
            ["Action"] = name,
            ["Arguments"] = args,
            ["Condition"] = condition,
            ["ConditionBranch"] = branch,
            ["Comment"] = comment,
            ["InferredFunctionality"] = null,
        };
        if (subMacro is not null) obj["SubMacro"] = subMacro;
        return obj;
    }

    private static JsonObject BuildComment(
        string text, string? condition, string? branch, string? subMacro)
    {
        var args = new JsonObject { ["Text"] = text };
        var obj = new JsonObject
        {
            ["Action"] = "Comment",
            ["Arguments"] = args,
            ["Condition"] = condition,
            ["ConditionBranch"] = branch,
            ["Comment"] = null,
            ["InferredFunctionality"] = null,
        };
        if (subMacro is not null) obj["SubMacro"] = subMacro;
        return obj;
    }

    private static JsonObject BuildGenericStatement(
        XElement el, string? condition, string? branch, string? subMacro)
    {
        var args = new JsonObject();
        foreach (var attr in el.Attributes())
        {
            args[attr.Name.LocalName] = attr.Value;
        }
        if (!string.IsNullOrWhiteSpace(el.Value) && !el.HasElements)
        {
            args["__Text"] = el.Value;
        }
        var obj = new JsonObject
        {
            ["Action"] = el.Name.LocalName,
            ["Arguments"] = args,
            ["Condition"] = condition,
            ["ConditionBranch"] = branch,
            ["Comment"] = null,
            ["InferredFunctionality"] = null,
        };
        if (subMacro is not null) obj["SubMacro"] = subMacro;
        return obj;
    }

    private static JsonObject BuildError(string message)
    {
        return new JsonObject
        {
            ["Action"] = "ParseError",
            ["Arguments"] = new JsonObject { ["message"] = message },
            ["Condition"] = null,
            ["ConditionBranch"] = null,
            ["Comment"] = null,
            ["InferredFunctionality"] = null,
        };
    }

    /// <summary>
    /// Reverses the C-style backslash escapes Access writes inside its
    /// <c>_AXL:</c> chunk strings: <c>\"</c> → <c>"</c>, <c>\\</c> → <c>\</c>,
    /// <c>\015</c> / <c>\012</c> → CR / LF. Other unrecognized backslash
    /// sequences are left intact so we don't accidentally munge XML content.
    /// </summary>
    private static string UnescapeSaveAsText(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length)
            {
                sb.Append(c);
                continue;
            }
            char next = s[i + 1];
            if (next == '"' || next == '\\' || next == '\'')
            {
                sb.Append(next);
                i++;
            }
            else if (next == '0' && i + 3 < s.Length
                     && IsOctalDigit(s[i + 2]) && IsOctalDigit(s[i + 3]))
            {
                int octal = ((s[i + 1] - '0') << 6) | ((s[i + 2] - '0') << 3) | (s[i + 3] - '0');
                sb.Append((char)octal);
                i += 3;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();

        static bool IsOctalDigit(char ch) => ch >= '0' && ch <= '7';
    }

    /// <summary>
    /// Removes default-namespace declarations from XML so descendant queries
    /// can use unqualified element names. We only target the
    /// <c>xmlns="…"</c> form Access emits; prefix declarations are left alone.
    /// </summary>
    private static string StripDefaultNamespace(string xml)
    {
        // Cheap regex-free strip: find ` xmlns="..."` and remove it. There may
        // be more than one (rare), so loop.
        const string marker = " xmlns=\"";
        int idx = xml.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return xml;

        var sb = new StringBuilder(xml.Length);
        int cursor = 0;
        while (idx >= 0)
        {
            sb.Append(xml, cursor, idx - cursor);
            int closeQuote = xml.IndexOf('"', idx + marker.Length);
            if (closeQuote < 0)
            {
                cursor = idx;
                break;
            }
            cursor = closeQuote + 1;
            idx = xml.IndexOf(marker, cursor, StringComparison.Ordinal);
        }
        sb.Append(xml, cursor, xml.Length - cursor);
        return sb.ToString();
    }
}
