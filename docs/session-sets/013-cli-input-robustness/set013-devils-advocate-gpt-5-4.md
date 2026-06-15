# Set 013 — devil's-advocate review

### Item 1 — UPHELD

The `--types` fix is sound on the current tree. `BuildPipeline` now validates
the split tokens against `Pipeline.KnownCategories` before filtering, so a bad
mixed list like `Forms,Formz` fails immediately instead of silently dropping the
bad value. `RemoveEmptyEntries | TrimEntries` removes whitespace-only entries,
and the defensive `filtered.IsEmpty` check catches `--types , ,`.

The more important contrarian check was whether `KnownCategories` could accept a
category that then became a phase-specific silent no-op. I could not
substantiate that on the current `Pipeline.Default()`: every accepted category
in the live union is currently backed by the relevant exporter/translator pair
(`Forms`, `Reports`, `Macros`, `ModulesAndClasses`, `Queries`, `Tables`,
`Relationships`, `Navigation`). So `IsEmpty` is computed over a broader surface
than any single command executes, but today that does not admit a false
non-empty pipeline for `extract` or `translate`.

The only remaining risk here is future doc drift, because the usage text still
lists categories manually. That is not a current defect: the current text now
matches the live set, including `Navigation`.

### Item 2 — UPHELD

The `Ordinal` versus `OrdinalIgnoreCase` interplay does not produce a real
collision bug in the current implementation. The dictionary key is the
filesystem-facing filename, so `OrdinalIgnoreCase` is the correct comparer on
Windows: two sanitized names that differ only by case still collide on disk and
must not both export. The `existing != name` check is only distinguishing the
same original object from a distinct source object; it is not the collision key.

The sharper concern was whether the fix still drops one of two valid Access
objects whose names sanitize to the same filename, for example `a/b` and `a\b`.
That behavior is real, but it is also exactly the set-specific contract: report
the collision honestly instead of silently overwriting. The current code and the
hermetic test pin that behavior. This is a completeness tradeoff, not a hidden
corruption path.

I also probed the reserved-name hypothesis. Access on this host does allow query
names such as `CON` and `a/b`, but I could not reproduce the feared reserved
filename failure with the actual exported suffix shape: creating `CON.txt` under
a normal temp directory succeeded. So the concrete reserved-name concern did not
land.

### Item 3 — UPHELD

The highest-risk hypothesis here was accepted-set regression, and I checked it
exhaustively. Every key read through `opts["x"]`, `opts.TryGetValue("x")`, or
`opts.ContainsKey("x")` in `Program.cs`, `IdentifyUsageCommand`,
`IdentifyCleanupCommand`, `ClassifyCommand`, `SanitizeCommand`,
`ReportBaselineCommand`, and `GenerateMigrationCommand` is present in that
command's `ValueOptions` or `FlagOptions`, and I did not find a dead accepted
option in any table.

I also could not substantiate the duplicate-option overreach claim. No current
subcommand documents or implements a repeatable option; list-valued inputs are
single-token comma-separated values (`--types`, `--reports`). Rejecting a second
`--key` is therefore aligned with the published surface.

The leading-`--` value concern remains theoretically possible, but I could not
turn it into a concrete current regression. Path-valued options remain
representable as absolute paths or with a `./` or `.\` prefix on Windows, and I
did not verify a real currently-supported non-path value that must literally be
passed as its own `--foo` token. The one legitimate positional mode,
bare-file `parse`, is still handled before `OptionParser` runs.

### Item 4 — UPHELD

The dispatch-table consolidation looks correct. Both bare-file exclusion and the
actual dispatch now derive from the same `Commands` dictionary, so the prior
drift mode is removed rather than merely retested.

I could not produce a meaningful casing disagreement between the old
`ToLowerInvariant()` exclusion and the new `OrdinalIgnoreCase` dictionary on the
actual command surface, because every shipped command name is fixed ASCII. A
token like `RUN` still dispatches correctly, and a token like `İdentify-usage`
does not become a valid command under either scheme. The method-group bindings
are also not risky here: every dictionary entry targets a static handler, and
the solution builds with this table in place.

### Item 5 — UPHELD

The connection-string fix is wired correctly, not just unit-tested in isolation.
All four OLE DB call sites now call `AceConnectionString.Build(provider, path)`
inside the existing `16.0 -> 12.0` fallback loop, then immediately probe the
result with `new OleDbConnection(cs).Open()`. That means provider fallback still
happens per provider candidate, and a malformed builder output would fail at the
same chokepoint rather than silently selecting the wrong provider.

The round-trip tests are therefore not the only evidence. They prove that the
builder quotes `Data Source` correctly for `;` and `"`, and the call-site code
still performs a real open probe before accepting the resulting string. I do not
see a surviving trailing-semicolon or provider-normalization bug here.

### Item 6 — CHALLENGED

There is a real surviving defect in the escaped-`]` case the prompt called out.
The new depth-aware `ExtractParametersClauseBody` and `SplitTopLevel` logic only
fix clause termination and top-level comma splitting. The per-entry parse still
closes a bracketed parameter name at the first `]`:

```csharp
int closeBracket = trimmed.IndexOf(']');
```

That is wrong for a valid Jet/Access escaped bracket name such as:

```sql
PARAMETERS [a]]b] Text;SELECT 1;
```

I reproduced this directly against the current implementation. The extractor
returns a declared parameter with `Name = "a"` and `DataType = "]b] Text"`
instead of `Name = "a]b"` and `DataType = "Text"`. So the new tokenization is
not complete for bracket-aware names; it still misparses one valid class of
identifier the prompt specifically flagged.

Severity: Minor. This is a narrow but real correctness bug in shipped parameter
metadata, and the HomeHealthCare byte-level proof does not exercise it.

### Item 7 — CHALLENGED

I think preserving exit 2 for `identify-cleanup` usage errors was the wrong call
from a CLI-contract point of view, even if it was a conscious scope cut.
`identify-cleanup --bogus x` is now honestly rejected, but it still exits 2,
while every other usage-error path the set touched exits 1. The set's own
black-box test pins that inconsistency.

The process rationale is clear: the set did not want to renumber a pre-existing
command-local convention. But the external signal is still wrong if a caller
distinguishes usage mistakes from unexpected execution failure. That means the
set left one known CLI honesty gap in place on the same surface it was
hardening. The fact that it is documented as DW-15 makes it disclosed, not
correct.

Severity: Minor. It is a disclosed inconsistency, not a hidden crash path, but
it is still a wrong external contract for this one subcommand.

## Cross-session closures

### Cross-session 1 — UPHELD

I could not substantiate the `Ordinal` versus `OrdinalIgnoreCase` collision-guard
suspect. The comparer choice is aligned with the Windows disk semantics that the
planner is protecting, and I did not find a pair that becomes a false collision
or a missed overwrite under the current code.

### Cross-session 2 — UPHELD

The S1 no-regen claim is thinner than a full risky-path corpus proof, but it is
still supportable as stated. The claim was not "this exercises the risky path";
it was "this change does not perturb HomeHealthCare golden output." The byte-
level proof plus the ordinary-name identity test are enough for that narrower
claim. The risky path itself is covered by focused hermetic sanitizer tests.

### Cross-session 3 — CHALLENGED

This is where the surviving defect actually lives. The single-corpus HHC proof
for F-1-6 says only that ordinary PARAMETERS names were golden-neutral on that
fixture. It does not cover escaped `]]` bracket names, and the current parser
still misreads them. So the closure is too strong if read as evidence of general
correctness rather than corpus neutrality.

### Cross-session 4 — UPHELD

I was able to turn this one from inspection into an exhaustive check. Every
current option read in the CLI code matches a declared accepted option, and I
did not find a command that now rejects a previously-read key or accepts a dead
one.

## Overall verdict

`ISSUES_FOUND`. Most of Set 013 is sound: the `--types` validation, filename
collision handling, shared option tables, dispatch-table consolidation, and OLE
DB builder wiring all hold up. The two surviving problems are narrower. One is
a real parser defect in `QueryMetadataExtractor` for valid escaped-`]`
PARAMETERS names. The other is a consciously deferred but still wrong CLI
contract on `identify-cleanup` usage exits.

The single highest-value next check is to add a hermetic regression test for
`PARAMETERS [a]]b] Text; ...` and fix the per-entry bracket parser to treat `]]`
as an escaped literal `]`. That resolves the one correctness defect I could
reproduce in the output-affecting path.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "AccessHarvester.Access/QueryMetadataExtractor.cs / Item 6",
      "summary": "The new bracket-aware PARAMETERS tokenization still misparses valid escaped-']' names because ParseParametersClause closes the name at the first ']' instead of treating ']]' as an escaped literal bracket.",
      "fix": "Keep the depth-aware clause splitter, but replace the first-']' name parse with a bracket scanner that recognizes ']]' as an escaped ']' and resumes scanning until the true closing bracket before parsing the trailing type text."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Cli/Commands/IdentifyCleanupCommand.cs / Item 7",
      "summary": "identify-cleanup still returns exit 2 for usage errors, so an unknown-switch mistake is surfaced as the command's 'unexpected/other failure' code instead of the CLI-wide usage-error code 1 used everywhere else.",
      "fix": "Normalize identify-cleanup usage errors onto exit 1, keep exit 2 for unexpected execution failures only, and update the dedicated black-box test that currently pins the inverted convention."
    }
  ]
}
```