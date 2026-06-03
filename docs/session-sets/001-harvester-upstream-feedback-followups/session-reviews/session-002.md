# Verification Round 1

1. **Parameterized-query signal: correctness and independence from category**

This is implemented correctly on the code path that matters.

The IR was widened in the right place: `IrQuery` now carries `Parameters`, and `IrLoader` populates that field at load time via `ExtractQueryParameters(sql)`. That helper explicitly reuses `AccessHarvester.Access.QueryMetadataExtractor.ParseParameters`, so Session 2 does **not** duplicate parsing logic in the usage layer. That matches the stated scope and the Session 1/2 discipline about threading harvested facts forward rather than re-deriving them later.

On the classifier side, query parameter recognition is also correctly separated from the Q2–Q7 category branch. `ClassifyQuery` computes category/confidence first, then independently calls `BuildQueryParameterEvidence(query)` and threads the result into `BuildFinding(...)`. That means a query can still classify via Q3/Q2 and **also** fire `ParameterizedInput`, which is exactly the upstream-feedback gap this session was supposed to close. The added test `Parameterized_Query_Used_As_Report_RecordSource_Still_Surfaces_The_Signal` directly proves that independence.

Coverage of evidence sources in code is also as specified:

- declared `PARAMETERS` entries: via `IrQuery.Parameters` with `Source == "declared"`
- `[Forms]!...` control references: via `IrQuery.Parameters` with `Source == "form-reference"`
- `[Reports]!...` control references: via `IrQuery.Parameters` with `Source == "report-reference"`
- inline bracketed prompts: via `ContainsParamBracket(sql)` when no declared/reference evidence already exists
- ODBC `?` markers: via `sql.Contains('?')`

The main correctness caveat is the one already documented in the strategy doc: bracketed-token scanning is heuristic and can misread bracketed field names like `[Order Date]` as prompts. That risk is real here because `BuildQueryParameterEvidence` uses the existing `ContainsParamBracket` heuristic. The good news is that this signal is additive only: it raises a question, but does not change the query category branch. So the false-positive blast radius is limited.

I do see one verification gap, though: the new tests cover declared parameters, form references, inline prompts, and branch independence, but they do **not** explicitly cover the `[Reports]!...` reference path or the ODBC `?` marker path. The implementation is present, but those two branches are unpinned by regression tests.

2. **Parameter-dialog form recognition (F8) is evidence-gated and bounded**

The rule ordering and the emission discipline are both correct.

In `UsageClassifier`, the form path now evaluates in the right order:

- bound forms first (`hasRecordSource`)
- launcher/navigation rules before F8 (`hasNavEdgesOut`, `launcherName`, `startupOpened`)
- then F8 (`paramDialogName`)
- then generic VBA-only `Unknown` (F6)

That means the two required guards are preserved:

- a bound `*Param*` form stays a data-entry/reporting form because the `RecordSource` branches win first
- a launcher/menu form wins over F8 because F5/F5b execute before the name-based parameter-dialog branch

The negative tests for both cases are present and correctly targeted:
- `Bound_Param_Named_Form_Stays_Data_Entry_Not_A_Param_Dialog`
- `Launcher_Named_Form_Wins_Over_Param_Dialog`

The Session 1 “no emission-time recomputation” rule is also followed. The classifier decides F8 once, records `paramEvidence`, and then passes:
- `parameterDialogForm: paramEvidence != null`
- `parameterEvidence: paramEvidence`

into `BuildFinding`. `BuildSignals` and `BuildSummary` consume those threaded values; they do **not** call `IsParameterDialogName` again. So the signal and summary remain tied to the actual fired rule, not to later name re-derivation. That is exactly the discipline the calibration note required.

There is one heuristic boundary worth noting, but it is not an implementation leak relative to the spec: `IsParameterDialogName` matches any occurrence of `Param`, `Filter`, `Criteria`, or `Dialog`. That is intentionally broad, so an unbound non-parameter dialog with `Dialog` in the name could be recognized by F8. However, that broadness is in the written strategy itself, and the resulting action is still conservative: it keeps `Unknown` and asks a specific clarifying question. So I would treat that as heuristic risk, not a code defect.

3. **Category discipline (no over-reach)**

This concern is satisfied.

No new usage category was introduced in the diff. F8 explicitly sets:

- `category = UsageCategory.Unknown`
- `confidence = 0.40`

and then distinguishes the object through:
- `ParameterizedInput`
- a parameter-dialog-specific summary
- `ParameterDialogFlow`

That is exactly the contract described in the updated strategy doc: recognized, but not positively categorized. The tests reinforce that intent: `Param_Dialog_Form_Is_Recognized_Not_Generic_Unknown` explicitly asserts the form remains `Unknown`.

That is the right call for this session. The code now distinguishes “generic unknown unbound form” from “likely parameter-collection dialog” without prematurely inventing a new taxonomy bucket. Given the downstream consumer is AI and not a human reviewer, that additive signal/question design is better than category inflation.

4. **Question generation and suppression**

The question behavior is implemented correctly, and the suppression logic is scoped correctly.

`QuestionGenerator` first derives:

- `parameterizedInput` from the fired `ParameterizedInput` signal
- `isParamDialog = parameterizedInput && objectType == "Form"`

Then it does the right split:

- for recognized parameter-dialog forms: emit `ParameterDialogFlow`
- for parameterized queries/reports: emit `ParameterizedQuery`

The suppression behavior is also correct:

- `NoRecordSource` is suppressed when `isParamDialog`
- `UnrecognizedVbaPattern` is suppressed when `isParamDialog`

That means recognized parameter dialogs no longer get the redundant generic questions. The test `Param_Dialog_Form_Is_Recognized_Not_Generic_Unknown` checks exactly that suppression, and `Generic_Unknown_Form_Still_Gets_Generic_Questions` proves the suppression is specific rather than global.

For queries, additivity is preserved. Nothing in the new `ParameterizedQuery` branch suppresses the Session 1 out-of-band question, and the test `Inline_Bracketed_Prompt_Query_Fires_Param_Signal_And_Keeps_OutOfBand` confirms both questions coexist. That matches the strategy: entry-path uncertainty and parameter-source uncertainty are different questions.

One small observation: `QuestionGenerator` has a branch for parameterized `Report` objects, but this session intentionally does not emit `ParameterizedInput` for reports. So that branch is currently dormant. That is not a defect given the stated scope; it is just future-facing code.

5. **Signal / schema / summary consistency**

This is internally consistent.

The code adds `UsageSignalType.ParameterizedInput` and the schema enum in `object-use-schema.json` was updated to include it. Likewise, `UncertaintySignal.ParameterizedQuery` and `ParameterDialogFlow` were added in code and in the schema enum. The CLI/pilot run succeeding against the schema is strong evidence that emitted values and schema enumerations align.

The summary logic also follows the required gating discipline. `BuildSummary` only emits the parameter-dialog-specific text when `parameterDialogForm` is true, and that flag is only set from the classifier’s F8 evidence path. So the summary cannot drift independently from the fired rule. That directly addresses the Session 1 lesson.

Similarly, `BuildSignals` emits `ParameterizedInput` only when the threaded `parameterEvidence` list is non-empty. It does not inspect the object name or SQL again to decide whether the signal fired. So the emitted signal cannot disagree with the classifier’s decision path in the way Session 1 warned against.

In short: schema enum, signal emission, and summary text are all aligned around the same threaded state.

6. **Scope discipline and test adequacy**

Scope discipline is good.

Nothing in the diff touches COM extraction logic, cleanup semantics, duplicate-table-shape heuristics, obsolete-module-header heuristics, or golden regeneration. The changes are confined to usage-side IR, classification, question writing, schema, docs, and tests. That matches the session scope exactly.

The new test file is strong overall. It covers the major scenarios you called out:

- declared `PARAMETERS` query
- parameterized query used as a report `RecordSource`
- form-control-reference query
- plain non-parameterized query negative
- inline bracketed prompt retaining `OutOfBandEntryPossible`
- parameter-dialog recognition with suppression
- opened-as-dialog corroboration
- filter-named form without chrome
- bound `*Param*` negative guard
- launcher-wins-over-F8
- generic unknown form still getting generic questions

So the important behavioral claims are well represented.

The only notable gap is that concern 1 explicitly includes two additional evidence paths that are implemented but not regression-tested:
- `[Reports]![...]![...]` control references
- ODBC `?` parameter markers

Given those branches are straightforward and the implementation is small, this is not a major problem, but it is still a real coverage gap for the stated session goals.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/ParameterDialogTests.cs",
      "summary": "There is no regression test covering the `[Reports]![...]![...]` query-parameter path even though `BuildQueryParameterEvidence` explicitly supports `report-reference` evidence.",
      "fix": "Add a query fixture using a report control reference and assert `ParameterizedInput` fires with `report-control-reference:` evidence plus the `ParameterizedQuery` question."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/ParameterDialogTests.cs",
      "summary": "There is no regression test covering the ODBC `?` marker path, so one of the explicit Session 2 parameter-evidence branches is unpinned.",
      "fix": "Add a query fixture containing an ODBC-style `?` parameter marker and assert `ParameterizedInput` fires with `odbc-parameter-marker` evidence without changing the existing category behavior."
    }
  ]
}
```

---

# Verification Round 2

1. **Parameterized-query signal: correctness and independence from category**

This is implemented correctly. `IrQuery` now carries `Parameters`, and `IrLoader.LoadQueries` populates that field by calling the new `IrLoader.ExtractQueryParameters(sql)`, which in turn reuses `AccessHarvester.Access.QueryMetadataExtractor.ParseParameters(sql)` rather than duplicating parsing logic. That satisfies the load-time persistence requirement for declared `PARAMETERS` entries and `[Forms]!` / `[Reports]!` control references, and it matches the session’s stated design that parameter metadata must survive into the classifier instead of being reparsed there.

The classifier-side signal is also correctly independent from the Q2–Q7 category path. In `ClassifyQuery`, category/confidence are determined first, then `BuildQueryParameterEvidence(query)` is computed separately and threaded into `BuildFinding(..., parameterEvidence: parameterEvidence)`. That means a query can still classify via Q3 as `Reporting` at high confidence and also emit `ParameterizedInput`, which is exactly the upstream-feedback gap this session was meant to close. The new tests cover the intended cases: declared `PARAMETERS`, form-control reference, report-control reference, inline bracketed prompt, ODBC `?`, plain non-parameterized negative, and the key independence case where a parameterized query is a report `RecordSource`.

On false positives: the remaining risk is the documented shallow bracket heuristic, not a regression introduced by this session. `BuildQueryParameterEvidence` uses `ContainsParamBracket(sql)` for undeclared inline prompts, so a bracketed field name such as `[Order Date]` can still be read as a prompt. The strategy doc explicitly documents that limitation, and the new signal is additive; this session did not newly entangle that heuristic with category selection beyond the pre-existing Q5 behavior. I do not see a correctness defect here.

2. **Parameter-dialog form recognition (F8) is evidence-gated and bounded**

The F8 implementation respects the intended rule boundaries. In `ClassifyForm`, branch order is: bound form handling first, then forms with outbound navigation edges, then launcher detection (`isLauncher`), then the new `paramDialogName` branch, then generic VBA-only unknown handling. That means a bound `*Param*` form cannot fall into F8, and a launcher/menu form wins before F8. This is exactly what the strategy text requires, and the negative guards in the tests verify both cases.

The evidence-gating discipline is also correct. `paramDialogName` is only used inside the classifier to decide whether F8 fires; the downstream summary and signal are not recomputed from the name. Instead, F8 creates `paramEvidence`, and `BuildFinding` receives both `parameterDialogForm: paramEvidence != null` and `parameterEvidence: paramEvidence`. `BuildSummary` keys off `parameterDialogForm`, and `BuildSignals` keys off `parameterEvidence`. That mirrors the Session 1 “thread an explicit flag/evidence list; do not re-derive from the name in emission” rule.

I do not see a boundary leak. The only caveat is that the corroborating evidence label `opened-as-dialog` is heuristic, because the code adds it when the form is a navigation target, not when an actual `acDialog` mode is proven. But that is consistent with the session’s scope: this layer is recognizing likely dialog flows, not proving exact UI open modes. It does not overclassify the object; it only adds bounded evidence to an `Unknown` form.

3. **Category discipline (no over-reach)**

Category discipline is preserved. No new usage category was introduced; the changes only add a new signal family and two new uncertainty signals. In the F8 branch, the form remains `UsageCategory.Unknown` with low confidence, and the strategy doc was updated to state exactly that: F8 is a recognition rule that marks a parameter-dialog flow, not a positive taxonomy assignment.

That is the right call. A `frmReportParam_*`-style form can be recognized as “likely a parameter/filter dialog” without knowing which report/query it drives or how values are consumed. Promoting that to a new positive category would overstate certainty and blur the existing taxonomy. The implementation instead makes such forms distinguishable from generic unknowns through three additive outputs: `ParameterizedInput`, a parameter-dialog-aware summary, and the specific `ParameterDialogFlow` question. That matches both the spec and the intended downstream-consumer behavior.

4. **Question generation and suppression**

This piece is correct and specifically scoped. `QuestionGenerator` now detects `ParameterizedInput` in the emitted signal set, then treats a form with that fired signal as a recognized parameter dialog for question-writing purposes. In that case it emits `ParameterDialogFlow`, and the guards `!isParamDialog` on the `NoRecordSource` and `UnrecognizedVbaPattern` paths suppress the generic questions. The parameter-dialog regression test explicitly verifies both suppression behaviors.

The generic case remains intact. An unbound VBA-only form without the F8 recognition path still gets `NoRecordSource` and `UnrecognizedVbaPattern`, and the corresponding negative test covers that. On the query side, `ParameterizedQuery` is additive, not substitutive: nothing in the new code suppresses `OutOfBandEntryPossible`, and the inline-bracket test confirms both questions can coexist. That is the correct behavior because they express different uncertainty dimensions.

One implementation detail is worth noting but is not a defect in this session: `isParamDialog` is inferred in the question writer as `ParameterizedInput && objectType == "Form"`, not from a dedicated F8 flag. In the current scope that is safe, because the only form-side producer of `ParameterizedInput` is F8. If future sessions add other form-side parameter signals, that inference may need tightening. As implemented here, it is coherent.

5. **Signal / schema / summary consistency**

The schema and emitted values are aligned. `UsageSignalType` now includes `ParameterizedInput`, `UncertaintySignal` includes `ParameterizedQuery` and `ParameterDialogFlow`, and `object-use-schema.json` was updated to admit exactly those enum values with descriptions that match the implementation. The clean CLI run against HomeHealthCare is consistent with that: if emitted values had diverged from the schema, this would have surfaced.

The signal/summary agreement is also handled correctly. For forms, the summary is gated by the threaded `parameterDialogForm` flag, not by re-checking the object name. The signal is gated by the threaded `parameterEvidence` list, not by name inspection inside `BuildSignals`. That avoids the Session 1 failure mode where emission logic could drift from the actual classifier decision. For queries, `ParameterizedInput` is likewise driven by the classifier-produced evidence list, so signal emission matches the same decision path that built the evidence.

I do not see a path where the summary and signal could disagree with the rule that actually fired. In the F8 case, both are driven from the same `paramEvidence` creation point. In non-F8 form cases, both remain off. That consistency is solid.

6. **Scope discipline and test adequacy**

Scope discipline is clean. The diff is confined to usage-side IR, loader, classifier, question generation, signal types, schema, and planning docs. There are no COM extraction changes, no cleanup-pipeline changes, no golden regeneration, and no unrelated heuristic additions like duplicate-table-shape or obsolete-module-header work. This matches the stated session boundary exactly.

Test coverage is now adequate for the claims in concerns 1–5. The new `ParameterDialogTests` cover: declared `PARAMETERS`, parameterized query used as a report `RecordSource` (independence from category), form-control reference, report-control reference, ODBC `?`, plain non-parameterized negative, inline bracket prompt plus retained `OutOfBandEntryPossible`, parameter-dialog recognition with suppression, opened-as-dialog corroboration, filter-named form without chrome, bound `*Param*` negative guard, launcher-wins-over-F8, and generic unknown form retaining generic questions. Those were the important behavioral claims, and the two Round 1 coverage gaps have been closed. Given the unchanged green test/build results, I do not see an uncovered blocking claim remaining in this session.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
