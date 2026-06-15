using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Cli.Commands;
using AccessHarvester.Core;
using AccessHarvester.Core.Abstractions;
using AccessHarvester.Core.Com;
using AccessHarvester.Pipeline.Validation;
using AccessHarvester.Pipeline.DocGen;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using CorePipeline = AccessHarvester.Core.Pipeline;

[assembly: SupportedOSPlatform("windows")]

namespace AccessHarvester.Cli;

public static class Program
{
    // Per-command accepted-option sets (006 O-1-4). Each Run* method passes its
    // own value-option and flag-option names to the shared OptionParser so an
    // unknown --switch is an honest usage error (exit 1) rather than a silently
    // ignored token. These preserve each command's exact accepted-option set —
    // the consolidation adds unknown-switch rejection, it does not change which
    // options a command accepts.
    private static readonly string[] ExtractValueOptions   = ["db", "out", "types"];
    private static readonly string[] ExtractFlagOptions    = ["json-status", "no-clean"];
    private static readonly string[] TranslateValueOptions = ["in", "out", "types"];
    private static readonly string[] TranslateFlagOptions  = ["json-status", "no-clean"];
    private static readonly string[] RunValueOptions       = ["db", "out", "types"];
    private static readonly string[] RunFlagOptions        = ["json-status", "no-clean"];
    private static readonly string[] GenerateDocsValueOptions = ["in", "out"];
    private static readonly string[] GenerateDocsFlagOptions  = [];
    private static readonly string[] ValidateValueOptions  = ["in", "manifest"];
    private static readonly string[] ValidateFlagOptions   = ["json-status"];
    private static readonly string[] ParseValueOptions     = ["in"];
    private static readonly string[] ParseFlagOptions      = [];

    // The single subcommand table (006 O-1-7). Both the dispatch switch and the
    // bare-file-mode exclusion test derive from this one map, so adding a
    // subcommand can no longer drift the two out of sync (a missed bare-file
    // entry used to make `AccessHarvester <newcmd>` fall into implicit parse).
    private static readonly IReadOnlyDictionary<string, Func<string[], int>> Commands =
        new Dictionary<string, Func<string[], int>>(StringComparer.OrdinalIgnoreCase)
        {
            ["extract"]            = RunExtract,
            ["translate"]          = RunTranslate,
            ["run"]                = RunRun,
            ["parse"]              = RunParse,
            ["generate-docs"]      = RunGenerateDocs,
            ["validate"]           = RunValidate,
            ["identify-usage"]     = IdentifyUsageCommand.Run,
            ["identify-cleanup"]   = IdentifyCleanupCommand.Run,
            ["classify"]           = ClassifyCommand.Run,
            ["sanitize"]           = SanitizeCommand.Run,
            ["report-baseline"]    = ReportBaselineCommand.Run,
            ["generate-migration"] = GenerateMigrationCommand.Run,
        };

    private static readonly JsonSerializerOptions StatusJsonOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        // Required for Encoding.GetEncoding(1252) on .NET Core / .NET 5+.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        int exitCode;
        try
        {
            exitCode = Dispatch(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Usage error: {ex.Message}");
            PrintUsage();
            exitCode = 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            exitCode = 2;
        }

        return FinalizeExit(exitCode);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    /// <summary>
    /// Hard-exits the process when, and only when, this run activated an Access
    /// COM apartment. Such a run otherwise wedges in native process shutdown
    /// (consistent with COM apartment un-initialization following the Access
    /// interop) AFTER all classification work and output files are written and
    /// the exact <c>msaccess.exe</c> child has been gracefully reaped — so the
    /// process never terminates and a downstream consumer's <c>classify</c>
    /// (or any other Access-backed subcommand) hangs indefinitely. Root cause
    /// and the validating probes (Environment.Exit still wedges;
    /// <c>TerminateProcess</c> exits cleanly in ~7&#160;s with the right exit
    /// code and no zombie) are documented in
    /// <c>docs/session-sets/harvester-fix-classify-timeout/root-cause-findings.md</c>.
    /// </summary>
    /// <remarks>
    /// Scope: the guard fires only on <see cref="AccessComSession.ActivatedInThisProcess"/>,
    /// i.e. exactly the COM-activating command paths — NOT unconditionally for
    /// every subcommand (parse / validate / translate / generate-docs and any
    /// usage-error early return take the normal managed return path). The CLI's
    /// only buffered output sinks are stdout/stderr; both are flushed before the
    /// hard exit, and all artifact files are written synchronously by the
    /// command before it returns, so skipping finalizers loses nothing.
    /// </remarks>
    private static int FinalizeExit(int exitCode)
    {
        if (!AccessComSession.ActivatedInThisProcess)
            return exitCode;

        Console.Out.Flush();
        Console.Error.Flush();
        TerminateProcess(GetCurrentProcess(), unchecked((uint)exitCode));
        return exitCode; // unreachable: TerminateProcess does not return.
    }

    private static int Dispatch(string[] args)
    {
        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (IsVersionFlag(args[0]))
        {
            PrintVersion();
            return 0;
        }

        // Bare-file mode: if the sole argument is not a known subcommand,
        // not a help flag, and not an option flag, treat it as an implicit
        // `parse <file>` call so the harness contract `<exe> <file>` works
        // without a subcommand prefix. The known-command exclusion is taken
        // from the single Commands table (O-1-7) — more robust than a
        // dot/separator heuristic, handles extensionless file paths, and can
        // no longer drift from the dispatch switch.
        if (args.Length == 1
            && !args[0].StartsWith("--", StringComparison.Ordinal)
            && !IsHelpFlag(args[0])
            && !IsVersionFlag(args[0])
            && !Commands.ContainsKey(args[0]))
        {
            return RunParse(args);
        }

        var rest = args.Skip(1).ToArray();
        return Commands.TryGetValue(args[0], out var handler)
            ? handler(rest)
            : UnknownCommand(args[0]);
    }

    private static int RunExtract(string[] args)
    {
        var opts = OptionParser.Parse(args, ExtractValueOptions, ExtractFlagOptions);
        string db = Path.GetFullPath(Require(opts, "db"));
        string outDir = Path.GetFullPath(Require(opts, "out"));
        bool clean = !HasNoClean(opts);
        var pipeline = BuildPipeline(opts);

        var startedAt = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = pipeline.Extract(db, outDir, clean);
        sw.Stop();

        Console.Error.WriteLine($"Extracted to: {result.RawDir}");
        ReportExtract(result.Categories);

        int exit = AnyFailures(result.Categories) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "extract",
                HarvesterVersion = ResolveHarvesterVersion(),
                StartedAt = startedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                DurationMs = sw.ElapsedMilliseconds,
                Database = db,
                OutputRoot = outDir,
                BaseDir = result.BaseDir,
                RawDir = result.RawDir,
                Clean = clean,
                ExitCode = exit,
                Extract = SummarizeExtract(result.Categories),
            });
        }
        return exit;
    }

    private static int RunTranslate(string[] args)
    {
        var opts = OptionParser.Parse(args, TranslateValueOptions, TranslateFlagOptions);
        string rawDir = Path.GetFullPath(Require(opts, "in"));
        string kbDir = Path.GetFullPath(Require(opts, "out"));
        bool clean = !HasNoClean(opts);
        var pipeline = BuildPipeline(opts);

        var startedAt = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = pipeline.Translate(rawDir, kbDir, clean);
        sw.Stop();

        Console.Error.WriteLine($"Translated to: {result.KbDir}");
        ReportTranslate(result.Categories);

        int exit = AnyFailures(result.Categories) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "translate",
                HarvesterVersion = ResolveHarvesterVersion(),
                StartedAt = startedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                DurationMs = sw.ElapsedMilliseconds,
                RawDir = rawDir,
                KbDir = kbDir,
                Clean = clean,
                ExitCode = exit,
                Translate = SummarizeTranslate(result.Categories),
            });
        }
        return exit;
    }

    private static int RunRun(string[] args)
    {
        var opts = OptionParser.Parse(args, RunValueOptions, RunFlagOptions);
        string db = Path.GetFullPath(Require(opts, "db"));
        string outDir = Path.GetFullPath(Require(opts, "out"));
        bool clean = !HasNoClean(opts);
        var pipeline = BuildPipeline(opts);

        var startedAt = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = pipeline.Run(db, outDir, clean);
        sw.Stop();

        Console.Error.WriteLine($"Raw: {result.RawDir}");
        Console.Error.WriteLine($"KB:  {result.KbDir}");
        Console.Error.WriteLine($"IR:  {result.IrDir}");
        ReportExtract(result.Extract);
        ReportTranslate(result.Translate);
        ReportEnrich(result.Enrich);

        int exit = (AnyFailures(result.Extract) || AnyFailures(result.Translate) || AnyFailures(result.Enrich)) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "run",
                HarvesterVersion = ResolveHarvesterVersion(),
                StartedAt = startedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                DurationMs = sw.ElapsedMilliseconds,
                Database = db,
                OutputRoot = outDir,
                BaseDir = result.BaseDir,
                RawDir = result.RawDir,
                KbDir = result.KbDir,
                IrDir = result.IrDir,
                Clean = clean,
                ExitCode = exit,
                Extract = SummarizeExtract(result.Extract),
                Translate = SummarizeTranslate(result.Translate),
                Enrich = SummarizeEnrich(result.Enrich),
            });
        }
        return exit;
    }

    private static string ResolveHarvesterVersion() => DatabaseManifest.ResolveHarvesterVersion();

    private static CorePipeline BuildPipeline(Dictionary<string, string> opts)
    {
        var pipeline = CorePipeline.Default();
        if (opts.TryGetValue("types", out var types))
        {
            var cats = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // 006 F-1-3: --types values are otherwise passed straight into the pure
            // WithCategories set filter, so a typo'd or stale category name matches
            // nothing and the run reports "Ok: 0, Failed: 0" and exits 0 — a silent
            // empty success the exit-code-keying consumer reads as "all objects
            // processed." Reject any unknown value (and an all-filtered-out result)
            // as a usage error (the existing ArgumentException → exit-1 path) so the
            // failure is honest.
            var known = pipeline.KnownCategories;
            var knownSet = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
            var unknown = cats.Where(c => !knownSet.Contains(c)).ToArray();
            if (unknown.Length > 0)
                throw new ArgumentException(
                    $"Unknown --types value(s): {string.Join(", ", unknown)}. "
                    + $"Valid categories: {string.Join(", ", known)}.");

            var filtered = pipeline.WithCategories(cats);
            if (filtered.IsEmpty)
                throw new ArgumentException(
                    $"--types selected no categories to process. "
                    + $"Valid categories: {string.Join(", ", known)}.");
            pipeline = filtered;
        }
        return pipeline;
    }

    // ── generate-docs subcommand ────────────────────────────────────────────────
    // Generates a markdown schema summary from a DatabaseManifest JSON file.
    //
    // Usage: AccessHarvester generate-docs --in <manifest.json> --out <output-dir>
    //
    // Writes: <output-dir>/<database-slug>/schema-summary.md
    // Exit codes: 0 = success, 1 = usage error, 2 = unexpected error.
    private static int RunGenerateDocs(string[] args)
    {
        var opts = OptionParser.Parse(args, GenerateDocsValueOptions, GenerateDocsFlagOptions);
        string manifestFile = Path.GetFullPath(Require(opts, "in"));
        string outDir = Path.GetFullPath(Require(opts, "out"));

        string manifestJson = File.ReadAllText(manifestFile, System.Text.Encoding.UTF8);

        // Derive the slug from the database name encoded in the manifest.
        using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
        string dbName = "(output)";
        if (doc.RootElement.TryGetProperty("Database", out var db) && db.TryGetProperty("Name", out var nameEl))
            dbName = nameEl.GetString() ?? dbName;
        string slug = System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileNameWithoutExtension(dbName), @"[^\w-]", "-").ToLowerInvariant();

        string targetDir = Path.Combine(outDir, slug);
        Directory.CreateDirectory(targetDir);
        string targetFile = Path.Combine(targetDir, "schema-summary.md");

        var generator = new SchemaDocGenerator();
        string markdown = generator.Generate(manifestJson);
        File.WriteAllText(targetFile, markdown, System.Text.Encoding.UTF8);

        Console.Error.WriteLine($"Schema summary written to: {targetFile}");
        return 0;
    }

    // ── validate subcommand ─────────────────────────────────────────────────────
    // Validates a `run` output (or a standalone manifest file) against the
    // harvester-schema.v1.1.json embedded in this binary.
    //
    // Usage:
    //   AccessHarvester validate --in <output-root-dir>      (finds manifest.json recursively)
    //   AccessHarvester validate --manifest <path>           (validates a single file)
    //   AccessHarvester validate --in <dir> --json-status
    //
    // Exit codes:
    //   0  validation passed
    //   1  usage error (bad args, file/dir not found, manifest not parseable as JSON)
    //   2  unexpected error (I/O failure, embedded schema load failure)
    //   3  schema-validation errors found
    private static int RunValidate(string[] args)
    {
        var opts = OptionParser.Parse(args, ValidateValueOptions, ValidateFlagOptions);

        bool hasIn       = opts.ContainsKey("in");
        bool hasManifest = opts.ContainsKey("manifest");

        if (!hasIn && !hasManifest)
            throw new ArgumentException("One of --in <output-root-dir> or --manifest <path> is required.");
        if (hasIn && hasManifest)
            throw new ArgumentException("--in and --manifest are mutually exclusive.");

        string[] manifestPaths;
        if (hasManifest)
        {
            string path = Path.GetFullPath(opts["manifest"]);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Manifest file not found: {path}");
                return 1;
            }
            manifestPaths = [path];
        }
        else
        {
            string inDir = Path.GetFullPath(opts["in"]);
            if (!Directory.Exists(inDir))
            {
                Console.Error.WriteLine($"Input directory not found: {inDir}");
                return 1;
            }
            manifestPaths = Directory.GetFiles(inDir, "manifest.json", SearchOption.AllDirectories);
            if (manifestPaths.Length == 0)
            {
                Console.Error.WriteLine($"No manifest.json found under: {inDir}");
                return 1;
            }
        }

        // Per-document schema dispatch (v1.1 additive bump's
        // backward-compat policy: v1.0 manifests still validate via
        // their own embedded schema; v1.1 manifests validate against
        // v1.1; documents without a SchemaVersion fall back to the
        // current schema and the validator will report the missing
        // field as a normal validation error).
        var validators = new Dictionary<string, SchemaValidator>(StringComparer.Ordinal);
        var allErrors = new List<(string Path, IReadOnlyList<string> Errors)>();
        var perManifestSchemaVersion = new List<(string Path, string SchemaVersion)>();

        foreach (string manifestPath in manifestPaths)
        {
            string manifestJson;
            try { manifestJson = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Cannot read manifest: {ex.Message}");
                return 1;
            }

            string documentSchemaVersion;
            try
            {
                using var probe = JsonDocument.Parse(manifestJson);
                documentSchemaVersion = probe.RootElement.TryGetProperty("SchemaVersion", out var v)
                    && v.ValueKind == JsonValueKind.String
                        ? v.GetString() ?? SchemaVersionLiteral
                        : SchemaVersionLiteral;
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Not valid JSON ({manifestPath}): {ex.Message}");
                return 1;
            }

            if (!validators.TryGetValue(documentSchemaVersion, out var validator))
            {
                if (!TryLoadEmbeddedSchemaForVersion(documentSchemaVersion, out var schemaJson))
                {
                    Console.Error.WriteLine(
                        $"No embedded schema for SchemaVersion '{documentSchemaVersion}' in: {manifestPath}. "
                        + $"Supported versions: {string.Join(", ", SupportedSchemaVersions)}.");
                    return 1;
                }
                validator = SchemaValidator.FromText(schemaJson);
                validators[documentSchemaVersion] = validator;
            }

            perManifestSchemaVersion.Add((manifestPath, documentSchemaVersion));

            var result = validator.Validate(manifestJson);
            if (!result.IsValid)
                allErrors.Add((manifestPath, result.Errors));
        }

        int exit;
        if (allErrors.Count == 0)
        {
            // Describe what was actually validated against: usually one
            // schema, but per-document dispatch means a mixed input set
            // can light up both versions in a single run.
            string versionsChecked = string.Join(", ", validators.Keys.OrderBy(v => v, StringComparer.Ordinal).Select(v => "v" + v));
            Console.Error.WriteLine($"Validation passed ({manifestPaths.Length} manifest(s) checked against schema {versionsChecked}).");
            exit = 0;
        }
        else
        {
            foreach (var (path, errors) in allErrors)
            {
                Console.Error.WriteLine($"Validation errors in: {path}");
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"  {error}");
                    string? hint = WellKnownHint(error);
                    if (hint is not null)
                        Console.Error.WriteLine($"    Hint: {hint}");
                }
            }
            exit = 3;
        }

        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command       = "validate",
                SchemaVersion = SchemaVersionLiteral,
                SupportedSchemaVersions = SupportedSchemaVersions,
                Manifests     = manifestPaths,
                PerManifestSchemaVersion = perManifestSchemaVersion.Select(e => new
                {
                    Path = e.Path,
                    SchemaVersion = e.SchemaVersion,
                }).ToArray(),
                ExitCode      = exit,
                Errors        = allErrors.Select(e => new
                {
                    Path       = e.Path,
                    ErrorCount = e.Errors.Count,
                    Messages   = e.Errors,
                }).ToArray(),
            });
        }

        return exit;
    }

    // The schema versions this CLI knows how to validate. v1.0 is
    // retained alongside v1.1 so old manifests stay valid input to
    // the current `validate` subcommand (per the v1.1 additive bump's
    // backward-compat policy). When a new version lands, add the
    // string here and bundle the matching <EmbeddedResource> entry in
    // AccessHarvester.Cli.csproj — TryLoadEmbeddedSchemaForVersion
    // resolves the resource name from the version string.
    private static readonly string[] SupportedSchemaVersions = ["1.0", "1.1"];

    // Returns the embedded schema JSON matching <paramref name="version"/>;
    // returns false (with schemaJson = "") when no embedded schema for
    // that version exists. Caller is responsible for surfacing a usage
    // error to the operator.
    private static bool TryLoadEmbeddedSchemaForVersion(string version, out string schemaJson)
    {
        if (Array.IndexOf(SupportedSchemaVersions, version) < 0)
        {
            schemaJson = "";
            return false;
        }

        var asm = typeof(Program).Assembly;
        string resourceName = $"AccessHarvester.Cli.harvester-schema.v{version}.json";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            schemaJson = "";
            return false;
        }
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        schemaJson = reader.ReadToEnd();
        return true;
    }

    // Maps well-known v1.0 error patterns to short actionable hints.
    private static string? WellKnownHint(string error)
    {
        // camelCase key where PascalCase is required (D20 migration gap)
        if (error.Contains("[required]") &&
            (error.Contains("schemaType") || error.Contains("schemaVersion") ||
             error.Contains("harvesterVersion") || error.Contains("database")))
            return "Keys must be PascalCase (e.g. SchemaType, not schemaType). " +
                   "Run tools/migrate_v0_to_v1.py to convert a v0 document.";

        // Missing root required fields
        if (error.Contains("[required]") &&
            (error.Contains("SchemaType") || error.Contains("SchemaVersion") ||
             error.Contains("HarvesterVersion") || error.Contains("Database")))
            return "DatabaseManifest requires: SchemaType (const \"DatabaseManifest\"), " +
                   "SchemaVersion (const \"1.0\"), HarvesterVersion (string), Database.Name (string).";

        // PredicateNode without NodeKind discriminator
        if (error.Contains("PredicateNode") || error.Contains("NodeKind"))
            return "PredicateNode requires a NodeKind discriminator field. " +
                   "See docs/schema/v1-adr.md § PredicateNode polymorphism.";

        // ParserQueryParameter.Source enum mismatch (fixture 12 known issue)
        if (error.Contains("Source") && error.Contains("Queries"))
            return "ParserQueryParameter.Source must be one of: " +
                   "FormControl | UserPrompt | NamedParameter | VbaExpression | ConcatenatedVariable.";

        return null;
    }

    // ── parse subcommand ────────────────────────────────────────────────────────
    // Parses a VBA source file through the ANTLR grammar and emits a
    // harness-compatible JSON result to stdout.
    //
    // Usage:  AccessHarvester parse <source-file>
    //         AccessHarvester parse --in <source-file>
    //
    // Output:
    //   { "status": "parsed"|"failed",
    //     "errors": [{"line":N,"column":N,"message":"..."}],
    //     "tree_stats": {"node_count":N,"max_depth":null,"rule_counts":{},
    //                    "error_token_count":N} }
    //
    // Exit codes: 0 = parsed, 1 = failed / usage error.
    //
    // "error_token_count" in tree_stats reports how many ERROR_TOKEN
    // characters the lexer produced (characters not matched by any lexer
    // rule). A non-zero count with status "parsed" means the stub module
    // rule accepted the file despite lexer gaps — useful for Session 3
    // tightening work.
    private static int RunParse(string[] args)
    {
        string? sourceFile = null;
        if (args.Length == 1 && !args[0].StartsWith("--", StringComparison.Ordinal))
        {
            sourceFile = args[0];
        }
        else if (args.Length > 0)
        {
            var opts = OptionParser.Parse(args, ParseValueOptions, ParseFlagOptions);
            opts.TryGetValue("in", out sourceFile);
        }

        if (sourceFile == null)
        {
            Console.Error.WriteLine("Usage: parse <source-file>  OR  parse --in <source-file>");
            return 1;
        }

        string fullPath = Path.GetFullPath(sourceFile);
        if (!File.Exists(fullPath))
        {
            EmitParseResult(
                "failed",
                [new ParseError(null, null, $"File not found: {fullPath}")],
                null);
            return 1;
        }

        // Encoding strategy for VBA source files:
        //   BOM present → honour it (UTF-8 BOM or UTF-16 LE BOM).
        //   No BOM      → Windows-1252, deterministically.
        //
        // Rationale: the Access IDE saves VBA source as Windows-1252 (ANSI)
        // without a BOM. A UTF-8-first / fallback strategy silently misclasses
        // Windows-1252 byte sequences that happen to form valid UTF-8 — e.g.
        // 0xC3 0xA9 ("Ã©" in Windows-1252 vs. "é" in UTF-8). This CLI is
        // Windows-only ([assembly: SupportedOSPlatform("windows")]), so
        // GetEncoding(1252) is safe and deterministic across all system locales.
        string source;
        try
        {
            var rawBytes = File.ReadAllBytes(fullPath);
            if (rawBytes.Length >= 3 && rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF)
                source = System.Text.Encoding.UTF8.GetString(rawBytes, 3, rawBytes.Length - 3);
            else if (rawBytes.Length >= 2 && rawBytes[0] == 0xFF && rawBytes[1] == 0xFE)
                source = System.Text.Encoding.Unicode.GetString(rawBytes, 2, rawBytes.Length - 2);
            else
                source = System.Text.Encoding.GetEncoding(1252).GetString(rawBytes);
        }
        catch (Exception ex)
        {
            EmitParseResult("failed", [new ParseError(null, null, $"Cannot read file: {ex.Message}")], null);
            return 1;
        }
        var errors = new List<ParseError>();

        var inputStream = new AntlrInputStream(source);
        var lexer = new VbaLexer(inputStream);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new ParseErrorCollector(errors));

        var tokenStream = new CommonTokenStream(lexer);
        tokenStream.Fill();

        int errorTokenCount = tokenStream.GetTokens()
            .Count(t => t.Type == VbaLexer.ERROR_TOKEN);

        var parser = new VbaParser(tokenStream);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ParseErrorCollector(errors));

        var tree = parser.module();
        bool failed = errors.Count > 0;

        EmitParseResult(
            failed ? "failed" : "parsed",
            errors,
            new { node_count = CountNodes(tree), max_depth = (int?)null,
                  rule_counts = new Dictionary<string, int>(),
                  error_token_count = errorTokenCount });
        return failed ? 1 : 0;
    }

    private static void EmitParseResult(string status, IReadOnlyList<ParseError> errors, object? treeStats)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            status,
            errors = errors.Select(e => new { line = e.Line, column = e.Column, message = e.Message }).ToArray(),
            tree_stats = treeStats,
        }, StatusJsonOptions));
    }

    private static int CountNodes(IParseTree tree)
    {
        int count = 1;
        for (int i = 0; i < tree.ChildCount; i++)
            count += CountNodes(tree.GetChild(i));
        return count;
    }

    private sealed record ParseError(int? Line, int? Column, string Message);

    private sealed class ParseErrorCollector(List<ParseError> errors)
        : BaseErrorListener, IAntlrErrorListener<int>
    {
        public override void SyntaxError(
            System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => errors.Add(new ParseError(line, charPositionInLine, msg));

        public void SyntaxError(
            System.IO.TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => errors.Add(new ParseError(line, charPositionInLine, msg));
    }

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown subcommand: {cmd}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            AccessHarvester — extract Access DB metadata and translate into a JSON knowledge base.

            Usage:
              AccessHarvester extract         --db <file.accdb> --out <output-root-dir> [--types ...] [--no-clean] [--json-status]
              AccessHarvester translate       --in <raw-dir>    --out <kb-dir>          [--types ...] [--no-clean] [--json-status]
              AccessHarvester run             --db <file.accdb> --out <output-root-dir> [--types ...] [--no-clean] [--json-status]
              AccessHarvester generate-docs   --in <manifest.json> --out <output-dir>
              AccessHarvester validate        --in <output-root-dir> [--json-status]
              AccessHarvester validate        --manifest <manifest.json> [--json-status]
              AccessHarvester identify-usage  --in <harvested-output-root> [--exclude-cleanup <path>] [--json-status]
              AccessHarvester identify-usage  --manifest <manifest.json>   [--exclude-cleanup <path>] [--json-status]
              AccessHarvester identify-cleanup --in <harvested-output-root> [--harvest-date <YYYY-MM-DD>] [--threshold-days <N>] [--no-com] [--json-status]
              AccessHarvester identify-cleanup --manifest <manifest.json>   [--harvest-date <YYYY-MM-DD>] [--threshold-days <N>] [--no-com] [--json-status]
              AccessHarvester classify        --in <file.accdb> --out <output-dir> [--exclude-cleanup <path>] [--json-status]
              AccessHarvester sanitize        --classification <classification.json> --in <file.accdb> --out <output-dir> [--json-status]
              AccessHarvester report-baseline --db <file.accdb> --out <output-root-dir> [--reports <name,name>] [--params <params.json>] [--include-pdf] [--no-clean] [--json-status]
              AccessHarvester generate-migration --in <harvested-output-root> --out <output-dir> [--classification <classification.json>] [--data <sanitized-csv-dir>] [--json-status]
              AccessHarvester parse           <source-file>
              AccessHarvester parse           --in <source-file>
              AccessHarvester                 <source-file>       (bare-file mode; equivalent to 'parse')
              AccessHarvester --version                            (print version + schema-version, exit 0)

            `run` is extract + translate + enrich in one invocation.
            `generate-docs` reads a DatabaseManifest JSON and writes a markdown schema summary to
              <output-dir>/<database-slug>/schema-summary.md.
            `report-baseline` captures RecordSource-query aggregates for every report in the
              database (or a --reports allowlist) under
              <output-root-dir>/AccessMetaData/<dbName>/reports-baseline/<report>/. aggregates.json
              reflects the report's RecordSource result set, NOT the rendered footer expressions
              (those supported footer display totals are captured separately in the DisplayTotals
              field), and is the deterministic parity ground truth. By default the PDF is NOT
              rendered; pass --include-pdf to additionally render output.pdf for reports whose
              aggregates captured successfully via DoCmd.OutputTo over the live COM session — a
              non-deterministic binary for human visual review written to the SEPARATE, non-gated
              sibling <output-root-dir>/AccessMetaData/<dbName>/reports-baseline-spot-check/<report>/
              (indexed by its own spot-check-manifest.json), never inside the byte-diffed corpus.
              Reports whose RecordSource needs a parameter that was
              not supplied surface as a Failed entry (Reason "AggregateFailed: ...") in the manifest
              rather than entering the render path; supply values via --params to capture them.
              Default scope is all reports; pass --reports <name,name> to
              scope. --params accepts a JSON file with the shape
              {"ReportName": {"ParamName": value}} for parametrized reports; parameter ORDER in
              each ReportName object is the positional binding order against the SQL's `?`
              placeholders (Access OLE DB ignores parameter names for text commands); unmentioned
              reports run unfiltered. A
              top-level baseline-manifest.json index lists every processed report with
              Name / Status / Paths / Reason fields, where Status is one of
              Captured / Skipped / Failed and Reason is non-null for non-Captured entries.
              By default the reports-baseline/ output root is cleaned before capture so a
              re-run cannot inherit stale PDFs from reports that are no longer being captured;
              pass --no-clean to preserve prior artifacts (the per-report dir for each report
              processed this run is still recreated fresh).
            `validate` checks a DatabaseManifest JSON against the embedded harvester-schema.v1.1.json.
              --in locates manifest.json recursively under <output-root-dir> (the --out root from `run`).
              --manifest validates a single specified manifest file directly.
            `generate-migration` emits a deterministic SQL Server migration (the classify -> sanitize ->
              migrate pipeline's final product) from already-harvested artifacts — no Microsoft Access
              required. Reads ir/Tables + ir/Relationships (schema) under --in, the optional reviewed
              --classification (multi-value / preserve decisions), and the optional --data CSV-per-table
              directory (the `sanitize` output). Writes schema.sql (CREATE TABLE + PK + FK), data.sql
              (FK-safe INSERT data-load, parents before children, with SET IDENTITY_INSERT framing), and
              migration-manifest.json (emitted files + table load order + every honestly flagged
              degradation: OLE / multi-value / linked-table / missing-data / FK-cycle) to
              <output-dir>/<database-slug>/. Run schema.sql first, then data.sql. When --data is omitted,
              schema.sql still emits and every table is flagged as missing data. Nothing is silently
              dropped or fabricated.

            Layout under <output-root-dir>: AccessMetaData/<dbName>/{raw,kb,ir}/<Category>/...

            --types         Comma-separated category filter. Valid categories:
                              Forms, Reports, Macros, ModulesAndClasses,
                              Queries, Tables, Relationships, Navigation
                            Default: all. An unknown value (or a filter that
                            selects nothing) is a usage error (exit 1), not a
                            silent empty success.
            --no-clean      Skip the default per-tier clean. By default `run`/`extract`/`translate`
                            delete the target tier directory (raw/ for extract, kb/ for translate,
                            raw+kb+ir for run) before writing, so a re-harvest of a database whose
                            source objects have been removed does not leave orphan files behind.
                            Pass --no-clean to merge-overwrite the prior output in place (the
                            legacy file-by-file overwrite behaviour); orphan files from objects
                            removed since the prior run will remain.
                            IMPORTANT: --types combined with the default clean deletes the entire
                            tier directory before writing, INCLUDING previously-harvested
                            unselected categories. For example, `run --types Forms` against an
                            existing output tree clears raw/, kb/, and ir/ wholesale and then
                            repopulates only Forms-related content; Reports / Queries / Tables
                            etc. from prior runs disappear. To do an incremental partial re-harvest
                            that preserves prior categories, pass --no-clean alongside --types.
            --include-pdf   (report-baseline only) Also render each successfully aggregated
                            report to output.pdf via DoCmd.OutputTo, into the non-gated
                            reports-baseline-spot-check/ sibling (NOT the byte-diffed corpus).
                            Off by default: the default capture is the deterministic
                            aggregates.json alone. The PDF is a non-deterministic binary
                            intended for human visual review only and never enters the
                            regression gate; a render failure is non-blocking and recorded on
                            spot-check-manifest.json without affecting the report's gated status.
            --json-status   Emit a JSON summary to stdout at end (human messages go to stderr).
                            For run/extract/translate the envelope carries provenance fields:
                            HarvesterVersion (deterministic), StartedAt (UTC ISO-8601,
                            non-deterministic), DurationMs (non-deterministic), Clean (the
                            effective per-tier clean mode for this invocation).

            `parse` emits harness-format JSON to stdout:
              { "status": "parsed"|"failed",
                "errors": [{"line":N,"column":N,"message":"..."}],
                "tree_stats": {"node_count":N,"max_depth":null,"rule_counts":{},
                               "error_token_count":N} }

            Exit codes for extract/translate/run/report-baseline:
              0  all objects processed successfully
              1  usage error
              2  unexpected error
              3  completed with per-object failures (or for report-baseline, per-report failures)

            Exit codes for parse / bare-file:
              0  file parsed successfully
              1  file failed to parse (or file not found / usage error)

            Exit codes for generate-docs:
              0  schema summary written successfully
              1  usage error
              2  unexpected error

            Exit codes for validate:
              0  validation passed (manifest conforms to its declared schema version — v1.0 or v1.1)
              1  usage error (bad args, file/dir not found, manifest not valid JSON)
              2  unexpected error (I/O or embedded schema failure)
              3  schema-validation errors found (details on stderr)

            Exit codes for generate-migration:
              0  clean migration (no degradations flagged)
              1  usage error (bad args, input dir / classification / data dir not found, invalid classification)
              2  unexpected error
              3  completed with flagged degradations (review migration-manifest.json)
            """);
    }

    private static bool IsHelpFlag(string arg)
        => arg is "-h" or "--help" or "/?" or "help";

    private static bool IsVersionFlag(string arg)
        => arg is "--version" or "version";

    // Schema version the CLI's pipeline produces — sourced from
    // DatabaseManifest.SchemaVersionLiteral (the value emitted into
    // manifest.json's $.SchemaVersion). Surfacing it via --version lets a
    // downstream consumer pin their integration to a specific schema shape
    // without parsing a sample manifest first.
    private const string SchemaVersionLiteral = DatabaseManifest.SchemaVersionLiteral;

    private static void PrintVersion()
    {
        Console.Out.WriteLine(
            $"AccessHarvester {DatabaseManifest.ResolveHarvesterVersion()} (schema v{SchemaVersionLiteral})");
    }

    private static string Require(Dictionary<string, string> opts, string key)
    {
        if (!opts.TryGetValue(key, out var value))
            throw new ArgumentException($"Missing required option: --{key}");
        return value;
    }

    private static bool HasJsonStatus(Dictionary<string, string> opts)
        => opts.ContainsKey("json-status");

    private static bool HasNoClean(Dictionary<string, string> opts)
        => opts.ContainsKey("no-clean");

    private static void EmitStatus(object status)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(status, StatusJsonOptions));
    }

    private static void ReportExtract(IReadOnlyList<CategoryExportResult> categories)
    {
        foreach (var cat in categories)
        {
            int ok = cat.Outcomes.Count(o => o.Success);
            int fail = cat.Outcomes.Count - ok;
            Console.Error.WriteLine($"  [extract] {cat.Category}: {ok} ok, {fail} failed");
            foreach (var f in cat.Outcomes.Where(o => !o.Success))
                Console.Error.WriteLine($"    [X] {f.Name}: {f.ErrorMessage}");
        }
    }

    private static void ReportTranslate(IReadOnlyList<CategoryTranslateResult> categories)
    {
        foreach (var cat in categories)
        {
            int ok = cat.Outcomes.Count(o => o.Success);
            int fail = cat.Outcomes.Count - ok;
            Console.Error.WriteLine($"  [translate] {cat.Category}: {ok} ok, {fail} failed");
            foreach (var f in cat.Outcomes.Where(o => !o.Success))
                Console.Error.WriteLine($"    [X] {f.Name}: {f.ErrorMessage}");
        }
    }

    private static void ReportEnrich(IReadOnlyList<CategoryEnrichResult> categories)
    {
        foreach (var cat in categories)
        {
            int ok = cat.Outcomes.Count(o => o.Success);
            int fail = cat.Outcomes.Count - ok;
            Console.Error.WriteLine($"  [enrich] {cat.Category}: {ok} ok, {fail} failed");
            foreach (var f in cat.Outcomes.Where(o => !o.Success))
                Console.Error.WriteLine($"    [X] {f.Name}: {f.ErrorMessage}");
        }
    }

    private static object SummarizeExtract(IReadOnlyList<CategoryExportResult> categories)
    {
        int ok = categories.Sum(c => c.Outcomes.Count(o => o.Success));
        int fail = categories.Sum(c => c.Outcomes.Count(o => !o.Success));
        var failures = categories
            .SelectMany(c => c.Outcomes
                .Where(o => !o.Success)
                .Select(o => new { Category = c.Category, Name = o.Name, Error = o.ErrorMessage }))
            .ToArray();
        var perCategory = categories.Select(c => new
        {
            Category = c.Category,
            Ok = c.Outcomes.Count(o => o.Success),
            Failed = c.Outcomes.Count(o => !o.Success),
        }).ToArray();
        return new { Ok = ok, Failed = fail, Categories = perCategory, Failures = failures };
    }

    private static object SummarizeTranslate(IReadOnlyList<CategoryTranslateResult> categories)
    {
        int ok = categories.Sum(c => c.Outcomes.Count(o => o.Success));
        int fail = categories.Sum(c => c.Outcomes.Count(o => !o.Success));
        var failures = categories
            .SelectMany(c => c.Outcomes
                .Where(o => !o.Success)
                .Select(o => new { Category = c.Category, Name = o.Name, Error = o.ErrorMessage }))
            .ToArray();
        var perCategory = categories.Select(c => new
        {
            Category = c.Category,
            Ok = c.Outcomes.Count(o => o.Success),
            Failed = c.Outcomes.Count(o => !o.Success),
        }).ToArray();
        return new { Ok = ok, Failed = fail, Categories = perCategory, Failures = failures };
    }

    private static object SummarizeEnrich(IReadOnlyList<CategoryEnrichResult> categories)
    {
        int ok = categories.Sum(c => c.Outcomes.Count(o => o.Success));
        int fail = categories.Sum(c => c.Outcomes.Count(o => !o.Success));
        var failures = categories
            .SelectMany(c => c.Outcomes
                .Where(o => !o.Success)
                .Select(o => new { Category = c.Category, Name = o.Name, Error = o.ErrorMessage }))
            .ToArray();
        var perCategory = categories.Select(c => new
        {
            Category = c.Category,
            Ok = c.Outcomes.Count(o => o.Success),
            Failed = c.Outcomes.Count(o => !o.Success),
        }).ToArray();
        return new { Ok = ok, Failed = fail, Categories = perCategory, Failures = failures };
    }

    private static bool AnyFailures(IReadOnlyList<CategoryExportResult> categories)
        => categories.Any(c => c.Outcomes.Any(o => !o.Success));

    private static bool AnyFailures(IReadOnlyList<CategoryTranslateResult> categories)
        => categories.Any(c => c.Outcomes.Any(o => !o.Success));

    private static bool AnyFailures(IReadOnlyList<CategoryEnrichResult> categories)
        => categories.Any(c => c.Outcomes.Any(o => !o.Success));
}
