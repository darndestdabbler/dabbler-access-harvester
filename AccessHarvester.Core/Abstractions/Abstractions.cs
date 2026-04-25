using AccessHarvester.Core.Com;

namespace AccessHarvester.Core.Abstractions;

public interface IObjectExporter
{
    string Category { get; }
    IReadOnlyList<ExportOutcome> Export(AccessComSession session, string rawCategoryDir);
}

public interface IObjectTranslator
{
    string Category { get; }
    IReadOnlyList<TranslateOutcome> Translate(string rawCategoryDir, string kbCategoryDir);
}

/// <summary>
/// Transforms one KB JSON artifact into an enriched IR JSON artifact. Applied per
/// file; Pipeline wraps the call with passthrough-copy for categories that have
/// no registered enricher.
/// </summary>
public interface IObjectEnricher
{
    string Category { get; }
    void EnrichFile(string kbFile, string irFile);
}

public sealed record ExportOutcome(string Name, bool Success, string? ErrorMessage = null);

public sealed record TranslateOutcome(string Name, bool Success, string? ErrorMessage = null);

public sealed record EnrichOutcome(string Name, bool Success, string? ErrorMessage = null);
