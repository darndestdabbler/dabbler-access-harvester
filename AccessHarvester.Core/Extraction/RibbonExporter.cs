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
/// Materialises the custom-ribbon definition the schema-only extractors miss:
/// the <b>rows</b> of any ribbon-shaped table (a table carrying both a
/// <c>RibbonName</c> and a <c>RibbonXML</c> column — canonically
/// <c>USysRibbons</c>), detected by column signature, not name. Emits
/// <c>raw/Navigation/ribbon-xml.json</c> (a faithful, lossless
/// <c>(RibbonName, RibbonXML)</c> row dump). The customUI XML is parsed
/// downstream by the pure <see cref="RibbonXmlParser"/>; this exporter only
/// reads the raw value.
/// </summary>
/// <remarks>
/// <para>
/// This is the DW-13 ribbon analogue of <see cref="SwitchboardExporter"/>: the
/// <c>USysRibbons.RibbonXML</c> LONGTEXT value is NOT carried by the schema-only
/// table export (<see cref="TableExporter"/> emits <c>USysRibbons</c> as DDL +
/// <c>Fields</c> only, no row data), so a custom ribbon's markup is invisible
/// without this row-read. Shares the <c>Navigation</c> category and the same
/// DAO/escaping/degradation discipline as <see cref="SwitchboardExporter"/>; a
/// database without a ribbon-shaped table produces an artifact with no rows
/// (and the navigation builder leaves <c>NavigationGraph.Ribbon</c> null).
/// </para>
/// <para>
/// <c>USysRibbons</c> is a user-system (<c>USys*</c>) table, which
/// <see cref="SystemObjectNames.IsSystemObjectName"/> does NOT classify as a
/// system object (only <c>MSys*</c> / <c>~*</c>), so it is enumerated normally.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class RibbonExporter : IObjectExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static RibbonExporter Instance { get; } = new();

    private RibbonExporter() { }

    public string Category => "Navigation";

    public IReadOnlyList<ExportOutcome> Export(AccessComSession session, string rawCategoryDir)
    {
        Directory.CreateDirectory(rawCategoryDir);
        var outcomes = new List<ExportOutcome>();

        dynamic db = session.Application.CurrentDb();

        var detected = new List<string>();
        var rows = new List<RibbonXmlRow>();

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
                // confirmed ribbon table; skip it (not an error).
                continue;
            }

            if (!RibbonSignatureDetector.IsRibbonTable(fieldNames)) continue;
            detected.Add(name);

            try
            {
                rows.AddRange(ReadRows(db, name));
                outcomes.Add(new ExportOutcome(name, true));
            }
            catch (Exception ex)
            {
                // Degradation discipline: record the failure, keep going.
                outcomes.Add(new ExportOutcome(name, false, $"Ribbon row-read failed: {ex.Message}"));
            }
        }

        // Shape-gated: a database with no ribbon-shaped table produces NOTHING
        // (no artifact file), so a non-ribbon corpus (e.g. HomeHealthCare) stays
        // byte-for-byte unchanged and NavigationGraph.Ribbon stays null. Only
        // emit the artifact when a ribbon table was actually detected AND it
        // carried at least one row: a detected-but-empty USysRibbons defines no
        // ribbon, so it must behave identically to a missing table (no artifact)
        // rather than emitting a rows:[] payload (016 S2 set-wide review, A5).
        if (detected.Count == 0 || rows.Count == 0)
            return outcomes;

        // Deterministic ordering independent of DAO enumeration order. RibbonXml
        // is the final tiebreaker (RibbonName is not unique within a table), so
        // the raw dump is byte-stable across runs even with duplicate names.
        detected.Sort(StringComparer.Ordinal);
        var orderedRows = rows
            .OrderBy(r => r.Table, StringComparer.Ordinal)
            .ThenBy(r => r.RibbonName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(r => r.RibbonXml ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var artifact = new RibbonXmlArtifact
        {
            Source = "usys-ribbons-row",
            DetectedTables = detected,
            Rows = orderedRows,
        };
        File.WriteAllText(
            Path.Combine(rawCategoryDir, "ribbon-xml.json"),
            JsonSerializer.Serialize(artifact, JsonOptions));

        return outcomes;
    }

    // Bounded, ordered SELECT of only the two canonical columns, escaped per
    // the OleDbValueSampler discipline. RibbonXML is a LONGTEXT (Memo) column;
    // DAO returns it as a string.
    private static IReadOnlyList<RibbonXmlRow> ReadRows(dynamic db, string tableName)
    {
        string table = OleDbValueSampler.EscapeIdentifier(tableName);
        string sql = $"SELECT [RibbonName],[RibbonXML] FROM [{table}] ORDER BY [RibbonName]";

        var rows = new List<RibbonXmlRow>();
        dynamic rs = db.OpenRecordset(sql);
        try
        {
            while (!rs.EOF)
            {
                rows.Add(new RibbonXmlRow
                {
                    Table = tableName,
                    RibbonName = ReadString(rs, "RibbonName"),
                    RibbonXml = ReadString(rs, "RibbonXML"),
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

    private static string? ReadString(dynamic rs, string field)
    {
        object value = rs.Fields[field].Value;
        if (value is null || value is DBNull) return null;
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
