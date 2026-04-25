using System.Text;
using System.Text.RegularExpressions;

namespace AccessHarvester.Core.Enrichment.Vba;

public sealed class VbaProcedureInfo
{
    public string Name { get; set; } = "";
    public string ProcedureType { get; set; } = "";
    public string AccessModifier { get; set; } = "Public";
    public bool IsStatic { get; set; }
    public string? ReturnType { get; set; }
    public List<VbaParameter> Parameters { get; set; } = new();
    public int StartLine { get; set; }
    public string? Lib { get; set; }
    public string? Alias { get; set; }
    public object? InferredFunctionality { get; set; }
    public object? Notes { get; set; }
}

public sealed class VbaParameter
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "Variant";
    public string PassingMechanism { get; set; } = "ByRef";
    public bool IsOptional { get; set; }
    public bool IsParamArray { get; set; }
    public bool IsArray { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Parses a single logical VBA signature line into a <see cref="VbaProcedureInfo"/>.
/// Line continuations (<c> _</c>) must already be folded by the caller; the parser
/// is intentionally line-oriented so the orchestration layer
/// (<see cref="VbaModuleSplitter"/>) owns line tracking and module-wide scanning.
/// </summary>
/// <remarks>
/// Handles five shapes: <c>Sub</c>, <c>Function</c>, <c>Property Get|Let|Set</c>,
/// and <c>[Public|Private] Declare [PtrSafe] Sub|Function ... Lib "..." [Alias "..."]</c>.
/// Case-insensitive throughout (VBA is). Bodies are NOT parsed — the contract is
/// signature-only per the project's "AI describes behavior" division of labor.
/// </remarks>
public static class VbaSignatureParser
{
    private static readonly Regex DeclareRegex = new(
        @"^(?:(?<modifier>Public|Private)\s+)?Declare\s+(?:(?<ptrsafe>PtrSafe)\s+)?(?<kind>Sub|Function)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+Lib\s+""(?<lib>[^""]*)""(?:\s+Alias\s+""(?<alias>[^""]*)"")?\s*\((?<args>.*)\)(?:\s+As\s+(?<ret>[A-Za-z_][A-Za-z0-9_\[\]\.]*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PropertyRegex = new(
        @"^(?:(?<modifier>Public|Private|Friend|Global)\s+)?(?:(?<static>Static)\s+)?Property\s+(?<kind>Get|Let|Set)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)(?:\s+As\s+(?<ret>[A-Za-z_][A-Za-z0-9_\[\]\.]*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubFunctionRegex = new(
        @"^(?:(?<modifier>Public|Private|Friend|Global)\s+)?(?:(?<static>Static)\s+)?(?<kind>Sub|Function)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)(?:\s+As\s+(?<ret>[A-Za-z_][A-Za-z0-9_\[\]\.]*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParameterRegex = new(
        @"^(?<optional>Optional\s+)?(?:(?<passing>ByVal|ByRef|ParamArray)\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<array>\s*\(\s*\))?(?:\s+As\s+(?<type>[A-Za-z_][A-Za-z0-9_\[\]\.]*))?(?:\s*=\s*(?<default>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Parses a fully-folded signature line. Returns null if no shape matches.</summary>
    public static VbaProcedureInfo? ParseSignature(string logicalLine)
    {
        if (string.IsNullOrWhiteSpace(logicalLine)) return null;
        string line = logicalLine.Trim();

        // Try Declare first (more specific than Sub/Function).
        var dm = DeclareRegex.Match(line);
        if (dm.Success)
        {
            string declKind = NormalizeSubFunction(dm.Groups["kind"].Value);
            return new VbaProcedureInfo
            {
                Name = dm.Groups["name"].Value,
                ProcedureType = "Declare" + declKind,
                AccessModifier = NormalizeModifier(dm.Groups["modifier"]),
                IsStatic = false,
                ReturnType = OptionalGroup(dm.Groups["ret"]),
                Parameters = ParseParameters(dm.Groups["args"].Value),
                Lib = OptionalGroup(dm.Groups["lib"]),
                Alias = OptionalGroup(dm.Groups["alias"]),
            };
        }

        var pm = PropertyRegex.Match(line);
        if (pm.Success)
        {
            return new VbaProcedureInfo
            {
                Name = pm.Groups["name"].Value,
                ProcedureType = "Property" + NormalizePropertyKind(pm.Groups["kind"].Value),
                AccessModifier = NormalizeModifier(pm.Groups["modifier"]),
                IsStatic = pm.Groups["static"].Success,
                ReturnType = OptionalGroup(pm.Groups["ret"]),
                Parameters = ParseParameters(pm.Groups["args"].Value),
            };
        }

        var sm = SubFunctionRegex.Match(line);
        if (sm.Success)
        {
            return new VbaProcedureInfo
            {
                Name = sm.Groups["name"].Value,
                ProcedureType = NormalizeSubFunction(sm.Groups["kind"].Value),
                AccessModifier = NormalizeModifier(sm.Groups["modifier"]),
                IsStatic = sm.Groups["static"].Success,
                ReturnType = OptionalGroup(sm.Groups["ret"]),
                Parameters = ParseParameters(sm.Groups["args"].Value),
            };
        }

        return null;
    }

    /// <summary>Strips line comments (<c>'</c> and a leading <c>Rem </c>) but
    /// honors string literals so <c>"don't"</c> isn't truncated.</summary>
    public static string StripComments(string line)
    {
        bool inString = false;
        // VBA's `Rem` form is a leading-statement comment; check stripped prefix.
        var leading = line.TrimStart();
        if (leading.StartsWith("Rem ", StringComparison.OrdinalIgnoreCase) ||
            leading.Equals("Rem", StringComparison.OrdinalIgnoreCase))
        {
            int idx = line.IndexOf("Rem", StringComparison.OrdinalIgnoreCase);
            return line.Substring(0, idx);
        }
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inString = !inString;
            else if (!inString && line[i] == '\'') return line.Substring(0, i);
        }
        return line;
    }

    private static List<VbaParameter> ParseParameters(string args)
    {
        var list = new List<VbaParameter>();
        if (string.IsNullOrWhiteSpace(args)) return list;

        foreach (var token in SplitArgumentsSafely(args))
        {
            var param = ParseParameter(token);
            if (param != null) list.Add(param);
        }
        return list;
    }

    private static List<string> SplitArgumentsSafely(string args)
    {
        var list = new List<string>();
        int parenLevel = 0;
        bool inString = false;
        int start = 0;

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '"') inString = !inString;
            else if (!inString && c == '(') parenLevel++;
            else if (!inString && c == ')') parenLevel--;
            else if (!inString && parenLevel == 0 && c == ',')
            {
                list.Add(args.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        if (start < args.Length)
        {
            var tail = args.Substring(start).Trim();
            if (tail.Length > 0) list.Add(tail);
        }
        return list;
    }

    private static VbaParameter? ParseParameter(string paramString)
    {
        var match = ParameterRegex.Match(paramString);
        if (!match.Success) return null;

        var p = new VbaParameter
        {
            Name = match.Groups["name"].Value,
            IsOptional = match.Groups["optional"].Success,
            DataType = match.Groups["type"].Success ? match.Groups["type"].Value : "Variant",
            DefaultValue = match.Groups["default"].Success ? match.Groups["default"].Value.Trim() : null,
            IsArray = match.Groups["array"].Success,
        };

        if (match.Groups["passing"].Success)
        {
            string passing = match.Groups["passing"].Value;
            if (passing.Equals("ParamArray", StringComparison.OrdinalIgnoreCase))
            {
                p.IsParamArray = true;
                p.IsArray = true;        // ParamArray is implicitly an array
                p.PassingMechanism = "ByRef";
            }
            else
            {
                p.PassingMechanism = NormalizePassing(passing);
            }
        }

        return p;
    }

    private static string NormalizeModifier(Group g) => !g.Success ? "Public" : g.Value.ToLowerInvariant() switch
    {
        "public" => "Public",
        "private" => "Private",
        "friend" => "Friend",
        "global" => "Global",
        _ => "Public",
    };

    private static string NormalizePassing(string raw) => raw.ToLowerInvariant() switch
    {
        "byval" => "ByVal",
        "byref" => "ByRef",
        _ => "ByRef",
    };

    private static string NormalizeSubFunction(string raw) => raw.ToLowerInvariant() switch
    {
        "sub" => "Sub",
        "function" => "Function",
        _ => raw,
    };

    private static string NormalizePropertyKind(string raw) => raw.ToLowerInvariant() switch
    {
        "get" => "Get",
        "let" => "Let",
        "set" => "Set",
        _ => raw,
    };

    private static string? OptionalGroup(Group g) => g.Success && g.Value.Length > 0 ? g.Value : null;
}
