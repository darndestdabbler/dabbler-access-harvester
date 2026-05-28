using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccessHarvester.Access;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;
using AccessHarvester.Core.Enrichment;
using AccessHarvester.Core.Extraction;
using AccessHarvester.Core.Translation;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Sql.Parser;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.References;
using VbaReferenceResolver = AccessHarvester.Vba.Resolver.References.ReferenceResolver;
using AccessHarvester.Vba.Resolver.Scopes;
using ManifestModuleKind = AccessHarvester.Pipeline.Manifest.ModuleKind;
using ResolverModuleKind = AccessHarvester.Vba.Resolver.Scopes.ModuleKind;

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

        EmitManifestArtifacts(extract.BaseDir, databasePath, extract.RawDir, enrich.IrDir);

        return new RunResult(
            extract.BaseDir, extract.RawDir, translate.KbDir, enrich.IrDir,
            extract.Categories, translate.Categories, enrich.Categories);
    }

    // V1 manifest emission: parse VBA module text from the raw/ tier,
    // run the resolve + classify chain, project to JSON, and assemble
    // into a DatabaseManifest alongside any form/report specs already
    // built by the ir/ enrichment pass.
    //
    // Module discovery reads *.txt files from raw/Modules/, raw/Forms/,
    // and raw/Reports/ (SaveAsText output). Form/report specs are loaded
    // from ir/Forms/*.ir.json and ir/Reports/*.ir.json via
    // FormMetadataExtractor + FormComponentSpecBuilder.
    //
    // Files that fail to parse are logged as HarvesterDiagnostics and
    // skipped rather than aborting the emission — one broken module
    // should not prevent the rest of the manifest from being written.
    private static void EmitManifestArtifacts(
        string baseDir, string databasePath, string rawDir, string irDir)
    {
        var database = new DatabaseInfo
        {
            Name = Path.GetFileName(databasePath),
            FilePath = Path.GetFullPath(databasePath),
            ExportedAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };

        var diagnostics = new List<HarvesterDiagnostic>();

        // Ensure Windows codepage 1252 (used by Access SaveAsText output) is
        // available. In .NET Core, GetEncoding(1252) requires this registration;
        // it is a no-op if the provider is already registered.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // ── 1. Parse VBA text from all three raw categories ──────────
        var parsed = new List<ParsedModuleEntry>();
        foreach (var category in new[] { "Modules", "Forms", "Reports" })
        {
            var categoryDir = Path.Combine(rawDir, category);
            if (!Directory.Exists(categoryDir)) continue;

            foreach (var file in Directory.GetFiles(categoryDir, "*.txt").OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var text = File.ReadAllText(file, Encoding.GetEncoding(1252));
                    var (ast, _) = VbaModuleBodyLoader.LoadAndParse(text);

                    // Class modules have "VERSION 1.0 CLASS" in the SaveAsText header.
                    bool isClassModule = category != "Modules"
                        || text.Contains("VERSION 1.0 CLASS\r\n", StringComparison.Ordinal)
                        || text.Contains("VERSION 1.0 CLASS\n", StringComparison.Ordinal);

                    var manifestKind = isClassModule
                        ? ManifestModuleKind.ClassModule
                        : ManifestModuleKind.Standard;
                    var resolverKind = category switch
                    {
                        "Forms" => ResolverModuleKind.Form,
                        "Reports" => ResolverModuleKind.Report,
                        _ when isClassModule => ResolverModuleKind.Class,
                        _ => ResolverModuleKind.Standard,
                    };

                    var project = new ProjectScope();
                    var visitor = new ScopeBuildingVisitor();
                    var scope = visitor.BuildModuleScope(project, ast, name, resolverKind);
                    var resolved = new VbaReferenceResolver().Resolve(scope, ast);
                    parsed.Add(new ParsedModuleEntry(name, manifestKind, category, resolved));
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new HarvesterDiagnostic
                    {
                        Severity = HarvesterDiagnosticSeverity.Warning,
                        Message = $"VBA parse failed for '{name}': {ex.Message}",
                        Code = "VBA_PARSE_FAILURE",
                    });
                }
            }
        }

        // ── 2. Cross-reference index and classification ────────────────
        var resolvedModules = parsed.Select(p => p.Resolved).ToList();
        var crossRefs = new CrossReferenceIndex(resolvedModules);
        var classifications = FunctionClassificationBuilder.BuildAll(resolvedModules, crossRefs);

        // ── 3. Standalone module exports ───────────────────────────────
        var moduleExports = parsed
            .Where(p => p.Category == "Modules")
            .Select(p =>
            {
                var parserOutput = ParserOutputProjector.Project(
                    p.Resolved, classifications, p.Name, p.Kind);
                return new ModuleExport
                {
                    ModuleName = p.Name,
                    ModuleKind = p.Kind,
                    ParserOutput = parserOutput,
                };
            })
            .ToList();

        // ── 4. Form and report exports (merge spec + parser output) ────
        var formExports = BuildFormOrReportExports(
            parsed, classifications, irDir, "Forms", diagnostics);
        var reportExports = BuildFormOrReportExports(
            parsed, classifications, irDir, "Reports", diagnostics);

        // ── 4b. Query exports (D14: Sql.Parsed + Sql.Parameters) ──────
        var queryExports = BuildQueryExports(rawDir, diagnostics);

        // ── 5. Assemble and write ──────────────────────────────────────
        IReadOnlyList<UnresolvedItem> unresolvedItems = Array.Empty<UnresolvedItem>();

        var manifest = DatabaseManifestAggregator.Build(
            database: database,
            harvesterVersion: ResolveHarvesterVersion(),
            forms: formExports,
            reports: reportExports,
            queries: queryExports,
            modules: moduleExports,
            unresolvedItems: unresolvedItems,
            diagnostics: diagnostics);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        File.WriteAllText(
            Path.Combine(baseDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, options));
        File.WriteAllText(
            Path.Combine(baseDir, "unresolved-items.json"),
            JsonSerializer.Serialize(unresolvedItems, options));
    }

    // Loads form/report specs from ir/ and merges with parser outputs
    // from the raw/ category. Returns one FormExport per form/report that
    // has both a spec (from ir/) and parser output (from raw/).
    // Forms with no spec are omitted; the parser output is not lost — the
    // HarvesterDiagnostic records the skip so the human can investigate.
    private static List<FormExport> BuildFormOrReportExports(
        List<ParsedModuleEntry> parsed,
        IReadOnlyList<FunctionClassification> classifications,
        string irDir,
        string category,
        List<HarvesterDiagnostic> diagnostics)
    {
        // Build parser-output map: form name → JsonNode
        var parserOutputs = new Dictionary<string, System.Text.Json.Nodes.JsonNode>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var p in parsed.Where(e => e.Category == category))
        {
            var output = ParserOutputProjector.Project(
                p.Resolved, classifications, p.Name, p.Kind);
            parserOutputs[p.Name] = output;
        }

        // Load form specs from ir/<category>/*.ir.json
        var irCategoryDir = Path.Combine(irDir, category);
        if (!Directory.Exists(irCategoryDir))
            return new List<FormExport>();

        var exports = new List<FormExport>();
        foreach (var irFile in Directory.GetFiles(irCategoryDir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            // ir/Forms/frmSomething.ir.json → name = "frmSomething"
            var irName = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(irFile));
            try
            {
                var irJson = File.ReadAllText(irFile, Encoding.UTF8);
                var metadata = FormMetadataExtractor.Extract(irName, irJson);
                var spec = FormComponentSpecBuilder.Build(metadata);

                parserOutputs.TryGetValue(irName, out var parserOutput);
                exports.Add(new FormExport { Spec = spec, ParserOutput = parserOutput });
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"Form spec load failed for '{irName}': {ex.Message}",
                    Code = "FORM_SPEC_LOAD_FAILURE",
                });
            }
        }

        return exports;
    }

    // Reads saved-query JSON files from raw/Queries/, parses each query's SQL
    // with SqlParserPipeline, builds a QueryAbstraction, and projects a QueryItem
    // JsonNode per the D14 spec.
    private static List<System.Text.Json.Nodes.JsonNode> BuildQueryExports(
        string rawDir,
        List<HarvesterDiagnostic> diagnostics)
    {
        var queryDir = Path.Combine(rawDir, "Queries");
        var exports = new List<System.Text.Json.Nodes.JsonNode>();
        if (!Directory.Exists(queryDir)) return exports;

        var readOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var file in Directory.GetFiles(queryDir, "*.json")
                                      .OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var raw = JsonSerializer.Deserialize<RawQueryRecord>(
                    File.ReadAllText(file, Encoding.UTF8), readOptions)
                    ?? throw new InvalidDataException("Null JSON root.");

                // Parse the SQL; graceful fallback when parse fails.
                QueryAbstraction? abstraction = null;
                try
                {
                    var sqlText = raw.Sql ?? string.Empty;
                    var parseResult = SqlParserPipeline.Parse(sqlText);
                    var sqlAst = parseResult.BuildAst(raw.Name ?? name);
                    abstraction = QueryAbstractionBuilder.Build(raw.Name ?? name, sqlAst);
                }
                catch
                {
                    // Parse failure → abstraction stays null → Raw-only Parsed
                }

                var queryNode = QuerySqlProjector.Project(
                    name: raw.Name ?? name,
                    queryTypeName: raw.QueryTypeName ?? "Select",
                    sqlText: raw.Sql ?? string.Empty,
                    abstraction: abstraction,
                    connect: raw.Connect,
                    returnsRecords: raw.ReturnsRecords,
                    odbcTimeout: raw.OdbcTimeout,
                    queryTypeCode: raw.QueryTypeCode);

                exports.Add(queryNode);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"Query export failed for '{name}': {ex.Message}",
                    Code = "QUERY_EXPORT_FAILURE",
                });
            }
        }

        return exports;
    }

    private sealed record RawQueryRecord(
        string? Name,
        int? QueryTypeCode,
        string? QueryTypeName,
        string? Sql,
        string? Connect,
        bool? ReturnsRecords,
        int? OdbcTimeout);

    private sealed record ParsedModuleEntry(
        string Name, ManifestModuleKind Kind, string Category, ResolvedModule Resolved);

    private static string ResolveHarvesterVersion()
    {
        var asm = typeof(Pipeline).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrEmpty(info?.InformationalVersion))
            return info!.InformationalVersion;
        return asm.GetName().Version?.ToString() ?? "0.0.0";
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
