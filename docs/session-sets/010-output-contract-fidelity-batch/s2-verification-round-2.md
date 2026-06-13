On the two accepted round-1 fixes:

- **F-1-4 (missing `ir/<category>` dir)** looks **correctly fixed**. The early return is gone; a missing directory now becomes an empty IR set, then `AddMissingSpecDiagnostics(...)` runs and emits `FORM_SPEC_MISSING` warnings for parsed modules with no matching IR spec. The helper sorts by `StringComparer.Ordinal`, so the emitted order is deterministic. The three added unit tests cover the key cases: empty IR set, partial overlap, full overlap. I do **not** see a regression in that path.

- **F-2-7 (blank numeric/bit cells)** is **fixed for the specific defect reported**. `IsInvalidDataToken(...)` now short-circuits on `string.IsNullOrWhiteSpace(raw)` and therefore agrees with `Format(...)` on null/blank/whitespace cells: they become clean `NULL`, not `InvalidDataToken`. The added theory explicitly pins the originally broken cases.

However, I **cannot** sign off on **“no new regression”** for the `SqlValueFormatter` change set overall.

The reason: `IsInvalidDataToken(...)` still has no explicit branch for **binary families** (`DbBinary` / `DbLongBinary` / `DbVarBinary`). Those types now fall into the default `!NumericToken.IsMatch(raw)` branch. That means ordinary binary payloads such as hex strings containing `A-F` would be reported as invalid tokens even though the formatter’s binary path emits them as `0x...`, not `NULL`. So the blank-cell mismatch is fixed, but the refactor appears to introduce the same class of **spurious degradation** for binary columns.

So my round-2 conclusion is:

- **(a)** blank numeric/bit cells: **yes, fixed**
- **(b)** missing-dir diagnostics + deterministic order: **yes, fixed**
- **(c)** no regression: **not fully verified** due to the binary-type hole in `IsInvalidDataToken(...)`

```json
{"verdict":"ISSUES_FOUND","issues":[{"severity":"MAJOR","location":"AccessHarvester.Pipeline/Migration/SqlValueFormatter.cs:IsInvalidDataToken","summary":"Binary column types are not handled explicitly, so DbBinary/DbLongBinary/DbVarBinary fall through to the numeric-regex branch and can be spuriously marked InvalidDataToken even though the formatter emits a binary literal rather than NULL.","fix":"Add an explicit binary-family branch in IsInvalidDataToken aligned with Format (either return false for binary tokens or validate them with a shared hex-token regex), and add a regression test covering a normal binary value such as DEADBEEF."}]}
```