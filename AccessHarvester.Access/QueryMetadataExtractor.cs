using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessHarvester.Access;

/// <summary>
/// Pure-logic transform from the raw <c>kb/Queries/&lt;Name&gt;.json</c> JSON
/// emitted by <c>AccessHarvester.Core.Extraction.QueryExporter</c> (passed
/// through unchanged by <c>JsonPassthroughTranslator</c>) into a typed
/// <see cref="QueryMetadata"/> record. No COM, no Access, no I/O — accepts a
/// <see cref="JsonElement"/> or a JSON string. Hermetically unit-testable.
/// </summary>
/// <remarks>
/// This extractor is the typed-metadata counterpart to the form / report
/// extractor authored in Plan Phase 4.4 wiring. It performs three jobs:
/// <list type="number">
///   <item>Lift exact-value keys (<c>Sql</c>, <c>Connect</c>,
///   <c>ReturnsRecords</c>, <c>OdbcTimeout</c>) into typed fields and remove
///   them from the raw bag.</item>
///   <item>Lift the decoded <c>QueryTypeName</c> into <see cref="QueryMetadata.QueryType"/>
///   while leaving the raw <c>QueryTypeCode</c> in the bag for round-trip
///   fidelity.</item>
///   <item>Detect parameters via <see cref="ParseParameters"/>: declared
///   PARAMETERS clauses and implicit <c>[Forms]![X]</c> / <c>[Reports]![X]</c>
///   references in the SQL text.</item>
/// </list>
/// Parameter detection is a regex-only shallow scan, deliberately so — true
/// SQL parsing is the <c>jet-sql-grammar-and-parser</c> set's deliverable.
/// </remarks>
public static class QueryMetadataExtractor
{
    private static readonly HashSet<string> PromotedExactValueKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",          // promoted to QueryMetadata.Name
        "Type",          // discriminator from the raw exporter; the C# type carries it
        "QueryTypeName", // promoted to QueryMetadata.QueryType (decoded lift)
        "Sql",
        "Connect",
        "ReturnsRecords",
        "OdbcTimeout",
    };

    public static QueryMetadata Extract(string rawJson)
    {
        if (rawJson is null) throw new ArgumentNullException(nameof(rawJson));
        using var doc = JsonDocument.Parse(rawJson);
        return Extract(doc.RootElement);
    }

    public static QueryMetadata Extract(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"Expected JSON object at the root of a Query payload but got {root.ValueKind}.",
                nameof(root));

        string name = ReadString(root, "Name")
            ?? throw new ArgumentException("Query payload is missing the required 'Name' field.", nameof(root));

        string? sql = ReadString(root, "Sql");

        return new QueryMetadata
        {
            Name = name,
            QueryType = ReadString(root, "QueryTypeName"),
            Sql = sql,
            Parameters = ParseParameters(sql),
            ReturnsRecords = ReadBool(root, "ReturnsRecords"),
            Connect = ReadString(root, "Connect"),
            OdbcTimeout = ReadInt(root, "OdbcTimeout"),
            Properties = BuildPropertiesBag(root),
        };
    }

    /// <summary>
    /// Detect parameters in <paramref name="sql"/>. Returns null when
    /// <paramref name="sql"/> is null / empty or no parameters are detected.
    /// </summary>
    /// <remarks>
    /// Two passes:
    /// <list type="number">
    ///   <item>If the SQL begins with a <c>PARAMETERS</c> clause (case-
    ///   insensitive, optional leading whitespace), parse each comma-separated
    ///   <c>&lt;name&gt; &lt;type&gt;</c> entry up to the terminating
    ///   semicolon. Bracketed names with internal spaces / punctuation
    ///   are supported.</item>
    ///   <item>Scan the full SQL (including the PARAMETERS clause if present
    ///   — DAO does this too) for <c>[Forms]![X]</c> /
    ///   <c>[Forms]![X]![Y]</c> / <c>[Reports]!...</c> patterns and emit one
    ///   parameter per distinct path.</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<QueryParameter>? ParseParameters(string? sql)
    {
        if (string.IsNullOrEmpty(sql)) return null;

        var parameters = new List<QueryParameter>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Declared PARAMETERS clause.
        var paramsMatch = ParametersClauseRegex.Match(sql);
        if (paramsMatch.Success)
        {
            foreach (var p in ParseParametersClause(paramsMatch.Groups[1].Value))
                parameters.Add(p);
        }

        // 2) Implicit [Forms]! / [Reports]! references anywhere in the SQL.
        foreach (Match m in FormReferenceRegex.Matches(sql))
        {
            string verbatim = m.Value;
            if (!seenPaths.Add(verbatim)) continue;
            parameters.Add(BuildReferenceParameter(verbatim, m, isForm: true));
        }
        foreach (Match m in ReportReferenceRegex.Matches(sql))
        {
            string verbatim = m.Value;
            if (!seenPaths.Add(verbatim)) continue;
            parameters.Add(BuildReferenceParameter(verbatim, m, isForm: false));
        }

        return parameters.Count > 0 ? parameters : null;
    }

    private static IEnumerable<QueryParameter> ParseParametersClause(string clauseBody)
    {
        // Split on commas at the top level. Bracketed identifiers in JET don't
        // contain unescaped commas, so a plain split is sufficient.
        foreach (var raw in clauseBody.Split(','))
        {
            string trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;

            string? name;
            string? dataType;

            if (trimmed.StartsWith('['))
            {
                int closeBracket = trimmed.IndexOf(']');
                if (closeBracket < 0)
                {
                    name = trimmed.TrimStart('[').Trim();
                    dataType = null;
                }
                else
                {
                    name = trimmed.Substring(1, closeBracket - 1);
                    string remainder = trimmed[(closeBracket + 1)..].Trim();
                    dataType = remainder.Length > 0 ? remainder : null;
                }
            }
            else
            {
                int firstSpace = trimmed.IndexOf(' ');
                if (firstSpace < 0)
                {
                    name = trimmed;
                    dataType = null;
                }
                else
                {
                    name = trimmed[..firstSpace].Trim();
                    dataType = trimmed[(firstSpace + 1)..].Trim();
                    if (dataType.Length == 0) dataType = null;
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                yield return new QueryParameter
                {
                    Name = name,
                    Source = "declared",
                    DataType = dataType,
                };
            }
        }
    }

    private static QueryParameter BuildReferenceParameter(string verbatim, Match m, bool isForm)
    {
        string? leaf = m.Groups[2].Success ? m.Groups[2].Value : (m.Groups[1].Success ? m.Groups[1].Value : null);
        return new QueryParameter
        {
            Name = leaf ?? verbatim,
            Source = isForm ? "form-reference" : "report-reference",
            Path = verbatim,
        };
    }

    private static IReadOnlyDictionary<string, JsonElement>? BuildPropertiesBag(JsonElement root)
    {
        var bag = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
        {
            if (PromotedExactValueKeys.Contains(prop.Name)) continue;
            // Clone the JsonElement so it survives disposal of the source JsonDocument.
            bag[prop.Name] = prop.Value.Clone();
        }
        return bag.Count > 0 ? bag : null;
    }

    private static string? ReadString(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static bool? ReadBool(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static int? ReadInt(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : null;
    }

    private static readonly RegexOptions DefaultRegex =
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>Matches a leading <c>PARAMETERS &lt;body&gt;;</c> clause.</summary>
    private static readonly Regex ParametersClauseRegex = new(
        @"\A\s*PARAMETERS\s+(.*?);",
        DefaultRegex | RegexOptions.Singleline);

    /// <summary>Matches <c>[Forms]![X]</c> or <c>[Forms]![X]![Y]</c>.</summary>
    private static readonly Regex FormReferenceRegex = new(
        @"\[Forms\]!\[(?<form>[^\]]+)\](?:!\[(?<ctl>[^\]]+)\])?",
        DefaultRegex);

    /// <summary>Matches <c>[Reports]![X]</c> or <c>[Reports]![X]![Y]</c>.</summary>
    private static readonly Regex ReportReferenceRegex = new(
        @"\[Reports\]!\[(?<report>[^\]]+)\](?:!\[(?<ctl>[^\]]+)\])?",
        DefaultRegex);
}
