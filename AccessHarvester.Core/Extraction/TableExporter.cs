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

        var relationsByChild = CollectRelationsByChildTable(db);

        foreach (dynamic td in tableDefs)
        {
            string name = td.Name;
            if (SystemObjectNames.IsSystemObjectName(name)) continue;

            try
            {
                if (!relationsByChild.TryGetValue(name, out List<TableDdlBuilder.DdlForeignKey>? fks))
                    fks = new List<TableDdlBuilder.DdlForeignKey>();
                var payload = BuildTablePayload(name, td, fks);
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

    private static object BuildTablePayload(string name, dynamic td, IReadOnlyList<TableDdlBuilder.DdlForeignKey> foreignKeys)
    {
        var fields = new List<object>();
        var ddlFields = new List<TableDdlBuilder.DdlField>();
        foreach (dynamic f in td.Fields)
        {
            int typeCode = (int)f.Type;
            string fName = (string)f.Name;
            int? fSize = SafeInt(() => (int)f.Size);
            bool fRequired = SafeBool(() => (bool)f.Required) ?? false;
            int? fAttrs = SafeInt(() => (int)f.Attributes);
            bool fAutoInc = fAttrs.HasValue && (fAttrs.Value & 0x10) != 0;
            string? fDefault = SafeString(() => (string)f.DefaultValue);

            fields.Add(new
            {
                Name = fName,
                TypeCode = typeCode,
                TypeName = FieldTypeName(typeCode),
                Size = fSize,
                Required = SafeBool(() => (bool)f.Required),
                AllowZeroLength = SafeBool(() => (bool)f.AllowZeroLength),
                DefaultValue = fDefault,
                ValidationRule = SafeString(() => (string)f.ValidationRule),
                ValidationText = SafeString(() => (string)f.ValidationText),
                InputMask = SafeString(() => (string)f.Properties("InputMask").Value),
                Format = SafeString(() => (string)f.Properties("Format").Value),
                DisplayControl = SafeInt(() => (int)f.Properties("DisplayControl").Value),
                Attributes = fAttrs,
                OrdinalPosition = SafeInt(() => (int)f.OrdinalPosition),
                ForeignName = SafeString(() => (string)f.ForeignName),
            });

            ddlFields.Add(new TableDdlBuilder.DdlField(
                Name: fName,
                TypeCode: typeCode,
                Size: fSize,
                Required: fRequired,
                IsAutoIncrement: fAutoInc,
                DefaultValue: fDefault));
        }

        var indexes = new List<object>();
        TableDdlBuilder.DdlPrimaryKey? primaryKey = null;
        foreach (dynamic idx in td.Indexes)
        {
            var idxFields = new List<object>();
            var pkFieldNames = new List<string>();
            foreach (dynamic ixf in idx.Fields)
            {
                string ixfName = (string)ixf.Name;
                idxFields.Add(new
                {
                    Name = ixfName,
                    Attributes = SafeInt(() => (int)ixf.Attributes),
                });
                pkFieldNames.Add(ixfName);
            }
            bool? isPrimary = SafeBool(() => (bool)idx.Primary);
            string idxName = (string)idx.Name;
            indexes.Add(new
            {
                Name = idxName,
                Primary = isPrimary,
                Unique = SafeBool(() => (bool)idx.Unique),
                Required = SafeBool(() => (bool)idx.Required),
                Fields = idxFields,
            });
            if (primaryKey is null && isPrimary == true && pkFieldNames.Count > 0)
                primaryKey = new TableDdlBuilder.DdlPrimaryKey(idxName, pkFieldNames);
        }

        string ddl = TableDdlBuilder.Build(name, ddlFields, primaryKey, foreignKeys);

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
            Ddl = ddl,
            Fields = fields,
            Indexes = indexes,
        };
    }

    private static Dictionary<string, List<TableDdlBuilder.DdlForeignKey>> CollectRelationsByChildTable(dynamic db)
    {
        // OrdinalIgnoreCase: DAO returns table names from two independent enumerations
        // (TableDefs vs. Relations); Access table names are case-preserving but
        // case-insensitive, so casing drift between the collections must not lose FKs.
        var map = new Dictionary<string, List<TableDdlBuilder.DdlForeignKey>>(StringComparer.OrdinalIgnoreCase);
        dynamic relations;
        try { relations = db.Relations; }
        catch { return map; }

        foreach (dynamic rel in relations)
        {
            try
            {
                int attributes = (int)rel.Attributes;
                // Skip relations marked dbRelationDontEnforce (0x2) — they have no
                // FK clause in the DDL.
                if ((attributes & 0x00000002) != 0) continue;

                string relName = (string)rel.Name;
                string parentTable = (string)rel.Table;
                string childTable = (string)rel.ForeignTable;

                var pairs = new List<(string ChildField, string ParentField)>();
                foreach (dynamic f in rel.Fields)
                    pairs.Add(((string)f.ForeignName, (string)f.Name));

                var fk = new TableDdlBuilder.DdlForeignKey(
                    Name: relName,
                    ParentTable: parentTable,
                    FieldPairs: pairs,
                    CascadeUpdate: (attributes & 0x00000100) != 0,
                    CascadeDelete: (attributes & 0x00001000) != 0);

                if (!map.TryGetValue(childTable, out var list))
                {
                    list = new List<TableDdlBuilder.DdlForeignKey>();
                    map[childTable] = list;
                }
                list.Add(fk);
            }
            catch { /* skip malformed relation */ }
        }
        return map;
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
