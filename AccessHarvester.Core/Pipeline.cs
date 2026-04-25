using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;
using AccessHarvester.Core.Enrichment;
using AccessHarvester.Core.Extraction;
using AccessHarvester.Core.Translation;

namespace AccessHarvester.Core;

/// <summary>
/// Orchestrates the three-phase pipeline: extract Access objects to raw/ artifacts,
/// translate those into kb/ JSON, then enrich into ir/ JSON (normalized geometry,
/// etc.). Phases are independently callable.
/// </summary>
public sealed class Pipeline
{
    private readonly IReadOnlyList<IObjectExporter> _exporters;
    private readonly IReadOnlyList<IObjectTranslator> _translators;
    private readonly IReadOnlyList<IObjectEnricher> _enrichers;

    public Pipeline(
        IEnumerable<IObjectExporter> exporters,
        IEnumerable<IObjectTranslator> translators,
        IEnumerable<IObjectEnricher> enrichers)
    {
        _exporters = exporters.ToArray();
        _translators = translators.ToArray();
        _enrichers = enrichers.ToArray();
    }

    /// <summary>
    /// Returns a new Pipeline with only the exporters/translators whose Category
    /// appears in <paramref name="categories"/> (case-insensitive match).
    /// </summary>
    public Pipeline WithCategories(IEnumerable<string> categories)
    {
        var set = new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);
        return new Pipeline(
            _exporters.Where(e => set.Contains(e.Category)),
            _translators.Where(t => set.Contains(t.Category)),
            _enrichers.Where(e => set.Contains(e.Category)));
    }

    public IReadOnlyList<string> ExporterCategories
        => _exporters.Select(e => e.Category).ToArray();

    public IReadOnlyList<string> TranslatorCategories
        => _translators.Select(t => t.Category).ToArray();

    /// <summary>
    /// Default pipeline covering all categories the harvester currently supports.
    /// </summary>
    public static Pipeline Default() => new(
        exporters: new IObjectExporter[]
        {
            SaveAsTextExporter.Forms,
            SaveAsTextExporter.Reports,
            SaveAsTextExporter.Macros,
            SaveAsTextExporter.Modules,
            QueryExporter.Instance,
            TableExporter.Instance,
            RelationshipExporter.Instance,
        },
        translators: new IObjectTranslator[]
        {
            SaveAsTextTranslator.Forms,
            SaveAsTextTranslator.Reports,
            SaveAsTextTranslator.Macros,
            SaveAsTextTranslator.Modules,
            JsonPassthroughTranslator.Queries,
            JsonPassthroughTranslator.Tables,
            JsonPassthroughTranslator.Relationships,
        },
        enrichers: new IObjectEnricher[]
        {
            GeometryEnricher.Forms,
            GeometryEnricher.Reports,
            LabelResolver.Forms,
            LabelResolver.Reports,
            PropertyDecoder.Forms,
            PropertyDecoder.Reports,
            TextCapacityEnricher.Forms,
            TextCapacityEnricher.Reports,
            VbaSignatureEnricher.Forms,
            VbaSignatureEnricher.Reports,
            VbaSignatureEnricher.ModulesAndClasses,
            MacroActionsEnricher.Macros,
            MacroActionsEnricher.Forms,
            MacroActionsEnricher.Reports,
        });

    public static string BaseDir(string outputRoot, string dbName)
        => Path.Combine(outputRoot, "AccessMetaData", dbName);

    public static string RawDir(string outputRoot, string dbName)
        => Path.Combine(BaseDir(outputRoot, dbName), "raw");

    public static string KbDir(string outputRoot, string dbName)
        => Path.Combine(BaseDir(outputRoot, dbName), "kb");

    public static string IrDir(string outputRoot, string dbName)
        => Path.Combine(BaseDir(outputRoot, dbName), "ir");

    public ExtractResult Extract(string databasePath, string outputRoot)
    {
        string dbName = Path.GetFileNameWithoutExtension(databasePath);
        string baseDir = BaseDir(outputRoot, dbName);
        string rawDir = RawDir(outputRoot, dbName);
        Directory.CreateDirectory(rawDir);

        var categories = new List<CategoryExportResult>();
        using var session = new AccessComSession(databasePath);
        foreach (var exporter in _exporters)
        {
            var categoryDir = Path.Combine(rawDir, exporter.Category);
            var outcomes = exporter.Export(session, categoryDir);
            categories.Add(new CategoryExportResult(exporter.Category, outcomes));
        }

        return new ExtractResult(baseDir, rawDir, categories);
    }

    public TranslateResult Translate(string rawDir, string kbDir)
    {
        Directory.CreateDirectory(kbDir);
        var categories = new List<CategoryTranslateResult>();
        foreach (var translator in _translators)
        {
            var rawCategoryDir = Path.Combine(rawDir, translator.Category);
            var kbCategoryDir = Path.Combine(kbDir, translator.Category);
            var outcomes = translator.Translate(rawCategoryDir, kbCategoryDir);
            categories.Add(new CategoryTranslateResult(translator.Category, outcomes));
        }
        return new TranslateResult(rawDir, kbDir, categories);
    }

    public EnrichResult Enrich(string kbDir, string irDir)
    {
        Directory.CreateDirectory(irDir);
        var categories = new List<CategoryEnrichResult>();

        WriteDefaultsArtifact(irDir);

        if (!Directory.Exists(kbDir))
            return new EnrichResult(kbDir, irDir, categories);

        foreach (var kbCategoryDir in Directory.GetDirectories(kbDir))
        {
            string categoryName = Path.GetFileName(kbCategoryDir);
            string irCategoryDir = Path.Combine(irDir, categoryName);
            Directory.CreateDirectory(irCategoryDir);

            var categoryEnrichers = _enrichers
                .Where(e => e.Category.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var outcomes = new List<EnrichOutcome>();
            foreach (var file in Directory.GetFiles(kbCategoryDir, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string targetFile = Path.Combine(irCategoryDir, name + ".ir.json");
                try
                {
                    if (categoryEnrichers.Count == 0)
                    {
                        File.Copy(file, targetFile, overwrite: true);
                    }
                    else
                    {
                        // First enricher reads from kb/ and writes to ir/.
                        // Subsequent enrichers read and rewrite the ir/ file in place,
                        // each adding its own contribution. Enrichers run in the order
                        // registered in the Pipeline's constructor.
                        categoryEnrichers[0].EnrichFile(file, targetFile);
                        for (int i = 1; i < categoryEnrichers.Count; i++)
                            categoryEnrichers[i].EnrichFile(targetFile, targetFile);
                    }
                    outcomes.Add(new EnrichOutcome(name, true));
                }
                catch (Exception ex)
                {
                    outcomes.Add(new EnrichOutcome(name, false, ex.Message));
                }
            }
            categories.Add(new CategoryEnrichResult(categoryName, outcomes));
        }

        return new EnrichResult(kbDir, irDir, categories);
    }

    private static void WriteDefaultsArtifact(string irDir)
    {
        var asm = typeof(Pipeline).Assembly;
        const string resourceName = "AccessHarvester.Core.Resources.property-defaults.json";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return;
        using var reader = new StreamReader(stream);
        File.WriteAllText(Path.Combine(irDir, "Defaults.json"), reader.ReadToEnd());
    }

    public RunResult Run(string databasePath, string outputRoot)
    {
        var extract = Extract(databasePath, outputRoot);
        string dbName = Path.GetFileNameWithoutExtension(databasePath);
        string kbDir = KbDir(outputRoot, dbName);
        var translate = Translate(extract.RawDir, kbDir);
        string irDir = IrDir(outputRoot, dbName);
        var enrich = Enrich(translate.KbDir, irDir);
        return new RunResult(
            extract.BaseDir, extract.RawDir, translate.KbDir, enrich.IrDir,
            extract.Categories, translate.Categories, enrich.Categories);
    }
}

public sealed record CategoryExportResult(string Category, IReadOnlyList<ExportOutcome> Outcomes);
public sealed record CategoryTranslateResult(string Category, IReadOnlyList<TranslateOutcome> Outcomes);
public sealed record CategoryEnrichResult(string Category, IReadOnlyList<EnrichOutcome> Outcomes);

public sealed record ExtractResult(string BaseDir, string RawDir, IReadOnlyList<CategoryExportResult> Categories);
public sealed record TranslateResult(string RawDir, string KbDir, IReadOnlyList<CategoryTranslateResult> Categories);
public sealed record EnrichResult(string KbDir, string IrDir, IReadOnlyList<CategoryEnrichResult> Categories);
public sealed record RunResult(
    string BaseDir,
    string RawDir,
    string KbDir,
    string IrDir,
    IReadOnlyList<CategoryExportResult> Extract,
    IReadOnlyList<CategoryTranslateResult> Translate,
    IReadOnlyList<CategoryEnrichResult> Enrich);
