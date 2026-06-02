# Verification Round 1

1. **Launcher recognition is correct in the classifier, and the bounds are mostly right.**

   The core F5b behavior is implemented as intended:

   - For forms, launcher recognition is based on:
     - `SignalIndex.IsLauncherFormName(form.Name)`, or
     - `index.IsStartupOpened(form.Name)`.
   - `HasLauncherChrome(form)` is only a corroborator. It never classifies by itself because `isLauncher` is defined as `launcherName || startupOpened`; chrome only changes confidence inside that branch.
   - The negative guard for stripped-chrome parameter dialogs is therefore real: a form like `frmReportParam_Client` with no launcher name and no startup linkage will not become `NavigationShell` just because selectors/buttons are off.

   The ordering also protects the specific false-positive case you called out:

   - A form with a `RecordSource` reaches the earlier RecordSource-driven branches before F5b.
   - So a bound form whose name contains `"Menu"` will still classify from its data/reporting signals rather than being forced into `NavigationShell`.

   That said, there are two caveats:

   - The naming heuristics are intentionally broad (`Contains("Menu")`, `Contains("Switchboard")`, etc.), so unbound utility forms/macros with `"Menu"` in the name can still be promoted to `NavigationShell`. That is consistent with the session strategy, but it remains a heuristic risk.
   - The narrative pilot numbers (`frmSwitchboard`/`frmReportMenu` at `0.85`) do **not** match the shown F5b logic. The implementation tops out at `0.80` for name+chrome, `0.75` for startup-opened, and `0.70` for name-only. That is a docs/actuals mismatch, not a classifier-scope problem.

2. **The out-of-band reframing is directionally correct, but there is one honesty bug for queries.**

   The framing change itself is correct and in scope:

   - `NoNavigationEntry` is retired from emission.
   - `OutOfBandEntryPossible` explicitly explains startup/direct-object-pane/ribbon/VBA access.
   - `UnreferencedObject` is reworded so missing references are not presented as deletability evidence.
   - The generator avoids noisy overlap:
     - unbound forms do **not** get both `NoRecordSource` and out-of-band,
     - `NavigationShell` objects are excluded,
     - `PossiblyUnused` objects get only the reframed `UnreferencedObject`.

   However, `QuestionGenerator.HasHarvestedOpener(...)` is too narrow for queries. It currently considers:

   - navigation target,
   - VBA reference,
   - used as RecordSource,
   - used as RowSource.

   It does **not** consider query-to-query SQL references (`UsedInQuerySql` / equivalent inbound SQL use). So a query that is clearly in the harvested graph because another query references it can still get:

   > “No harvested opener was found...”

   That is not honest for that case. It is not an out-of-band entry path; it is an in-graph dependency that the harvester already knows about.

3. **Helper artifacts are correctly tied to launcher behavior.**

   This part looks good.

   - **Switchboard/menu macros**
     - `ClassifyMacro` now treats a macro as `NavigationShell` if it emits navigation edges **or** has launcher-style naming.
     - That matches the session rule that helper macros should be recognized even when parsing did not yield explicit nav edges.

   - **Switchboard/menu data tables**
     - `T5b` is inserted before the unreferenced fallback.
     - `tblSwitchboardItems`-style tables now classify as `StandaloneUtility`, not `PossiblyUnused`.
     - `BuildSummary` gives them a launcher-aware summary.
     - Because `UnreferencedObject` questions are only emitted for `PossiblyUnused`, these tables no longer get the deletion-scented “unreferenced” checklist item.

   So `tblSwitchboardItems` can no longer fall through to “unused” solely because it lacks inbound references. That is the correct Session 1 behavior.

   The only residual heuristic risk here is that `IsLauncherDataTableName` treats `*MenuItems*` as launcher infrastructure. In some domains, that can be a real business table name. That is broader than the safer `Switchboard` pattern, but it is a heuristic-risk note more than a scope violation.

4. **Schema additions are present, but there is a real signal/classifier consistency problem.**

   The schema side is updated correctly:

   - `LauncherSurface` and `StartupEntry` are added to the signal enum.
   - `OutOfBandEntryPossible` is added to the uncertainty enum.
   - `NoNavigationEntry` remains in the schema for backward compatibility and is documented as retired.

   The main consistency problem is in `BuildSignals` for forms:

   - The classifier only uses startup linkage as launcher evidence in the **no-RecordSource** launcher path.
   - But `BuildSignals` marks `LauncherSurface` as fired for **any** form opened by `AutoExec`, regardless of whether the form has a `RecordSource`.

   That creates a disagreement case:
   - a bound startup-opened form can classify as `MainDataEntry`/`Reporting`,
   - while still emitting `LauncherSurface = true`.

   `StartupEntry = true` is fine there; `LauncherSurface = true` is not aligned with the classifier’s own rule.

   There is also a second, smaller inconsistency:
   - the docs and comments say launcher-chrome is part of LauncherSurface evidence,
   - but `BuildSignals` never emits `"launcher-chrome"` at all,
   - even when the classifier used it to raise confidence from `0.70` to `0.80`.

   So the new signal family is present in schema/output, but it does not yet fully mirror the classifier’s actual decision basis.

5. **Scope discipline is good.**

   I do not see scope bleed into later sessions or sibling sets.

   - No parameter-dialog/parameterized-query promotion was introduced.
     - The explicit negative guard for `frmReportParam_*` staying `Unknown` is preserved.
   - No cleanup semantics were altered.
   - No stale/deletability heuristics were added.
   - No golden regeneration was attempted here.
   - The changes are confined to usage classification, usage signals, question wording, and the strategy/schema contract.

   On scope alone, this session stays inside Session 1.

6. **The test suite is good for the HomeHealthCare-shaped regressions, but it misses the two main edge cases introduced by the code.**

   The added tests cover the intended happy-paths well:

   - switchboard form delegating to macro,
   - helper macro,
   - switchboard helper table not becoming `PossiblyUnused`,
   - modal report menu,
   - startup-opened form,
   - chrome-only negative guard,
   - bound-form/report out-of-band questions,
   - suppression on `NavigationShell` and `PossiblyUnused`,
   - retirement of emitted `NoNavigationEntry`.

   The notable gaps are:

   - **No regression test for a bound launcher-named form**  
     There is no explicit test proving that a form with `RecordSource` plus a name like `frmClientMenu` still follows the RecordSource branch and does **not** become `NavigationShell`.

   - **No regression test for query-SQL inbound references suppressing out-of-band**  
     This is important because the current code appears wrong there.

   - **No regression test for signal/category alignment on startup-opened bound forms**  
     If `LauncherSurface` is intended to mirror classifier launcher recognition, there should be a test for that boundary.

   - **No regression test for launcher-chrome evidence emission**  
     If the signal contract is meant to expose `"launcher-chrome"`, that should be asserted.

   Also, as noted above, the prose pilot claim of `0.85` confidence is not pinned by tests and does not follow from the shown implementation.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/UsageIdentification/QuestionGenerator.cs",
      "summary": "OutOfBandEntryPossible can be emitted for queries that are already referenced by other queries because HasHarvestedOpener does not treat query-SQL usage as a harvested opener/reference.",
      "fix": "Extend HasHarvestedOpener to count inbound query-SQL references for queries (for example via UsedInQuerySql or an inverse query-reference index) so intermediate queries are not told that no harvested opener was found."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs",
      "summary": "BuildSignals does not mirror the classifier's launcher logic for forms: it marks any startup-opened form as LauncherSurface even when the form is bound, and it never emits launcher-chrome evidence.",
      "fix": "Gate LauncherSurface for forms on the same no-RecordSource launcher predicates used by F5b, keep StartupEntry as the standalone positive startup signal for all object types, and include launcher-chrome evidence when it contributes to the launcher confidence."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Pipeline.Tests/UsageIdentification",
      "summary": "The new regression tests do not cover the main boundary cases of a bound '*Menu*' form staying off F5b and a query referenced only through query SQL suppressing OutOfBandEntryPossible.",
      "fix": "Add focused tests for a RecordSource-backed launcher-named form, for a query consumed by another query's SQL, and for the intended LauncherSurface behavior on startup-opened bound forms."
    }
  ]
}
```

---

# Verification Round 2

1. **Launcher recognition is correct and appropriately bounded**

The form-side `F5b` implementation now matches the Session 1 intent.

What is correct:

- `UsageClassifier` computes:
  - `launcherName = SignalIndex.IsLauncherFormName(form.Name)`
  - `startupOpened = index.IsStartupOpened(form.Name)`
  - `launcherChrome = HasLauncherChrome(form)`
  - `isLauncher = launcherName || startupOpened`
- The new `else if (isLauncher)` branch sits **after** the existing `RecordSource` branches and after the explicit nav-edge shell branch.
  - That means a **bound** form still classifies from its `RecordSource` path first.
  - So the false-positive case called out in the prompt — a form whose name contains `"Menu"` but which has a `RecordSource` — is handled correctly. The added `Bound_Launcher_Named_Form_Follows_RecordSource_Branch_Not_F5b` test exercises that.

The launcher-chrome bound is also correct:

- `HasLauncherChrome` only checks classic unbound-shell traits:
  - no `RecordSource`
  - `RecordSelectors` false
  - `NavigationButtons` false
- But chrome is **not** used in `isLauncher`; it only adjusts confidence once name/startup evidence already exists.
- `BuildSignals` mirrors that by only emitting `"launcher-chrome"` when a primary launcher signal already fired.

So the negative guard is correct:

- `frmReportParam_*`-style stripped-chrome dialogs are **not** promoted to `NavigationShell`.
- The new `Stripped_Chrome_Parameter_Dialog_Without_Launcher_Name_Stays_Unknown` test verifies that.

Residual heuristic risk still exists, but it is the documented heuristic, not an implementation drift:

- `IsLauncherFormName` treats `*Menu*`, `*Switchboard*`, `Main`, `Startup`, `Home`, etc. as launcher-ish.
- That can still over-classify some genuinely unbound non-launcher utilities if they happen to use those names.
- But that risk is bounded:
  - bound forms are protected by the `RecordSource` branches
  - chrome alone does not classify
  - startup linkage is a legitimate positive entry signal

So concern 1 is functionally satisfied.

---

2. **Out-of-band reframing is honest and no longer reads like a cleanup hint**

This part is substantially fixed and aligned with the spec.

What changed correctly in `QuestionGenerator`:

- The older `NoNavigationEntry` emission is gone.
- The new question is `OutOfBandEntryPossible`, with wording that explicitly frames missing openers as:
  - application startup
  - direct object-pane navigation
  - ribbon/menu command
  - VBA in another object
- The reworded `UnreferencedObject` question now explicitly says that a missing harvested reference is **not by itself evidence of staleness**.

The firing scope is also correct:

- Eligible objects are:
  - all `Report`
  - all `Query`
  - `Form` only when `recordSource` is present
- Additional guards:
  - `confidence < 0.8`
  - not `NavigationShell`
  - not `PossiblyUnused`
  - no harvested opener via `HasHarvestedOpener(...)`

That means:

- unbound forms do **not** get both `NoRecordSource` and out-of-band uncertainty piled on
- `PossiblyUnused` objects keep only the reframed `UnreferencedObject` question
- launcher shells are not nagged with an entry-path uncertainty

The Round 1 query-SQL miss is fixed:

- `HasHarvestedOpener` now includes `index.UsedInQuerySql.Contains(name)`
- so intermediate queries consumed only by another query’s SQL are treated as in-graph dependencies, not as missing-openers
- the new `Query_Consumed_By_Another_Query_Does_Not_Get_OutOfBand_Question` test covers that

Backward compatibility is handled correctly:

- `UncertaintySignal.NoNavigationEntry` remains in code and schema
- the schema description explicitly says it is retained for historical outputs and no longer emitted

One subtle point is also correct: startup-opened objects are suppressed from out-of-band uncertainty because they are already `NavigationTargets`, so `HasHarvestedOpener` returns true through `isNavigationTarget`.

So concern 2 is satisfied.

---

3. **Helper artifacts are now tied to the launcher, with one minor wording defect**

The helper-table behavior is correct:

- Table rule `T5b` now recognizes launcher data tables by name
- it runs before the `unreferenced`/`PossiblyUnused` path
- those tables classify as `StandaloneUtility`
- `BuildSummary` gives them a launcher-aware summary
- `BuildSignals` emits `LauncherSurface`
- because they are not `PossiblyUnused`, they do **not** get the `UnreferencedObject` question

That directly addresses `tblSwitchboardItems` no longer reading as a generic unreferenced object. The new test covers this well.

The macro behavior is mostly correct:

- macros with nav edges remain `NavigationShell`
- launcher-named macros also classify as `NavigationShell` even if no nav edges parsed

However, there is still a **minor explanation-layer defect** in the macro summary:

- In `UsageClassifier`, macro summary is built as:

  > `"Macro '{macro.Name}' containing navigation actions that open forms or reports for a launcher surface"`

- That summary is used whenever `isLauncherMacro` is true
- But `isLauncherMacro` is true for **either**:
  - `hasNavEdges`
  - **or** `launcherNamed`

So in the name-only fallback case — the exact new Session 1 path for macros whose launcher role is inferred from naming even though their opens did not parse into edges — the summary still asserts that the macro is *containing navigation actions that open forms or reports*. That overstates the harvested evidence.

Classification is fine; the wording is the problem.

Recommended behavior would be:

- keep the current summary when `hasNavEdges == true`
- use a softer summary when `launcherNamed == true && hasNavEdges == false`, e.g.:
  - `"Macro '{name}' appears to support a launcher surface by naming convention"`
  - or
  - `"Launcher/helper macro '{name}' associated with application navigation"`

So concern 3 is mostly satisfied, but there is one minor honesty issue in the name-only macro summary path.

---

4. **Signal/schema consistency is good**

The schema and signal plumbing are consistent.

Schema updates are correct:

- `LauncherSurface` and `StartupEntry` were added to the signal enum in `object-use-schema.json`
- `OutOfBandEntryPossible` was added to the uncertainty enum
- `NoNavigationEntry` remains in the uncertainty enum with explicit backward-compatibility text

Signal emission now agrees with the new classifier behavior for the important form cases:

- `LauncherSurface` for forms is now gated to:
  - unbound form
  - launcher name and/or startup-opened
  - chrome only as corroboration
- That mirrors `F5b`, which fixes the Round 1 mismatch where a bound startup-opened or launcher-named form could emit `LauncherSurface=true` while classifying from a data branch.
- `StartupEntry` is emitted independently, which is correct:
  - a bound startup-opened form can honestly show `StartupEntry=true`
  - while still **not** claiming `LauncherSurface`

The added boundary tests cover that exact distinction.

For tables and macros:

- launcher-named helper tables emit `LauncherSurface`
- launcher helper macros emit `LauncherSurface` via nav edges and/or launcher naming

One nuance worth stating explicitly: `LauncherSurface` is **not** a synonym for `NavigationShell`. A form can still classify `NavigationShell` via the older F5 “has navigation edges” rule without firing `LauncherSurface` if it is not specifically launcher-like by Session 1 heuristics. That is consistent with the signal’s narrower meaning.

So concern 4 is satisfied.

---

5. **Scope discipline is maintained**

I do not see scope bleed.

In scope items are what changed:

- launcher/switchboard/startup/menu recognition on the usage side
- explicit out-of-band entry uncertainty wording and question generation
- helper macro/table explanation
- signal/schema/docs alignment for the above

Out-of-scope items were not pulled in:

- no parameter-dialog or parameterized-query promotion
  - the negative guard test for `frmReportParam_Client` confirms this remains `Unknown`
- no cleanup-pipeline semantics change
  - the diffs are confined to usage identification, schema, docs, and tests
- no COM extraction changes
- no golden regeneration
  - as stated, pilot evidence was run against committed IR without regenerating the committed golden
- no duplicate-table-shape or obsolete-module-header heuristics folded in

So concern 5 is satisfied.

---

6. **Test coverage is strong, but one core new macro path is still unpinned**

The regression coverage is materially improved and covers the major Session 1 scenarios well:

Covered well:

- switchboard form delegating opens to a helper macro
- switchboard helper macro with nav edges
- switchboard helper table not becoming `PossiblyUnused`
- modal report-menu form
- startup-opened launcher form
- chrome-only negative guard for parameter dialog
- bound launcher-named form boundary
- bound startup-opened form boundary
- launcher-chrome evidence emission
- out-of-band suppression for in-graph query dependencies
- `PossiblyUnused` vs out-of-band question separation
- retirement of emitted `NoNavigationEntry`

The remaining gap is this:

- there is **no direct regression test for the new macro fallback path where a macro is classified as launcher/helper by name alone, with no parsed navigation edges**.

That matters because:

- it is one of the specific Session 1 additions
- it is also the path where the macro summary wording currently overstates evidence

A targeted missing test would look like:

- macro name such as `mcrSwitchboardHelper` or `mcrMainMenu`
- no `OpenForm`/`OpenReport` actions that would generate nav edges
- assert:
  - `Classification == NavigationShell`
  - `LauncherSurface == true`
  - summary uses cautious name-based wording, not “containing navigation actions...”

So test coverage is good overall, but not complete for one of the newly added macro behaviors.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs, tests/AccessHarvester.Pipeline.Tests/UsageIdentification/LauncherSurfaceTests.cs",
      "summary": "The new name-only launcher-macro fallback is not directly tested, and in that path the macro summary overstates harvested evidence by claiming the macro contains navigation actions even when classification came only from naming.",
      "fix": "Split the macro summary by evidence source so name-only launcher macros use cautious wording, and add a regression test for a switchboard/menu-named macro with no parsed navigation edges asserting NavigationShell, LauncherSurface, and the non-overstated summary."
    }
  ]
}
```

---

# Verification Round 3

### 1. Launcher recognition is correct and bounded

The core F5b behavior is implemented correctly in `UsageClassifier.ClassifyForm`. The new launcher path is driven by:

- `SignalIndex.IsLauncherFormName(form.Name)`
- `index.IsStartupOpened(form.Name)`
- `HasLauncherChrome(form)` as a corroborator only

and it sits **after** the RecordSource-driven branches. That means a bound form still classifies from its data signals first, which is exactly the intended guardrail. The added regression for `frmClientMenu` confirms the important false-positive boundary: a bound form whose name contains `Menu` does **not** get forced into `NavigationShell`.

The chrome rule is also properly bounded now. `HasLauncherChrome` returns false for bound forms, and `BuildSignals` only emits `LauncherSurface` for forms when the form is unbound **and** already has a primary launcher signal (name/startup). So stripped chrome does not classify or signal launcher-ness on its own. The negative guard test for `frmReportParam_Client` correctly preserves Session 2 scope: it stays `Unknown`, not `NavigationShell`.

Startup linkage is handled as intended for the direct AutoExec case. `SignalIndex.Build` now collects `StartupOpenedObjects`, and the classifier uses that to recognize a startup-opened unbound form as a launcher surface even when it emits no nav edges of its own.

What is still slightly off is the **explanation text**, not the classification logic: launcher-specific summary wording is now applied too broadly to all `NavigationShell` forms, including ordinary edge-backed nav shells that have no launcher-specific evidence. I call that out below as a minor issue.

---

### 2. Out-of-band reframing is honest and not a staleness signal

This part is materially improved and aligned with the session rule.

`QuestionGenerator` now treats “no opener found” as an uncertainty explanation instead of a cleanup hint:

- `OutOfBandEntryPossible` uses startup/direct-object-pane/ribbon/VBA wording
- `UnreferencedObject` is reworded to explicitly say that missing references are **not** evidence of staleness
- `NoNavigationEntry` is retained for backward compatibility, but no longer emitted

The firing scope is also correctly bounded:

- Reports and queries are eligible
- Forms are eligible **only when bound**
- `NavigationShell` is excluded
- `PossiblyUnused` is excluded
- unbound forms therefore do not get both `NoRecordSource` and out-of-band entry questions piled together

The Round 1 major bug is fixed correctly: `HasHarvestedOpener` now checks `index.UsedInQuerySql`, so an intermediate query consumed only by another query’s SQL is treated as in-graph and does **not** get the out-of-band question.

I do see one remaining **test gap** here: there is still no direct positive regression for a query that is **not** `PossiblyUnused`, has no harvested opener, and should therefore emit `OutOfBandEntryPossible`. The report case is covered, and the query suppression case is covered, but the positive query path itself is not pinned.

---

### 3. Helper artifacts are tied to the launcher

This is largely correct.

For switchboard/helper tables, the new `T5b` rule does what Session 1 asked for:

- `SignalIndex.IsLauncherDataTableName` recognizes switchboard/menu config tables
- classification becomes `StandaloneUtility` instead of falling through to `PossiblyUnused`
- `BuildSummary` gives launcher-aware wording
- `QuestionGenerator` does not emit `UnreferencedObject` because that only fires for `PossiblyUnused`

That means `tblSwitchboardItems` can no longer read as deletable solely because it lacks inbound references. This is the right behavior.

For macros, the new distinction between:

- edge-backed launcher helpers, and
- name-only launcher helpers

is also improved. The name-only case now uses cautious wording and no longer falsely claims parsed navigation actions.

However, there is one remaining wording problem: in `ClassifyMacro`, **every** macro with parsed navigation edges now gets the summary:

> “containing navigation actions that open forms or reports for a launcher surface”

That is too strong. A macro can open a form/report without being demonstrably a switchboard/menu launcher helper. So the summary is honest for `mcrSwitchboardActions`, but it overstates generic edge-backed macros. That is a minor explanation-layer defect.

---

### 4. Signal/schema consistency

The enum/schema work is in good shape:

- `LauncherSurface` and `StartupEntry` were added to `UsageSignalType`
- `OutOfBandEntryPossible` was added to `UncertaintySignal`
- the JSON schema enum lists were updated accordingly
- `NoNavigationEntry` remains present in schema/contract for backward compatibility, which is appropriate

The signal/category mismatch from Round 1 is also fixed. `BuildSignals` now mirrors the form-side F5b guard properly:

- bound launcher-named forms no longer emit `LauncherSurface`
- bound startup-opened forms can emit `StartupEntry` without incorrectly emitting `LauncherSurface`
- launcher chrome now appears as evidence only when it is a corroborator, not as a standalone trigger

So I do **not** see a remaining runtime signal/category contradiction of the type that existed in Round 1.

The remaining inconsistency is at the explanation layer: launcher-specific summary wording is broader than launcher-specific signal evidence. In other words, the signals are now disciplined, but the summary text still sometimes says “launcher” where the evidence only says “navigation shell”.

---

### 5. Scope discipline

The change stays within Session 1 scope.

I do not see evidence of spillover into:

- parameter-dialog or parameterized-query promotion
- cleanup/staleness semantics
- COM extraction
- committed golden regeneration

The explicit negative guard for `frmReportParam_*` is present and matches the session boundary. The cleanup-side “absence of references is not stale-like evidence” rule is respected here: this session only changes usage explanation/question wording and launcher recognition, not cleanup decisions.

So on scope control, this round is disciplined.

---

### 6. Test adequacy

The new test coverage is strong and clearly targeted to the session risks. The suite now covers:

- switchboard form delegating opens to a helper macro
- edge-backed switchboard helper macro
- name-only launcher macro summary caution
- launcher helper table not becoming `PossiblyUnused`
- modal report menu form
- startup-opened form with `StartupEntry`
- stripped-chrome parameter dialog negative guard
- bound launcher-named form boundary
- bound startup-opened form boundary
- launcher-chrome evidence emission
- in-graph query dependency suppressing out-of-band entry
- `NoNavigationEntry` retirement behavior

That said, I would still flag two uncovered/undercovered claims:

1. There is no direct positive regression for a **query** that should emit `OutOfBandEntryPossible`.
2. There is no regression for a **generic non-launcher** edge-backed navigation form/macro preserving neutral summary wording, which would have caught the remaining summary overstatement.

So the suite is very close, but not fully airtight on the last explanation-layer edge cases.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs (BuildSummary, ClassifyMacro)",
      "summary": "Launcher-specific usage summaries are now emitted for all NavigationShell forms and all edge-backed macros, even when the evidence only shows generic navigation behavior rather than switchboard/menu/startup launcher traits.",
      "fix": "Make launcher-aware summary text conditional on actual launcher evidence (F5b name/startup/launcher-surface evidence or launcher-named helper macro); keep the older neutral navigation-shell wording for generic edge-backed forms/macros that are not positively identified as launcher helpers."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Pipeline.Tests/UsageIdentification/QuestionGeneratorTests.cs / LauncherSurfaceTests.cs",
      "summary": "The suite still lacks a direct positive regression asserting that a non-PossiblyUnused Query with no harvested opener emits OutOfBandEntryPossible.",
      "fix": "Add a query test case that classifies as Reporting or Unknown (not PossiblyUnused), has no harvested opener, and asserts OutOfBandEntryPossible is emitted while NoNavigationEntry remains absent."
    }
  ]
}
```

---

# Verification Round 4

1. **Launcher recognition is correct and mostly well-bounded.**

The form-side F5b implementation matches the Session 1 intent well:

- `UsageClassifier` now recognizes an unbound launcher form from either:
  - a launcher-like name (`IsLauncherFormName`), or
  - startup linkage (`index.IsStartupOpened(form.Name)`).
- `HasLauncherChrome` is only used as a corroborator. It cannot classify on its own, because `isLauncher` is computed from name/startup only, and chrome merely raises confidence.
- The negative guard for stripped-chrome parameter dialogs is present and appropriate:
  - `frmReportParam_Client` remains `Unknown`
  - `LauncherSurface` does not fire
- The bound-form guard is also present:
  - the classifier order still lets the `RecordSource` branch win before F5b
  - `BuildSignals` now mirrors that by only firing `LauncherSurface` for unbound forms
  - the added tests for `frmClientMenu` and startup-opened bound forms directly cover that boundary

So for forms, the launcher recognition is behaving as intended and the “chrome alone must not classify” rule is correctly enforced.

For macros, the distinction between:
- edge-backed navigation macro,
- name-only launcher helper macro,
- generic non-launcher macro

is now also properly bounded in summary wording. That fixes the earlier overstatement problem.

The one remaining boundedness problem is not on forms; it is on **tables** and I cover it under concerns 3 and 4.

---

2. **Out-of-band reframing is honest and no longer acts like a staleness hint.**

This part is implemented cleanly.

### What is correct

`QuestionGenerator` now replaces the old `NoNavigationEntry` framing with `OutOfBandEntryPossible`, and the new wording is consistent with the spec:

- it explicitly names startup, direct object-pane navigation, ribbon/menu commands, and VBA in another object
- it asks for confirmation of reachability vs genuine non-use
- it does **not** imply the object is deletable

The firing scope is also correctly bounded:

- **Reports**: eligible
- **Queries**: eligible
- **Forms**: eligible only when they have a `RecordSource`
- **Unbound forms**: excluded, so they do not get both `NoRecordSource` and out-of-band questions piled on
- **NavigationShell**: excluded
- **PossiblyUnused**: excluded, so they keep only the reframed `UnreferencedObject` question

The Round 1 defect around intermediate queries is fixed properly: `HasHarvestedOpener` now treats `UsedInQuerySql` as an in-graph opener/reference. That means a query consumed only by another query’s SQL no longer gets the wrong out-of-band question.

The reworded `UnreferencedObject` question is also aligned with the session rule: missing references are described as uncertainty, not as evidence of staleness.

### Backward-compatibility handling

`NoNavigationEntry` is retained in `UncertaintySignal` and in the JSON schema enum, but the current writer no longer emits it. That matches the stated compatibility requirement.

I do not see any remaining issue in the out-of-band question logic itself.

---

3. **Helper artifacts are mostly tied to the launcher correctly, but the table handling is still too broad after classification.**

The intended positive cases are good:

- `mcrSwitchboardActions` is treated as `NavigationShell`
- name-only launcher helper macros are classified as `NavigationShell` with cautious wording
- `tblSwitchboardItems` no longer falls through to `PossiblyUnused`
- the launcher helper table no longer gets an `UnreferencedObject` question

That satisfies the session goal for the HomeHealthCare-shaped case.

### Remaining issue

The problem is that **launcher-table handling is only bounded in the classifier, not in the later summary/signal emission**.

Specifically:

- `ClassifyTable` uses `IsLauncherDataTableName(table.Name)` only in the `T5b` branch, which is good
- but `BuildSummary` unconditionally special-cases **any** table whose name matches `IsLauncherDataTableName`
- and `BuildSignals` also unconditionally fires `LauncherSurface` for **any** table whose name matches `IsLauncherDataTableName`

So a table like `tblMenuItems` can be classified by some other positive table rule, yet still:
- get a launcher-specific summary, and
- emit `LauncherSurface=true`

even if it is actually business data rather than switchboard configuration.

That means the “helper artifact is tied to the launcher” behavior is not actually tied to the T5b decision; it is re-inferred later from name alone. This is the remaining defect in the set.

---

4. **Schema updates are correct, but there is still one signal/category consistency hole.**

The schema changes themselves are good:

- `LauncherSurface` added to `UsageSignalType` and schema enum
- `StartupEntry` added to `UsageSignalType` and schema enum
- `OutOfBandEntryPossible` added to `UncertaintySignal` and schema enum
- compatibility note for retained `NoNavigationEntry` is documented

For forms, the signal/category agreement is now materially better:

- bound launcher-named or startup-opened forms no longer emit `LauncherSurface`
- `StartupEntry` can still honestly fire independently
- the tests added for these cases are the right ones

For queries, the in-graph dependency fix keeps question generation aligned with actual harvested references.

### Remaining inconsistency

The remaining disagreement is again on **launcher-data tables**:

- `LauncherSurface` for tables is driven by `IsLauncherDataTableName(name)` alone
- the table’s category may come from some other rule
- `BuildSummary` can still force a launcher-specific narrative even when the classifier did not choose the launcher-table branch

So there remains a real case where the new signal and summary can disagree with the actual category decision.

This is not a schema problem; it is an emission-logic problem in `UsageClassifier`.

---

5. **Scope discipline is good.**

I do not see scope creep.

What stayed in bounds:

- No parameter-dialog promotion:
  - `frmReportParam_*` remains `Unknown`
  - chrome alone does not create launcher classification
- No cleanup-pipeline changes:
  - all changes are under usage identification, schema, docs, and tests
- No COM extraction changes
- No committed usage golden regeneration
- No duplicate-table-shape / obsolete-module-header heuristics mixed in
- No reopening of cleanup semantics

So aside from the one remaining table signal/summary issue, the session stayed disciplined.

---

6. **Test coverage is strong for the intended session cases, but it misses the remaining table false-positive boundary.**

The new and updated tests cover the stated session claims well:

- switchboard form delegating to helper macro
- switchboard helper macro
- cautious summary for name-only launcher macro
- switchboard helper table not `PossiblyUnused`
- modal report menu
- launcher-chrome corroboration
- startup-opened form
- stripped-chrome parameter dialog negative guard
- bound launcher-named form boundary
- startup-opened bound form boundary
- query consumed by another query SQL suppressing out-of-band
- query with no opener getting out-of-band
- neutral summaries for generic edge-backed nav form and macro

That is good coverage for concerns 1, 2, and the already-found macro wording issues.

### Missing regression

There is still no regression for the remaining launcher-table boundedness hole, e.g. a table such as:

- `tblMenuItems`
- positively used by a form/query and/or relationships

should **not** automatically emit `LauncherSurface` or get the launcher-config summary merely because its name matches the helper-table heuristic.

A targeted regression here would have caught the current issue.

---

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs (BuildSummary, BuildSignals table launcher handling)",
      "summary": "Launcher-table summary and LauncherSurface emission are re-derived from table name alone, so a non-launcher table with a name like 'tblMenuItems' can still get launcher-specific output even when the classifier chose a different table rule.",
      "fix": "Thread an explicit 'launcher-data-table' flag from the T5b classification path into BuildFinding/BuildSummary/BuildSignals (or otherwise gate the table launcher summary/signal on the actual T5b decision rather than name alone), and add a regression test for a positively used launcher-like table name that must not emit launcher output."
    }
  ]
}
```
