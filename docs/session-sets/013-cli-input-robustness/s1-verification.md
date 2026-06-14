1. **F-1-3 validation correctness/completeness**

The `--types` fix looks correct and complete for the stated bug. The important behavior change is that validation now happens **before** the category filter is allowed to turn a typo into an empty pipeline. `cats` is split with `RemoveEmptyEntries | TrimEntries`, then each supplied token is checked against a case-insensitive `knownSet` built from `pipeline.KnownCategories`. That means:

- a pure typo like `Formz` is rejected immediately with an `ArgumentException`;
- a mixed list like `Forms,Formz` is also rejected immediately, so the bad value is **not** silently dropped while the valid one proceeds;
- casing and whitespace are handled correctly (`forms`, ` Forms `, etc.);
- `--types , ,` becomes an empty `cats` array and is caught by the defensive `filtered.IsEmpty` guard.

The ordering of the checks is also right. Unknown values are detected first, so an actually bad category gets the specific “Unknown --types value(s)” message instead of the more generic “selected no categories” message.

Using `Pipeline.KnownCategories` is the right source of truth for validation. It is the live union of exporter/translator/enricher categories, case-insensitively de-duplicated, and the new tests explicitly pin that `Navigation` is present. That avoids repeating a stale hardcoded validation list in code.

On exit codes: I do not see a path where a **valid** category filter changes a previously valid run into exit 1. The only new exit-1 paths are invalid user input: unknown category or empty selection. The black-box CLI test only drives `translate`, but because the change is inside shared `BuildPipeline`, and the prompt confirms that same builder feeds `extract`/`translate`/`run`, the behavior applies uniformly across all three commands.

2. **F-1-2 sanitization/collision behavior**

The sanitization is applied at the correct place: the **object-name-derived filename component**, not the object name itself. `SaveAsText` still receives the original Access object name (`name`), while the output path now uses the sanitized `fileName`. That is exactly what you want: preserve database lookup semantics, sanitize only the filesystem target.

`PlanOutputFiles` is a sound extraction of the risky logic. It makes the behavior explicit and testable:

- separator-bearing names like `a/b` or `a\b` are flattened to `a_b.txt`;
- ordinary names remain unchanged;
- output order is preserved;
- collisions are detected before any overwrite can happen.

The collision guard is honest. It keys claimed filenames with `StringComparer.OrdinalIgnoreCase`, which matches the Windows filesystem behavior that matters here. If two distinct source names sanitize to the same path, the first name claims it and the second gets a failure outcome with a collision message. That satisfies the set constraint: no silent overwrite.

I do not see a meaningful false-positive collision risk in this implementation. Treating case-only filename differences as collisions is correct on Windows. The only names flagged are names that would genuinely contend for the same on-disk path after sanitization. Also, because Access object names are unique within a collection, the `existing != name` check is sufficient to distinguish “same source object” from “distinct objects colliding.”

The COM no-regen evidence also aligns with this change: HHC names are ordinary names, `SanitizeFileName` is identity for them, and the byte-level golden reproduction proves no unintended drift in the normal path.

3. **Set-specific constraints and usage text**

I do not see a remaining path that would silently drop bad input, silently overwrite output, or change the exit code of a valid run.

- **Silent drop of invalid `--types`:** prevented, because any unknown entry causes an immediate usage error before filtering.
- **Silent empty success from empty selection:** prevented by the `filtered.IsEmpty` guard.
- **Silent overwrite from sanitized path collision:** prevented by explicit collision reporting on the later object.
- **Valid run exit code:** preserved; the new failure paths are limited to invalid CLI input.

The usage text is now accurate for the current tree. It includes `Navigation`, and it explicitly states that an unknown value or a filter selecting nothing is a usage error with exit 1. The actual runtime error text also lists the valid categories from the live `KnownCategories` set, which is the important operational behavior.

One minor architectural note, not an issue: the validation source is now live/dynamic, while the usage text remains manually maintained. That does leave future doc drift possible in theory, but the existing CLI usage drift tests are the right compensating control, and nothing in this session indicates the text is currently wrong.

4. **Lean/factoring and test adequacy**

The factoring is sound.

- `Pipeline.KnownCategories` and `Pipeline.IsEmpty` belong on `Pipeline`; they describe pipeline state and make the CLI logic simpler and less error-prone.
- `SaveAsTextExporter.PlanOutputFiles` is a good pure extraction. It isolates filename planning/collision semantics from COM export mechanics and makes the bug fix hermetically testable.

The test set is adequate for these two fixes:

- `PipelineCategoryValidationTests` pins the live category source of truth, dedup semantics, `Navigation`, empty selection, and case-insensitive valid filtering.
- `CliTypesValidationTests` pins the externally visible exit-code behavior: unknown => exit 1 with valid-list guidance, empty selection => exit 1, valid filter => exit 0.
- `SaveAsTextExporterSanitizeTests` pins separator sanitization, identity for ordinary names, collision reporting, and non-collision ordinary cases.
- The byte-level COM `CanonicalGoldenRegressionTests` prove no unintended golden drift from the `SaveAsTextExporter` change.

I do not see a material correctness gap in the implementation or the evidence for the scoped fixes.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```