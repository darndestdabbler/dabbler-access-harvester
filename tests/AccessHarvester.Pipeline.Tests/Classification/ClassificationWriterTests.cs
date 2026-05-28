using System;
using System.IO;
using System.Reflection;
using AccessHarvester.Pipeline.Classification;
using Json.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Classification;

public class ClassificationWriterTests
{
    /// <summary>
    /// End-to-end serialization round-trip: build a small but representative
    /// finding tree, write it, parse it back, and validate the result against
    /// the committed Draft-07 schema. Catches drift between the writer's
    /// output shape and the contract finalized in Session 1.
    /// </summary>
    [Fact]
    public void Writer_output_validates_against_classification_schema()
    {
        var findings = SmallFindings();
        string json = ClassificationWriter.Serialize(findings);

        // Locate the schema file from the source tree (the schema is not
        // embedded into Pipeline; it lives alongside Pipeline/Classification/).
        string schemaPath = LocateClassificationSchema();
        var schema = JsonSchema.FromFile(schemaPath);

        using var doc = JsonDocument.Parse(json);
        var result = schema.Evaluate(
            doc.RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid, FormatErrors(result));
    }

    [Fact]
    public void Writer_emits_required_table_human_override_with_scanner_defaults()
    {
        var findings = SmallFindings();
        string json = ClassificationWriter.Serialize(findings);
        var root = JsonNode.Parse(json)!.AsObject();
        var firstTableOverride = root["tables"]!.AsArray()[0]!["humanOverride"]!.AsObject();
        Assert.Null(firstTableOverride["isLookup"]);
        Assert.Null(firstTableOverride["reviewedBy"]);
        Assert.Equal(string.Empty, firstTableOverride["notes"]!.GetValue<string>());
    }

    [Fact]
    public void Writer_emits_required_column_human_override_with_scanner_defaults()
    {
        var findings = SmallFindings();
        string json = ClassificationWriter.Serialize(findings);
        var root = JsonNode.Parse(json)!.AsObject();
        var firstColumnOverride = root["tables"]!.AsArray()[0]!["columns"]!.AsArray()[0]!["humanOverride"]!.AsObject();
        Assert.Null(firstColumnOverride["category"]);
        Assert.Null(firstColumnOverride["maskingStrategy"]);
        Assert.False(firstColumnOverride["preserveAutonumber"]!.GetValue<bool>());
        Assert.Null(firstColumnOverride["reviewedBy"]);
        Assert.Equal(string.Empty, firstColumnOverride["notes"]!.GetValue<string>());
    }

    [Fact]
    public void Writer_serializes_scannedUtc_as_iso8601_with_trailing_Z()
    {
        var findings = SmallFindings();
        string json = ClassificationWriter.Serialize(findings);
        var root = JsonNode.Parse(json)!.AsObject();
        string scanned = root["scannedUtc"]!.GetValue<string>();
        Assert.EndsWith("Z", scanned);
        Assert.Equal("2026-05-28T18:00:00Z", scanned);
    }

    [Fact]
    public void Writer_emits_kebab_case_category_and_hint_names()
    {
        var findings = SmallFindings();
        string json = ClassificationWriter.Serialize(findings);
        Assert.Contains("\"guessedCategory\": \"surrogate-autonumber-pk\"", json);
        Assert.Contains("\"tableHint\": \"entity-pii-bearing\"", json);
    }

    // ───────────────────────────────────────────── helpers

    private static ClassificationFindings SmallFindings()
    {
        var cfg = new ScanConfig(
            SystemColumnPatterns: new[] { "LastModified*" },
            PiiNamePatterns:      new[] { "First*", "Last*" },
            PiiValuePatterns:     new[] { "us-ssn" },
            ValueSampleSize:      200);

        var col = new ColumnFinding(
            Name: "PersonID",
            OrdinalPosition: 0,
            DataType: "Long",
            IsAutoNumber: true,
            IsPrimaryKey: true,
            IsForeignKey: false,
            FkTarget: null,
            IsMemo: false,
            IsOleObject: false,
            IsMultiValue: false,
            IsRequired: true,
            RowCount: 12,
            NullCount: 0,
            Cardinality: 12,
            UniquenessRatio: 1.0,
            RelativeCardinality: 1.0,
            PiiNameMatch: false,
            PiiValueMatch: false,
            ValueFormatPattern: null,
            GuessedCategory: ColumnCategory.SurrogateAutonumberPk,
            Confidence: 0.98,
            FiredHeuristics: new[] { "autonumber-pk" });

        var table = new TableFinding(
            Name: "Person",
            IsLinkedTable: false,
            RowCount: 12,
            LookupGuess: false,
            NonKeyNonSystemColumnCount: 0,
            ReferencedByFkCount: 0,
            TableHint: TableHint.EntityPiiBearing,
            Confidence: 0.92,
            FiredHeuristics: new[] { "pii-veto" },
            Columns: new[] { col });

        return new ClassificationFindings(
            DatabaseName: "Test.accdb",
            ScannedUtc:   new DateTimeOffset(2026, 5, 28, 18, 0, 0, TimeSpan.Zero),
            Config:       cfg,
            Tables:       new[] { table });
    }

    private static string LocateClassificationSchema()
    {
        // tests/AccessHarvester.Pipeline.Tests/bin/Debug/net10.0 → walk up to the repo root.
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            string candidate = Path.Combine(dir, "AccessHarvester.Pipeline", "Classification", "classification-schema.json");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException("Could not locate classification-schema.json by walking up from the test bin dir.");
    }

    private static string FormatErrors(EvaluationResults result)
    {
        if (result.IsValid) return "(no errors)";
        var sb = new System.Text.StringBuilder("Schema validation failed:\n");
        foreach (var item in result.Details)
        {
            if (item.IsValid) continue;
            sb.Append("  ").Append(item.InstanceLocation).Append(": ");
            if (item.Errors is { Count: > 0 })
            {
                foreach (var e in item.Errors)
                    sb.Append('[').Append(e.Key).Append("] ").Append(e.Value).Append("; ");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
