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
using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using VbaReferenceResolver = AccessHarvester.Vba.Resolver.References.ReferenceResolver;
using AccessHarvester.Vba.Resolver.Scopes;
using ManifestModuleKind = AccessHarvester.Pipeline.Manifest.ModuleKind;
using ResolverModuleKind = AccessHarvester.Vba.Resolver.Scopes.ModuleKind;
using VbaParseMode = AccessHarvester.Vba.Parser.ParseMode;
using VbaModule = AccessHarvester.Vba.Parser.Ast.Module;
using ParserProcedure = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

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
            SwitchboardExporter.Instance,
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
            JsonPassthroughTranslator.Navigation,
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

        // F-3-4: one Access object-model registry, built once from the
        // already-enriched ir/ form/report metadata and shared across the
        // whole resolve pass. It supplies Me/bare-control resolution and
        // member-chain walks (Forms!/Reports!/subform navigation). The
        // prior code constructed the resolver with NO registry, so every
        // form/report Me.<control> reference fell to Unresolved.
        var accessRegistry = BuildAccessObjectModelRegistry(irDir);

        // F-3-4: ONE ProjectScope shared across every module so the
        // resolver's cross-module Public lookup (ReferenceResolver
        // Step 1.5, which walks project.Children for sibling modules) can
        // fire. The prior code built `new ProjectScope()` INSIDE the
        // per-module loop, making each module an only child — so a bare
        // call to another module's Public proc could never resolve. This
        // mirrors SymbolDbBuilder's shared-ProjectScope pattern, the
        // correct wiring already proven in-repo.
        var project = new ProjectScope();

        // Pass 1: parse + scope-build every module INTO the shared
        // project. Resolution is deferred to pass 2 (below) so that every
        // sibling module's scope is registered in `project` before any
        // module is resolved — otherwise the first module would resolve
        // against a project that does not yet contain its siblings.
        var pendingModules = new List<PendingModule>();
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

                    // F-3-4: Form/Report modules get an owningClass so the
                    // resolver can bind `Me`, `Me.<control>`, and bare
                    // control names through the Access registry. The
                    // owningClass name is the module name (= the kb file
                    // name = FormMetadata.Name), which the registry's
                    // by-Name index resolves controls against. Standard
                    // and standalone-class modules have no host
                    // form/report context, so owningClass stays null
                    // (scoped to Form/Report per the 006 recommendation).
                    var owningClass = resolverKind switch
                    {
                        ResolverModuleKind.Form =>
                            NewOwningClass(name, ClassDeclarationKind.FormModule),
                        ResolverModuleKind.Report =>
                            NewOwningClass(name, ClassDeclarationKind.ReportModule),
                        _ => null,
                    };

                    var visitor = new ScopeBuildingVisitor();
                    var scope = visitor.BuildModuleScope(
                        project, ast, name, resolverKind, owningClass);
                    pendingModules.Add(
                        new PendingModule(name, manifestKind, category, ast, scope));
                }
                catch (Exception ex)
                {
                    // Degradation contract under the shared ProjectScope:
                    // the failure here is a PARSE failure — LoadAndParse
                    // throws (the F-3-2 AstBuilder NRE class) BEFORE
                    // BuildModuleScope runs, so no ModuleScope is created
                    // and nothing is attached to `project`. (A scope-build
                    // exception cannot leak a usable partial scope either:
                    // ScopeBuildingVisitor's only post-`new ModuleScope`
                    // throws are the LocalDeclaration/LocalConstant
                    // "outside a procedure scope" guards, which are
                    // unreachable from a parser-produced module AST —
                    // Statements are not ModuleItems, so a local
                    // declaration can only be walked inside a ProcedureScope
                    // where CurrentProcedureScope is non-null.) So a
                    // degraded module contributes no sibling-visible
                    // symbols, matching the prior per-module-isolation
                    // behavior.
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

        // Pass 2: resolve every parsed module against the shared project +
        // registry. Cross-module Public lookup, Me/control resolution, and
        // member-chain walks all fire now that `project` holds every
        // sibling scope and the resolver carries the Access registry
        // (F-3-4). A resolution failure degrades that one module — it
        // keeps a kind-correct stub for ModulesAndClasses, same as a parse
        // failure — rather than aborting the whole manifest. The degraded
        // module's scope was already FULLY built in pass 1, so its Public
        // declarations remain visible to sibling modules; that is correct
        // (those symbols genuinely exist in source — a sibling's bare call
        // to the module resolves accurately even though the module's own
        // ParserOutput is a stub), not a leak.
        foreach (var pm in pendingModules)
        {
            try
            {
                var resolved = new VbaReferenceResolver(accessRegistry)
                    .Resolve(pm.Scope, pm.Ast);
                parsed.Add(new ParsedModuleEntry(pm.Name, pm.Kind, pm.Category, resolved));
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"VBA resolution failed for '{pm.Name}': {ex.Message}",
                    Code = "VBA_RESOLVE_FAILURE",
                });
                if (pm.Category == "ModulesAndClasses")
                    moduleParseFailures.Add((pm.Name, pm.Kind));
            }
        }

        // ── 2. Cross-reference index and classification ────────────────
        var resolvedModules = parsed.Select(p => p.Resolved).ToList();
        var crossRefs = new CrossReferenceIndex(resolvedModules);
        var classifications = FunctionClassificationBuilder.BuildAll(resolvedModules, crossRefs);

        // ── 2b. Navigation graph (F-2-2 + 012 R2-1 / DW-9) ─────────────
        // Build the navigation graph off the SAME resolvedModules + crossRefs
        // seam that feeds FunctionClassificationBuilder above (006 F-2-2:
        // "the seam already exists"). This surfaces the VBA-side navigation
        // edges — DoCmd.OpenForm / OpenReport / Close / BrowseTo in form,
        // report, and standard-module code-behind — and the dynamic
        // (non-literal / missing-target) dispatches as unresolved targets.
        //
        // 012 S1 (DW-9) retires the former inputs=null: the SwitchboardExporter
        // now materialises the switchboard *rows* (in NO harvested artifact
        // before) into raw/Navigation, the macro IR yields MacroChains, and the
        // StartUpForm DAO property yields the default form. Those populate the
        // already-modelled NavigationGraphInputs so the graph emits the
        // Switchboard tree, ordered EntryPoints, and Macros. Ribbon stays null
        // until a real-ribbon corpus case (DW-9 ribbon note). DW-10 (R2-2 chain
        // derivation) remains for sessions 2-3.
        var navigationInputs = BuildNavigationInputs(kbDir, irDir, diagnostics);
        var navigationGraph = NavigationGraphBuilder.Build(resolvedModules, crossRefs, navigationInputs);

        // 012 S2 (DW-10 / R2-2): derive the cross-object navigation CHAIN
        // edges — launcher → report (embedded macros), report/form →
        // record-source query/table, and query parameter suppliers — over
        // the IR surfaces the pipeline just wrote. Pure, no VBA (the
        // code-behind hops are the S3 producer's job). Attached to the
        // already-built navigation graph so the VBA-side Edges are untouched.
        var chainResult = NavigationChainBuilder.Build(
            AccessHarvester.Pipeline.UsageIdentification.IrLoader.Load(Path.GetDirectoryName(irDir)!));
        navigationGraph = navigationGraph with
        {
            ChainEdges = chainResult.Edges,
            UnresolvedChainTargets = chainResult.UnresolvedTargets,
        };

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
        // F-2-2: BuildFormOrReportExports now also runs the Phase 6.3–6.5
        // enrichers per object (EventHandlerClassifier / ValidationRuleExtractor
        // / RowSourceSpecBuilder, stitched by EnrichmentPipelineRunner): it
        // populates the per-reference Classification slots on the emitted
        // FormComponentSpec and accumulates the run-wide enrichment arrays.
        var formResult = BuildFormOrReportExports(
            parsed, classifications, irDir, "Forms", diagnostics);
        var reportResult = BuildFormOrReportExports(
            parsed, classifications, irDir, "Reports", diagnostics);
        var formExports = formResult.Exports;
        var reportExports = reportResult.Exports;

        // ── 4b. Query exports (D14: Sql.Parsed + Sql.Parameters) ──────
        var queryExports = BuildQueryExports(rawDir, diagnostics);

        // ── 4c. Table + Relationship exports (loaded from ir/) ────────
        // Tables and Relationships pass through the kb/ → ir/ pipeline
        // via JsonPassthroughTranslator with no enricher, so the ir/
        // files are byte-equivalent to the typed records and can be
        // deserialized directly.
        var tableExports = BuildTableExports(irDir, diagnostics);
        var relationshipExports = BuildRelationshipExports(irDir, diagnostics);

        // ── 4d. Run-wide enrichment arrays (F-2-2) ─────────────────────
        // ValidationRules unions all three ValidationRuleExtractor entry
        // points: table-field / table-level rules (ExtractFromTable), plus
        // the form/report control + BeforeUpdate-handler rules accumulated
        // by the per-object EnrichmentPipelineRunner pass above. Order is
        // deterministic: tables (ir order) → forms (ir order) → reports
        // (ir order); each source is internally deterministic and the
        // manifest aggregator preserves caller order.
        var validationRules = new List<ValidationRule>();
        foreach (var table in tableExports)
            validationRules.AddRange(ValidationRuleExtractor.ExtractFromTable(table));
        validationRules.AddRange(formResult.ValidationRules);
        validationRules.AddRange(reportResult.ValidationRules);

        var rowSourceSpecs = new List<RowSourceSpec>();
        rowSourceSpecs.AddRange(formResult.RowSourceSpecs);
        rowSourceSpecs.AddRange(reportResult.RowSourceSpecs);

        var eventHandlerClassifications = new List<EventHandlerClassification>();
        eventHandlerClassifications.AddRange(formResult.EventHandlerClassifications);
        eventHandlerClassifications.AddRange(reportResult.EventHandlerClassifications);

        // ── 5. Assemble and write ──────────────────────────────────────
        // F-2-2: aggregate unresolved items from the navigation graph's
        // unresolved targets + the per-procedure unresolved references the
        // function-classification pass collected. This is exactly the
        // aggregation the proven hermetic EndToEndFixtureTests exercise.
        // Resolver diagnostics (ResolvedModule.Diagnostics) are not fed
        // separately: the same unresolved references already surface via
        // FunctionClassification.UnresolvedReferences, so adding them would
        // double-emit one construct under two UnresolvedSource values.
        var unresolvedItems = UnresolvedItemAggregator.Build(
            navigationGraph: navigationGraph,
            functionClassifications: classifications);

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
            validationRules: validationRules,
            rowSourceSpecs: rowSourceSpecs,
            functionClassifications: classifications,
            eventHandlerClassifications: eventHandlerClassifications,
            navigationGraph: navigationGraph,
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
    private static FormReportExports BuildFormOrReportExports(
        List<ParsedModuleEntry> parsed,
        IReadOnlyList<FunctionClassification> classifications,
        string irDir,
        string category,
        List<HarvesterDiagnostic> diagnostics)
    {
        // Build parser-output map: form name → JsonNode, and a parallel
        // resolved-module map so the F-2-2 enrichment pass can recover each
        // object's parsed VBA procedures (for EventHandlerClassifier /
        // BeforeUpdate validation extraction) by name.
        var parserOutputs = new Dictionary<string, System.Text.Json.Nodes.JsonNode>(
            StringComparer.OrdinalIgnoreCase);
        var resolvedByName = new Dictionary<string, ResolvedModule>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var p in parsed.Where(e => e.Category == category))
        {
            var output = ParserOutputProjector.Project(
                p.Resolved, classifications, p.Name, p.Kind);
            parserOutputs[p.Name] = output;
            resolvedByName[p.Name] = p.Resolved;
        }

        // F-2-2 run-wide enrichment accumulators (this category only; the
        // caller unions Forms + Reports). The per-object EnrichmentPipelineRunner
        // pass below appends to these in deterministic ir-file order.
        var validationRules = new List<ValidationRule>();
        var rowSourceSpecs = new List<RowSourceSpec>();
        var eventHandlerClassifications = new List<EventHandlerClassification>();

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

                // F-2-2: run the Phase 6.3–6.5 enrichers for this object.
                // EnrichmentPipelineRunner stitches EventHandlerClassifier
                // (event-handler categories), ValidationRuleExtractor
                // (design-time + BeforeUpdate-handler rules), and
                // RowSourceSpecBuilder (combo/list row sources + cascading
                // drivers), returning an enriched spec whose EventHandler /
                // ValidationRule references carry their Classification slots
                // populated. Procedures come from the object's resolved
                // module (null/empty when it has no code-behind, in which
                // case only design-time validation rules are produced).
                IReadOnlyList<ParserProcedure> procedures =
                    resolvedByName.TryGetValue(irName, out var resolvedModule)
                        ? resolvedModule.Ast.Items.OfType<ParserProcedure>().ToList()
                        : Array.Empty<ParserProcedure>();
                var enrichment = EnrichmentPipelineRunner.Run(spec, metadata, procedures);
                validationRules.AddRange(enrichment.ValidationRules);
                rowSourceSpecs.AddRange(enrichment.RowSourceSpecs);
                eventHandlerClassifications.AddRange(enrichment.EventHandlerClassifications);

                parserOutputs.TryGetValue(irName, out var parserOutput);
                exports.Add(new FormExport { Spec = enrichment.EnrichedSpec, ParserOutput = parserOutput });
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

        return new FormReportExports(
            exports, validationRules, rowSourceSpecs, eventHandlerClassifications);
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

    // 012 S1 (DW-9): assembles the NavigationGraphInputs from the
    // materialised navigation chrome — the switchboard row dump
    // (kb/Navigation/switchboard-items.json), the macro IR (ir/Macros),
    // and the startup form (kb/Navigation/startup-options.json). Returns
    // null when no navigation inputs exist at all, so the builder leaves
    // the top-level surfaces null exactly as before this set landed.
    // Ribbon stays null (no USysRibbons producer; deferred).
    internal static NavigationGraphInputs? BuildNavigationInputs(
        string kbDir, string irDir, List<HarvesterDiagnostic> diagnostics)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        SwitchboardStructure? switchboard = null;
        var switchboardFile = Path.Combine(kbDir, "Navigation", "switchboard-items.json");
        if (File.Exists(switchboardFile))
        {
            try
            {
                var artifact = JsonSerializer.Deserialize<SwitchboardItemsArtifact>(
                    File.ReadAllText(switchboardFile, Encoding.UTF8), options);
                switchboard = SwitchboardStructureBuilder.Build(artifact);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"Switchboard items load failed: {ex.Message}",
                    Code = "SWITCHBOARD_LOAD_FAILURE",
                });
            }
        }

        string? defaultForm = null;
        var startupFile = Path.Combine(kbDir, "Navigation", "startup-options.json");
        if (File.Exists(startupFile))
        {
            try
            {
                var startup = JsonSerializer.Deserialize<StartupOptionsArtifact>(
                    File.ReadAllText(startupFile, Encoding.UTF8), options);
                defaultForm = startup?.StartUpForm;
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"Startup options load failed: {ex.Message}",
                    Code = "STARTUP_OPTIONS_LOAD_FAILURE",
                });
            }
        }

        var macros = MacroChainBuilder.Build(Path.Combine(irDir, "Macros"));

        // Nothing to supply -> null inputs (graph keeps VBA-side edges only).
        if (switchboard is null && macros.Count == 0 && string.IsNullOrEmpty(defaultForm))
            return null;

        return new NavigationGraphInputs
        {
            Ribbon = null,
            Switchboard = switchboard,
            Macros = macros,
            DefaultFormName = defaultForm,
        };
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

    // F-2-2: carrier for BuildFormOrReportExports' two outputs — the
    // per-object FormExport list (unchanged) plus the run-wide enrichment
    // arrays the per-object EnrichmentPipelineRunner pass accumulated. The
    // caller unions the Forms and Reports results before handing them to
    // DatabaseManifestAggregator.
    private sealed record FormReportExports(
        List<FormExport> Exports,
        List<ValidationRule> ValidationRules,
        List<RowSourceSpec> RowSourceSpecs,
        List<EventHandlerClassification> EventHandlerClassifications);

    // Carrier across the F-3-4 two-pass split: pass 1 parses + scope-builds
    // into the shared ProjectScope and stores the AST + scope here; pass 2
    // resolves each against the now-complete project + Access registry.
    private sealed record PendingModule(
        string Name, ManifestModuleKind Kind, string Category, VbaModule Ast, ModuleScope Scope);

    // F-3-4: build the Access object-model registry from the enriched
    // ir/ form/report metadata so the resolver can bind Me/control
    // references in form and report code-behind. Best-effort per file —
    // a form/report whose ir JSON cannot be read is skipped (its Me
    // references simply stay Unresolved) rather than failing the whole
    // manifest; the same metadata is re-read with explicit diagnostics in
    // BuildFormOrReportExports, so a load failure is not silently lost.
    private static IAccessObjectModelRegistry BuildAccessObjectModelRegistry(string irDir)
    {
        var forms = new List<FormMetadata>();
        foreach (var (name, json) in ReadIrJsonFiles(irDir, "Forms"))
        {
            try { forms.Add(FormMetadataExtractor.Extract(name, json)); }
            catch { /* surfaced as FORM_SPEC_LOAD_FAILURE in BuildFormOrReportExports */ }
        }

        var reports = new List<ReportMetadata>();
        foreach (var (name, json) in ReadIrJsonFiles(irDir, "Reports"))
        {
            try { reports.Add(ReportMetadataExtractor.Extract(name, json)); }
            catch { /* surfaced as FORM_SPEC_LOAD_FAILURE in BuildFormOrReportExports */ }
        }

        return new AccessObjectModelRegistry(forms, reports, new TypeLibraryRegistry());
    }

    private static IEnumerable<(string Name, string Json)> ReadIrJsonFiles(
        string irDir, string category)
    {
        var dir = Path.Combine(irDir, category);
        if (!Directory.Exists(dir)) yield break;
        foreach (var file in Directory.GetFiles(dir, "*.ir.json")
                                      .OrderBy(f => f, StringComparer.Ordinal))
        {
            // ir/Forms/frmFoo.ir.json → "frmFoo" (strip .ir then .json).
            var name = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(file));
            yield return (name, File.ReadAllText(file, Encoding.UTF8));
        }
    }

    // F-3-4: a minimal owningClass for a Form/Report module so the
    // resolver has a class context for Me/bare-control binding. The
    // declaration's provenance is never emitted — a degenerate
    // (0,0)-(0,1) range mirrors the resolver's own integration tests.
    private static ClassDeclaration NewOwningClass(string name, ClassDeclarationKind kind)
        => new(
            name,
            new SourceDeclarationProvenance(
                new AccessHarvester.Vba.Parser.Ast.SourceRange(
                    new AccessHarvester.Vba.Parser.Ast.SourceLocation("", 0, 0),
                    new AccessHarvester.Vba.Parser.Ast.SourceLocation("", 0, 1))),
            kind,
            Array.Empty<AccessHarvester.Vba.Parser.Ast.TypeReference>());

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
