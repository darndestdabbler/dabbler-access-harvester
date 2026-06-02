# Verification Round 1

(A) CLI wiring correctness

The main success-path wiring is present and mostly correct. `GenerateMigrationCommand.Run` parses options, resolves full paths, optionally reads the classification with `ClassificationReader.ReadFile`, assembles generator inputs via `MigrationIrReader.Read(inRoot, classification, dataDir)`, builds the table CSV callback with `OpenTableCsv(dataDir)`, invokes `SqlServerMigrationGenerator.Generate(inputs, ...)`, creates the `<out>/<database-slug>/` subdirectory, and writes all three required artifacts: `schema.sql`, `data.sql`, and `migration-manifest.json`. Exit code selection on the success path is also aligned with the spec: `0` when `result.Degradations.Count == 0`, otherwise `3`. Missing required args and missing input paths return `1`; unexpected exceptions return `2`. The output layout is sound, and slug derivation is reasonable for normal database names (`Path.GetFileNameWithoutExtension`, non-word replacement, lowercase).

There are, however, two real CLI issues.

First, `ParseOptions` does not reject unknown switches. Any token beginning with `--` is accepted; only `json-status` is treated as a flag, and every other key/value pair is stored without validation. `Run` then only consults `in`, `out`, `classification`, `data`, and `json-status`, silently ignoring everything else. That means typos like `--datta`, `--clasification`, or `--ouut` are not treated as “invalid args” with exit `1`; instead, the command can proceed with altered behavior. The worst case is a typo in `--data`: the run can succeed as a degraded migration with exit `3` and a manifest full of `MissingDataFile` degradations, even though the actual problem was bad CLI input. That is contrary to the documented usage semantics.

Second, the `catch (InvalidDataException ex)` block is too broad and always reports `classification document is invalid`. `InvalidDataException` is a plausible failure mode not just for classification parsing, but also for malformed IR or malformed sanitized CSV input. In those cases the command would still return `1`, which is defensible for bad input, but the diagnostic would be false and misdirect the user. So the wiring is functionally close, but not fully correct on invalid-arg/error-path handling.

(B) Reader/disposal ownership

This part is correct. The generator contract says `openTableCsv` returns a `TextReader?`, and the generator side owns disposal (`using (reader)` in `SqlServerDataLoadGenerator`). The command’s callback returns a fresh `new StreamReader(path)` each time and does not dispose it locally, which is exactly what that ownership model requires. There is no double-dispose and no leak here. Returning `null` for missing files is also consistent with the generator’s degradation behavior. No issue on disposal ownership.

(C) Honest degradation + manifest

The manifest generation is honest in the way the spec asks. `BuildManifestJson` serializes each degradation directly from `result.Degradations` and includes the required fields: `Kind`, `Table`, `Column`, and `Reason`. `Kind` is explicitly serialized as `d.Kind.ToString()`, so the enum appears as a string, not an integer. `DegradationCount` is derived from the same list. `TableLoadOrder` is emitted from the generator result. There is no fabrication layer here; the command is not inventing degradations or suppressing them after generation.

The exit behavior is also correct: any degradation causes exit `3`, which is the right honest signal for “completed, but review required.” The command still writes `schema.sql`, `data.sql`, and the manifest before returning `3`, which matches the intended degraded-but-usable workflow.

I do not see silent dropping inside the Session 3 command wrapper itself. The one caveat is upstream of the manifest: because unknown CLI options are silently ignored, a mistyped `--data` or `--classification` can lead to a manifest that is internally honest about what the command actually did, but misleading relative to what the user intended. That is a CLI validation bug, not a manifest fabrication bug.

(D) Golden-script test rigor

The golden-script tests are materially rigorous and do pin the emitted SQL. `MigrationGoldenScriptTests` generates against committed HomeHealthCare fixture IR plus committed sample CSVs, then compares `result.DdlSql` and `result.DataSql` to committed `schema.sql` and `data.sql` with LF normalization. That is appropriate for a repository golden under cross-platform/autocrlf conditions. It is not a raw byte-for-byte file test, but it is a stable content-golden exactly as described by the concern.

The structural SQL checks are also meaningful rather than tautological. The test asserts:
- representative `CREATE TABLE` and FK statements are present,
- system tables do not leak,
- `GO` batch count equals `CREATE TABLE` + `ALTER TABLE` count,
- `IDENTITY_INSERT` ON/OFF is balanced and present,
- a parent table (`tlkpStates`) appears before its child (`tblClients`) in the data-load,
- actual sample row literals appear in `data.sql`.

Those checks are heuristic rather than a full SQL parser, but they match the requested rigor in the concern. The committed `data.sql` shown is self-consistent with the committed sample CSVs shown: `tlkpStates.csv` produces the three state inserts, and `tlkpPlanStatuses.csv` produces the three identity-preserving inserts. The “missing data file” test also verifies that absent sample CSVs become degradations instead of silent schema omission. Overall, this is a solid hermetic golden setup.

(E) Determinism

Determinism looks good. There is an explicit generate-twice test asserting equality of `DdlSql`, `DataSql`, and `TableLoadOrder`. On top of that, `SqlServerMigrationGenerator` sorts degradations with a stable comparer before returning them, which gives the manifest deterministic degradation ordering by construction even though there is no separate manifest byte-golden.

Line-ending handling is also done in a way that should prevent autocrlf flakiness for the gated tests:
- golden SQL comparisons normalize both expected and actual to LF,
- `CountGoBatches` also LF-normalizes before splitting,
- CSV input is read through `StreamReader`, which tolerates CRLF/LF differences.

The command writes files using UTF-8 without BOM, which is stable. I do not see a determinism or line-ending flake risk in the shown Session 3 work.

(F) Spec completeness

Within the material provided, Session 3 is largely complete:
1. CLI subcommand + manifest: implemented.
2. End-to-end emit + hermetic golden: implemented with both pipeline tests and integration tests.
3. Docs: the summary covers interpretation guide, install guide, roadmap/planning updates, and explicitly mentions outputs, degradation semantics, exit codes, and hermetic validation.

I do not see obvious out-of-scope creep. The work stays focused on the COM-free `generate-migration` capstone.

The only completeness gap I would call out is still the CLI robustness issue from (A): because unknown options are silently accepted and one broad error catch misattributes invalid input, the subcommand is not fully up to the documented “usage error on invalid args” contract. That is enough to keep this from a clean verification.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Cli/Commands/GenerateMigrationCommand.cs",
      "summary": "ParseOptions accepts unknown --options and silently ignores them, so typos like --datta or --clasification can produce a degraded migration instead of the documented usage-error exit code 1.",
      "fix": "Validate parsed option names against the supported set (in, out, classification, data, json-status) and throw an ArgumentException for any unknown switch."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Cli/Commands/GenerateMigrationCommand.cs",
      "summary": "The broad InvalidDataException catch always reports 'classification document is invalid', which can misattribute malformed IR or sanitized CSV failures and obscure the real error path.",
      "fix": "Limit the classification-specific InvalidDataException handling to ClassificationReader.ReadFile or use more specific exception handling/messages for classification, IR, and CSV input failures."
    }
  ]
}
```

---

# Verification Round 2

Fix 1: unknown-option handling  
This fix looks correct and complete. `ParseOptions` now explicitly distinguishes known value options (`in`, `out`, `classification`, `data`) from known flags (`json-status`) and throws `ArgumentException` for anything else. `Run` already converts `ArgumentException` into a usage error with exit code 1, so a typo like `--datta` no longer falls through into a degraded migration path. The new integration test exercises exactly that case and matches the intended behavior (`exit 1` plus “Unknown option”). I do not see a regression in normal option parsing from this change.

Fix 2: `InvalidDataException` attribution  
The narrowing of the `InvalidDataException` catch does fix the original bug: malformed IR or sanitized CSV input will no longer be mislabeled as “classification document is invalid”, because that message is now emitted only around `ClassificationReader.ReadFile(classFull)`.

However, the refactor is not fully regression-free. Classification loading now happens outside the later `try/catch` that handles `FileNotFoundException` and generic `Exception`. That means any non-`InvalidDataException` failure while opening/reading `--classification` can now escape as an unhandled exception instead of producing the command’s normal exit code and stderr message. Examples include the file disappearing after the `File.Exists` check, sharing violations, or other I/O/access errors. So the original misattribution is fixed, but the implementation introduced a new unhandled-exception path.

```json
{"verdict":"ISSUES_FOUND","issues":[{"severity":"Minor","location":"AccessHarvester.Cli/Commands/GenerateMigrationCommand.cs","summary":"The scoped classification-only InvalidDataException catch fixed the misattributed error message, but moving ClassificationReader.ReadFile outside the main try/catch introduced an uncaught path for non-InvalidData failures while reading --classification.","fix":"Keep the narrow InvalidDataException handling for classification parse errors, but wrap classification loading in the broader command-level exception handling as well, or add FileNotFoundException/IOException/Exception handling around ClassificationReader.ReadFile so classification read failures still return controlled exit codes and messages."}]}
```

---

# Verification Round 3

The fix resolves the Round 2 finding.

`ClassificationReader.ReadFile` is now back inside the command-level `try`, which restores the missing protection for non-`InvalidDataException` failures during classification loading. That means classification read failures such as a late `FileNotFoundException` race, sharing/I/O failures, or other unexpected exceptions are no longer able to escape uncaught; they now flow into the existing outer handlers and produce the intended controlled exit codes.

The nested `try/catch (InvalidDataException)` is also scoped correctly: it wraps only `ClassificationReader.ReadFile`. So:

- a malformed `classification.json` is still truthfully reported as `classification document is invalid` with exit code `1`;
- non-parse classification read failures now hit the outer handlers (`FileNotFoundException` => `1`, other exceptions => `2`);
- `InvalidDataException` thrown later by `MigrationIrReader.Read(...)`, CSV handling, or generation is no longer misattributed to the classification file, because those calls are outside the tight inner catch.

I do not see a regression in the exception attribution or exit-code behavior related to the R2 issue. The reported clean build and passing `GenerateMigrationCommandTests` are consistent with that assessment.

```json
{"verdict":"VERIFIED","issues":[]}
```
