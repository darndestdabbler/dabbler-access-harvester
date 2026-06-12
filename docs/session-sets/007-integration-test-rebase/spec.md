# Integration Test Rebase Spec

> **Purpose:** Restore the `AccessHarvester.Integration` suite to green on a
> clean checkout by re-basing the 7 tests that depend on the never-committed,
> NorthwindDevEdition-derived `examples/northwind-output.json` onto committed
> HomeHealthCare-derived fixtures, and dispositioning the one residual COM
> `sanitize` timeout (re-measured 2026-06-01 as an intermittent contention
> flake). This is **compliance/maintenance work**, not feature work: it is the
> standing recommendation from set `005` (re-confirmed as P0 by set `006`'s
> review), and it unblocks every correctness fix `006` proposed — they all
> regenerate goldens and need a green integration baseline to validate against.
> **Created:** 2026-06-12
> **Session Set:** `docs/session-sets/007-integration-test-rebase/`
> **Prerequisite:** `005-northwind-embedded-residue-remediation` (complete —
> the NorthwindDevEdition-free baseline this set re-bases onto);
> `006-solution-review-and-upstream-feedback-discovery` (complete — provenance
> of the P0 ranking and the constraint framing)
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 2
prerequisites:
  - slug: 005-northwind-embedded-residue-remediation
    condition: complete
  - slug: 006-solution-review-and-upstream-feedback-discovery
    condition: complete
```

> Rationale: no UI surface in this repo; the quality gate is build + tests +
> cross-provider verification. The set touches test code and committed example
> fixtures only — `requiresE2E` does not apply (the Integration suite itself
> is the behavior under repair). Full tier keeps mandatory cross-provider
> verification on both sessions.

---

## Project Overview

Set `005` left the tracked tree NorthwindDevEdition-free at `HEAD` and
recorded the cost: the `AccessHarvester.Integration` suite has **8
pre-existing failures on a clean checkout** (`005` change-log → "Known
follow-up"). Seven depend on `examples/northwind-output.json` — a local-only,
NorthwindDevEdition-derived manifest example that was never committed and must
never be regenerated (regeneration reintroduces the residue `003`–`005`
removed). One is the `SanitizeCommandTests.HomeHealthCare_sanitize…` timeout,
re-measured on 2026-06-01 (`harvester-report-displayed-total-parity` S2) as an
**intermittent contention flake**, not a persistent failure (the persistent
COM-shutdown offender was `classify`, fixed in `harvester-fix-classify-timeout`).

The grep-grounded dependency surface (2026-06-12): the following five test
files reference `RepoPaths.Examples()/northwind-output.json` —

- `tests/AccessHarvester.Integration/Pipeline/ManifestSchemaConformanceTests.cs:152`
  (smoke fixture; the in-file comment says it deliberately exercises a
  **v1.0-shape** manifest with no `Documentation`/`FileMap`)
- `tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs:75,101`
- `tests/AccessHarvester.Integration/Cli/CliValidateTests.cs:21,256,268`
- `tests/AccessHarvester.Integration/Cli/CliGenerateDocsTests.cs:21`
- `tests/AccessHarvester.Integration/Cli/CliDeterminismTests.cs:44`

The committed, compliant replacement source is the HomeHealthCare golden
(`samples/GoldenOutput/HomeHealthCare/manifest.json`, regenerated fresh and
re-validated by `006` Session 4). `examples/` currently holds only small
form-shaped examples (`complex-form.json`, `minimal-form.json`,
`validate-invalid-manifest.json`) — there is no manifest-shaped committed
example today.

**Non-goals:** fixing any of the `006` review findings (wrong
`QueryTypeName`, empty `ParserOutput`, BOM, etc. — those are sets P1a/P1b/P2
onward). The fixtures this set commits may legitimately embody those known
defects; these tests assert tool behavior over a manifest input, not manifest
content correctness.

## Fixture Strategy (decided in Session 1, implemented same session)

Session 1 step 3 makes the copy-vs-reference call explicitly, but the
constraints are fixed here:

- Replacement fixtures derive **only** from committed HomeHealthCare
  artifacts (the golden manifest, or a hand-trimmed variant of it).
- `ManifestSchemaConformanceTests`' v1.0-shape compatibility intent must be
  preserved — if the current golden is v1.1-shaped, the v1.0-shape case gets
  a hand-trimmed committed variant (e.g.
  `examples/homehealthcare-output.v1_0.json`), not a silent intent downgrade.
- Whatever is committed under `examples/` follows the recorded lesson
  *"Regenerate Committed Example Files Alongside The Schema Set That Produces
  Them"* — the example must state (in a sibling README note or header comment
  in the test) where it came from and when it must be refreshed.

---

## Sessions

### Session 1 of 2: Inventory, Fixture Strategy, And Hermetic Re-Base

**Steps:**

1. Read `docs/planning/project-guidance.md`, `docs/planning/lessons-learned.md`
   (mandatory pre-session reading; specifically the incremental-test-by-project,
   never-background-dotnet, TRX-as-truth, and `.accdb`-sweep conventions), the
   `005` change-log "Known follow-up" section, and this spec.
2. Enumerate the failing tests precisely: from the five files above, list every
   `[SkippableFact]`/`[Fact]` whose body resolves `northwind-output.json`, and
   record what each test actually asserts (validate happy path, validate
   invalid-manifest exit contract, generate-docs output shape, byte-level
   determinism, packaged-binary smoke, manifest schema conformance incl. the
   v1.0-shape case). Confirm the expected failure count (7) or document the
   true number.
3. Decide the fixture strategy within the constraints above: point tests at
   the committed golden directly vs commit `examples/homehealthcare-output.json`
   (plus the v1.0-shape variant if needed). Record the decision and rationale
   in the session log; prefer the leanest option that preserves each test's
   intent and keeps `examples/` consumer-meaningful.
4. Implement the re-base: update the five test files (and add the committed
   fixture file(s) per step 3). Update assertion expectations from
   Northwind-derived names/counts to HomeHealthCare-derived ones, preserving
   each assertion's intent — do not weaken assertions to make them pass.
5. Hermetic-host validation: `dotnet build`, then run the re-based tests that
   do not require Access COM (the manifest-input CLI tests spawn the CLI
   against JSON files and do not open an `.accdb`). Sequential, foreground.
6. Cross-provider verification of the session's diff (include the five test
   files post-edit, the new fixture(s), and the step-2 inventory).
7. Closeout: `disposition.json`, `close_session`, commit, push, notify.

**Creates:** committed manifest example fixture(s) under `examples/` (per the
step-3 decision; e.g. `examples/homehealthcare-output.json`, optionally a
v1.0-shape variant)
**Touches:** `tests/AccessHarvester.Integration/Pipeline/ManifestSchemaConformanceTests.cs`,
`tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs`,
`tests/AccessHarvester.Integration/Cli/CliValidateTests.cs`,
`tests/AccessHarvester.Integration/Cli/CliGenerateDocsTests.cs`,
`tests/AccessHarvester.Integration/Cli/CliDeterminismTests.cs`, set artifacts
**Ends with:** zero references to `northwind-output.json` under `tests/`
(`grep -r northwind-output tests/` returns nothing); the re-based non-COM
tests pass on the dev host; verification VERIFIED.
**Progress keys:** `s1-inventory`, `s1-fixtures-rebased`, `s1-verified`

---

### Session 2 of 2: COM-Tier Validation, Flake Disposition, And Next-Set Authoring

**Steps:**

1. On the Access-equipped host: run the full `AccessHarvester.Integration`
   suite — deliberately, sequentially, foreground, with a TRX logger and the
   TRX `<Counters>` as the completion signal (the wrapper may hang after xUnit
   finishes; reap any wedged `testhost` via WMI `Terminate` per standing
   authorization). Expected outcome: the 7 re-based tests green; total
   failures 0, or 1 if the sanitize flake fires.
2. Disposition the `SanitizeCommandTests.HomeHealthCare_sanitize…` flake,
   bounded: if it failed in the batch run, re-run it targeted (the 2026-06-01
   re-measurement passed targeted in 0.5 s). If intermittent: apply the
   cheapest contention mitigation consistent with the recorded conventions
   (collection serialization is already in place; consider budget alignment or
   pre-staged input per Testing Conventions mitigation (a)) OR record it as a
   quarantine-candidate with a `Skip.If` + issue note, per the recorded
   lesson's guidance. If it has become persistent: do NOT open-ended-debug —
   record evidence and propose a scoped investigation set in the closeout. The
   step is time-boxed to characterization plus at most one mechanical
   mitigation.
3. Sweep spurious `samples/*.accdb` diffs (`git status --short -- samples/`,
   `git checkout -- samples/*.accdb`) before staging anything.
4. **Author the next session set** (the last-session-authors-next-set pattern,
   adopted 2026-06-12): create
   `docs/session-sets/008-manifest-vba-ingestion-and-parser-hardening/` with a
   `spec.md` and a `not-started` v4 `session-state.json` stub, authored per
   the canonical session-set-authoring-guide against **actual post-007
   completion state**. Source material: `006`'s `review-recommendations.md`
   §3 P1a plus the Recommendation blocks of F-3-1 (Critical), F-3-2, F-3-3,
   F-3-6, F-3-9/F-3-10 ride-alongs in `s3-review-findings.md`. The maintainer
   accepts or vetoes the new set at trigger time; authoring it does not start
   it.
5. Cross-provider verification — closeout grade: prose-first lead before the
   file listings, plus the required JSON response block. Include the TRX
   counter evidence and the 008 spec.
6. Closeout: `disposition.json` **before** the closeout commit,
   `change-log.md`, `next-session-set-recommendation.md` (routed analysis —
   carry the hard constraints, including never-regenerate-northwind, verbatim
   in the routed prompt per the 2026-06-12 lesson), `close_session`, commit,
   push, notify.

**Creates:** `docs/session-sets/008-manifest-vba-ingestion-and-parser-hardening/spec.md`,
`.../008-manifest-vba-ingestion-and-parser-hardening/session-state.json`,
set closeout artifacts (`change-log.md`, `next-session-set-recommendation.md`,
`disposition.json`)
**Touches:** possibly `tests/AccessHarvester.Integration/Cli/SanitizeCommandTests.cs`
(step-2 mitigation or quarantine only), set artifacts
**Ends with:** `AccessHarvester.Integration` green on this host per TRX (0
failures, or the sanitize flake explicitly dispositioned), zero
`northwind-output.json` dependence anywhere in `tests/`, and set `008`
authored and ready to trigger.
**Progress keys:** `s2-integration-green`, `s2-flake-dispositioned`,
`s2-next-set-authored`, `s2-verified`, `set-complete`

---

## Stop Rules

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json`**
  or any NorthwindDevEdition-derived artifact — regeneration reintroduces the
  residue sets `003`–`005` removed. All replacement fixtures derive from
  committed HomeHealthCare artifacts only.
- **Fixtures, not behavior.** This set changes test code and committed example
  fixtures only. No shipping (non-test) `.cs`, schema, or golden change. If a
  product defect blocks a green re-base, record it with evidence, stop at the
  boundary, and surface it in the notification — the `006` follow-up sets own
  product fixes.
- **Do not weaken assertions** to force green. A re-based test must check the
  same contract its Northwind version checked; intent changes require explicit
  disclosure in the verification prompt.
- **Sanitize-flake work is time-boxed** to characterize + one mechanical
  mitigation or quarantine-with-record. A persistent root-cause hunt is its
  own future set.
- COM runs are deliberate, sequential, foreground; sweep spurious `.accdb`
  diffs before staging; never background a `dotnet` invocation.

## End-Of-Set Deliverables

- `AccessHarvester.Integration` green on a clean checkout + Access host (or
  green-with-dispositioned-flake, explicitly recorded).
- Committed HomeHealthCare-derived manifest example fixture(s) under
  `examples/` with provenance noted.
- Zero `northwind-output.json` references under `tests/`.
- Set `008-manifest-vba-ingestion-and-parser-hardening` authored
  (spec + state stub), ready for the maintainer to trigger.
- Standard set artifacts: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md` per session,
  `sN-issues.json` for findings-bearing rounds, `disposition.json`,
  `change-log.md`, `next-session-set-recommendation.md`.
