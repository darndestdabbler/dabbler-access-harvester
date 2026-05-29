using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AccessHarvester.Pipeline.Sanitization;

/// <summary>
/// Orchestrates per-row sanitization. Pure of I/O except for the file
/// writes performed through <see cref="CsvWriter"/>; callers can substitute
/// any <see cref="TextWriter"/> via <see cref="ICsvOutput"/>.
/// </summary>
public sealed class MaskingEngine
{
    private readonly IReadOnlyDictionary<MaskingStrategy, IMaskingHandler> _handlers;

    public MaskingEngine() : this(DefaultHandlers()) { }

    public MaskingEngine(IReadOnlyDictionary<MaskingStrategy, IMaskingHandler> handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public static IReadOnlyDictionary<MaskingStrategy, IMaskingHandler> DefaultHandlers() =>
        new Dictionary<MaskingStrategy, IMaskingHandler>
        {
            [MaskingStrategy.Preserve]      = new PreserveHandler(),
            [MaskingStrategy.MockReplace]   = new MockReplaceHandler(),
            [MaskingStrategy.Generalize]    = new GeneralizeHandler(),
            [MaskingStrategy.Redact]        = new RedactHandler(),
            [MaskingStrategy.ScrubFreeText] = new ScrubFreeTextHandler(),
            [MaskingStrategy.Regenerate]    = new RegenerateHandler(),
            [MaskingStrategy.RemapId]       = new RemapIdHandler(),
        };

    /// <summary>
    /// Runs the sanitizer end-to-end. Linked tables, OLE-Object columns,
    /// and multi-value columns are skipped (linked tables not present in
    /// the .accdb being sanitized; OLE/multi-value rows are surfaced as a
    /// per-table skip in the report).
    /// </summary>
    public SanitizationReport Run(
        ClassificationDocument document,
        IRowSource rowSource,
        ICsvOutput csvOutput,
        DateTimeOffset sanitizationEpoch)
    {
        if (document  is null) throw new ArgumentNullException(nameof(document));
        if (rowSource is null) throw new ArgumentNullException(nameof(rowSource));
        if (csvOutput is null) throw new ArgumentNullException(nameof(csvOutput));

        var graphResult = FkGraphBuilder.Build(document);
        var graph    = graphResult.Graph;
        var missing  = graphResult.MissingFkTargets;
        var registry = new RemapRegistry();

        var tableResults = new List<TableSanitizationResult>(document.Tables.Count);
        foreach (var table in document.Tables)
        {
            if (table.IsLinkedTable) continue;
            tableResults.Add(SanitizeTable(table, graph, registry, rowSource, csvOutput, sanitizationEpoch));
        }

        return new SanitizationReport(
            DatabaseName:        document.Database,
            SanitizationEpoch:   sanitizationEpoch,
            OutputDirectory:     csvOutput.OutputDirectoryLabel,
            Tables:              tableResults,
            MissingFkTargets:    missing,
            RemapMapSizes:       registry.Sizes());
    }

    private TableSanitizationResult SanitizeTable(
        ClassifiedTable table,
        FkGraph graph,
        RemapRegistry registry,
        IRowSource rowSource,
        ICsvOutput csvOutput,
        DateTimeOffset epoch)
    {
        // Decide strategies once per column, then re-use across every row.
        var strategies = new List<(ClassifiedColumn Column, MaskingStrategy Strategy, IMaskingHandler Handler)>(table.Columns.Count);
        foreach (var col in table.Columns)
        {
            var key = new TableColumn(table.Name, col.Name);
            var decision = EffectiveCategoryResolver.Resolve(col, key, graph);
            if (!_handlers.TryGetValue(decision.Strategy, out var handler))
                throw new InvalidOperationException(
                    $"No masking handler registered for strategy '{decision.Strategy.ToWire()}' (column {table.Name}.{col.Name}).");
            strategies.Add((col, decision.Strategy, handler));
        }

        var columnNames = strategies.Select(s => s.Column.Name).ToArray();
        using var writer = csvOutput.OpenTable(table.Name);
        writer.WriteHeader(columnNames);

        int rowsRead = 0, rowsWritten = 0;
        long rowIndex = 0;
        var rowBuffer = new object?[columnNames.Length];

        foreach (var row in rowSource.ReadRows(table.Name, columnNames))
        {
            rowsRead++;
            for (int i = 0; i < strategies.Count; i++)
            {
                var (col, _, handler) = strategies[i];
                object? original = row.TryGetValue(col.Name, out var v) ? v : null;
                var ctx = new MaskingContext(table.Name, col, registry, epoch);
                rowBuffer[i] = handler.Apply(original, ctx, rowIndex);
            }
            writer.WriteRow(rowBuffer);
            rowsWritten++;
            rowIndex++;
        }

        var applied = strategies
            .Select(s => new AppliedStrategy(s.Column.Name, s.Strategy))
            .ToList();
        return new TableSanitizationResult(
            Name:               table.Name,
            RowsRead:           rowsRead,
            RowsWritten:        rowsWritten,
            OutputFile:         csvOutput.OutputFilePath(table.Name),
            AppliedStrategies:  applied);
    }
}

/// <summary>Abstract per-table CSV destination. Lets tests collect output into a memory stream.</summary>
public interface ICsvOutput
{
    string OutputDirectoryLabel { get; }
    string OutputFilePath(string tableName);
    CsvWriter OpenTable(string tableName);
}

/// <summary>Writes per-table CSVs to a directory on disk.</summary>
public sealed class FileSystemCsvOutput : ICsvOutput
{
    private readonly string _root;

    public FileSystemCsvOutput(string rootDirectory)
    {
        _root = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        Directory.CreateDirectory(_root);
    }

    public string OutputDirectoryLabel => _root;

    public string OutputFilePath(string tableName) =>
        Path.Combine(_root, $"{Slugify(tableName)}.csv");

    public CsvWriter OpenTable(string tableName)
    {
        string path = OutputFilePath(tableName);
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var writer = new StreamWriter(stream, new UTF8Encoding(false));
        return new CsvWriter(writer, ownsWriter: true);
    }

    private static string Slugify(string tableName)
    {
        var sb = new StringBuilder(tableName.Length);
        foreach (char c in tableName)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
            else sb.Append('_');
        }
        return sb.ToString();
    }
}
