using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;
using static AccessHarvester.Pipeline.Tests.Sanitization.TestFixtures;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

public class MaskingEngineTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Engine_writes_one_csv_per_table()
    {
        var doc = Doc(
            Table("Person",
                Col("PersonID",  ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
                Col("FirstName", ColumnCategory.DirectIdentifierPii)));

        var rows = new InMemoryRowSource();
        rows.Add("Person", new Dictionary<string, object?> { ["PersonID"] = 1, ["FirstName"] = "Alice" });
        rows.Add("Person", new Dictionary<string, object?> { ["PersonID"] = 2, ["FirstName"] = "Bob"   });

        var captured = new InMemoryCsvOutput();
        var report = new MaskingEngine().Run(doc, rows, captured, Epoch);

        Assert.Single(report.Tables);
        var person = report.Tables[0];
        Assert.Equal("Person", person.Name);
        Assert.Equal(2, person.RowsRead);
        Assert.Equal(2, person.RowsWritten);

        var lines = captured.GetLines("Person");
        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Equal("PersonID,FirstName", lines[0]);
        Assert.Contains("MOCK-FirstName-0", lines[1]);
        Assert.Contains("MOCK-FirstName-1", lines[2]);
    }

    [Fact]
    public void Engine_applies_correct_strategy_per_column()
    {
        var doc = Doc(Table("People",
            Col("Id",         ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
            Col("FirstName",  ColumnCategory.DirectIdentifierPii),
            Col("DOB",        ColumnCategory.QuasiIdentifier, dataType: "Date/Time"),
            Col("Notes",      ColumnCategory.FreeTextMemo, memo: true, dataType: "Memo"),
            Col("LastModified", ColumnCategory.SystemAudit, dataType: "Date/Time", required: true)));

        var rows = new InMemoryRowSource();
        rows.Add("People", new Dictionary<string, object?>
        {
            ["Id"] = 5,
            ["FirstName"] = "Carol",
            ["DOB"] = new DateTime(1985, 6, 12),
            ["Notes"] = "private medical notes",
            ["LastModified"] = new DateTime(2020, 1, 1),
        });

        var captured = new InMemoryCsvOutput();
        var report = new MaskingEngine().Run(doc, rows, captured, Epoch);

        var strategies = report.Tables[0].AppliedStrategies.ToDictionary(s => s.ColumnName, s => s.Strategy);
        Assert.Equal(MaskingStrategy.RemapId,       strategies["Id"]);
        Assert.Equal(MaskingStrategy.MockReplace,   strategies["FirstName"]);
        Assert.Equal(MaskingStrategy.Generalize,    strategies["DOB"]);
        Assert.Equal(MaskingStrategy.ScrubFreeText, strategies["Notes"]);
        Assert.Equal(MaskingStrategy.Regenerate,    strategies["LastModified"]);
    }

    [Fact]
    public void Engine_skips_linked_tables()
    {
        var doc = new ClassificationDocument(
            Version: "1.0", Database: "Test.accdb", ScannedUtc: Epoch, Config: EmptyConfig,
            Tables: new[]
            {
                Table("Local",
                    Col("Id", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true)),
                new ClassifiedTable(
                    Name: "Remote",
                    IsLinkedTable: true,
                    RowCount: 0, LookupGuess: false, NonKeyNonSystemColumnCount: 0,
                    ReferencedByFkCount: 0, TableHint: "linked", Confidence: 1.0,
                    FiredHeuristics: Array.Empty<string>(), HumanOverride: EmptyTableOverride,
                    Columns: new[]
                    {
                        Col("Id", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
                    }),
            });

        var rows = new InMemoryRowSource();
        rows.Add("Local", new Dictionary<string, object?> { ["Id"] = 1 });

        var captured = new InMemoryCsvOutput();
        var report = new MaskingEngine().Run(doc, rows, captured, Epoch);

        Assert.Single(report.Tables);
        Assert.Equal("Local", report.Tables[0].Name);
    }

    [Fact]
    public void Engine_with_no_rows_writes_header_only()
    {
        var doc = Doc(Table("Empty",
            Col("Id",   ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true),
            Col("Note", ColumnCategory.GenericNonSensitive)));

        var captured = new InMemoryCsvOutput();
        var report = new MaskingEngine().Run(doc, new InMemoryRowSource(), captured, Epoch);

        Assert.Equal(0, report.Tables[0].RowsRead);
        var lines = captured.GetLines("Empty");
        Assert.Single(lines);
        Assert.Equal("Id,Note", lines[0]);
    }

    [Fact]
    public void Report_serializer_emits_stable_v1_shape()
    {
        var doc = Doc(Table("T",
            Col("Id", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true, required: true)));
        var rows = new InMemoryRowSource();
        rows.Add("T", new Dictionary<string, object?> { ["Id"] = 1 });

        var captured = new InMemoryCsvOutput();
        var report = new MaskingEngine().Run(doc, rows, captured, Epoch);

        string json = SanitizationRunner.SerializeReport(report);

        Assert.Contains("\"version\": \"1.0\"", json);
        Assert.Contains("\"database\": \"Test.accdb\"", json);
        Assert.Contains("\"strategy\": \"remap-id\"", json);
    }
}

internal sealed class InMemoryCsvOutput : ICsvOutput
{
    private readonly Dictionary<string, MemoryStream> _streams = new();
    private readonly Dictionary<string, StreamWriter> _writers = new();

    public string OutputDirectoryLabel => "<memory>";
    public string OutputFilePath(string tableName) => $"<memory>/{tableName}.csv";

    public CsvWriter OpenTable(string tableName)
    {
        var ms = new MemoryStream();
        var sw = new StreamWriter(ms, new System.Text.UTF8Encoding(false), leaveOpen: true);
        _streams[tableName] = ms;
        _writers[tableName] = sw;
        return new CsvWriter(sw, ownsWriter: true);
    }

    public string[] GetLines(string tableName)
    {
        // CsvWriter.Dispose() flushed the underlying writer; the memory
        // stream now contains the full output. leaveOpen on the StreamWriter
        // keeps the MemoryStream accessible after Dispose.
        var ms = _streams[tableName];
        return System.Text.Encoding.UTF8.GetString(ms.ToArray())
            .Split(new[] { "\r\n" }, StringSplitOptions.None)
            .Where(s => s.Length > 0)
            .ToArray();
    }
}
