Your response MUST begin with a detailed prose analysis for EACH concern below
BEFORE the JSON block. Do not skip the analysis. Both the prose analysis and the
trailing JSON block are required. Prefer a false-positive ISSUES_FOUND over a
false-negative VERIFIED: if something looks wrong, flag it.

# Session-verification — Set 013 (CLI Input Robustness), Session 2 of 2 (closeout, 006 P5)

You are the cross-provider verifier for the **closeout** session of a 2-session
set in a C#/.NET 10 toolchain that harvests Microsoft Access metadata for a
downstream AI consumer. The CLI's primary consumer keys off exit codes
(`0 = all objects processed successfully`). This set closes the 006 review's P5
lane: CLI input-validation gaps where invalid/edge input produced a wrong or
silently-degraded result instead of an honest error.

Session 2 delivered four fixes: **O-1-4** (shared `ParseOptions`), **F-1-5**
(OLE DB connection-string building), **F-1-6** (bracket-aware `PARAMETERS`
tokenisation), **O-1-7** (dispatch-table drift). Session 1 (already VERIFIED,
embedded below as earlier-session evidence) delivered **F-1-3** (`--types`
validation) and **F-1-2** (object-name sanitisation).

---

## O-1-4 — One shared `ParseOptions` with unknown-switch rejection

Before: `ParseOptions` was copy-pasted seven times (`Program.cs` shared by 6
subcommands + six command classes). Only `GenerateMigrationCommand` rejected
unknown option keys; every other command silently stored an unrecognised
`--key value` pair and never read it, so a typo (`--datta` for `--data`)
degraded the run instead of erroring. New single implementation:

```csharp
// AccessHarvester.Cli/OptionParser.cs (new)
public static class OptionParser
{
    public static Dictionary<string, string> Parse(
        string[] args,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> flagOptions)
    {
        var values = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(flagOptions, StringComparer.OrdinalIgnoreCase);
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected positional argument: {token}");
            string key = token[2..];
            bool isFlag = flags.Contains(key);
            bool isValue = values.Contains(key);
            if (!isFlag && !isValue)
                throw new ArgumentException($"Unknown option: --{key}");
            if (isFlag) { opts[key] = "true"; }
            else
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Option --{key} requires a value.");
                opts[key] = args[++i];
            }
        }
        return opts;
    }
}
```

All seven `ParseOptions` copies now delegate to this. Each subcommand passes its
own accepted value/flag sets so the accepted-option set is **preserved exactly**
(the consolidation adds rejection, it does not change which options a command
accepts). The per-command accepted sets are:

| subcommand | value options | flag options |
|---|---|---|
| extract | db, out, types | json-status, no-clean |
| translate | in, out, types | json-status, no-clean |
| run | db, out, types | json-status, no-clean |
| generate-docs | in, out | — |
| validate | in, manifest | json-status |
| parse | in | — |
| identify-usage | in, manifest, exclude-cleanup | json-status |
| identify-cleanup | in, manifest, harvest-date, threshold-days | no-com, json-status |
| classify | in, out, exclude-cleanup | json-status |
| sanitize | in, out, classification | json-status |
| report-baseline | db, out, reports, params | no-clean, include-pdf, json-status |
| generate-migration | in, out, classification, data | json-status |

Program.cs example (extract):
```csharp
private static readonly string[] ExtractValueOptions = ["db", "out", "types"];
private static readonly string[] ExtractFlagOptions  = ["json-status", "no-clean"];
// ...
var opts = OptionParser.Parse(args, ExtractValueOptions, ExtractFlagOptions);
```
Command-class example (sanitize):
```csharp
private static readonly string[] ValueOptions = ["in", "out", "classification"];
private static readonly string[] FlagOptions = ["json-status"];
private static Dictionary<string, string> ParseOptions(string[] args)
    => OptionParser.Parse(args, ValueOptions, FlagOptions);
```

**Pins:** hermetic `OptionParserTests` (8 cases: value capture, flag→true,
case-insensitivity, unknown→throw, positional→throw, missing-value→throw,
option-as-value→throw, empty args). Black-box `CliUnknownSwitchTests`: a theory
over 11 subcommands asserts `<cmd> --bogus x` → exit 1 + "Unknown option:
--bogus"; a separate fact pins `identify-cleanup` → exit 2 (see disclosure).

## O-1-7 — Single subcommand table feeds dispatch AND bare-file exclusion

Before: the bare-file-mode exclusion list (`args[0] is not ("extract" or ...)`)
and the dispatch `switch` were two hand-synced lists; a missed bare-file entry
made `AccessHarvester <newcmd>` fall into implicit parse. Now both derive from
one table:
```csharp
private static readonly IReadOnlyDictionary<string, Func<string[], int>> Commands =
    new Dictionary<string, Func<string[], int>>(StringComparer.OrdinalIgnoreCase)
    {
        ["extract"] = RunExtract, ["translate"] = RunTranslate, ["run"] = RunRun,
        ["parse"] = RunParse, ["generate-docs"] = RunGenerateDocs, ["validate"] = RunValidate,
        ["identify-usage"] = IdentifyUsageCommand.Run, ["identify-cleanup"] = IdentifyCleanupCommand.Run,
        ["classify"] = ClassifyCommand.Run, ["sanitize"] = SanitizeCommand.Run,
        ["report-baseline"] = ReportBaselineCommand.Run, ["generate-migration"] = GenerateMigrationCommand.Run,
    };
// bare-file mode: ... && !Commands.ContainsKey(args[0]) -> RunParse(args)
// dispatch:        Commands.TryGetValue(args[0], out var handler) ? handler(rest) : UnknownCommand(args[0])
```

## F-1-5 — OLE DB connection strings via `OleDbConnectionStringBuilder`

Re-grounding found **four** identical sites (spec said two): `OleDbDaoSchemaReader`,
`OleDbValueSampler`, `OleDbRowSource`, `OleDbReportQueryExecutor`. Each did
`$"Provider={provider};Data Source={path};Persist Security Info=False;"` — a `;`
(legal NTFS filename char) or quote in the path broke parsing, and the catch
then misreported it as "ACE provider missing." All four now call:
```csharp
[SupportedOSPlatform("windows")]
internal static class AceConnectionString // AccessHarvester.Core/Com/AceConnectionString.cs (new)
{
    public static string Build(string provider, string databasePath)
    {
        var builder = new OleDbConnectionStringBuilder { Provider = provider, DataSource = databasePath };
        builder["Persist Security Info"] = false;
        return builder.ConnectionString;
    }
}
// each site: string cs = AceConnectionString.Build(provider, path);
```
**Pin:** hermetic `AceConnectionStringTests` — ordinary path round-trips
DataSource+Provider; a `;`-bearing path and a quote-bearing path each round-trip
(parse the produced string back, assert `DataSource` equals the input). No COM.

## F-1-6 — Bracket-aware `PARAMETERS`-clause tokenisation

DAO accepts `,` and `;` inside a bracketed prompt name (`[Enter start date,
please]`, `[Pick one; carefully]` are single parameters). Before, the clause was
matched with `@"\A\s*PARAMETERS\s+(.*?);"` (terminates at the **first** `;`,
truncating a name-internal `;`) and the body was split with `clauseBody.Split(',')`
(shredding a name-internal `,` into two garbage entries). Now both honour
brackets only at depth 0:
```csharp
// prefix-only regex now: @"\A\s*PARAMETERS\s+"
private static string? ExtractParametersClauseBody(string sql)
{
    var prefix = ParametersClausePrefixRegex.Match(sql);
    if (!prefix.Success) return null;
    int start = prefix.Length, depth = 0;
    for (int i = start; i < sql.Length; i++)
    {
        char c = sql[i];
        if (c == '[') depth++;
        else if (c == ']') { if (depth > 0) depth--; }
        else if (c == ';' && depth == 0) return sql[start..i];
    }
    return null; // no depth-0 terminator -> no declared clause (matches prior regex no-match)
}
private static IEnumerable<string> SplitTopLevel(string s, char separator)
{
    int depth = 0, segStart = 0;
    for (int i = 0; i < s.Length; i++)
    {
        char c = s[i];
        if (c == '[') depth++;
        else if (c == ']') { if (depth > 0) depth--; }
        else if (c == separator && depth == 0) { yield return s[segStart..i]; segStart = i + 1; }
    }
    yield return s[segStart..];
}
// ParseParametersClause now: foreach (var raw in SplitTopLevel(clauseBody, ',')) { ...existing per-entry parse... }
```
The per-entry parser (bracket name → close-`]` → name + remainder type) is
unchanged. **Pins:** `QueryMetadataExtractorTests` — comma-in-bracket → one
param; semicolon-in-bracket → not truncated; two comma-bearing names → two
params; an ordinary two-param HHC-shaped clause still → two params (regression).

**Golden impact (the one fix that can change shipped output): NONE.** HHC's two
PARAMETERS queries use `[Enter Client ID:]`, `[Enter Provider ID:]`,
`[Enter Fiscal Year:]` — no `,`/`;` inside any bracket — so the change is
behaviour-preserving for them. Proven **byte-level**: the COM
`CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests` passed
without regeneration (a fresh COM run reproduces the committed HHC golden
exactly). No golden regen performed.

---

## Earlier-session evidence (Session 1, already VERIFIED — embedded per the cross-session-evidence convention)

**F-1-3** (`--types` validation) — `BuildPipeline` in `Program.cs`:
```csharp
var cats = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var known = pipeline.KnownCategories;
var knownSet = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
var unknown = cats.Where(c => !knownSet.Contains(c)).ToArray();
if (unknown.Length > 0)
    throw new ArgumentException($"Unknown --types value(s): {string.Join(", ", unknown)}. Valid categories: {string.Join(", ", known)}.");
var filtered = pipeline.WithCategories(cats);
if (filtered.IsEmpty)
    throw new ArgumentException($"--types selected no categories to process. Valid categories: {string.Join(", ", known)}.");
```
S2 did NOT touch this block; the S2 O-1-4 change is upstream of it (`BuildPipeline`
is called with the already-parsed `opts`). The `--types` value is a value option
in extract/translate/run, preserved in the O-1-4 accepted sets above.

**F-1-2** (object-name sanitisation) — `SaveAsTextExporter.PlanOutputFiles`
sanitises the object-name path component and reports a collision rather than
silently overwriting. Untouched by S2.

---

## Validation results (this host, Windows + Access)

- Hermetic `AccessHarvester.Tests`: **691 passed, 0 failed** (2 skipped) — TRX counters.
- Black-box `AccessHarvester.Integration` (CliUnknownSwitchTests + CliTypesValidationTests): **15 passed**.
- COM tier `AccessHarvester.Integration` (CanonicalGoldenRegressionTests +
  ManifestSchemaConformanceTests + CliExtractTests + CliRunTests + CliTranslateTests):
  **10 passed** — golden byte-identical (no regen).
- `dotnet build` solution: 0 errors.

## Disclosures (evaluate these explicitly)

1. **F-1-5 expanded from 2 to 4 sites.** Re-grounding found four identical
   `BuildConnectionString` interpolations, not the two the spec named (sets
   008–012 added sites since the 006 probe). All four were fixed for consistency.
2. **DW-15 — `identify-cleanup` returns exit 2 for usage errors (not 1).** Its
   `Run` summary documents the inverted convention "0 on success, 2 on usage
   error, 1 on other failure." O-1-4 only adds rejection; it deliberately does
   NOT renumber this command's exit code (out of P5 scope). Before this set,
   `identify-cleanup --bogus x` already exited 2 (via the "--in required" path),
   so the change is exit-code-preserving for this command — only the message
   improved. Recorded as DW-15 and pinned by a dedicated test asserting exit 2.
3. **No golden regen.** Proven byte-level (above), not asserted.

## Concerns to analyse (prose first, each one)

1. **O-1-4 correctness + accepted-set preservation.** Does any command's accepted
   value/flag set in the table above differ from what that command actually
   reads? Could the new rejection break a previously-accepted invocation? Is the
   shared parser's flag/value/positional/missing-value handling correct?
2. **O-1-7 dispatch equivalence.** Is the `Commands`-table dispatch + bare-file
   exclusion behaviourally identical to the prior hardcoded switch + exclusion
   list (including case-insensitivity)?
3. **F-1-5 correctness.** Does `OleDbConnectionStringBuilder` correctly quote a
   `;`/quote-bearing `Data Source`? Any functional difference from the prior
   interpolation for ordinary paths that could affect the four readers?
4. **F-1-6 correctness + golden-neutrality.** Is the bracket-aware
   clause-extraction + top-level split correct for: nested/unbalanced brackets,
   no-terminator clauses, name-internal `,`/`;`, and ordinary clauses? Is the
   "golden-neutral, no regen" claim sound given the byte-level COM proof?
5. **Scope discipline + exit-code honesty.** Is preserving `identify-cleanup`'s
   exit 2 (DW-15) the right call vs. normalising to 1? Any place the new
   validation produces a silent drop or a changed exit code on *valid* input?

After your analysis, output a JSON block in this exact format:
```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    { "severity": "Critical" | "Major" | "Minor", "location": "<file or test>", "summary": "<one sentence>", "fix": "<recommended fix>" }
  ]
}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit this block.
