using System.Globalization;
using System.Linq;
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
using VbaParseMode = AccessHarvester.Vba.Parser.ParseMode;

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

    public ExtractResult Extract(string databasePath, string outputRoot, bool clean = true)
    {
        string dbName = Path.GetFileNameWithoutExtension(databasePath);
        string baseDir = BaseDir(outputRoot, dbName);
        string rawDir = RawDir(outputRoot, dbName);
        if (clean && Directory.Exists(rawDir)) Directory.Delete(rawDir, recursive: true);
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

    public TranslateResult Translate(string rawDir, string kbDir, bool clean = true)
    {
        if (clean && Directory.Exists(kbDir)) Directory.Delete(kbDir, recursive: true);
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

    public EnrichResult Enrich(string kbDir, string irDir, bool clean = true)
    {
        if (clean && Directory.Exists(irDir)) Directory.Delete(irDir, recursive: true);
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

    public RunResult Run(string databasePath, string outputRoot, bool clean = true)
    {
        var extract = Extract(databasePath, outputRoot, clean);
        string dbName = Path.GetFileNameWithoutExtension(databasePath);
        string kbDir = KbDir(outputRoot, dbName);
        var translate = Translate(extract.RawDir, kbDir, clean);
        string irDir = IrDir(outputRoot, dbName);
        var enrich = Enrich(translate.KbDir, irDir, clean);

        EmitManifestArtifacts(extract.BaseDir, databasePath, extract.RawDir, translate.KbDir, enrich.IrDir);
        EmitRootIndex(outputRoot);

        return new RunResult(
            extract.BaseDir, extract.RawDir, translate.KbDir, enrich.IrDir,
            extract.Categories, translate.Categories, enrich.Categories);
    }

    // V1 manifest emission: parse VBA module text from the raw/ tier,
    // run the resolve + classify chain, project to JSON, and assemble
    // into a DatabaseManifest alongside any form/report specs already
    // built by the ir/ enrichment pass.
    //
    // Module discovery reads *.txt files from raw/ModulesAndClasses/
    // (SaveAsText output) and form/report VBA from the kb tier's
    // VbaCode field (kb/Forms/*.json, kb/Reports/*.json) — a raw
    // form/report .txt is layout DSL with the VBA after a
    // `CodeBehindForm` marker, which the whole-file parse cannot see
    // (006 review F-3-1). Form/report specs are loaded from
    // ir/Forms/*.ir.json and ir/Reports/*.ir.json via
    // FormMetadataExtractor + FormComponentSpecBuilder.
    //
    // Files that fail to parse are logged as HarvesterDiagnostics and
    // skipped rather than aborting the emission — one broken module
    // should not prevent the rest of the manifest from being written.
    // Parses that succeed in a degraded mode (recovery used, or parse
    // diagnostics present) are visible as VBA_PARSE_DEGRADED
    // diagnostics rather than silently shipping a reduced ParserOutput.
    // (Internal, not private, so the hermetic ingestion-path tests can
    // drive manifest emission against a staged raw/kb/ir tree — the
    // method is pure file IO; no Access COM.)
    internal static void EmitManifestArtifacts(
        string baseDir, string databasePath, string rawDir, string kbDir, string irDir)
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
            // Forms and Reports source their VBA from the kb tier's
            // VbaCode field (everything from the SaveAsText
            // `CodeBehindForm` marker onward, captured by
            // AccessTextParser). Feeding the raw .txt whole-file fed
            // layout DSL to the VBA parser and produced zero items for
            // every form/report, silently (006 review F-3-1).
            bool fromKb = category != "ModulesAndClasses";
            var categoryDir = Path.Combine(fromKb ? kbDir : rawDir, category);
            if (!Directory.Exists(categoryDir)) continue;

            var searchPattern = fromKb ? "*.json" : "*.txt";
            foreach (var file in Directory.GetFiles(categoryDir, searchPattern).OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                // Kind detection is lifted out of the parse try-block so a
                // parse failure can still surface a kind-correct stub
                // below. A file-read failure leaves isClassModule at its
                // category default (false for ModulesAndClasses,
                // true elsewhere) — the worst case is one stub with the
                // standalone-vs-class flag wrong, which is materially
                // better than dropping the module from the manifest.
                bool isClassModule = fromKb;
                try
                {
                    string text;
                    if (fromKb)
                    {
                        // A form/report without code-behind has no VbaCode
                        // key in its kb JSON (SaveAsText emitted no
                        // CodeBehindForm section) — nothing to parse, and
                        // its export's ParserOutput stays absent per
                        // omission semantics.
                        var vbaCode = ReadKbVbaCode(file);
                        if (vbaCode is null) continue;
                        // The kb VbaCode starts with the `CodeBehindForm`
                        // marker + `Attribute VB_*` lines, which the
                        // canonical-header recognition in
                        // VbaModuleBodyLoader does not strip.
                        text = VbeHeaderStripper.Strip(vbaCode);
                    }
                    else
                    {
                        // Shared SaveAsText decode (BOM → strict UTF-8 → cp1252),
                        // matching the translate path so the run pipeline agrees on
                        // encoding (010: UTF-8-without-BOM "§" no longer mojibakes).
                        text = SaveAsTextDecoder.ReadText(file);

                        // Within ModulesAndClasses, class modules carry the
                        // "VERSION 1.0 CLASS" SaveAsText header; standalone
                        // modules do not. Forms / Reports are always class
                        // modules.
                        isClassModule =
                            text.Contains("VERSION 1.0 CLASS\r\n", StringComparison.Ordinal)
                            || text.Contains("VERSION 1.0 CLASS\n", StringComparison.Ordinal);
                    }

                    var (ast, parseResult) = VbaModuleBodyLoader.LoadAndParse(text);
                    // ModeUsed reports which parse *strategy* ran — the
                    // pipeline default is Recovering even for a clean
                    // parse — so the degraded signal is the presence of
                    // parse diagnostics (or the lossy fallback), not a
                    // non-Strict mode.
                    if (parseResult.Diagnostics.Count > 0
                        || parseResult.ModeUsed == VbaParseMode.Lossy)
                    {
                        diagnostics.Add(new HarvesterDiagnostic
                        {
                            Severity = HarvesterDiagnosticSeverity.Warning,
                            Message = $"VBA parse degraded for '{name}': "
                                + $"mode={parseResult.ModeUsed}, "
                                + $"diagnostics={parseResult.Diagnostics.Count}",
                            Code = "VBA_PARSE_DEGRADED",
                        });
                    }

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

    // Reads the top-level VbaCode string out of a kb-tier form/report
    // JSON file. Returns null when the object has no code-behind — the
    // translator only writes VbaCode when SaveAsText emitted a
    // CodeBehindForm section.
    private static string? ReadKbVbaCode(string kbJsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(kbJsonPath, Encoding.UTF8));
        return doc.RootElement.TryGetProperty("VbaCode", out var vbaCode)
            && vbaCode.ValueKind == JsonValueKind.String
                ? vbaCode.GetString()
                : null;
    }

    // Cross-DB roll-up index emitted at the AccessMetaData/ root after
    // every Run, so a batch consumer that points at the same --out root
    // for many databases gets a single up-to-date enumeration without
    // having to scan the filesystem itself.
    //
    // Determinism: the index is rebuilt from scratch on every Run by
    // re-scanning <outputRoot>/AccessMetaData/*/manifest.json. Per-DB
    // entries carry only the database name, the relative manifest path,
    // and the schema version the manifest declares — no timestamps, no
    // durations. Entries are sorted by DatabaseName (Ordinal). Manifests
    // that fail to parse are skipped silently rather than poisoning the
    // index for the surviving DBs; the per-DB Run that emitted the bad
    // manifest already surfaced the problem through its own diagnostics.
    //
    // Public so a downstream batch driver (or a hermetic test) can
    // refresh the index without re-running Pipeline.Run against a live
    // .accdb — useful when a DB has been deleted from disk and the
    // surviving index needs to drop the stale entry.
    public static void EmitRootIndex(string outputRoot)
    {
        string rootDir = Path.Combine(outputRoot, "AccessMetaData");
        if (!Directory.Exists(rootDir)) return;

        var entries = new List<RootIndexEntry>();
        foreach (var dbDir in Directory.GetDirectories(rootDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            string manifestPath = Path.Combine(dbDir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            string databaseName;
            string schemaVersion = DatabaseManifest.SchemaVersionLiteral;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
                var root = doc.RootElement;
                databaseName = root.TryGetProperty("Database", out var db)
                    && db.TryGetProperty("Name", out var nameEl)
                    && nameEl.ValueKind == JsonValueKind.String
                        ? nameEl.GetString() ?? Path.GetFileName(dbDir)
                        : Path.GetFileName(dbDir);
                if (root.TryGetProperty("SchemaVersion", out var sv) && sv.ValueKind == JsonValueKind.String)
                    schemaVersion = sv.GetString() ?? schemaVersion;
            }
            catch (JsonException)
            {
                continue;
            }

            // Relative paths use forward slashes regardless of OS so the
            // index is portable to any tool that consumes it.
            string relativeManifest =
                $"{Path.GetFileName(dbDir)}/manifest.json";

            entries.Add(new RootIndexEntry
            {
                Name = databaseName,
                Manifest = relativeManifest,
                SchemaVersion = schemaVersion,
            });
        }

        entries = entries.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();

        var index = new RootIndex
        {
            SchemaType = "AccessMetaDataRootIndex",
            SchemaVersion = "1.0",
            Databases = entries,
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        File.WriteAllText(
            Path.Combine(rootDir, "index.json"),
            JsonSerializer.Serialize(index, options));
    }

    private sealed class RootIndex
    {
        public string SchemaType { get; set; } = "AccessMetaDataRootIndex";
        public string SchemaVersion { get; set; } = "1.0";
        public List<RootIndexEntry> Databases { get; set; } = new();
    }

    private sealed class RootIndexEntry
    {
        public string Name { get; set; } = "";
        public string Manifest { get; set; } = "";
        public string SchemaVersion { get; set; } = "";
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
            "## AI consumers: read the workflow first",
            "",
            "If you are an AI agent consuming this output, do NOT hand-derive",
            "an object inventory. Read section 3.1 \"The canonical consumer",
            "workflow\" in the interpretation guide first: after `run`, the",
            "`identify-usage` and `identify-cleanup` subcommands already",
            "classify every object (with confidence scores and clarifying",
            "questions) and flag stale ones, writing `object-use.json` /",
            "`object-cleanup.json` plus `*-checklist.json` review files for a",
            "developer to confirm. Point those tools' `--in` at this",
            "per-database directory (the one that contains `ir/`) — not the",
            "output root.",
            "",
            "Interpretation guide (start at section 3.1):",
            "https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md",
            "",
            "## Developer review: opening the checklists",
            "",
            "The `*-checklist.json` files are the human-review surface. When you",
            "present them, do NOT hand over raw JSON — tell the developer what",
            "each file is, what to do, and which zero-install editor opens it",
            "(interpretation guide section 3.3 is the mandatory instruction):",
            "",
            "- `object-use-checklist.json` and `object-cleanup-checklist.json`",
            "  open in the uat-checklist-editor (hosted, zero-install):",
            "  https://darndestdabbler.github.io/uat-checklist-editor/",
            "- `classification.json` (from `classify` / `sanitize`) opens in the",
            "  classification-editor — not hosted yet; the developer downloads",
            "  and opens `tools/classification-editor.html` locally.",
            "",
            "Plain-language developer guide for the review step:",
            "https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/reviewing-the-feedback-artifacts.md",
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

        // Load form specs from ir/<category>/*.ir.json. A missing directory means
        // zero ir files — fall through to the FORM_SPEC_MISSING diff below so a
        // parsed-but-unenriched module is still reported (F-1-4), rather than
        // early-returning and silently dropping every parser output.
        var irCategoryDir = Path.Combine(irDir, category);
        var irFiles = Directory.Exists(irCategoryDir)
            ? Directory.GetFiles(irCategoryDir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

        var exports = new List<FormExport>();
        var irNamesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var irFile in irFiles)
        {
            // ir/Forms/frmSomething.ir.json → name = "frmSomething"
            var irName = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(irFile));
            irNamesSeen.Add(irName);
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

        AddMissingSpecDiagnostics(parserOutputs.Keys, irNamesSeen, category, diagnostics);

        return exports;
    }

    // Honour the contract BuildFormOrReportExports' header promises: a parsed module
    // with no matching ir/<category>/*.ir.json (its enrichment failed, so it exists in
    // raw/ but not ir/) is dropped from the export set — record the skip so a manifest
    // consumer sees a Diagnostics[] entry rather than a silently shrunk object set
    // (F-1-4). Works whether the whole ir/<category> directory was absent (irNamesSeen
    // empty → every parsed module is reported) or only some specs were missing.
    // Deterministic Ordinal order for the determinism gate. Internal for unit testing.
    internal static void AddMissingSpecDiagnostics(
        IEnumerable<string> parserOutputNames,
        ISet<string> irNamesSeen,
        string category,
        List<HarvesterDiagnostic> diagnostics)
    {
        foreach (var missing in parserOutputNames
                     .Where(n => !irNamesSeen.Contains(n))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            diagnostics.Add(new HarvesterDiagnostic
            {
                Severity = HarvesterDiagnosticSeverity.Warning,
                Message = $"Parser output for {category} '{missing}' has no matching ir/{category}/{missing}.ir.json; the module was parsed but its enriched spec is absent, so it is omitted from the manifest.",
                Code = "FORM_SPEC_MISSING",
            });
        }
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

                // Parse the SQL; graceful fallback when parse fails. A parse
                // failure still ships the query (Raw-only Parsed), but no
                // longer silently — F-3-6: the bare catch here previously
                // erased the fact that a query downgraded to Raw-only, so a
                // consumer could not tell a Raw-only query apart from one the
                // parser never attempted.
                QueryAbstraction? abstraction = null;
                try
                {
                    var sqlText = raw.Sql ?? string.Empty;
                    var parseResult = SqlParserPipeline.Parse(sqlText);
                    var sqlAst = parseResult.BuildAst(raw.Name ?? name);
                    abstraction = QueryAbstractionBuilder.Build(raw.Name ?? name, sqlAst);
                }
                catch (Exception ex)
                {
                    // Parse failure → abstraction stays null → Raw-only Parsed,
                    // recorded as a diagnostic so the downgrade is visible.
                    diagnostics.Add(new HarvesterDiagnostic
                    {
                        Severity = HarvesterDiagnosticSeverity.Warning,
                        Message = $"Query SQL parse failed for '{raw.Name ?? name}'; "
                            + $"shipping Raw-only Sql.Parsed: {ex.Message}",
                        Code = "QUERY_SQL_PARSE_FAILURE",
                    });
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

    // Manifest provenance is sourced from the single canonical helper on
    // DatabaseManifest so the version stamped into manifest.json matches
    // exactly what the CLI's --json-status envelope and --version surface
    // emit for the same run.
    private static string ResolveHarvesterVersion()
        => DatabaseManifest.ResolveHarvesterVersion();
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
