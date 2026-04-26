**VERDICT: ISSUES_FOUND**

- **Severity: Major**
- **Finding:** The H-D05 fixture is not aligned to the stated hazard. `Resume 0` is not one of the uncovered forms listed for H-D05 (`Resume`, `Resume Next`, `Resume <label>`), and its comment treats it as a special Resume form, conflating it with the separate `On Error GoTo 0` hazard.
- **Location:** `docs/test-cases/edge/01-h-d05-resume-forms.bas`
- **Suggested fix:** Remove `Resume 0` from this fixture and keep only the three required forms: bare `Resume`, `Resume Next`, and `Resume <label>`. If numeric labels are intentionally being tested, do that explicitly and justify it against the spec, not as a synonym for bare `Resume`.

- **Severity: Major**
- **Finding:** The H-D12 fixture contains a non-minimal and likely invalid call form: `ProcedureWithNamedArgs "Value3", Opt2:="C", 100, 200, "end"` places positional arguments after a named argument. It also mixes in the separate ParamArray hazard, which violates the “one fixture exercises one hazard” standard.
- **Location:** `docs/test-cases/edge/02-h-d12-named-arguments.bas`
- **Suggested fix:** Reduce the fixture to legal named-argument calls only. Keep examples like pure named arguments and positional-then-named arguments, but remove the trailing positional-after-named case and remove the `ParamArray` dependency from this fixture.

- **Severity: Major**
- **Finding:** The pinned expected JSON for the named-arguments fixture is not structurally credible as shown. It is marked `source.kind: "procedure"` but its location starts at line 1 and its `originalSource` includes header comments and module-level content, not a single procedure span.
- **Location:** `docs/test-cases/edge/02-h-d12-named-arguments.expected.json`
- **Suggested fix:** Repin the JSON so the recorded source span matches exactly one harvested procedure, or refactor the fixture to a single-procedure module if the harness expects one source item per fixture.

- **Severity: Major**
- **Finding:** The ParamArray fixture also does not match the stated fixture shape. The `.bas` contains multiple procedures (`Log`, `Join`, `TestCalls`), while the expected JSON is shown as `source.kind: "procedure"` spanning the whole module from line 1. This is the same procedure-vs-module mismatch as above.
- **Location:** `docs/test-cases/edge/03-h-e07-param-array.bas`, `docs/test-cases/edge/03-h-e07-param-array.expected.json`
- **Suggested fix:** Reduce this to one minimal procedure or function with a single `ParamArray ... As Variant` parameter and repin the JSON to that exact procedure. Avoid extra helper procedures if the expected artifact is per-procedure.

- **Severity: Minor**
- **Finding:** The H-D04 fixture is not minimal. It includes `On Error GoTo <label>`, `On Error Resume Next`, and `Resume Next`, even though the uncovered gap is specifically the `On Error GoTo 0` sub-case. The comments also describe a runtime flow that is not actually reached because the division-by-zero transfers control before `On Error GoTo 0`.
- **Location:** `docs/test-cases/edge/05-h-d04-on-error-goto-zero.bas`
- **Suggested fix:** Simplify the fixture to a small procedure whose only purpose is to parse `On Error GoTo 0` in context. Remove unrelated Resume-form statements and the unreachable runtime narrative.

- **Severity: Info**
- **Finding:** The reported green build/test run is not strong evidence that these new edge fixtures are correct end-to-end. The issues above suggest the new fixtures are either not compiled/harvested under automated assertions or are only schema-validated, not semantically pinned.
- **Location:** Edge-fixture test integration
- **Suggested fix:** Add `docs/test-cases/edge/*` to the same automated parse/harvest golden tests used for the existing 12 fixtures so invalid source forms and procedure-span mismatches fail CI.