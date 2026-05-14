using System.Text.Json;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Extracts Access table schemas via DAO's CurrentDb().TableDefs. Writes one JSON
/// file per table containing structural metadata (fields, indexes). Skips system
/// tables (names starting with "MSys" or "~") and hidden-schema tables that can't
/// be introspected.
/// </summary>
public sealed class TableExporter : IObjectExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static TableExporter Instance { get; } = new();

    private TableExporter() { }

    public string Category => "Tables";

    public IReadOnlyList<ExportOutcome> Export(AccessComSession session, string rawCategoryDir)
    {
        Directory.CreateDirectory(rawCategoryDir);
        var outcomes = new List<ExportOutcome>();

        dynamic db = session.Application.CurrentDb();
        dynamic tableDefs = db.TableDefs;

        foreach (dynamic td in tableDefs)
        {
            string name = td.Name;
            if (name.StartsWith("MSys") || name.StartsWith("~")) continue;

            try
            {
                var payload = BuildTablePayload(name, td);
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

    private static object BuildTablePayload(string name, dynamic td)
    {
        var fields = new List<object>();
        foreach (dynamic f in td.Fields)
        {
            int typeCode = (int)f.Type;
            fields.Add(new
            {
                Name = (string)f.Name,
                TypeCode = typeCode,
                TypeName = FieldTypeName(typeCode),
                Size = SafeInt(() => (int)f.Size),
                Required = SafeBool(() => (bool)f.Required),
                AllowZeroLength = SafeBool(() => (bool)f.AllowZeroLength),
                DefaultValue = SafeString(() => (string)f.DefaultValue),
                ValidationRule = SafeString(() => (string)f.ValidationRule),
                ValidationText = SafeString(() => (string)f.ValidationText),
                InputMask = SafeString(() => (string)f.Properties("InputMask").Value),
                Format = SafeString(() => (string)f.Properties("Format").Value),
                DisplayControl = SafeInt(() => (int)f.Properties("DisplayControl").Value),
                Attributes = SafeInt(() => (int)f.Attributes),
                OrdinalPosition = SafeInt(() => (int)f.OrdinalPosition),
                ForeignName = SafeString(() => (string)f.ForeignName),
            });
        }

        var indexes = new List<object>();
        foreach (dynamic idx in td.Indexes)
        {
            var idxFields = new List<object>();
            foreach (dynamic ixf in idx.Fields)
            {
                idxFields.Add(new
                {
                    Name = (string)ixf.Name,
                    Attributes = SafeInt(() => (int)ixf.Attributes),
                });
            }
            indexes.Add(new
            {
                Name = (string)idx.Name,
                Primary = SafeBool(() => (bool)idx.Primary),
                Unique = SafeBool(() => (bool)idx.Unique),
                Required = SafeBool(() => (bool)idx.Required),
                Fields = idxFields,
            });
        }

        return new
        {
            Name = name,
            Type = "Table",
            Attributes = SafeInt(() => (int)td.Attributes),
            DateCreated = SafeDateTime(() => (DateTime)td.DateCreated),
            LastUpdated = SafeDateTime(() => (DateTime)td.LastUpdated),
            RecordCount = SafeInt(() => (int)td.RecordCount),
            SourceTableName = SafeString(() => (string)td.SourceTableName),
            Connect = SafeString(() => (string)td.Connect),
            ValidationRule = SafeString(() => (string)td.ValidationRule),
            ValidationText = SafeString(() => (string)td.ValidationText),
            Fields = fields,
            Indexes = indexes,
        };
    }

    // DAO dbType* constants for field types.
    private static string FieldTypeName(int code) => code switch
    {
        1 => "Boolean",
        2 => "Byte",
        3 => "Integer",
        4 => "Long",
        5 => "Currency",
        6 => "Single",
        7 => "Double",
        8 => "DateTime",
        9 => "Binary",
        10 => "Text",
        11 => "LongBinary",
        12 => "Memo",
        15 => "GUID",
        16 => "BigInt",
        17 => "VarBinary",
        18 => "Char",
        19 => "Numeric",
        20 => "Decimal",
        21 => "Float",
        22 => "Time",
        23 => "TimeStamp",
        101 => "AttachmentField",
        102 => "ComplexByte",
        103 => "ComplexInteger",
        104 => "ComplexLong",
        105 => "ComplexSingle",
        106 => "ComplexDouble",
        107 => "ComplexGUID",
        108 => "ComplexDecimal",
        109 => "ComplexText",
        _ => $"Unknown({code})"
    };

    private static string? SafeString(Func<string> get) { try { return get(); } catch { return null; } }
    private static bool? SafeBool(Func<bool> get) { try { return get(); } catch { return null; } }
    private static int? SafeInt(Func<int> get) { try { return get(); } catch { return null; } }
    private static DateTime? SafeDateTime(Func<DateTime> get) { try { return get(); } catch { return null; } }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
