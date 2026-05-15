# AI Assignment: `generalization-validation-on-non-northwind` Session 1

**Objective:** Inventory the `Add a Client` workflow from the HomeHealthCare sample, author the corresponding seven-section workflow package, generate characterization tests for its behavior-bearing procedures, and perform a cross-provider verification of the session artifacts.

---

### Task 1: Required Reading

**Progress Key:** `session-1/required-reading`

Read the following documents to establish context for the session goals and constraints.

-   `docs/planning/project-guidance.md`
-   `docs/planning/lessons-learned.md`
-   `docs/planning/session-set-authoring-guide.md`
-   `docs/planning/strategy.md`
-   `docs/homehealthcare-northwind-comparison.md`
-   `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
-   `docs/session-sets/workflow-package-pilot/_template.md`

Confirm completion before proceeding.

---

### Task 2: Prior Session Commit Check

**Progress Key:** `session-1/prior-session-committed-check`

Execute `git status --short` against the following paths:
-   `docs/session-sets/workflow-package-pilot/`
-   `docs/session-sets/form-report-code-and-grouping/`
-   `tests/AccessHarvester.CharacterizationTests/`

If any uncommitted files are found, STOP and report them. Otherwise, proceed.

---

### Task 3: Harvester Pipeline Execution or Fallback

**Progress Key:** `session-1/harvester-pipeline-or-fallback`

**Primary path (if Access COM is available):**
Run the harvester pipeline against `samples/HomeHealthCare.accdb`.
-   **KB output path:** `tmp/generalization-validation-on-non-northwind/kb/`
-   **IR output path:** `tmp/generalization-validation-on-non-northwind/ir/`

**Fallback path (if Access COM is unavailable):**
Use the pre-generated YAML files located at `samples/DatabaseYaml/HomeHealthCare.accdb/` as the structured source for inventory. Document the use of this fallback path at the top of the `inventory.md` file created in the next step.

---

### Task 4: HHC Inventory

**Progress Key:** `session-1/inventory`

Analyze the structured source from Task 3 to identify all artifacts comprising the `Add a Client` workflow. Create and populate `docs/session-sets/generalization-validation-on-non-northwind/inventory.md` with the findings.

For each artifact, note its Northwind analogue, if any.

**Artifacts to locate:**
-   **Forms:** `frmClientSearch`, `frmClientDetail`, and any other forms in the add path.
-   **Event Handlers:** The "Add Client" button's `OnClick` event on `frmClientSearch`, `Form_Load`/`Form_Open`/`Form_BeforeUpdate`/`Form_BeforeInsert` on `frmClientDetail`.
-   **Queries & Tables:** Identify bound queries and the underlying tables, primarily `tblClients` and its related entities (e.g., addresses, contacts).
-   **Behavior-Bearing Procedures:** Identify VBA functions/subs and data macros (`mcrValidation` on `tblClients`) that perform validation, calculation, state changes, or gate saving/deleting.

---

### Task 5: Author `Add a Client` Workflow Package

**Progress Key:** `session-1/package-draft`

Create the file `docs/session-sets/generalization-validation-on-non-northwind/packages/add-a-client.md`. Author the package using the seven-section template.

-   Pay close attention to the `OpenArgs` contract used by `frmClientDetail`.
-   Adhere strictly to the anti-coupling and evidence rules from the `workflow-package-pilot` session set.
-   **STOP Rule:** If you are forced to invent fields or logic to fit the seven-section template, STOP and report the mismatch to the human operator.

---

### Task 6: Generate Characterization Tests

**Progress Key:** `session-1/characterization-tests`

For each behavior-bearing procedure identified in Task 4 that is not tightly coupled to the UI, generate characterization tests.

1.  Create a new test file under `tests/AccessHarvester.CharacterizationTests/AddAClient/`.
2.  Use the path-b-probe pattern: translate the VBA procedure to C# for test scaffolding, write xUnit tests to verify its logic, then discard the C# translation, retaining only the stable tests.
3.  If a procedure is too UI-coupled to test in isolation, log it in the "Open questions / ambiguity log" section of the package instead of creating a test file.

---

### Task 7: Record Harvester Coverage Notes

**Progress Key:** `session-1/harvester-coverage-notes`

In the `inventory.md` file, add inline notes assessing the harvester's output quality and coverage for the HomeHealthCare database artifacts. Examples:
-   "VBA signatures for `basDataOperations` appear correct."
-   "Data macros on `tblClients` are visible but the action list is sparse."

---

### Task 8: Build and Test

**Progress Key:** `session-1/build-test`

Execute the following commands sequentially and report their full output.

1.  `dotnet build`
2.  `dotnet test`

Ensure all tests pass before proceeding.

---

### Task 9: Cross-Provider Session Verification

**Progress Key:** `session-1/verification`

Invoke the `session-verification` task. The router will delegate this to the alternate provider for independent peer review of the artifacts generated in this session. The output will be saved to `docs/session-sets/generalization-validation-on-non-northwind/session-reviews/session-001.md`.

---

## Session 1 Actuals

- **Orchestrator used:** Codex GPT-5 / OpenAI.
- **Actual routed cost:** $0.0757 total. `ai-assignment.md` authoring (`analysis` -> `gemini-pro`): $0.0161. Cross-provider verification (`query("gemini-pro", task_type="session-verification")`): $0.0511. Next-session recommendation (`analysis` -> `gemini-pro`): $0.0085.
- **What landed:** `inventory.md`, `packages/add-a-client.md`, the `tests/AccessHarvester.CharacterizationTests/AddAClient/` characterization suite, `session-reviews/session-001.md`, `activity-log.json`, `session-state.json`, and `session-1-next-orchestrator-recommendation.md`.
- **Build and test:** `dotnet build` succeeded. `dotnet test` initially failed only because the pre-existing canonical Northwind temp fixture under `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/` was absent locally; the session regenerated it with `AccessHarvester.Cli` and re-ran `dotnet test` to a full green pass.
- **Verification route:** Used `query("gemini-pro", task_type="session-verification")` directly because `ai_router/router-config.yaml` pins `session-verification` to `gpt-5-4` for the Claude orchestrator; leaving that override in place would not have been cross-provider for this OpenAI-led session. Round 1 verdict: `VERIFIED`, issues: `[]`.
- **Notes for next-session calibration:** The verification prompt shape worked well: analysis-first framing plus a trailing JSON block produced a substantive round-1 review with no terse-verdict retry. The key source-grounding correction for this set is now stable: `tblClients` has no attached data macro, so Session 2 should keep treating the workflow as form-code-plus-table-rule enforced while independently pressure-testing the package's delete/deactivate and dual-add-entry claims.

## Next-session orchestrator recommendation (Session 2)

**Next-session orchestrator recommendation (Session 2):** Anthropic (Claude Code / Max), high effort.

The session-2 spec explicitly requires an independent review conducted by "The OTHER orchestrator from Session 1." Since Session 1 was orchestrated by OpenAI/Codex, Session 2 must be orchestrated by Anthropic. The effort is high, as the session includes a full semantic review of the Session 1 package and tests against source artifacts, authoring new falsification tests for missed invariants, and authoring the final generalization-findings report with its strategic verdict.

This session is the capstone for the set, and its primary deliverable is the `generalization-findings.md` report. The orchestrator must synthesize the results of the peer review into a clear verdict (`generalizes-cleanly`, `revise-and-retry`, or `pivot`) and provide the supporting evidence, which requires strong analytical and writing capabilities.
