# Lessons Learned — Dabbler Access Harvester

> **Purpose:** Capture durable tactics, failure patterns, and workflow
> lessons that should influence future sessions before the same mistake
> repeats.
> **Status:** Start small in one file. If this grows too large, split it
> into a `lessons-learned/` folder with a short index and topic files.

Review this file before every AI-led session.

Unlike `project-guidance.md`, which captures durable principles and
conventions, this file is for practical lessons learned from actual
execution in this repo. A lesson that has been applied as the right
call in at least two different contexts is a promotion candidate for
Conventions (or, rarely, Principles) in `project-guidance.md`. See
`docs/ai-led-session-workflow.md` Step 9 (run after the Step 8 notify)
for the end-of-session-set reorganization review.

## How To Use This File

- Read it before starting a new session.
- Add entries when a failure, surprise, or avoidable friction reveals a
  better repeatable strategy.
- Prefer short, action-oriented lessons over long retrospectives.
- When a lesson has proven itself in two or more different contexts,
  propose promoting it to `project-guidance.md` — as a Convention if it
  is a specific rule, or a Principle if it is durable strategy. Never
  delete a lesson; only move it.

## Entry Template

Use this structure for new entries:

```markdown
## <Short lesson title>

- **Context:** What kind of work exposed the issue
- **Failure or friction:** What went wrong
- **Lesson:** The durable takeaway
- **Action for future sessions:** What the AI orchestrator should do next time
```

---

## Seeded Lessons (Inherited From Sibling Workflow Use)

These are lessons that have already been validated in sibling repos using
the same AI-led workflow. They are seeded here because they are
genuinely workflow-discipline lessons (not UI-specific) and the
underlying failure modes apply to this repo too. Each is preserved as a
reference; if it proves out twice in *this* repo, propose promotion to
`project-guidance.md`.

### Do Not Run Build And Test In Parallel Against The Same Worktree

- **Context:** Full-solution validation runs on Windows.
- **Failure or friction:** Running `dotnet build` and `dotnet test` in
  parallel caused file-lock contention and obscured the real
  validation result.
- **Lesson:** Build and test should be run sequentially unless there
  is a specific reason to parallelize isolated targets safely.
- **Action for future sessions:** Prefer sequential validation, and if
  lock contention appears, use `dotnet build-server shutdown` plus
  conservative build settings before retrying.

### Send Related Files To The Cross-Provider Verifier Or Disclaim What Is Out Of Scope

- **Promoted to `project-guidance.md` → Conventions → Workflow
  Expectations** in the sibling repo. Repeated here because it is the
  primary defense against false-positive verifier findings, and the
  harvester pipeline crosses many files per stage (extractor →
  translator → enricher → schema), making this failure mode easy to
  hit in single-file-focused sessions.
- **Context:** End-of-session cross-provider verification via the AI
  router.
- **Failure or friction:** Round-1 verification produced false
  positives claiming that endpoint or method actions were "missing"
  when they actually lived in sibling files that were not included in
  the verification prompt.
- **Lesson:** The verifier only sees what the prompt includes. If the
  session touches one file in a conceptual area, related files in the
  same area should be supplied too, or the prompt should explicitly
  say "X lives in `<other-file>` and is not being changed this
  session" so the verifier does not hallucinate missing code.
- **Action for future sessions:** Before calling the router for
  verification, list related files the verifier might reasonably look
  for and either include them or add a "files not in scope but
  present in the repo" section to the prompt.

### `route(task_type=documentation)` Wraps Content In A Code-Documentation Template

- **Promoted to `project-guidance.md` → Conventions → Workflow
  Expectations** in the sibling repo.
- **Context:** Generating change logs / READMEs / session summaries
  through the AI router.
- **Failure or friction:** The `documentation` prompt template is
  shaped for "document this code" work. When the content is
  narrative meta-prose, the wrapped prompt produced
  `Gemini Pro 400 Bad Request` on every retry. The same content sent
  directly via raw `httpx` returned 200.
- **Lesson:** The router's per-task-type template meaningfully alters
  the prompt shape. For narrative prose, use
  `task_type="summarization"` (no template wrapping) rather than
  `task_type="documentation"`, which is reserved for code
  documentation.
- **Action for future sessions:** Reserve
  `task_type="documentation"` for code-doc generation. For change
  logs, release notes, session summaries, and similar narrative
  prose, use `task_type="summarization"`. If `summarization` returns
  Gemini 400 on a prompt above ~2KB, pin `max_tier=1` to force
  Gemini Flash.

### Mechanical-Gate Task Types Calibrate To Sonnet-Cost For Adequate Quality

- **Promoted to `project-guidance.md` → Conventions → Workflow
  Expectations** in the sibling repo. Especially relevant here
  because schema validation, AST-vs-source diffing, and parse-tree
  shape comparison are all mechanical correspondence checks.
- **Context:** Mechanical correspondence checks (schema validation,
  step-by-step parity, gate checks) routed through the AI router.
- **Failure or friction:** Pinning these to Opus is overkill —
  Sonnet at default effort produces equivalent verdicts at a
  fraction of the cost. The orchestrator may itself be Opus, in
  which case an Opus-pinned route just pays Anthropic to re-summon
  another instance of the current judgment.
- **Lesson:** Distinguish mechanical correspondence from synthesis.
  Mechanical → Sonnet (or let the router's tier picker decide);
  synthesis → Opus.
- **Action for future sessions:** When adding a new task type to
  `routing.task_type_overrides`, ask whether it is mechanical
  pattern-matching with placeholder substitution (Sonnet) or novel
  reasoning (Opus). Cheap-model risk is false-VERIFIED; mitigate
  with explicit prompt instruction ("prefer false-positive
  ISSUES_FOUND over false-negative VERIFIED") and keep
  cross-provider auto-verify on.

### Verifier Selection Is Rule-Based, Not Map-Based

- **Promoted to `project-guidance.md` → Conventions → Workflow
  Expectations** in the sibling repo, in the form of router-config
  guidance.
- **Context:** Cross-provider verifier selection
  (`verification.py` `pick_verifier_model`).
- **Lesson:** Verifier eligibility is a role (different provider,
  enabled as verifier, tier match, cheapest survivor), not a
  user-to-user binding. Preferred pairings become optional hints
  that only take effect when they survive the rules.
- **Action for future sessions:** When adding a model to
  `router-config.yaml`, add it with `is_enabled_as_verifier: false`
  until calibration data justifies promoting it. Never re-introduce
  a map-first selection pattern.

### Heuristics That Earn Their Keep: Stop Tuning The Router Complexity Score

- **Context:** AI router complexity scoring (`complexity.weights`
  and `task_type_scores` in `router-config.yaml`).
- **Failure or friction:** None — the heuristic works well enough.
  The temptation is to invest engineering in tightening it
  (embeddings, ML-based prediction).
- **Lesson:** The current heuristic plus the 2-try escalation
  safety net is the intended stopping point. The engineering time
  saved is worth more than the handful of retried API calls the
  tighter heuristic would prevent. Do not introduce ML or
  embeddings here.
- **Action for future sessions:** If a review surfaces "the router
  should predict complexity better", point at this entry. The only
  acceptable knob changes are adjusting the existing weights or
  adding keywords to the high/low lists.

### Do Not Present An Inferred Root Cause As Fact Without Verifying It

- **Context:** Any user-reported symptom whose explanation depends
  on an assumed environmental, operational, or version condition.
- **Failure or friction:** A plausible-sounding root cause was
  presented as fact and turned out to be wrong on multiple premises
  the orchestrator had not actually verified.
- **Lesson:** A reasoned inference from source control and config
  files is not evidence. For every factual claim a root-cause
  theory depends on, there is a specific command or file check
  that proves or disproves it. Run that check before writing the
  theory up.
- **Action for future sessions:** Before presenting a "root cause"
  in any plan, findings doc, or session review, list every factual
  claim the cause depends on. For each claim, name the verification
  command (`dotnet --version`, `dotnet nuget list`, `ls -la` on the
  actual path, inspection of `.deps.json`, etc.). Run them. Mark
  any un-verified claim as "hypothesis, not verified" in the
  writing.

---

## Harvester-Specific Lessons

### `session-verification` Needs A `task_type_overrides` Entry, Or `route()` Will Pick The Orchestrator's Own Provider

- **Context:** End-of-session Step 6 cross-provider verification.
  Sessions 1 and 2 of `accessexportertemp1-import` both hit it.
- **Failure or friction:** The workflow doc Step 6 says to call
  `route(task_type="session-verification", complexity_hint=70, ...)`,
  but `session-verification` was not in
  `routing.task_type_overrides`. With Claude Opus as the
  orchestrator, the tier picker resolves complexity 70 → tier 3 →
  `opus` (Anthropic = the orchestrator's own provider), defeating
  the cross-provider intent. The Session 1 orchestrator worked
  around this with `query("gpt-5-4", task_type="session-verification")`.
  Session 2 hit the identical issue and used the same workaround,
  confirming the pattern.
- **Lesson:** `route()` selection is provider-agnostic — it picks the
  cheapest Tier-N model regardless of which orchestrator is calling.
  For task types that *must* be cross-provider, an explicit
  `task_type_overrides` entry is required, and the chosen model has
  to be from a different provider than the orchestrator. There is no
  one-value-fits-all answer because the workflow allows the human to
  swap the orchestrator session-to-session — the override is
  calibrated for the *expected* orchestrator and the human
  re-calibrates if they switch.
- **Action for future sessions:** The router-config.yaml override is
  now in place (`session-verification: gpt-5-4`, calibrated for the
  Claude orchestrator). Future Claude-led sessions can call
  `route(task_type="session-verification", ...)` per the workflow
  doc verbatim and get a cross-provider review automatically. **If
  the human switches the orchestrator to Codex (OpenAI) or Gemini
  (Google) for a future session, swap the override value before
  starting** — for Codex use `opus` (or `sonnet`), for Gemini use
  `opus` or `gpt-5-4`. Until that swap, do *not* use `route()` for
  session-verification under the wrong orchestrator; fall back to
  `query("<cross-provider-model>", task_type="session-verification")`
  as Sessions 1 and 2 did.
