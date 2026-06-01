using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using AccessHarvester.Pipeline.Classification;

namespace AccessHarvester.Core.Com;

/// <summary>
/// Reads an Access database's structural metadata by combining OLE DB
/// schema rowsets (TABLES / COLUMNS / PRIMARY_KEYS / FOREIGN_KEYS) with
/// DAO interop calls that recover the bits OLE DB hides — the AutoNumber
/// attribute and the implicit FK encoded by an Access "Lookup" combo-box
/// <c>RowSource</c> property (plan § 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>ACE provider bitness.</b> The Microsoft.ACE.OLEDB.16.0 / .12.0
/// providers must match the process bitness. The harvester ships as x64
/// and requires the 64-bit ACE redistributable on the host. The reader
/// tries the 16.0 provider first and falls back to 12.0; if both are
/// missing the constructor surfaces a clear error rather than a generic
/// COM <c>0x80004005</c>.
/// </para>
/// <para>
/// <b>DAO interop.</b> DAO is reached through the in-process Access
/// instance (<see cref="AccessComSession"/>) rather than via a direct
/// <c>DAO.DBEngine</c> activation — keeping the COM activation count
/// down. The session's <c>CurrentDb</c> exposes the
/// <c>TableDefs</c>/<c>Fields</c>/<c>Properties</c> chain we need. Every
/// RCW we acquire (outer collections and inner items alike) is released
/// in a paired <c>finally</c> block so a partial-failure exception path
/// cannot leave live COM references behind.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class OleDbDaoSchemaReader : ISchemaReader, IDegradationReporter
{
    public RawSchema Read(string databasePath)
        => ReadCore(databasePath, sink: null);

    public RawSchema ReadWithDegradations(
        string databasePath, IList<ClassificationDegradation> sink)
        => ReadCore(databasePath, sink);

    public IReadOnlyList<ColumnStatistics> SampleWithDegradations(
        string databasePath, RawSchema schema, ScanConfig config,
        IList<ClassificationDegradation> sink)
        => throw new NotSupportedException(
            "OleDbDaoSchemaReader does not implement value sampling; use OleDbValueSampler.");

    private static RawSchema ReadCore(string databasePath, IList<ClassificationDegradation>? sink)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"Database not found: {databasePath}");

        string connectionString = BuildConnectionString(databasePath);

        // First pass: OLE DB schema rowsets give us tables, columns, PKs, FKs
        // for the non-system relational surface.
        IReadOnlyList<OleDbTableRow>   tableRows;
        IReadOnlyList<OleDbColumnRow>  columnRows;
        IReadOnlyList<OleDbPkColumn>   primaryKeys;
        IReadOnlyList<OleDbFkColumn>   foreignKeys;
        using (var conn = new OleDbConnection(connectionString))
        {
            conn.Open();
            tableRows   = ReadTables(conn);
            columnRows  = ReadColumns(conn);
            primaryKeys = ReadPrimaryKeys(conn);
            foreignKeys = ReadForeignKeys(conn);
        }

        // Second pass: DAO via Access COM session to recover the
        // AutoNumber attribute and the implicit-FK Lookup RowSource.
        var daoAugmentations = new Dictionary<(string Table, string Column), DaoColumnAugmentation>();
        var linkedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadDaoAugmentations(databasePath, daoAugmentations, linkedTableNames, sink);

        // Merge into RawSchema. Resolve composite-FK detection: an OLE DB
        // foreign-key relationship that spans multiple columns shows up as
        // multiple OleDbFkColumn rows with the same constraint key. The
        // single-column FK model accepts each row individually but emits a
        // degradation so the operator knows v1.0 cannot represent the
        // multi-column relationship authoritatively.
        var fkByTableColumn = new Dictionary<(string Table, string Column), RawFkTarget>();
        DetectCompositeFks(foreignKeys, fkByTableColumn, sink);

        // Layer in the implicit DAO RowSource-derived FKs (only when OLE DB
        // did not already declare one for that column — declared
        // relationships win).
        var pkByTable = primaryKeys
            .GroupBy(p => p.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p.ColumnName).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var aug in daoAugmentations)
        {
            if (aug.Value.ImplicitFkRowSource is null) continue;
            if (fkByTableColumn.ContainsKey(aug.Key)) continue;

            var resolved = ResolveImplicitFk(aug.Value.ImplicitFkRowSource, pkByTable, sink, aug.Key);
            if (resolved is not null)
                fkByTableColumn[aug.Key] = resolved;
        }

        var pkSetByTable = pkByTable.ToDictionary(
            kv => kv.Key,
            kv => new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        var columnsByTable = columnRows
            .GroupBy(c => c.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.OrdinalPosition).ToList(), StringComparer.OrdinalIgnoreCase);

        var tables = new List<RawTable>(tableRows.Count);
        foreach (var t in tableRows)
        {
            if (!columnsByTable.TryGetValue(t.Name, out var cols)) cols = new List<OleDbColumnRow>();
            var pkSet = pkSetByTable.GetValueOrDefault(t.Name) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool linked = t.IsLinked || linkedTableNames.Contains(t.Name);

            var rawCols = new List<RawColumn>(cols.Count);
            foreach (var c in cols)
            {
                var aug = daoAugmentations.TryGetValue((t.Name, c.ColumnName), out var a) ? a : null;
                bool isAuto      = aug?.IsAutoNumber == true;
                bool isMemo      = c.IsMemo;
                bool isOle       = c.IsOleObject;
                bool isMultiVal  = aug?.IsMultiValue == true;
                bool isRequired  = c.IsRequired ?? (aug?.IsRequired ?? false);

                bool isPk = pkSet.Contains(c.ColumnName);
                fkByTableColumn.TryGetValue((t.Name, c.ColumnName), out var fk);

                rawCols.Add(new RawColumn(
                    Name: c.ColumnName,
                    OrdinalPosition: c.OrdinalPosition,
                    DataType: c.DataTypeName,
                    IsAutoNumber: isAuto,
                    IsPrimaryKey: isPk,
                    IsForeignKey: fk is not null,
                    FkTarget: fk,
                    IsMemo: isMemo,
                    IsOleObject: isOle,
                    IsMultiValue: isMultiVal,
                    IsRequired: isRequired));
            }

            tables.Add(new RawTable(t.Name, linked, rawCols));
        }

        return new RawSchema(
            DatabaseName: Path.GetFileName(databasePath),
            Tables: tables);
    }

    // ───────────────────────────────────────────── OLE DB rowsets

    private static IReadOnlyList<OleDbTableRow> ReadTables(OleDbConnection conn)
    {
        DataTable? schema = conn.GetOleDbSchemaTable(
            OleDbSchemaGuid.Tables,
            new object?[] { null, null, null, "TABLE" });
        if (schema is null) return Array.Empty<OleDbTableRow>();

        var rows = new List<OleDbTableRow>();
        foreach (DataRow row in schema.Rows)
        {
            string? name = row["TABLE_NAME"] as string;
            if (string.IsNullOrEmpty(name)) continue;
            if (name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.StartsWith("~",    StringComparison.Ordinal))           continue; // temp/system
            rows.Add(new OleDbTableRow(name, IsLinked: false));
        }

        // LINK tables come back under a different TABLE_TYPE.
        DataTable? linkSchema = conn.GetOleDbSchemaTable(
            OleDbSchemaGuid.Tables,
            new object?[] { null, null, null, "LINK" });
        if (linkSchema is not null)
        {
            foreach (DataRow row in linkSchema.Rows)
            {
                string? name = row["TABLE_NAME"] as string;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)) continue;
                rows.Add(new OleDbTableRow(name, IsLinked: true));
            }
        }

        return rows;
    }

    private static IReadOnlyList<OleDbColumnRow> ReadColumns(OleDbConnection conn)
    {
        DataTable? schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, null);
        if (schema is null) return Array.Empty<OleDbColumnRow>();

        var rows = new List<OleDbColumnRow>();
        foreach (DataRow row in schema.Rows)
        {
            string? tableName  = row["TABLE_NAME"]  as string;
            string? columnName = row["COLUMN_NAME"] as string;
            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(columnName)) continue;
            if (tableName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)) continue;

            int ordinal = row["ORDINAL_POSITION"] is int o ? o
                        : row["ORDINAL_POSITION"] is long ol ? (int)ol
                        : 0;
            // ORDINAL_POSITION is 1-based in OLE DB; normalize to 0-based.
            if (ordinal > 0) ordinal -= 1;

            int dataTypeCode = row["DATA_TYPE"] is int dt ? dt
                             : row["DATA_TYPE"] is short ds ? ds
                             : 0;
            string typeName = MapOleDbType(dataTypeCode);
            bool isMemo  = dataTypeCode == 130 && row["CHARACTER_MAXIMUM_LENGTH"] is int n && n > 4000; // wcharLongVar
            isMemo      |= dataTypeCode == 203; // DBTYPE_WLONGVARCHAR (LongText)
            bool isOle   = dataTypeCode == 205; // DBTYPE_LONGVARBINARY (OLE Object)
            bool? required = row["IS_NULLABLE"] is bool b ? !b : (bool?)null;

            rows.Add(new OleDbColumnRow(
                TableName: tableName,
                ColumnName: columnName,
                OrdinalPosition: ordinal,
                DataTypeName: typeName,
                IsMemo: isMemo,
                IsOleObject: isOle,
                IsRequired: required));
        }
        return rows;
    }

    private static IReadOnlyList<OleDbPkColumn> ReadPrimaryKeys(OleDbConnection conn)
    {
        DataTable? schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, null);
        if (schema is null) return Array.Empty<OleDbPkColumn>();

        var rows = new List<OleDbPkColumn>();
        foreach (DataRow row in schema.Rows)
        {
            string? tableName  = row["TABLE_NAME"]  as string;
            string? columnName = row["COLUMN_NAME"] as string;
            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(columnName)) continue;
            rows.Add(new OleDbPkColumn(tableName, columnName));
        }
        return rows;
    }

    private static IReadOnlyList<OleDbFkColumn> ReadForeignKeys(OleDbConnection conn)
    {
        DataTable? schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, null);
        if (schema is null) return Array.Empty<OleDbFkColumn>();

        var rows = new List<OleDbFkColumn>();
        foreach (DataRow row in schema.Rows)
        {
            string? fkTable  = row["FK_TABLE_NAME"]  as string;
            string? fkColumn = row["FK_COLUMN_NAME"] as string;
            string? pkTable  = row["PK_TABLE_NAME"]  as string;
            string? pkColumn = row["PK_COLUMN_NAME"] as string;
            string? fkName   = row.Table.Columns.Contains("FK_NAME") ? row["FK_NAME"] as string : null;
            if (string.IsNullOrEmpty(fkTable)  || string.IsNullOrEmpty(fkColumn)
             || string.IsNullOrEmpty(pkTable)  || string.IsNullOrEmpty(pkColumn)) continue;
            rows.Add(new OleDbFkColumn(fkTable, fkColumn, pkTable, pkColumn, fkName));
        }
        return rows;
    }

    /// <summary>
    /// A multi-row constraint (same FK_NAME across N FK_COLUMN_NAME rows) is
    /// a composite foreign key. v1.0 of the classification schema does not
    /// model composite FKs (one Column entry per source column, each
    /// carrying its own FkTarget). We keep the individual rows so the
    /// per-column FK fact stays accurate, but surface a degradation so the
    /// downstream sanitizer's remap-in-lockstep logic does not silently
    /// treat a composite relationship as N independent ones.
    /// </summary>
    /// <remarks>
    /// The detector ONLY fires when <c>FK_NAME</c> is populated by the OLE DB
    /// provider — that is the only stable constraint identifier. A
    /// fallback that grouped by source-table → target-table pair would
    /// conflate two independent single-column FKs from the same source
    /// to the same target (e.g. <c>Order.BillingAddressID</c> and
    /// <c>Order.ShippingAddressID</c> both → <c>Address.AddressID</c>)
    /// into a false composite-FK degradation. When <c>FK_NAME</c> is
    /// absent the detector skips degradation emission and keeps the
    /// per-column FK facts.
    /// </remarks>
    private static void DetectCompositeFks(
        IReadOnlyList<OleDbFkColumn> fks,
        Dictionary<(string Table, string Column), RawFkTarget> sink,
        IList<ClassificationDegradation>? degradations)
    {
        // Always record the per-column FK targets, whether or not we have a
        // stable constraint identifier.
        foreach (var fk in fks)
            sink[(fk.FkTableName, fk.FkColumnName)] = new RawFkTarget(fk.PkTableName, fk.PkColumnName);

        // Composite detection requires a real FK_NAME — without it we
        // cannot distinguish a composite constraint from two parallel
        // single-column constraints.
        if (degradations is null) return;
        var withNames = fks.Where(f => !string.IsNullOrEmpty(f.ConstraintName));
        var grouped   = withNames.GroupBy(f => f.ConstraintName!, StringComparer.OrdinalIgnoreCase);
        foreach (var group in grouped)
        {
            var items = group.ToList();
            if (items.Count <= 1) continue;
            degradations.Add(new ClassificationDegradation(
                Kind: DegradationKind.CompositeFkUnsupported,
                Location: items[0].FkTableName,
                Detail: $"Composite foreign key '{group.Key}' " +
                        $"({items.Count} columns -> {items[0].PkTableName}) " +
                        $"represented as {items.Count} single-column FKs; schema v1.0 does not model composite FKs."));
        }
    }

    /// <summary>
    /// Resolve a combo-box <c>RowSource</c> string into an authoritative
    /// FK target column. We only accept resolutions where we can pick a
    /// real target column from <paramref name="pkByTable"/>; otherwise we
    /// emit a degradation and return <c>null</c> so the caller doesn't
    /// invent a non-existent column name. (Earlier the implementation
    /// would emit <c>fkTarget.column = "*"</c> as a placeholder — that
    /// poisoned downstream consumers because the bogus column name still
    /// validates structurally but means nothing to the sanitizer.)
    /// </summary>
    private static RawFkTarget? ResolveImplicitFk(
        string rowSource,
        IDictionary<string, List<string>> pkByTable,
        IList<ClassificationDegradation>? degradations,
        (string Table, string Column) sourceKey)
    {
        string s = rowSource.Trim();
        if (s.StartsWith('[') && s.EndsWith(']')) s = s[1..^1];

        string? namedTarget = null;
        string? namedColumn = null;

        // Simple table name → target the table's PK if it's single-column.
        if (!s.Contains(' ', StringComparison.Ordinal)
            && !s.Contains(';', StringComparison.Ordinal)
            && !s.Contains('(', StringComparison.Ordinal))
        {
            namedTarget = s;
        }
        else
        {
            // SELECT col [, ...] FROM TableName [;]
            var m = Regex.Match(
                s,
                @"^\s*SELECT\s+(?<col>\[?[A-Za-z_][A-Za-z0-9_]*\]?)(?:\s*,[^;]*)?\s+FROM\s+(?<tbl>\[?[A-Za-z_][A-Za-z0-9_]*\]?)\s*;?\s*$",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                namedTarget = m.Groups["tbl"].Value.Trim('[', ']');
                namedColumn = m.Groups["col"].Value.Trim('[', ']');
            }
        }

        if (namedTarget is null)
        {
            degradations?.Add(new ClassificationDegradation(
                Kind: DegradationKind.UnresolvedImplicitFk,
                Location: $"{sourceKey.Table}.{sourceKey.Column}",
                Detail: $"Combo-box RowSource '{rowSource}' is too complex to resolve to a single FK target."));
            return null;
        }

        if (!pkByTable.TryGetValue(namedTarget, out var pkCols) || pkCols.Count == 0)
        {
            degradations?.Add(new ClassificationDegradation(
                Kind: DegradationKind.UnresolvedImplicitFk,
                Location: $"{sourceKey.Table}.{sourceKey.Column}",
                Detail: $"Combo-box RowSource targets table '{namedTarget}' but it has no declared primary key."));
            return null;
        }
        if (pkCols.Count > 1)
        {
            degradations?.Add(new ClassificationDegradation(
                Kind: DegradationKind.UnresolvedImplicitFk,
                Location: $"{sourceKey.Table}.{sourceKey.Column}",
                Detail: $"Combo-box RowSource targets table '{namedTarget}' but its primary key is composite; cannot represent in schema v1.0."));
            return null;
        }

        // Prefer the column the RowSource named explicitly when it matches a
        // real column in the target table's PK; otherwise fall back to the
        // single PK column.
        string targetCol = namedColumn ?? pkCols[0];
        if (namedColumn is not null
            && !string.Equals(namedColumn, pkCols[0], StringComparison.OrdinalIgnoreCase))
            targetCol = pkCols[0]; // PK is authoritative; the SELECT projection might pick a display column

        return new RawFkTarget(namedTarget, targetCol);
    }

    // ───────────────────────────────────────────── DAO augmentations

    private sealed class DaoColumnAugmentation
    {
        public bool IsAutoNumber { get; set; }
        public bool IsMultiValue { get; set; }
        public bool IsRequired   { get; set; }
        /// <summary>The raw RowSource string the resolver decides what to do with later. Kept here so the resolver can run after PK metadata is consolidated.</summary>
        public string? ImplicitFkRowSource { get; set; }
    }

    private static void ReadDaoAugmentations(
        string databasePath,
        Dictionary<(string Table, string Column), DaoColumnAugmentation> augmentations,
        HashSet<string> linkedTableNames,
        IList<ClassificationDegradation>? degradations)
    {
        const int dbAutoIncrField = 0x00000010;
        const int dbAttachedTable = 0x40000000;
        const int dbAttachedODBC  = unchecked((int)0x20000000);
        const int dbComplexField  = unchecked((int)0xC00); // 0x400 | 0x800 (multi-value)

        using var session = new AccessComSession(databasePath);
        dynamic app = session.Application;
        object? currentDbObj  = null;
        object? tableDefsObj  = null;
        try
        {
            currentDbObj = app.CurrentDb();
            dynamic currentDb = currentDbObj!;
            tableDefsObj = currentDb.TableDefs;
            dynamic tableDefs = tableDefsObj!;

            int count = (int)tableDefs.Count;
            for (int i = 0; i < count; i++)
            {
                object? td = null;
                try
                {
                    td = tableDefs[i];
                    dynamic table = td!;
                    string tableName = (string)table.Name;
                    if (tableName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)) continue;
                    if (tableName.StartsWith("~",    StringComparison.Ordinal))           continue;

                    int tableAttrs = (int)table.Attributes;
                    bool isLinked = (tableAttrs & (dbAttachedTable | dbAttachedODBC)) != 0;
                    if (isLinked) linkedTableNames.Add(tableName);

                    object? fieldsObj = null;
                    try
                    {
                        fieldsObj = table.Fields;
                        dynamic fields = fieldsObj!;
                        int fCount = (int)fields.Count;
                        for (int f = 0; f < fCount; f++)
                        {
                            object? fd = null;
                            try
                            {
                                fd = fields[f];
                                dynamic field = fd!;
                                string colName = (string)field.Name;
                                int fieldAttrs = (int)field.Attributes;

                                var aug = new DaoColumnAugmentation
                                {
                                    IsAutoNumber       = (fieldAttrs & dbAutoIncrField) != 0,
                                    IsMultiValue       = (fieldAttrs & dbComplexField) != 0,
                                    IsRequired         = TryReadBoolProperty(field, "Required") ?? false,
                                    ImplicitFkRowSource = TryReadStringProperty(field, "RowSource"),
                                };
                                augmentations[(tableName, colName)] = aug;
                            }
                            catch (Exception ex)
                            {
                                degradations?.Add(new ClassificationDegradation(
                                    Kind: DegradationKind.SchemaReadError,
                                    Location: $"{tableName}.<field#{f}>",
                                    Detail: $"DAO field-read failure: {ex.Message}"));
                            }
                            finally
                            {
                                ReleaseRcw(fd);
                            }
                        }
                    }
                    finally
                    {
                        ReleaseRcw(fieldsObj);
                    }
                }
                catch (Exception ex)
                {
                    degradations?.Add(new ClassificationDegradation(
                        Kind: DegradationKind.SchemaReadError,
                        Location: $"<TableDef#{i}>",
                        Detail: $"DAO TableDef-read failure: {ex.Message}"));
                }
                finally
                {
                    ReleaseRcw(td);
                }
            }
        }
        finally
        {
            ReleaseRcw(tableDefsObj);
            ReleaseRcw(currentDbObj);
        }
    }

    private static void ReleaseRcw(object? rcw)
    {
        if (rcw is null) return;
        try { Marshal.ReleaseComObject(rcw); } catch { /* RCW already released */ }
    }

    /// <summary>
    /// HRESULTs returned by DAO when a property is genuinely absent from a
    /// <c>Properties</c> collection. Any OTHER <see cref="COMException"/>
    /// HRESULT is a real reader fault and is rethrown so the surrounding
    /// per-field catch can record a <see cref="DegradationKind.SchemaReadError"/>.
    /// </summary>
    /// <remarks>
    /// 0x800A0CC1 = 3265 = DAO error "Item not found in this collection."
    /// 0x800A0CB3 = 3251 = "Operation is not supported for this type of object,"
    /// which DAO sometimes returns when probing properties on a field whose
    /// type has no notion of that property (e.g. <c>RowSource</c> on a
    /// plain Long column).
    /// 0x800A0CC6 = 3270 = DAO error "Property not found." DAO returns this
    /// (rather than 3265) when the field object exists but the named property
    /// is simply not present in its <c>Properties</c> collection — the common
    /// case for probing <c>Required</c> / <c>RowSource</c> on a field that
    /// never had them set. Observed on every field of the rebuilt
    /// <c>HomeHealthCare.accdb</c> fixture (93/93), which had been recording
    /// each as a spurious <see cref="DegradationKind.SchemaReadError"/> and
    /// forcing exit code 3; see
    /// docs/session-sets/harvester-fix-classify-timeout/root-cause-findings.md (E5).
    /// </remarks>
    private static readonly HashSet<int> DaoPropertyAbsentHResults = new()
    {
        unchecked((int)0x800A0CC1),
        unchecked((int)0x800A0CB3),
        unchecked((int)0x800A0CC6),
    };

    /// <summary>
    /// True when <paramref name="hresult"/> is a DAO HRESULT that means
    /// "this property does not exist on this field" rather than a genuine
    /// reader fault. This is the exact predicate the per-property
    /// <c>catch … when</c> filters use, exposed (via InternalsVisibleTo) so the
    /// absent-property HRESULT mapping can be asserted hermetically without a
    /// live Access/COM instance.
    /// </summary>
    internal static bool IsDaoPropertyAbsentHResult(int hresult)
        => DaoPropertyAbsentHResults.Contains(hresult);

    /// <summary>
    /// Reads a typed DAO property. Catches only the specific DAO HRESULTs
    /// that mean "this property does not exist on this field"; every
    /// other <see cref="COMException"/> bubbles so the per-field catch can
    /// record a <see cref="DegradationKind.SchemaReadError"/>. Other
    /// exception types (e.g. <see cref="InvalidCastException"/> on a typed
    /// conversion) likewise surface.
    /// </summary>
    private static bool? TryReadBoolProperty(dynamic field, string propName)
    {
        object? propsObj = null;
        object? propObj  = null;
        try
        {
            propsObj = field.Properties;
            dynamic props = propsObj!;
            propObj = props[propName];
            dynamic prop = propObj!;
            return (bool)prop.Value;
        }
        catch (COMException ex) when (IsDaoPropertyAbsentHResult(ex.HResult))
        {
            return null;
        }
        finally
        {
            ReleaseRcw(propObj);
            ReleaseRcw(propsObj);
        }
    }

    private static string? TryReadStringProperty(dynamic field, string propName)
    {
        object? propsObj = null;
        object? propObj  = null;
        try
        {
            propsObj = field.Properties;
            dynamic props = propsObj!;
            propObj = props[propName];
            dynamic prop = propObj!;
            object? v = prop.Value;
            return v?.ToString();
        }
        catch (COMException ex) when (IsDaoPropertyAbsentHResult(ex.HResult))
        {
            return null;
        }
        finally
        {
            ReleaseRcw(propObj);
            ReleaseRcw(propsObj);
        }
    }

    // ───────────────────────────────────────────── connection / types

    private static string BuildConnectionString(string path)
    {
        // Try the 16.0 ACE provider first; fall back to 12.0 if unavailable.
        string[] providers = { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" };
        Exception? lastError = null;
        foreach (var provider in providers)
        {
            string cs = $"Provider={provider};Data Source={path};Persist Security Info=False;";
            try
            {
                using var probe = new OleDbConnection(cs);
                probe.Open();
                return cs;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }
        throw new InvalidOperationException(
            "No working Microsoft.ACE.OLEDB provider was found. Install the Microsoft Access Database Engine Redistributable (64-bit) that matches the harvester process bitness. " +
            $"Last error: {lastError?.Message}");
    }

    /// <summary>Maps a DBTYPE_* code to a display-friendly DAO-style name.</summary>
    private static string MapOleDbType(int dataTypeCode) => dataTypeCode switch
    {
        2   => "Integer",       // DBTYPE_I2
        3   => "Long",          // DBTYPE_I4
        4   => "Single",        // DBTYPE_R4
        5   => "Double",        // DBTYPE_R8
        6   => "Currency",      // DBTYPE_CY
        7   => "Date/Time",     // DBTYPE_DATE
        11  => "Yes/No",        // DBTYPE_BOOL
        17  => "Byte",          // DBTYPE_UI1
        72  => "GUID",          // DBTYPE_GUID
        128 => "Binary",        // DBTYPE_BYTES
        130 => "Text",          // DBTYPE_WSTR (short text)
        131 => "Decimal",       // DBTYPE_NUMERIC
        135 => "Date/Time",     // DBTYPE_DBTIMESTAMP
        203 => "Memo",          // DBTYPE_WLONGVARCHAR
        204 => "Binary",        // DBTYPE_VARBINARY
        205 => "OLE Object",    // DBTYPE_LONGVARBINARY
        _   => $"DBTYPE({dataTypeCode})",
    };

    private sealed record OleDbTableRow(string Name, bool IsLinked);

    private sealed record OleDbColumnRow(
        string TableName,
        string ColumnName,
        int    OrdinalPosition,
        string DataTypeName,
        bool   IsMemo,
        bool   IsOleObject,
        bool?  IsRequired);

    private sealed record OleDbPkColumn(string TableName, string ColumnName);

    private sealed record OleDbFkColumn(
        string FkTableName, string FkColumnName,
        string PkTableName, string PkColumnName,
        string? ConstraintName);
}
