# CLI End-to-End Test Audit Spec — **STUB**

> **Status:** STUB authored 2026-05-17 by `remaining-work-planner` Session 2.
> Acceptance criteria are pinned; implementation shape is left for
> the set's own Session 1.
>
> **Purpose:** For every harvester CLI subcommand, confirm at least
> one black-box end-to-end test invokes the binary and asserts on
> its output and exit code; add the missing tests; and add a
> determinism gate (run twice, diff, assert stable).
> Closes Phase 4 Criterion #4.
> **Created:** 2026-05-17
> **Session Set:** `docs/session-sets/harvester-finish-cli-e2e-test-audit/`
> **Prerequisite:** `harvester-finish-canonical-golden-output` —
> the golden corpus is the determinism gate's natural input.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface; the "E2E" here means CLI black-box
> tests, not the workflow's `requiresE2E` UI-flow semantics.

---

## Origin and authority

- **Authority for scope:** the gap audit at
  [`docs/session-sets/remaining-work-planner/gap-audit.md`](../remaining-work-planner/gap-audit.md)
  → GAP #4.
- **Authority for "Done":**
  [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4
  Criterion #4: "every harvester subcommand (`extract`, `translate`,
  `run`, `parse`, `generate-docs`) is documented, has end-to-end
  test coverage, and produces deterministic output."
- **Existing coverage to NOT duplicate:**
  - `tests/AccessHarvester.Integration/Cli/CliBootstrapTests.cs`
    (no-args, --help, unknown subcommand).
  - `tests/AccessHarvester.Integration/Cli/CliParseTests.cs`
    (parse subcommand, several fixtures).
  - `tests/AccessHarvester.Integration/Cli/CliGenerateDocsTests.cs`
    (generate-docs subcommand).
  - `tests/AccessHarvester.Integration/Pipeline/EndToEndPipelineTests.cs`
    exercises `Pipeline.Default().Run(...)` in-process but does NOT
    invoke the CLI binary.

The work shrinks accordingly: `extract`, `translate`, `run` need
black-box CLI tests; everything else needs a determinism gate.

---

## Project Overview

The CLI dispatcher (`AccessHarvester.Cli/Program.cs`) exposes
`extract`, `translate`, `run`, `parse`, `generate-docs`, plus
bare-file mode (which routes to `parse`). `parse` and
`generate-docs` already have black-box tests via `CliInvoker.Run(...)`;
`bootstrap` (no-args / --help / unknown) is covered.

What is missing:

1. Black-box tests for `extract`, `translate`, `run`. These all
   require Access COM and so are `[SkippableFact]`-gated on the
   `AccessAvailability.SkipIfNoAccess()` helper that already exists
   in `tests/AccessHarvester.Integration/TestSupport/`.
2. A determinism gate: for each subcommand that produces output, run
   it twice against the same input and assert the output is
   byte-identical (after normalization, per the canonical-golden
   set's normalization layer). The natural inputs are the canonical
   fixtures from `harvester-finish-canonical-golden-output`.

### In scope

1. Black-box `extract` test (binary invocation, asserts on exit
   code + `--json-status` envelope shape + presence of `raw/`
   tier directories).
2. Black-box `translate` test (consumes prior `extract` output;
   asserts on `kb/` tier directories).
3. Black-box `run` test (full pipeline; asserts on
   `raw/` + `kb/` + `ir/` + manifest + UnresolvedItems —
   the last two depending on
   `harvester-finish-canonical-golden-output` Session 2 landing).
4. Determinism gate: for each of `parse`, `generate-docs`,
   `run` (the three with non-trivial output), run twice and assert
   normalized output is byte-identical. **The `run` gate subsumes
   `extract` and `translate`** since `run` invokes both as
   sub-phases; a determinism failure in either surfaces as a
   `run` diff. This is the deliberate satisfaction of Criterion
   #4's "every harvester subcommand produces deterministic output"
   without redundant single-stage gates over the same code paths.
5. README / `--help` text reviewed for currency: subcommand list
   matches dispatcher; option lists match `ParseOptions`;
   exit-code legend is accurate.

### Out of scope

- Adding new subcommands. If a gap surfaces a missing subcommand
  (e.g., `validate-schema`), the fix is a separate session set, not
  scope here.
- Changing the CLI's option-parsing or dispatch behavior. If the
  audit surfaces a real defect (e.g., `--json-status` envelope is
  inconsistent across subcommands), the fix is a tree-amendment to
  the originating session set or a tiny dedicated fix set.
- Re-testing what `CliBootstrapTests` / `CliParseTests` /
  `CliGenerateDocsTests` already cover.
- **Strategy.md "Frozen Unless Reauthorized" items remain frozen.**
  Specifically: Blazor application slices / workflows / pilots,
  production IR codegen, `dabbler-platform-discovery` refresh,
  additional Northwind workflows beyond the existing four pilot
  packages, CRUD-scaffolder expansion, and any item the 2026-05-17
  strategic review froze. CLI test coverage is harvester-side
  only; no test exercises a downstream Blazor / IR-codegen /
  platform-integration consumer.

---

## Sessions

### Session 1 of 2: Black-box Tests for `extract` / `translate` / `run`

**Steps:**

1. Elaborate this stub.
2. Author `ai-assignment.md` via routed analysis.
3. Author three test classes under
   `tests/AccessHarvester.Integration/Cli/`:
   - `CliExtractTests` — `[SkippableFact]`-gated; invokes
     `AccessHarvester extract --db <HHC.accdb> --out <tmp>` and
     asserts on exit + raw tier shape + `--json-status` envelope.
   - `CliTranslateTests` — depends on a fresh `extract` run (set
     up in test fixture); invokes
     `AccessHarvester translate --in <raw> --out <kb>` and asserts
     on kb tier shape.
   - `CliRunTests` — invokes `AccessHarvester run --db <HHC.accdb>
     --out <tmp>`; asserts on all three tier directories +
     manifest presence (once
     `harvester-finish-canonical-golden-output` Session 2 wires
     manifest emission).
4. `dotnet build && dotnet test` sequentially.
5. End-of-session verification + commit + push + notify.

**Creates:**
- `tests/AccessHarvester.Integration/Cli/CliExtractTests.cs`
- `tests/AccessHarvester.Integration/Cli/CliTranslateTests.cs`
- `tests/AccessHarvester.Integration/Cli/CliRunTests.cs`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/ai-assignment.md`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/session-reviews/session-001.md`

**Ends with:** every CLI subcommand has at least one black-box
test invoking the binary.

---

### Session 2 of 2: Determinism Gate + Help-Text Audit

**Steps:**

1. Add determinism tests (run-twice-diff) for the three
   subcommands that produce non-trivial output: `parse`,
   `generate-docs`, and `run`. **The `run` determinism gate
   subsumes `extract` and `translate`** because `run = extract +
   translate + enrich` per `PrintUsage()` and the dispatcher — a
   determinism failure in either sub-phase surfaces as a `run`
   diff. This satisfies Criterion #4's "every harvester subcommand
   produces deterministic output" without authoring duplicative
   single-stage gates that would re-test the same code paths.
   Document the subsumption in the test class's XML doc-comment so
   a future reader sees the intent. The `run` determinism gate may
   reuse the `harvester-finish-canonical-golden-output` regression
   harness directly rather than duplicating it; Session 1 decides.
2. Audit `PrintUsage()` output for currency against the dispatcher.
   If any drift (missing subcommand, wrong flag, stale exit code),
   patch `PrintUsage()` AND add a test that asserts on the
   subcommand list / exit-code legend so future drift is caught.
3. `dotnet build && dotnet test` sequentially.
4. End-of-session verification.
5. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
6. Step 9 reorganization review AFTER notify.

**Creates:**
- Determinism test class(es) under
  `tests/AccessHarvester.Integration/Cli/` or
  `tests/AccessHarvester.Integration/Pipeline/`.
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/change-log.md`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/disposition.json`
- `docs/session-sets/harvester-finish-cli-e2e-test-audit/session-reviews/session-002.md`

**Touches:**
- `AccessHarvester.Cli/Program.cs` — only if `PrintUsage()` drift
  is discovered.

**Ends with:** Phase 4 Criterion #4 closed: every subcommand has
black-box coverage; determinism asserted; help text current.

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.20 per session
  (cross-provider session-verification only).
  ~$0.10–$0.40 across the set.
- **In-conversation token spend:** Low-to-moderate. The tests are
  mostly mechanical wrapping of `CliInvoker.Run(...)` patterns
  already established by `CliParseTests` / `CliGenerateDocsTests`.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Determinism fails** | Two runs against the same input produce different output even after normalization | Stop. Surface to the human. Non-determinism is a project-guidance Principle violation; fix lives in the producing component, not in the test. |
| **`PrintUsage()` drift exposes a missing subcommand** | The dispatcher accepts a subcommand the usage text omits (or vice versa) — signals a prior shipping defect | Stop. Surface to the human. Either land a corrective patch as a tree-amendment OR file as a strategy-level question if the missing surface is non-trivial. |
| **Access COM unavailable on CI** | The `[SkippableFact]`-gated tests always skip; criterion can't actually verify | Surface to the human at the end of Session 1. Criterion #4 closes "for hosts that can run the tests"; CI-availability of Access is a separate infrastructure concern. |

---

## Why this is a stub

The stub fixes the contract: three new black-box test classes,
determinism for three subcommands, help-text audit. Test file
shapes, harness reuse decisions, and the determinism-gate's
normalization-layer reuse from `harvester-finish-canonical-golden-output`
are Session 1's call.
