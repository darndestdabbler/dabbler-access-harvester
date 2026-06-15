Begin with a short prose analysis, then the required JSON block. Prefer a
false-positive ISSUES_FOUND over a false-negative VERIFIED.

# Session-verification Round 2 — Set 013 Session 2 (006 P5)

Round 1 returned ISSUES_FOUND with ONE Major (everything else analysed as sound):

> **Major — `AccessHarvester.Cli/OptionParser.cs`**: the shared parser silently
> overwrites repeated options, so malformed invocations like `--out a --out b`
> or `--types forms --types queries` still degrade to a wrong-but-successful run
> instead of an honest usage error.
> Fix: reject duplicate keys (value and flag), or document+test a merge policy.

## Fix applied

No subcommand documents a repeatable option (list-valued options such as
`--types` take a single comma-separated value), so a repeated `--key` is a
malformed invocation, not a merge. `OptionParser.Parse` now rejects it as a
usage error (the existing exit-1 path). The guard is keyed case-insensitively,
so `--out a --OUT b` is also caught.

```csharp
string key = token[2..];
bool isFlag = flags.Contains(key);
bool isValue = values.Contains(key);
if (!isFlag && !isValue)
    throw new ArgumentException($"Unknown option: --{key}");

// Reject a repeated option rather than silently letting the last occurrence
// win (006 P5 honest-error contract). No subcommand documents a repeatable
// option, so a second --key is a malformed invocation, not a merge.
if (opts.ContainsKey(key))
    throw new ArgumentException($"Option --{key} specified more than once.");

if (isFlag) { opts[key] = "true"; }
else
{
    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException($"Option --{key} requires a value.");
    opts[key] = args[++i];
}
```

## New pins (added to the hermetic `OptionParserTests`)

```csharp
[Fact] // "--out a --out b" -> throws "Option --out specified more than once."
public void Parse_RepeatedValueOption_Throws() { ... }
[Fact] // "--json-status --json-status" -> throws "... specified more than once."
public void Parse_RepeatedFlagOption_Throws() { ... }
[Fact] // "--out a --OUT b" -> throws "... specified more than once."
public void Parse_RepeatedOptionDifferingOnlyInCase_Throws() { ... }
```

## Re-validation

- Hermetic `OptionParserTests` (now 11 cases incl. the 3 above) + the F-1-5 /
  F-1-6 unit tests: **31 passed** in the filtered run; full `AccessHarvester.Tests`
  was 691/0 before this additive guard.
- Black-box `CliUnknownSwitchTests` + `CliTypesValidationTests`: **15 passed**.
- The guard is `OptionParser`-only and fires solely on a repeated key; the COM
  CLI command tests (CliExtract/Run/Translate, already green) pass distinct
  options, so they are unaffected. The byte-level golden proof (F-1-6
  golden-neutral) is unchanged — `CanonicalGoldenRegressionTests` does not go
  through `OptionParser`.

Confirm whether the Round-1 Major is now resolved. Everything else in Round 1
was analysed as sound (O-1-4 accepted-set preservation, O-1-7 dispatch
equivalence, F-1-5, F-1-6 golden-neutrality, DW-15 scope call); you do not need
to re-derive those unless this fix introduced a new problem.

After your analysis, output the JSON block:
```json
{ "verdict": "VERIFIED" | "ISSUES_FOUND", "issues": [ { "severity": "...", "location": "...", "summary": "...", "fix": "..." } ] }
```
If VERIFIED, `issues` must be `[]`. Do not omit the block.
