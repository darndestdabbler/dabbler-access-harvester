using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core;
using AccessHarvester.Core.Abstractions;
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
    private static readonly HashSet<string> FlagOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json-status"
    };

    private static readonly JsonSerializerOptions StatusJsonOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        // Required for Encoding.GetEncoding(1252) on .NET Core / .NET 5+.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        try
        {
            return Dispatch(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Usage error: {ex.Message}");
            PrintUsage();
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
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
        // without a subcommand prefix. Using a known-command exclusion set
        // is more robust than a dot/separator heuristic and handles
        // extensionless file paths correctly.
        if (args.Length == 1
            && !args[0].StartsWith("--", StringComparison.Ordinal)
            && !IsHelpFlag(args[0])
            && !IsVersionFlag(args[0])
            && args[0].ToLowerInvariant() is not ("extract" or "translate" or "run" or "parse" or "generate-docs" or "validate"))
        {
            return RunParse(args);
        }

        var rest = args.Skip(1).ToArray();
        return args[0].ToLowerInvariant() switch
        {
            "extract"       => RunExtract(rest),
            "translate"     => RunTranslate(rest),
            "run"           => RunRun(rest),
            "parse"         => RunParse(rest),
            "generate-docs" => RunGenerateDocs(rest),
            "validate"      => RunValidate(rest),
            _               => UnknownCommand(args[0]),
        };
    }

    private static int RunExtract(string[] args)
    {
        var opts = ParseOptions(args);
        string db = Path.GetFullPath(Require(opts, "db"));
        string outDir = Path.GetFullPath(Require(opts, "out"));
        var pipeline = BuildPipeline(opts);

        var result = pipeline.Extract(db, outDir);

        Console.Error.WriteLine($"Extracted to: {result.RawDir}");
        ReportExtract(result.Categories);

        int exit = AnyFailures(result.Categories) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "extract",
                Database = db,
                OutputRoot = outDir,
                BaseDir = result.BaseDir,
                RawDir = result.RawDir,
                ExitCode = exit,
                Extract = SummarizeExtract(result.Categories),
            });
        }
        return exit;
    }

    private static int RunTranslate(string[] args)
    {
        var opts = ParseOptions(args);
        string rawDir = Path.GetFullPath(Require(opts, "in"));
        string kbDir = Path.GetFullPath(Require(opts, "out"));
        var pipeline = BuildPipeline(opts);

        var result = pipeline.Translate(rawDir, kbDir);

        Console.Error.WriteLine($"Translated to: {result.KbDir}");
        ReportTranslate(result.Categories);

        int exit = AnyFailures(result.Categories) ? 3 : 0;
        if (HasJsonStatus(opts))
        {
            EmitStatus(new
            {
                Command = "translate",
                RawDir = rawDir,
                KbDir = kbDir,
                ExitCode = exit,
                Translate = SummarizeTranslate(result.Categories),
            });
        }
        return exit;
    }

    private static int RunRun(string[] args)
    {
        var opts = ParseOptions(args);
        string db = Path.GetFullPath(Require(opts, "db"));
        string outDir = Path.GetFullPath(Require(opts, "out"));
        var pipeline = BuildPipeline(opts);

        var result = pipeline.Run(db, outDir);

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
                Database = db,
                OutputRoot = outDir,
                BaseDir = result.BaseDir,
                RawDir = result.RawDir,
                KbDir = result.KbDir,
                IrDir = result.IrDir,
                ExitCode = exit,
                Extract = SummarizeExtract(result.Extract),
                Translate = SummarizeTranslate(result.Translate),
                Enrich = SummarizeEnrich(result.Enrich),
            });
        }
        return exit;
    }

    private static CorePipeline BuildPipeline(Dictionary<string, string> opts)
    {
        var pipeline = CorePipeline.Default();
        if (opts.TryGetValue("types", out var types))
        {
            var cats = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            pipeline = pipeline.WithCategories(cats);
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
        var opts = ParseOptions(args);
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
    // harvester-schema.v1.0.json embedded in this binary.
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
        var opts = ParseOptions(args);

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

        string schemaJson = LoadEmbeddedSchema();
        var validator = SchemaValidator.FromText(schemaJson);

        var allErrors = new List<(string Path, IReadOnlyList<string> Errors)>();
        foreach (string manifestPath in manifestPaths)
        {
            string manifestJson;
            try { manifestJson = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Cannot read manifest: {ex.Message}");
                return 1;
            }

            try { using var _ = JsonDocument.Parse(manifestJson); }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Not valid JSON ({manifestPath}): {ex.Message}");
                return 1;
            }

            var result = validator.Validate(manifestJson);
            if (!result.IsValid)
                allErrors.Add((manifestPath, result.Errors));
        }

        int exit;
        if (allErrors.Count == 0)
        {
            Console.Error.WriteLine($"Validation passed ({manifestPaths.Length} manifest(s) checked against schema v{SchemaVersionLiteral}).");
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
                Manifests     = manifestPaths,
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

    private static string LoadEmbeddedSchema()
    {
        var asm = typeof(Program).Assembly;
        using var stream = asm.GetManifestResourceStream("AccessHarvester.Cli.harvester-schema.v1.0.json")
            ?? throw new InvalidOperationException("Embedded schema resource not found in assembly.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
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
            var opts = ParseOptions(args);
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
              AccessHarvester extract       --db <file.accdb> --out <output-root-dir> [--types ...] [--json-status]
              AccessHarvester translate     --in <raw-dir>    --out <kb-dir>          [--types ...] [--json-status]
              AccessHarvester run           --db <file.accdb> --out <output-root-dir> [--types ...] [--json-status]
              AccessHarvester generate-docs --in <manifest.json> --out <output-dir>
              AccessHarvester validate      --in <output-root-dir> [--json-status]
              AccessHarvester validate      --manifest <manifest.json> [--json-status]
              AccessHarvester parse         <source-file>
              AccessHarvester parse         --in <source-file>
              AccessHarvester               <source-file>       (bare-file mode; equivalent to 'parse')
              AccessHarvester --version                          (print version + schema-version, exit 0)

            `run` is extract + translate + enrich in one invocation.
            `generate-docs` reads a DatabaseManifest JSON and writes a markdown schema summary to
              <output-dir>/<database-slug>/schema-summary.md.
            `validate` checks a DatabaseManifest JSON against the embedded harvester-schema.v1.0.json.
              --in locates manifest.json recursively under <output-root-dir> (the --out root from `run`).
              --manifest validates a single specified manifest file directly.

            Layout under <output-root-dir>: AccessMetaData/<dbName>/{raw,kb,ir}/<Category>/...

            --types         Comma-separated category filter. Valid categories:
                              Forms, Reports, Macros, ModulesAndClasses,
                              Queries, Tables, Relationships
                            Default: all.
            --json-status   Emit a JSON summary to stdout at end (human messages go to stderr).

            `parse` emits harness-format JSON to stdout:
              { "status": "parsed"|"failed",
                "errors": [{"line":N,"column":N,"message":"..."}],
                "tree_stats": {"node_count":N,"max_depth":null,"rule_counts":{},
                               "error_token_count":N} }

            Exit codes for extract/translate/run:
              0  all objects processed successfully
              1  usage error
              2  unexpected error
              3  completed with per-object failures

            Exit codes for parse / bare-file:
              0  file parsed successfully
              1  file failed to parse (or file not found / usage error)

            Exit codes for generate-docs:
              0  schema summary written successfully
              1  usage error
              2  unexpected error

            Exit codes for validate:
              0  validation passed (manifest conforms to schema v1.0)
              1  usage error (bad args, file/dir not found, manifest not valid JSON)
              2  unexpected error (I/O or embedded schema failure)
              3  schema-validation errors found (details on stderr)
            """);
    }

    private static bool IsHelpFlag(string arg)
        => arg is "-h" or "--help" or "/?" or "help";

    private static bool IsVersionFlag(string arg)
        => arg is "--version" or "version";

    // Schema version the CLI's pipeline produces — must match the constant
    // initialiser on DatabaseManifest.SchemaVersion (the value emitted into
    // manifest.json's $.SchemaVersion). Surfacing it via --version lets a
    // downstream consumer pin their integration to a specific schema shape
    // without parsing a sample manifest first.
    private const string SchemaVersionLiteral = "1.0";

    private static void PrintVersion()
    {
        var harvesterAsm = typeof(DatabaseManifest).Assembly;
        string harvesterVersion =
            harvesterAsm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? harvesterAsm.GetName().Version?.ToString()
            ?? "0.0.0";

        // Strip the trailing "+<commit-sha>" SourceLink decoration the SDK
        // appends to InformationalVersion when the build is in a git tree;
        // the version string the consumer pins should be the semver, not
        // the build provenance.
        int plus = harvesterVersion.IndexOf('+');
        if (plus >= 0) harvesterVersion = harvesterVersion[..plus];

        Console.Out.WriteLine($"AccessHarvester {harvesterVersion} (schema v{SchemaVersionLiteral})");
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--"))
                throw new ArgumentException($"Unexpected positional argument: {token}");
            var key = token.Substring(2);
            if (FlagOptions.Contains(key))
            {
                opts[key] = "true";
            }
            else
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                    throw new ArgumentException($"Option --{key} requires a value.");
                opts[key] = args[++i];
            }
        }
        return opts;
    }

    private static string Require(Dictionary<string, string> opts, string key)
    {
        if (!opts.TryGetValue(key, out var value))
            throw new ArgumentException($"Missing required option: --{key}");
        return value;
    }

    private static bool HasJsonStatus(Dictionary<string, string> opts)
        => opts.ContainsKey("json-status");

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
