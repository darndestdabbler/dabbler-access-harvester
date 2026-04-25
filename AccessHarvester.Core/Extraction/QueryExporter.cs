using System.Text.Json;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Extracts Access queries via DAO's CurrentDb().QueryDefs. Writes one JSON file per
/// query containing Name, QueryType, SQL, and a handful of other first-class
/// properties. Skips system/temp queries (names beginning with "~").
/// </summary>
public sealed class QueryExporter : IObjectExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static QueryExporter Instance { get; } = new();

    private QueryExporter() { }

    public string Category => "Queries";

    public IReadOnlyList<ExportOutcome> Export(AccessComSession session, string rawCategoryDir)
    {
        Directory.CreateDirectory(rawCategoryDir);
        var outcomes = new List<ExportOutcome>();

        dynamic db = session.Application.CurrentDb();
        dynamic queryDefs = db.QueryDefs;

        foreach (dynamic qd in queryDefs)
        {
            string name = qd.Name;
            if (name.StartsWith("~")) continue;

            try
            {
                int queryTypeCode = (int)qd.Type;
                var payload = new
                {
                    Name = name,
                    Type = "Query",
                    QueryTypeCode = queryTypeCode,
                    QueryTypeName = QueryTypeName(queryTypeCode),
                    Sql = SafeString(() => (string)qd.SQL),
                    Connect = SafeString(() => (string)qd.Connect),
                    ReturnsRecords = SafeBool(() => (bool)qd.ReturnsRecords),
                    OdbcTimeout = SafeInt(() => (int)qd.ODBCTimeout),
                };
                string outFile = Path.Combine(rawCategoryDir, SanitizeFileName(name) + ".json");
                File.WriteAllText(outFile, JsonSerializer.Serialize(payload, JsonOptions));
                outcomes.Add(new ExportOutcome(name, true));
            }
            catch (Exception ex)
            {
                outcomes.Add(new ExportOutcome(name, false, ex.Message));
            }
        }

        return outcomes;
    }

    // DAO dbQueryType enumeration.
    private static string QueryTypeName(int code) => code switch
    {
        0 => "Select",
        16 => "MakeTable",
        32 => "Append",
        48 => "Update",
        64 => "Delete",
        80 => "Union",
        96 => "Xbase",
        112 => "CrossTab",
        128 => "DataDefinition",
        144 => "SqlPassthrough",
        160 => "SetOperation",
        176 => "SpMakeTable",
        192 => "SpAppend",
        208 => "SpUpdate",
        224 => "SpDelete",
        _ => $"Unknown({code})"
    };

    private static string? SafeString(Func<string> get)
    {
        try { return get(); } catch { return null; }
    }
    private static bool? SafeBool(Func<bool> get)
    {
        try { return get(); } catch { return null; }
    }
    private static int? SafeInt(Func<int> get)
    {
        try { return get(); } catch { return null; }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
