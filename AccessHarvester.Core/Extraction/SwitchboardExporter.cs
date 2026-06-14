using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;
using AccessHarvester.Pipeline.Enrichment;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Materialises navigation chrome that the schema-only extractors miss:
/// the <b>rows</b> of any switchboard-shaped table (detected by column
/// signature, not name) plus the database's startup form. Emits
/// <c>raw/Navigation/switchboard-items.json</c> (a faithful, lossless row
/// dump) and <c>raw/Navigation/startup-options.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the one new data surface 011's Hard Constraints deferred to
/// this set (DW-9). It stays narrow: detection is by the canonical column
/// signature (<see cref="SwitchboardSignatureDetector"/>); acquisition is a
/// bounded, ordered SELECT of only the canonical columns (plus
/// <c>ParentID</c> when present), gated to shape-matched tables — not a
/// general row-data export surface.
/// </para>
/// <para>
/// The read uses a DAO recordset on the already-open COM session (the
/// extractor tier's native access path — <see cref="TableExporter"/> /
/// <see cref="QueryExporter"/> all use DAO), honouring the
/// <see cref="OleDbValueSampler"/> escaping/degradation discipline:
/// identifiers are guarded by <see cref="OleDbValueSampler.EscapeIdentifier"/>,
/// the SELECT is bounded and ordered, and per-table failures degrade to an
/// unsuccessful <see cref="ExportOutcome"/> rather than aborting the run.
/// This avoids opening a second OLE DB connection against the file the COM
/// session already holds.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class SwitchboardExporter : IObjectExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SwitchboardExporter Instance { get; } = new();

    private SwitchboardExporter() { }

    public string Category => "Navigation";

    public IReadOnlyList<ExportOutcome> Export(AccessComSession session, string rawCategoryDir)
    {
        Directory.CreateDirectory(rawCategoryDir);
        var outcomes = new List<ExportOutcome>();

        dynamic db = session.Application.CurrentDb();

        // ── Switchboard rows ───────────────────────────────────────────
        var detected = new List<string>();
        var tables = new List<SwitchboardTableRows>();

        foreach (dynamic td in db.TableDefs)
        {
            string name = td.Name;
            if (SystemObjectNames.IsSystemObjectName(name)) continue;

            List<string> fieldNames;
            try
            {
                fieldNames = new List<string>();
                foreach (dynamic f in td.Fields) fieldNames.Add((string)f.Name);
            }
            catch
            {
                // A table whose fields cannot be introspected cannot be a
                // confirmed switchboard; skip it (not an error).
                continue;
            }

            if (!SwitchboardSignatureDetector.IsSwitchboardTable(fieldNames)) continue;
            detected.Add(name);

            bool hasParent = SwitchboardSignatureDetector.HasParentIdColumn(fieldNames);
            try
            {
                var rows = ReadRows(db, name, hasParent);
                tables.Add(new SwitchboardTableRows
                {
                    Table = name,
                    HasParentId = hasParent,
                    Rows = rows,
                });
                outcomes.Add(new ExportOutcome(name, true));
            }
            catch (Exception ex)
            {
                // Degradation discipline: record the failure, keep going.
                outcomes.Add(new ExportOutcome(name, false, $"Switchboard row-read failed: {ex.Message}"));
            }
        }

        // Deterministic ordering independent of DAO enumeration order.
        detected.Sort(StringComparer.Ordinal);
        var orderedTables = tables.OrderBy(t => t.Table, StringComparer.Ordinal).ToList();

        var switchboardArtifact = new SwitchboardItemsArtifact
        {
            Source = "switchboard-row",
            DetectedTables = detected,
            Tables = orderedTables,
        };
        File.WriteAllText(
            Path.Combine(rawCategoryDir, "switchboard-items.json"),
            JsonSerializer.Serialize(switchboardArtifact, JsonOptions));

        // ── Startup options (DAO database property) ────────────────────
        string? startUpForm = null;
        ExportOutcome startupOutcome;
        try
        {
            startUpForm = ReadStartUpForm(db);
            startupOutcome = new ExportOutcome("startup-options", true);
        }
        catch (Exception ex)
        {
            // A genuine read failure on a *present* StartUpForm property is
            // surfaced as a degradation rather than collapsing silently to
            // null (which is the legitimate "no startup form" result).
            startupOutcome = new ExportOutcome("startup-options", false, $"StartUpForm read failed: {ex.Message}");
        }

        var startup = new StartupOptionsArtifact { StartUpForm = startUpForm };
        File.WriteAllText(
            Path.Combine(rawCategoryDir, "startup-options.json"),
            JsonSerializer.Serialize(startup, JsonOptions));
        outcomes.Add(startupOutcome);

        return outcomes;
    }

    // Bounded, ordered SELECT of only the canonical columns (+ ParentID
    // when present), escaped per the OleDbValueSampler discipline.
    private static IReadOnlyList<SwitchboardRawRow> ReadRows(dynamic db, string tableName, bool hasParent)
    {
        string table = OleDbValueSampler.EscapeIdentifier(tableName);
        string columns = "[SwitchboardID],[ItemNumber],[ItemText],[Command],[Argument]"
                         + (hasParent ? ",[ParentID]" : string.Empty);
        string sql = $"SELECT {columns} FROM [{table}] ORDER BY [SwitchboardID],[ItemNumber]";

        var rows = new List<SwitchboardRawRow>();
        dynamic rs = db.OpenRecordset(sql);
        try
        {
            while (!rs.EOF)
            {
                rows.Add(new SwitchboardRawRow
                {
                    SwitchboardID = ReadInt(rs, "SwitchboardID"),
                    ItemNumber = ReadInt(rs, "ItemNumber"),
                    ItemText = ReadString(rs, "ItemText"),
                    Command = ReadInt(rs, "Command"),
                    Argument = ReadString(rs, "Argument"),
                    ParentID = hasParent ? ReadInt(rs, "ParentID") : null,
                });
                rs.MoveNext();
            }
        }
        finally
        {
            try { rs.Close(); } catch { /* recordset already closed */ }
        }
        return rows;
    }

    private static int? ReadInt(dynamic rs, string field)
    {
        object value = rs.Fields[field].Value;
        if (value is null || value is DBNull) return null;
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? ReadString(dynamic rs, string field)
    {
        object value = rs.Fields[field].Value;
        if (value is null || value is DBNull) return null;
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    // The "Display Form/Page" startup option lives in the StartUpForm DAO
    // database property; the property is ABSENT from the collection when no
    // startup form is configured. We distinguish that expected-absent case
    // (-> null, no error) from a genuine read failure on a *present*
    // property (-> exception, surfaced as a degradation by the caller) by
    // probing the collection for membership rather than blanket-catching.
    private const string StartUpFormProperty = "StartUpForm";

    private static string? ReadStartUpForm(dynamic db)
    {
        bool present = false;
        foreach (dynamic prop in db.Properties)
        {
            if (string.Equals((string)prop.Name, StartUpFormProperty, StringComparison.OrdinalIgnoreCase))
            {
                present = true;
                break;
            }
        }
        if (!present) return null; // expected: no startup form configured

        // Property exists: read it. A failure here is a real degradation and
        // is allowed to propagate to the caller's try/catch.
        object value = db.Properties[StartUpFormProperty].Value;
        if (value is null || value is DBNull) return null;
        string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        // Access stores "(none)" when the option was set then cleared.
        return string.IsNullOrWhiteSpace(text) || text == "(none)" ? null : text;
    }
}
