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
        // On-disk category name is "ModulesAndClasses" (matches the
        // SaveAsTextExporter / Translator / VbaSignatureEnricher
        // category strings). The standalone-module category branches
        // off the SaveAsText "VERSION 1.0 CLASS" header below.
        var parsed = new List<ParsedModuleEntry>();
        // Side-list of ModulesAndClasses entries whose VBA parse threw.
        // The kind is still recoverable from the SaveAsText header so a
        // kind-correct stub ModuleExport can surface at step 3, keeping
        // the per-category count invariant
        // (manifest.Modules.length == ir/ModulesAndClasses/*.ir.json).
        var moduleParseFailures =
            new List<(string Name, ManifestModuleKind Kind)>();
        foreach (var category in new[] { "ModulesAndClasses", "Forms", "Reports" })
        {
            var categoryDir = Path.Combine(rawDir, category);
            if (!Directory.Exists(categoryDir)) continue;

            foreach (var file in Directory.GetFiles(categoryDir, "*.txt").OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                // Kind detection is lifted out of the parse try-block so a
                // parse failure can still surface a kind-correct stub
                // below. A file-read failure leaves isClassModule at its
                // category default (false for ModulesAndClasses,
                // true elsewhere) — the worst case is one stub with the
                // standalone-vs-class flag wrong, which is materially
                // better than dropping the module from the manifest.
                bool isClassModule = category != "ModulesAndClasses";
                try
                {
                    var text = File.ReadAllText(file, Encoding.GetEncoding(1252));

                    // Within ModulesAndClasses, class modules carry the
                    // "VERSION 1.0 CLASS" SaveAsText header; standalone
                    // modules do not. Forms / Reports are always class
                    // modules.
                    isClassModule = category != "ModulesAndClasses"
                        || text.Contains("VERSION 1.0 CLASS\r\n", StringComparison.Ordinal)
                        || text.Contains("VERSION 1.0 CLASS\n", StringComparison.Ordinal);

                    var (ast, _) = VbaModuleBodyLoader.LoadAndParse(text);

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
                    if (category == "ModulesAndClasses")
                    {
                        moduleParseFailures.Add((name,
                            isClassModule
                                ? ManifestModuleKind.ClassModule
                                : ManifestModuleKind.Standard));
                    }
                }
            }
        }

        // ── 2. Cross-reference index and classification ────────────────
        var resolvedModules = parsed.Select(p => p.Resolved).ToList();
        var crossRefs = new CrossReferenceIndex(resolvedModules);
        var classifications = FunctionClassificationBuilder.BuildAll(resolvedModules, crossRefs);

        // ── 3. Standalone module exports ───────────────────────────────
        // Emits one ModuleExport per standalone module AND per
        // class module discovered under raw/ModulesAndClasses/.
        // Form / Report class modules surface via formExports /
        // reportExports below, so they are excluded here. Parse-failed
        // modules surface as stub ModuleExports with ParserOutput=null
        // so per-category counts stay equal to the on-disk ir/ tally.
        // The final OrderBy preserves byte-deterministic ordering
        // regardless of which list a given module came from.
        var moduleExports = parsed
            .Where(p => p.Category == "ModulesAndClasses")
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
            .Concat(moduleParseFailures.Select(f => new ModuleExport
            {
                ModuleName = f.Name,
                ModuleKind = f.Kind,
                ParserOutput = null,
            }))
            .OrderBy(m => m.ModuleName, StringComparer.Ordinal)
            .ToList();

        // ── 4. Form and report exports (merge spec + parser output) ────
        var formExports = BuildFormOrReportExports(
            parsed, classifications, irDir, "Forms", diagnostics);
        var reportExports = BuildFormOrReportExports(
            parsed, classifications, irDir, "Reports", diagnostics);

        // ── 4b. Query exports (D14: Sql.Parsed + Sql.Parameters) ──────
        var queryExports = BuildQueryExports(rawDir, diagnostics);

        // ── 4c. Table + Relationship exports (loaded from ir/) ────────
        // Tables and Relationships pass through the kb/ → ir/ pipeline
        // via JsonPassthroughTranslator with no enricher, so the ir/
        // files are byte-equivalent to the typed records and can be
        // deserialized directly.
        var tableExports = BuildTableExports(irDir, diagnostics);
        var relationshipExports = BuildRelationshipExports(irDir, diagnostics);

        // ── 5. Assemble and write ──────────────────────────────────────
        IReadOnlyList<UnresolvedItem> unresolvedItems = Array.Empty<UnresolvedItem>();

        // v1.1 (Session 2 of harvester-manifest-index-and-batch-readiness):
        // build the navigation index AFTER the per-object ir/ files have
        // been written. Counts come straight from the in-memory
        // collections that produced those files, so FileMap.<X>.Count
        // stays equal to manifest.<X>.Length by construction. Globs are
        // static — the on-disk layout is fixed by the three-tier output
        // model. Modules live under ir/ModulesAndClasses/ on disk but
        // surface at manifest.Modules; the glob reflects the on-disk
        // path so a consumer can find the files. Relationships have no
        // Summary count (DatabaseSummary intentionally omits one), but
        // their FileMap entry mirrors the manifest array length —
        // ManifestCompletenessTests pins both invariants.
        var fileMap = BuildFileMap(
            formExports.Count,
            reportExports.Count,
            queryExports.Count,
            moduleExports.Count,
            tableExports.Count,
            relationshipExports.Count);

        var documentation = new DocumentationPointer
        {
            Url = InterpretationGuideUrl,
            SchemaVersion = DatabaseManifest.SchemaVersionLiteral,
        };

        var manifest = DatabaseManifestAggregator.Build(
            database: database,
            harvesterVersion: ResolveHarvesterVersion(),
            forms: formExports,
            reports: reportExports,
            queries: queryExports,
            modules: moduleExports,
            tables: tableExports,
            relationships: relationshipExports,
            unresolvedItems: unresolvedItems,
            diagnostics: diagnostics,
            documentation: documentation,
            fileMap: fileMap);

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

        // v1.1 (Finding #2 — per the human-ratified open decision):
        // emit a tiny human pointer next to manifest.json so a consumer
        // who opens the output folder cold finds the entry point
        // without prior knowledge of the schema. Content is static and
        // byte-identical across runs — no timestamps, no fixture-specific
        // data — so it does not perturb the determinism gate.
        File.WriteAllText(
            Path.Combine(baseDir, "READ-ME-FIRST.md"),
            ReadMeFirstContent);
    }

    private static FileMap BuildFileMap(
        int formsCount,
        int reportsCount,
        int queriesCount,
        int modulesCount,
        int tablesCount,
        int relationshipsCount)
        => new()
        {
            Forms         = new() { Count = formsCount,         Glob = "ir/Forms/*.ir.json" },
            Reports       = new() { Count = reportsCount,       Glob = "ir/Reports/*.ir.json" },
            Queries       = new() { Count = queriesCount,       Glob = "ir/Queries/*.ir.json" },
            // On-disk category is ModulesAndClasses; manifest surfaces it
            // as Modules. Per ManifestCompletenessTests, the glob below
            // reflects the on-disk path so the consumer can actually find
            // the files.
            Modules       = new() { Count = modulesCount,       Glob = "ir/ModulesAndClasses/*.ir.json" },
            Tables        = new() { Count = tablesCount,        Glob = "ir/Tables/*.ir.json" },
            Relationships = new() { Count = relationshipsCount, Glob = "ir/Relationships/*.ir.json" },
        };

    // Public canonical URL of the interpretation guide. Points at the
    // default branch (main); intentionally a public HTTPS URL rather
    // than a relative path so a binary-only consumer (no source repo)
    // can resolve it.
    private const string InterpretationGuideUrl =
        "https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md";

    // READ-ME-FIRST.md content. Static — no timestamps, no fixture
    // data — so the determinism gate stays green. Use LF line endings
    // explicitly (the StringBuilder-free verbatim literal would adopt
    // the source file's line endings, which differ across platforms).
    private static readonly string ReadMeFirstContent =
        string.Join("\n",
            "# Dabbler Access Harvester output",
            "",
            "The canonical entry point for this dataset is `manifest.json`.",
            "",
            "This output conforms to `harvester-schema` version 1.1. The",
            "manifest's `Documentation.Url` field points at the interpretation",
            "guide so a binary-only consumer can read the schema without the",
            "source repo. The manifest's `FileMap` field lists the on-disk",
            "globs for every per-object file under `ir/`.",
            "",
            "Interpretation guide:",
            "https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md",
            "");

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

    // Reads ir/Tables/*.ir.json (byte-equivalent to TableMetadata since
    // the kb/ → ir/ pipeline is a JsonPassthrough with no enricher) and
    // deserialises each into a TableMetadata record.
    private static List<TableMetadata> BuildTableExports(
        string irDir,
        List<HarvesterDiagnostic> diagnostics)
    {
        var dir = Path.Combine(irDir, "Tables");
        var exports = new List<TableMetadata>();
        if (!Directory.Exists(dir)) return exports;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in Directory.GetFiles(dir, "*.ir.json")
                                       .OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(file));
            try
            {
                var record = JsonSerializer.Deserialize<TableMetadata>(
                    File.ReadAllText(file, Encoding.UTF8), options)
                    ?? throw new InvalidDataException("Null JSON root.");
                exports.Add(record);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"Table IR load failed for '{name}': {ex.Message}",
                    Code = "TABLE_IR_LOAD_FAILURE",
                });
            }
        }

        return exports;
    }

    // Reads ir/Relationships/*.ir.json (byte-equivalent to
    // RelationshipMetadata) and deserialises each into a typed record.
    private static List<RelationshipMetadata> BuildRelationshipExports(
        string irDir,
        List<HarvesterDiagnostic> diagnostics)
    {
        var dir = Path.Combine(irDir, "Relationships");
        var exports = new List<RelationshipMetadata>();
        if (!Directory.Exists(dir)) return exports;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in Directory.GetFiles(dir, "*.ir.json")
                                       .OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(file));
            try
            {
                var record = JsonSerializer.Deserialize<RelationshipMetadata>(
                    File.ReadAllText(file, Encoding.UTF8), options)
                    ?? throw new InvalidDataException("Null JSON root.");
                exports.Add(record);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"Relationship IR load failed for '{name}': {ex.Message}",
                    Code = "RELATIONSHIP_IR_LOAD_FAILURE",
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
