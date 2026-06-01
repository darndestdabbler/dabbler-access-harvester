# Project Guidance — Dabbler Access Harvester

> **Purpose:** The single source of truth for durable strategic
> commitments (Principles) and specific rules and patterns (Conventions)
> that apply to this repo and to follow-on harvester / parser work.

Read this file before every AI-led session. Use it as the first-stop
reference before changing architecture, parser strategy, output schema,
testing approach, or workflow assets.

Session-set specs may narrow the scope for a particular effort, but they
should not silently violate anything here without explicit human
approval.

The file has two top-level sections:

- **Principles** — durable strategic commitments (the *why* and *what*).
  Slow to change; every principle should explain the reasoning behind
  itself.
- **Conventions** — specific rules, patterns, values, and code styles
  (the *how*). Faster to change; often promoted from successful
  lessons or demoted from over-general principles.

When a principle implies a specific rule, put the principle under
Principles and the concrete rule under Conventions. At the end of every
session set the orchestrator proposes reorganization candidates —
promotions from Lessons to Conventions/Principles, demotions,
relocations — and the human accepts or rejects each (see
`docs/ai-led-session-workflow.md` Step 9, post-notify).

---

## Principles

### Project Scope And Audience

- The harvester's goal is to produce structured metadata (and structured
  parse trees of VBA / Access expressions) at sufficient fidelity that a
  downstream AI can reliably interpret and document the original Access
  application's behavior. **AI interpretation is explicitly out of scope
  for this repo** — this codebase is mechanical extraction and
  deterministic enrichment.
- The downstream consumer is an AI agent, not a human reviewer. Outputs
  are JSON; layouts and styling do not matter; precision, completeness,
  and machine-readability do.
- There is no UI surface in this repo. The end-of-session quality gate
  is automated build / unit + integration tests / schema validation,
  backed by mandatory cross-provider AI verification. There is no
  human-UAT layer.

### Output Architecture

- Use the three-tier output model carried over from the source codebase:
  `raw/` (Access-native artifacts: SaveAsText output, DAO JSON dumps),
  `kb/` (clean parsed JSON — AccessNode tree for Forms/Reports,
  pass-through for Queries/Tables/Relationships), `ir/` (`kb/` plus
  mechanical enrichments — geometry, label resolution, property
  decoding, VBA signatures, macro actions).
- **Interpretive slots remain null at the `ir/` tier.** Fields like
  `purpose`, `notes`, `intent`, etc. are reserved for the AI consumer
  to fill downstream. The harvester does not guess at semantics.
- The VBA parser produces its own structured output (currently
  `harvester-schema.v0.json` under `docs/test-cases/`). For now this
  schema is kept independent of the `raw/kb/ir` schemas; reconciliation
  is a future-work decision, not an early commitment.
- Keep enrichers chainable per category: the first reads `kb/`,
  subsequent ones read+rewrite the `ir/` file in place. Categories
  without an enricher pass through unchanged.

### VBA Parser Strategy

- The parser must produce structured output that is **deterministic**
  given the same input. AI consumers will diff parser output across
  runs to detect changes; non-determinism breaks that workflow.
- **Unresolvable constructs are explicit, not silent.** When the parser
  cannot resolve a name, an SQL parameter, a dynamic SQL fragment, or
  a late-bound call, it emits a node with provenance metadata (e.g.,
  `{ source: "unresolved", reason: "<string>" }`) rather than
  swallowing the construct or substituting a guess. Test case 08
  (`08-unresolvable.bas`) is the canonical example.
- Test cases in `docs/test-cases/` are the source of truth for parser
  behavior. The `<n>.expected.json` file pinned next to each input is
  the contract; parser changes are validated by re-running each input
  and comparing against the expected output.
- Prefer a real grammar over regex pattern-matching. Even when a
  feature could be extracted with a regex (e.g., procedure
  signatures), the maintenance cost of a half-grammar exceeds the
  cost of doing it properly once.

### Platform And Runtime

- C# / .NET 10. Stay on the current .NET LTS line unless the human
  explicitly approves a change.
- The COM-based Access extractor requires Windows + Microsoft Access
  installed and is gated `[SupportedOSPlatform("windows")]`. Hermetic
  unit tests (parser, enrichers, schema validators) run anywhere;
  integration tests against committed `.accdb` fixtures use
  `[SkippableFact]` to skip on non-Windows or where Access is
  unavailable.
- Synthetic test DBs are built by Python (`pywin32` + Access COM) under
  `test-builder/`. Committed `.accdb` artifacts are checked into
  `tests/.../SyntheticDbs/` so integration runs do not depend on a
  rebuild step.

### Lean By Default

Carried forward from the source codebase: the user's preference is
**lean — don't add features, abstractions, or safety nets beyond what
the task in front of you requires.** This is non-trivial guidance for
LLM orchestrators, which tend to over-engineer when in doubt. Specific
applications:

- Don't add a defensive `try/catch` around code that has no realistic
  failure path. If the catch block does nothing meaningful, the catch
  is noise.
- Don't introduce an interface for a single implementation just because
  it might one day have a second.
- Don't write a fallback heuristic when the only known input format is
  the one the test fixtures use. The fallback is dead code that still
  needs maintenance.
- Don't generate auto-detection tooling to second-guess the data
  (e.g., name-scanners that "suggest" likely-PII columns the
  business already curates) when the consumer is downstream tooling
  that can ask for the curated answer directly.

### Testing Hierarchy

Testing follows this order of preference:

1. **Strict unit tests.** Use them for any logic that can be isolated
   from I/O, runtime infrastructure, or COM. Structure code so the
   pure-logic portions are factored into methods, services, or helpers
   that can be exercised without integration setup.
2. **Schema and contract tests.** Validate harvester output against
   `harvester-schema.v0.json` (parser) and any `kb/` / `ir/` schema
   (extraction). Treat schema drift as a real defect, not a cosmetic
   one — the downstream AI consumer's prompts depend on schema shape.
3. **Hermetic integration tests** against committed synthetic
   `.accdb` fixtures. These exercise the full pipeline on inputs whose
   expected output is checked in. Use `[SkippableFact]` so non-Windows
   runs skip cleanly.
4. **End-to-end tests** against the canonical fixtures
   (`samples/HomeHealthCare.accdb` primary,
   `samples/NorthwindDevEdition.accdb` secondary) for confidence on
   real-world inputs.
5. **Cross-provider AI verification** at end of session — the last
   line of defense before commit.

### Precedence

When guidance appears in multiple places, apply it in this order:

1. Explicit human direction in the current conversation
2. Session-specific spec requirements
3. This document
4. Supporting planning notes and roadmaps

If those sources conflict in a meaningful way, stop and confirm with
the human before proceeding.

---

## Canonical Test Fixtures

| Fixture | Role | Committed? |
|---|---|---|
| `samples/HomeHealthCare.accdb` | **Primary** — unbound forms, ADODB, validation-only data macros, Hungarian naming | Yes (YAML mirror in `samples/DatabaseYaml/HomeHealthCare.accdb/`) |
| `samples/NorthwindDevEdition.accdb` | **Secondary/reference** — bound split forms, subforms, audit data macros, AutoExec startup | Binary gitignored; YAML regenerated locally |

See `docs/homehealthcare-northwind-comparison.md` for the authoritative
fixture-selection rationale and structural comparison.

**Excluded fixtures:** `samples/MiniComplex.accdb` and
`samples/ProjectBoard.accdb` are excluded from automated test coverage
pending an error-audit pass. They may be used in narrow, explicitly-scoped
experiments only.

---

## Conventions

### Code Style

- Namespaces: `AccessHarvester.Cli`, `AccessHarvester.Core`,
  `AccessHarvester.Tests`. (Preliminary — Session Set #1 confirms or
  adjusts.)
- PascalCase for public members; `_camelCase` for private fields.
- Nullable reference types enabled.
- All async methods return `Task` and use the `Async` suffix.
- COM-touching code is gated `[SupportedOSPlatform("windows")]` at the
  assembly level (see `Com/AccessComSession.cs` in the source
  codebase).

### Output JSON Conventions

- Provenance fields use a `{ source, ... }` shape (e.g.,
  `Label: { source: "attached", text: "First Name" }`,
  `Label: { source: "unresolved" }`). Consumers should never need to
  special-case missing keys — emit explicit `unresolved` rather than
  omitting the field.
- Numeric units are explicit in field names where ambiguity is
  possible: `GeometryIn: { X, Y, W, H }` for inches alongside the
  raw twip values, not a bare `Geometry`.
- Only emit fields Access actually wrote — do not synthesize zeros or
  default values to fill gaps. The downstream consumer reads omission
  as "Access default for this property"; synthesizing zeros loses
  that signal. The exception is provenance fields above, where the
  emitted record is *about* the absence.

### Testing Conventions

- Factor pure-logic portions out of mixed-responsibility methods so
  they can be strict-unit-tested. Do not keep mixed-responsibility
  methods intact when the pure-logic portion can be split out.
- Hermetic unit tests are preferred over integration tests when the
  logic does not depend on Access COM.
- For the parser specifically, every new feature lands with a test
  case under `docs/test-cases/` — input file plus
  `<n>.expected.json`. The PR / session that adds the feature also
  adds the test case.
- Build and test should be run **sequentially**, not in parallel —
  file-lock contention against the same worktree on Windows
  obscures the real validation result.
- **Never background (`run_in_background`) a `dotnet build`/`test`/`run`,
  and never start one while another is still alive.** This is the
  specific trigger that spawns COM-RPC-wedged `testhost` / `.NET Host`
  zombies which hold the project DLL locks and fail every subsequent
  build with `MSB3027 … file is locked by: testhost`. A `PreToolUse`
  guard hook at
  [`.claude/hooks/guard-dotnet-parallel.ps1`](../../.claude/hooks/guard-dotnet-parallel.ps1)
  (wired in [`.claude/settings.json`](../../.claude/settings.json))
  enforces this mechanically — it blocks a backgrounded `dotnet
  build|test|run` and blocks a new one while a repo `testhost` is
  already running. **Do not remove it.** It activates on the next
  `/hooks` open or Claude Code restart after a fresh checkout. Recovery
  and root-cause detail: `lessons-learned.md` → "Never Background Or
  Parallelize `dotnet build`/`test`".
- **Integration tests that drive COM against a shared `.accdb`
  must be hermetic of each other's setup.** xUnit runs test
  *classes* in parallel within an assembly. Two
  `[SkippableFact]` tests that each spawn a CLI process touching
  the same sample `.accdb` will race on ACE OLE DB's file-level
  lock; the per-test budget (typically 60s) expires before the
  contention clears and one or both tests time out. The victim
  rotates non-deterministically across runs — first observed on
  `ClassifyCommandTests` in `harvester-data-classifier` Session 4
  (2026-05-28), then on `CliExtractTests` / `CliRunTests` /
  `CliTranslateTests` in
  `harvester-manifest-index-and-batch-readiness` Session 4
  (2026-05-29). Three mitigations, in order of preference:
  (a) pre-stage the required input (synthesize a minimal
  hand-authored `classification.json` / `manifest.json` / etc.
  for a small table — the upstream subcommand's behaviour is
  already covered by its own test class); (b) put the new test
  into the same `[Collection(...)]` as the conflicting class
  to serialise them; (c) factor the shared setup into an
  `ICollectionFixture` that runs once and is reused. The
  cheapest is usually (a); (b) works but trades parallelism for
  simplicity. Action-detail reference is preserved in
  `lessons-learned.md` →
  "Do Not Chain Parallel xUnit Integration Tests That Both
  Drive Access COM Against The Same `.accdb`".
  *Fix-forward tactic when picking up a session whose Integration
  test gate is red on multiple HomeHealthCare COM classes:* apply
  `[Collection("HomeHealthCareCom")]` to **every Integration test
  class that drives COM against `samples/HomeHealthCare.accdb`** —
  whether it spawns the CLI process (`*CommandTests.cs` under
  `tests/AccessHarvester.Integration/Cli/`) **or opens the database
  in-process via `CorePipeline`** (the `Pipeline/` classes:
  `CanonicalGoldenRegressionTests`, `EndToEndPipelineTests`,
  `ManifestSchemaConformanceTests`) — AND open the shared
  `HomeHealthCareComCollection` definition (or create one if the
  project doesn't have it yet). The contention is on ACE OLE DB's
  file-level lock, so an in-process CorePipeline class starves a
  CLI-spawning class just as surely as two CLI classes starve each
  other; scoping the fix to `Cli/*CommandTests.cs` alone leaves the
  race able to rotate to an in-process victim. Done in
  `harvester-report-baseline-capture` Session 2 (2026-05-29 —
  sixth-context confirmation of the underlying race) — knocked
  the project's failure count from 3 to 1. Broadened to the
  in-process Pipeline classes in
  `harvester-access-com-graceful-shutdown` Session 2 (2026-06-01)
  after `ClassifyCommandTests` timed out at 240s in the full tier
  run (7s in isolation) while a Pipeline class held the lock. 
  Mechanical; should be the orchestrator's first move before
  debugging individual timeouts.
- **Sweep spurious `.accdb` binary diffs before staging the
  closeout commit.** `dotnet test` runs that open a sample
  `.accdb` via Access COM (read-only at the SQL/DAO level)
  cause Access to mutate internal timestamps and persistence-
  state fields on close. The resulting `git status --short --
  samples/*.accdb` mods carry no semantic content — only
  Access-managed timestamp metadata — and must not be
  committed. As part of the Step 8 closeout pre-commit pass,
  run `git status --short -- samples/*.accdb` and
  `git checkout -- samples/*.accdb` for any `.accdb` shown as
  modified, BEFORE `git add` so the spurious touches are never
  staged. If a stale `MSACCESS.EXE` from the test run is
  holding the file (git checkout reports "unable to unlink"),
  `Stop-Process -Id <pid> -Force` first, then retry the
  checkout. Confirmed in two contexts:
  `harvester-object-cleanup-identifier` Session 3 (2026-05-28,
  original hit) and
  `harvester-manifest-index-and-batch-readiness` Session 4
  (2026-05-29, set-spanning confirmation). Action-detail
  reference is preserved in `lessons-learned.md` →
  "Sweep Spurious `.accdb` Binary Diffs After `dotnet test`
  Before Staging The Closeout Commit".
- **COM-driven runs self-terminate the exact Access instance they
  create — never reintroduce a blunt by-name `taskkill`.**
  [`AccessHarvester.Core/Com/AccessComSession.cs`](../../AccessHarvester.Core/Com/AccessComSession.cs)
  is the single COM instantiation chokepoint. On teardown it attempts a
  graceful `Quit(acQuitSaveNone)` with `Marshal.FinalReleaseComObject` in
  a `finally` (so the RCW is released even if `Quit` throws), runs
  `GC.Collect()` / `GC.WaitForPendingFinalizers()` **twice**, then
  bounded-waits and force-kills **only the exact PID** it captured up
  front via `hWndAccessApp` → `GetWindowThreadProcessId` (zero-guarded so
  a failed capture degrades to a no-op rather than targeting PID 0).
  Test teardown must **not** reintroduce a blunt by-name
  `taskkill /IM msaccess.exe` — it risks unrelated Access instances a
  user or parallel test owns and bypasses the graceful path. Rely on the
  chokepoint's exact-PID self-cleanup, and let an out-of-process CLI exit
  on its own (do not blunt-kill it mid-`Dispose`) so that self-cleanup
  can run; `msaccess.exe` spawned via COM is a DCOM-activated LocalServer
  and is **not** in the CLI's process tree, so a `taskkill /T` tree-kill
  could never reap it anyway. The pattern, the PID-capture mechanics, and
  the force-kill / `.laccdb` lock-file caveats are documented in
  [`docs/access-com-zombie-cleanup.md`](../access-com-zombie-cleanup.md)
  (added by the maintainer 2026-06-01; implemented in
  `harvester-access-com-graceful-shutdown` Sessions 1-2).
- **Run tests incrementally by project; isolate the Access-COM / PDF
  integration tier, and read the TRX (not the wrapper) for truth.**
  Do not fire a whole-solution `dotnet test`. Classify the change: a
  hermetic change (parsers, enrichers, writers, docs, golden-sample
  regen) validates with `dotnet build` of the solution plus
  `dotnet test` of only the affected hermetic project(s); the
  Access-COM / PDF tier (`AccessHarvester.Integration`, the
  `ReportBaseline`/`Synthetic*` COM classes in `AccessHarvester.Tests`,
  `Pipeline.Tests/Cleanup/ComDateMaximaProbeTests`) is run deliberately,
  by itself, last, and only when the change touches that surface — on a
  host with Access installed, expecting to babysit it. For a COM run,
  pass a TRX logger (`--logger "trx;LogFileName=..."`) and read the TRX
  `<Counters>` for pass/fail: the `dotnet test` wrapper frequently hangs
  *after* xUnit has finished and written the TRX (a COM-teardown stall),
  so the wrapper's exit is not a reliable completion signal. When the
  wrapper hangs (or a COM test wedges), reap the orphaned `testhost.exe`
  / `msaccess.exe` / `AccessHarvester.exe` with WMI
  `Get-CimInstance Win32_Process | Invoke-CimMethod -MethodName Terminate`
  — a COM-RPC-wedged `testhost` does not respond to `taskkill` /
  `Stop-Process`, but WMI `Terminate` reaps it (standing authorization to
  kill these zombies is on record). Confirmed in two contexts:
  `harvester-consumer-workflow-instructions` Session 2 (2026-06-01,
  original) and `harvester-report-displayed-total-parity` Session 2
  (2026-06-01, staged AccessHarvester.Tests → Integration → 8 hermetic
  projects separately, TRX-as-truth, WMI-reap throughout). Action-detail
  reference (including the per-project `--filter` recipes and the
  `cleanup-dev-orphans` path) is preserved in `lessons-learned.md` →
  "Run Tests Incrementally By Project; Isolate The Access-COM / PDF
  Integration Tests".

### Workflow Expectations

- Before every session, read this file, `docs/planning/lessons-learned.md`,
  and `docs/planning/session-set-authoring-guide.md`. The authoring guide
  is the source of truth for *spec* authoring (slug naming, sizing, the
  Session Set Configuration block); `docs/ai-led-session-workflow.md`
  remains the source of truth for *execution* mechanics.
- Save verifier output raw and never edit session-review files after
  they are written.
- Log every AI-led session step in the active session set. Specifically:
  call `log.log_step()` at every major step transition (initial setup
  / each Step 4 step / Step 6 verification / Step 7 fixes if any /
  Step 8 commit). `session-events.jsonl` is a separate ledger written
  by `start_session` + `close_session` and does NOT satisfy
  `close_session`'s `activity_log_entry` gate — that gate reads
  `activity-log.json` specifically, which `log_step` is what populates.
  Skipping `log_step` forces a backfill commit at closeout that adds
  noise to git history; the gate caught this pattern three times in
  two days (`harvester-manifest-index-and-batch-readiness` Sessions
  3 + 4, then `harvester-report-baseline-capture` Session 1) before
  it was promoted from a lesson to this Convention.
- AI instruction documents (`CLAUDE.md`, `AGENTS.md`, `GEMINI.md`)
  reference this file and `docs/ai-led-session-workflow.md` so future
  agent runs inherit the same durable expectations.
- **Session-state.json is the single source of truth for in-progress
  detection.** Call `register_session_start()` at Step 1 before the first
  `log_step()`, and `mark_session_complete()` at Step 8. External tooling
  (VS Code Session Set Explorer) flips state from this file; do not infer
  in-progress from activity-log presence.
- **Author `ai-assignment.md` and the next-orchestrator /
  next-session-set recommendations via routed analysis — never
  self-opine.** The orchestrator's own opinion of which model could have
  been cheaper is the precise thing the routed analysis is designed to
  displace.
- **Obey the spec's Session Set Configuration block at runtime.** Rules
  are conditional on the spec's `requiresUAT` and `requiresE2E` flags.
  Do not re-litigate those flags during a session — if a flag is wrong,
  surface it at the Step 9 reorganization review.
- When the human gives a session instruction or decision that appears
  durable enough to guide future sessions or future projects, ask
  whether it should be incorporated into this document.
- When a failure reveals a reusable strategy, recommend a
  corresponding update to `docs/planning/lessons-learned.md`.
- **Send related files to the cross-provider verifier or disclaim
  what is out of scope.** Before calling
  `route(task_type="session-verification")`, include related files
  the verifier might reasonably look for, or disclose them in an
  out-of-scope section of the prompt. Missing context produces false
  positives about "missing" code that actually lives in a sibling
  file; the verifier only sees what the prompt includes. Especially
  important here because the harvester's pipeline crosses many
  files (extractor → translator → enricher → schema), and any one
  session typically only touches one stage.
- **Mark `[Obsolete]` instead of deleting types whose consumers a
  future session already owns.** Before physically deleting a type
  whose consumers number in the dozens and a later session is
  scheduled to rewrite those consumers, mark `[Obsolete]` instead.
  The compiler warnings surface on every consumer so no reference
  slips through, the build stays green, and the deletion lands
  naturally in the session that rewrites the last consumer.
  Disclose the deviation in the end-of-session verification prompt.
- **When the orchestrator's model matches the routing target for a
  given task type, handle the task in-conversation rather than
  calling `route()`.** A `route()` call into the orchestrator's own
  model gives fresh context (occasionally useful for stochastic
  independence) but also pays the provider's API cost to summon
  another instance of the same judgment. The non-negotiable
  exception is `session-verification`, which MUST go through the
  router regardless, because the whole point is a cross-provider
  second opinion. Complements the existing Delegation Discipline
  criteria in `docs/ai-led-session-workflow.md`.
- **Use `route(task_type="summarization")` for narrative prose
  outputs** (change logs, session summaries, release notes).
  Reserve `route(task_type="documentation")` for code-doc generation
  — its prompt template wraps content in a "document this code"
  frame that mis-fits free-form prose and produces Gemini 400s.
  For change-log, session-summary, and release-note prompts — which
  predictably exceed ~2KB — **always pass `max_tier=1` proactively**
  rather than waiting for a Gemini 400. Confirmed in three contexts:
  `accessexportertemp1-import` Session 6, `parser-infrastructure`
  Session 4 change-log. The 400 adds wasted latency and a retry round;
  setting `max_tier=1` upfront avoids it entirely.
- **Pin mechanical-correspondence task types to Sonnet rather than
  Opus** in `routing.task_type_overrides`. Mechanical-correspondence
  tasks (schema validation, AST-vs-source diffing, parse-tree shape
  comparison against an expected fixture, redaction audits) are
  pattern matching with placeholder substitution — Sonnet at default
  effort handles them at a fraction of the cost. Keep
  cross-provider verification on so the cheaper model's
  false-VERIFIED risk is mitigated. Synthesis tasks (architecture,
  parser-grammar design, novel test-case authoring, plan
  generation) earn Opus.
- **Use Opus as the orchestrator (and author in-conversation) for
  synthesis-document work above ~3K input tokens.**
  `route(task_type="architecture", complexity_hint=80)` to Opus at
  `effort:max` with adaptive thinking is unreliable above ~3K input
  tokens — the call routinely times out at the provider's 600s
  boundary. Two reliable alternatives, in order of preference:
  (a) use Opus as the orchestrator and handle the synthesis
  in-conversation per the "When the orchestrator's model matches the
  routing target..." Convention above; (b) split the synthesis into
  focused subsections across multiple shorter `route()` calls under
  ~1500 input tokens each. Do **not** use `task_type="planning"` as a
  workaround — its prompt template frames the task as "plan how to do
  X" and produces meta-plans, not documents. Confirmed in two contexts
  on 2026-04-25: `vba-parser-session-set-generator` Session 4 and
  `vba-spec-study` Session 3. The companion guidance "**notify the
  human on `route()` timeout**" (see `lessons-learned.md` for action
  details) handles in-flight escalation: first timeout retries once,
  second timeout stops and notifies. Action-detail reference for
  this convention is preserved in `lessons-learned.md` →
  "Opus At `effort:max` + Adaptive Thinking Timeouts...".
- **Persist routed output to disk before any display or logging side
  effects.** `route()` returns `result.content` strings that may
  contain Unicode characters Windows `cp1252` cannot encode. Calling
  `print(result.content)` before writing the content to disk can crash
  the script and lose paid output. The required pattern is to write
  the content to a file first
  (`open(path, "w", encoding="utf-8")`) and only then print or log.
  See `lessons-learned.md` →
  "Persist Routed Output To Disk Before Any Display Or Logging Side
  Effects" for the failure case (`vba-spec-study` Session 3 lost a
  $1.29 Opus call to a `cp1252`-driven `print()` crash).
- **Always include a required JSON response block in session-verification
  prompts.** Append the structured JSON schema (defined in
  `ai-router/prompt-templates/verification.md`) to every
  `route(task_type="session-verification")` call so the verifier is
  forced to populate verdict and issues rather than returning a bare
  paragraph or single-line verdict. Format:
  ```
  After your analysis, output a JSON block in this exact format:
  ```json
  {
    "verdict": "VERIFIED" | "ISSUES_FOUND",
    "issues": [
      {
        "severity": "Critical" | "Major" | "Minor",
        "location": "<file or test name>",
        "summary": "<one-sentence description>",
        "fix": "<recommended fix>"
      }
    ]
  }
  ```
  If verdict is VERIFIED, `issues` must be an empty array `[]`.
  Do not omit this block.
  ```
  If the JSON block is absent from the response, treat it as malformed
  and retry once with an explicit reminder. Confirmed in two contexts:
  `resolver-declarations-and-scopes` Session 3 (first hit — bare
  `ISSUES_FOUND` string with no issue details required reprompting);
  `resolver-default-members-and-inference` Sessions 1-4 (applied
  consistently across 8 verification rounds without terse-verdict
  recurrence). The action-detail reference is preserved in
  `lessons-learned.md` → "Require A Trailing JSON Block In
  Session-Verification Prompts To Prevent Terse Verdicts".
- **Pair the JSON-block schema with an explicit prose-before-JSON
  lead on closeout-grade session-verification prompts.** The trailing
  JSON-block requirement forces verdict structure but does not by
  itself force a substantive audit trail — verifiers (gemini-pro
  empirically) can legally return only the JSON block with no
  preceding analysis prose, leaving a structurally valid but
  auditless verdict. On closeout-grade work (final session of a
  set, work that closes a strategy-level criterion, high-stakes
  refactor), open the verification prompt with a mandatory-phrased
  prose-first instruction placed BEFORE the file listings, e.g.,
  "Your response MUST begin with a detailed prose analysis for each
  concern below BEFORE the JSON block. Do not skip the analysis.
  Both the analysis and the JSON block are required." If Round 1
  returns only the JSON block despite the JSON-block instruction
  alone, retry once with the "show your reasoning before the JSON"
  reminder. Confirmed in three contexts: `harvester-finish-cli-e2e-test-audit`
  Session 2 (2026-05-18 — original failure; Round 1 returned a bare
  verdict, Round 2 with the reminder produced a substantive 5-point
  analysis), `harvester-finish-cli-packaging` Session 2 (2026-05-18 —
  proactive application; the lead was baked into the very first
  prompt and Round 1 returned a substantive 7-point analysis with
  empty issues on the first try, no Round 2 needed), and
  `harvester-object-cleanup-identifier` Session 3 (2026-05-28 — third
  proactive application; closeout-grade R1 returned a substantive
  7-point analysis with empty issues, no R2 needed). The
  proactive-application reading mirrors the precedent set by the
  "Write `disposition.json` Before The Final Session's Closeout
  Commit" promotion. The action-detail reference is preserved in
  `lessons-learned.md` → "Bare JSON Verdict From The Verifier (Block
  Present, Analysis Absent) — Reminder Prompts Fix It".
- **Verifier-failure escalation ladder.** When the configured
  cross-provider `session-verification` model fails (timeout, transient
  HTTP error, garbled output), do not silently retry until budget
  burns out. Climb the ladder in order, and only descend a step if the
  previous one keeps failing:
  1. **Try the same provider twice.** A single failure is often
     transient; a second confirms a real problem.
  2. **Switch to the other remaining cross-provider verifier.** If
     `gpt-5-4` is the configured choice and fails twice from a Claude
     orchestrator, fall back to `gemini-pro`; vice versa for a
     gpt-5-4-orchestrator scenario. The constraint is "different
     provider than the orchestrator" — `gemini-pro` and `gpt-5-4` both
     satisfy it for a Claude orchestrator. Use `query("<model>",
     task_type="session-verification", ...)` to bypass the
     `task_type_overrides` mapping for the call.
  3. **Decompose the verification into smaller chunks.** Split the
     prompt by concern (e.g., grammar-rule review and test-coverage
     review separately), each well under the timeout window. The same
     provider can succeed on smaller prompts; or split across both
     providers if needed.
  4. **Verify against a description-of-work, not the implementation.**
     Most verification failures aren't about whether the code is
     correct — they're about whether the orchestrator's *understanding*
     of the spec is correct. A description-only verification ("here's
     what I did and why; flag any wrong assumptions") catches the
     spec-misreading failure mode at a fraction of the prompt size.
  5. **Log an issue and move on.** If steps 1–4 all fail, write an
     issue entry into `session-sets/<slug>/issue-logs/session-NNN.json`
     with severity `Major` (verifier-blocked, not orchestrator-error)
     and proceed to commit. The next session can re-attempt
     verification once provider conditions improve. Do not skip
     commit just because verification is provider-broken — the work
     itself is still preserved in git for human review.
  Confirmed in `vba-statement-and-module-grammar` Session 1 (2026-04-26)
  where `gpt-5-4` timed out twice, fallback to `gemini-pro` succeeded
  cleanly. This convention complements the older "Notify The Human On
  `route()` Timeout" lesson (now superseded for verification-step
  timeouts — that lesson stays valid for non-verification routed
  calls where switching the orchestrator IS the right move).
- **Run each parallel session set in its own per-set git worktree.**
  When multiple session sets run in parallel, create a
  `session-set/<slug>` branch off main and a worktree for each set
  inside this container — `dabbler-access-harvester/<slug>/` under the
  bare-repo + flat-worktree layout (the standard going forward, see
  `dabbler-ai-orchestration/docs/planning/repo-worktree-layout.md`).
  All sessions of that set work inside the worktree; the last session
  merges `origin/main` into the session-set branch (resolving
  conflicts), merges the session-set branch into main, pushes, then
  removes the worktree and deletes the local + remote `session-set/<slug>`
  branch (per the workflow doc's Step 8 cleanup). `router-metrics.jsonl`
  and `samples/NorthwindDevEdition.accdb` are the predictable
  merge-noise files — expect one reconciliation commit per completed
  set. Confirmed in two contexts: initial
  `vba-lexical-and-expression-grammar` + `access-metadata-extraction`
  parallel run (first hit), and `vba-lexical-and-expression-grammar`
  Session 4 merge (second confirmation). Full action sequence in
  `lessons-learned.md` → "Run Parallel Session Sets In Per-Set Git
  Worktrees".

- **This repo uses the bare-repo + flat-worktree layout.** The
  container at `~/source/repos/dabbler-access-harvester/` holds
  `.bare/` (the real git data), a `.git` pointer file, `main/` (the
  main worktree), and one directory per active session-set worktree.
  The container has no source files at its top level — `dotnet
  build`, `python -m pytest`, etc. all run from inside `main/` (or
  `<slug>/`). The Python venv (`.venv/`) lives in `main/`, not at the
  container root. The full layout description, fresh-repo setup
  recipe, migration recipe, and gotchas live at
  `dabbler-ai-orchestration/docs/planning/repo-worktree-layout.md`.

- **Probe substrate value early.** When a project arc is "build
  substrate for an AI-driven downstream task," insert probe
  checkpoints after every 2-3 substrate-building session sets — a
  single procedure or artifact, multiple providers (or in-IDE
  orchestrator pair), the human adjudicates. The probe cost (a few
  routed calls plus a session or two of in-IDE work) is negligible
  compared to discovering the substrate-shape is wrong after another
  four days of token spend. Confirmed in two contexts: `vba-symbol-db`
  Session 7 (PIVOT verdict driven by the comprehension probe +
  calibration analysis) and `vba-symbol-db-path-b-probe` (a
  three-session, ~$0.21 probe that materially settled the Path A vs.
  Path B direction with a Hybrid recommendation grounded in concrete
  test pins). Action-detail reference is preserved in
  `lessons-learned.md` → "Probe Output Value Earlier; Don't Sink
  Token Budget Into Unvalidated Substrates".

- **Write `disposition.json` before the final session's closeout
  commit.** `mark_session_complete()` enforces a `disposition_present`
  closeout gate plus a clean-working-tree gate; writing
  `disposition.json` AFTER the closeout commit forces a second commit.
  On the final session, generate `disposition.json` as part of the
  Step 8 closeout artifacts — alongside `change-log.md` and
  `next-session-set-recommendation.md` — BEFORE staging the closeout
  commit. The file should include `status: "complete"`, a summary
  string, a `files_changed[]` list, a `next_session_set: {slug, summary}`
  block, and `blockers: []`. See
  `docs/session-sets/structured-form-report-extractor/disposition.json`
  for the canonical shape. Confirmed in two contexts:
  `form-report-code-and-grouping` Session 3 (the original failure
  mode: a tail commit was needed to satisfy the gate) and
  `generalization-validation-on-non-northwind` Sessions 1-2 (proactive
  application — Session 1 wrote disposition.json in-progress, Session
  2 updated it before the closeout commit; gate never fired).
  Action-detail reference is preserved in `lessons-learned.md` →
  "Write `disposition.json` Before The Final Session's Closeout
  Commit, Not After". The workflow doc's Step 8 also lists
  `disposition.json` explicitly as a generated artifact.

- **Verify the prior session actually committed and pushed before
  starting a new one.** External tooling reads `session-state.json`
  for in-progress detection (correct), but the orchestrator picking
  up the next session must NOT trust that signal alone for
  artifact-on-disk-vs-in-git questions. The git working tree is the
  only source of truth for "did the prior session actually commit?"
  As part of Step 0, after `register_session_start` and before the
  first content-producing step, run
  `git status --short -- docs/session-sets/<slug>/ tests/ tools/`.
  If untracked or unstaged files matching the prior session's
  expected deliverables exist, surface to the human: "Prior session
  marked complete but did not commit; should I include those files
  in this session's commit, or push them as a separate catch-up
  commit first?" Default to bundling unless the human says
  otherwise. Confirmed in two contexts: `vba-symbol-db-path-b-probe`
  Session 3 (2026-04-29, Codex/GPT-5.5 finished workflow steps but
  did not commit — Session 3 caught it in `git status` and bundled)
  and `workflow-package-pilot` Session 7 (2026-04-30, Codex/GPT-5.4
  finished Session 6 cleanly with VERIFIED across four Opus rounds
  but did not commit — Session 7 caught and bundled). The
  promotion-trigger lesson also asked for a
  `verify_prior_session_committed()` helper in
  `ai-router/session_log.py` so the check is mechanical rather than
  manual; that helper is a deliberate follow-up, not bundled with
  this convention. Action-detail reference is preserved in
  `lessons-learned.md` → "Verify The Prior Session Actually
  Committed And Pushed Before Starting A New One".

- **Post-fix sweep narrative summary blocks, not just the body, when
  a rule or contract changes.** When R1 cross-provider verification
  fixes a rule, contract, confidence tag, or authority framing in a
  narrative document, the orchestrator's natural editing motion
  targets the body; *intro / authority / stability / cross-cutting*
  blocks frequently lag and produce R2 / R3 Minor findings on stale
  framing. Before re-verifying, explicitly grep the frame blocks for
  pre-fix tokens (for confidence-tag changes: `inferred`, `not read`,
  `open question`; for rule/authority changes: "source of truth",
  "authority", "the lookup", "the closed set", "stability commitment",
  "trigger is"). Update any residue adjacent to the changed rule in
  the same pass that updates the body. Confirmed in two contexts:
  `generalization-validation-on-non-northwind` Session 2 (2026-05-15,
  confidence-tag change drove the original drift) and
  `harvester-finish-canonical-parse-success-report` Session 2
  (2026-05-17, rule-authority framing change drove a three-round
  drift where R2 caught Authority + Stability blocks and R3 caught
  the Cross-cutting bullet). The two triggers are different but the
  failure mode and mitigation are identical. Action-detail reference
  is preserved in `lessons-learned.md` → "Post-Fix Sweep All
  Narrative Artifacts When A Rule Or Contract Changes".

- **When using `JsonSchema.Net` for schema validation, key the schema
  cache by the schema's `$id` URI using `StringComparer.Ordinal` (not
  `OrdinalIgnoreCase`).** URI path components are case-sensitive per
  RFC 3986; `OrdinalIgnoreCase` can incorrectly alias distinct schemas
  with `$id` values that differ only in case. Separately, cache the
  schema text by file path (OrdinalIgnoreCase is correct for Windows
  file paths) so that path-based and text-based loads of the same
  schema share one `JsonSchema.Net` global-registry entry and do not
  trigger the "Overwriting registered schemas is not permitted"
  exception. Confirmed in `harvester-cli-validate-subcommand` Session 1
  (2026-05-28): verifier flagged the OrdinalIgnoreCase `$id` cache key
  as a correctness risk; the two-level cache redesign resolved it.

### Token-Window Budget Overrides

The human has recurring token allotments across three orchestrator providers with
different billing models. When approaching the end of a window, the human can say
**"maxout Claude"**, **"maxout Codex"**, or similar to override the `ai-assignment.md`
recommendations for that session.

**Billing model summary (informs what "maxout" means per provider):**
- **Anthropic (Claude Max):** Orchestrator use and routed API calls share the same
  allotment. Burning one burns the other.
- **GitHub Copilot (Codex):** Orchestrator use and routed API calls share the same
  allotment (same pattern as Anthropic).
- **Gemini:** API calls are per-call billed and separate from the Gemini Pro daily
  allotment. Daily allotment visibility is unavailable. No "maxout Gemini" phrase
  is defined — aggressive routing to Gemini drives up per-call cost without burning
  a free bucket.

**Phrase definitions:**

- **"maxout Claude"** — Use Opus 4.7 as orchestrator at effort=max. Drop `max_tier`
  caps on routed calls; push `complexity_hint` up so everything eligible routes to
  Opus/max. Cross-provider `session-verification` still goes to GPT-5.4 (mandatory,
  non-negotiable) and will draw from the Copilot allotment — a useful side effect
  when both windows need burning simultaneously.

- **"maxout Codex"** — Use GPT-5.5 or GPT-5.4 as orchestrator at
  reasoning_effort=high. Push routed calls to GPT-5.4/high. Cross-provider
  `session-verification` goes to Opus and draws from the Anthropic allotment.

**Hard constraint that survives any maxout:** `session-verification` is always
cross-provider. "Maxout" upgrades the tier and removes cost-saving caps; it never
eliminates the cross-provider verification step or routes verification back to the
orchestrator's own provider.

**Parallel-session window burning:** Running Claude Code and Codex in parallel on
independent branches (e.g., `vba-lexical-and-expression-grammar` + `access-metadata-extraction`)
naturally consumes both allotments simultaneously. "maxout Claude" on one branch and
"maxout Codex" on the other is the most efficient way to drain both windows when
both are running low.

**Subscription in-IDE orchestrator pair as cross-provider rigor.**
When a routed cross-provider rigor model has been calibrated and
found to pay high cost for low-correlation signal (e.g.,
`vba-symbol-db` Session 6's Pearson +0.004 between self-rated
confidence and cross-provider agreement), the cost-effective
substitute is a subscription-paid in-IDE orchestrator pair: one
provider authors, the other reviews. Codex (GitHub Copilot
window) translates a procedure; Claude (Anthropic Max window)
peer-reviews and adds falsification tests; the cross-provider
property is preserved without any per-call routed cost beyond one
mandatory cross-provider session-verification per session.
Confirmed in two contexts: `vba-symbol-db` Session 7's PIVOT
verdict (which articulated the pattern) and
`vba-symbol-db-path-b-probe` Sessions 2-3 (which executed it —
Codex translated, Claude reviewed, $0.078 routed across both
sessions vs. the ~$24 routed-rigor cost of the calibration arc).
The trade is wall-clock (slower per session, parallelizable across
two IDE windows) and the loss of fine-grained per-symbol cost
telemetry. Drop AI-self-rated columns
(`Confidence*` / `Justification*`) from the writeback path at the
same time — the routed-rigor calibration proved they had no
correlation value. Action-detail reference is preserved in
`lessons-learned.md` → "When Routed Cross-Provider Rigor Returns
'Your Rigor Isn't Paying For Itself,' Drop The Rigor MODEL — Not
The Rigor Itself".

### Cross-Repo Coordination

- This repo borrows code from `../AccessExporterTemp1`. That sibling
  repo is **not under git** (per its BATON.md), so changes there are
  not reversible by `git reset`. When importing code, prefer
  copying over symlinking, and treat the imported snapshot as the
  starting point — divergence from the source codebase is expected
  and fine.
- The VBA parser plan and `docs/test-cases/` already live in this
  repo and are scoped to this project from the start; no import
  needed there.
