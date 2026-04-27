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
4. **End-to-end tests** against larger committed sample DBs (e.g.,
   `samples/NorthwindDevEdition.accdb`) for confidence on real-world
   inputs.
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

### Workflow Expectations

- Before every session, read this file and
  `docs/planning/lessons-learned.md`.
- Save verifier output raw and never edit session-review files after
  they are written.
- Log every AI-led session step in the active session set.
- AI instruction documents (`CLAUDE.md`, `AGENTS.md`, `GEMINI.md`)
  reference this file and `docs/ai-led-session-workflow.md` so future
  agent runs inherit the same durable expectations.
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
  `session-set/<slug>` branch off main and a worktree at
  `../<repo>-<slug>` for each set. All sessions of that set work
  inside the worktree; the last session merges `origin/main` into the
  session-set branch (resolving conflicts), then merges the
  session-set branch into main and pushes. `router-metrics.jsonl` and
  `samples/NorthwindDevEdition.accdb` are the predictable merge-noise
  files — expect one reconciliation commit per completed set. Confirmed
  in two contexts: initial `vba-lexical-and-expression-grammar` +
  `access-metadata-extraction` parallel run (first hit), and
  `vba-lexical-and-expression-grammar` Session 4 merge (second
  confirmation). Full action sequence in `lessons-learned.md` →
  "Run Parallel Session Sets In Per-Set Git Worktrees".

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
