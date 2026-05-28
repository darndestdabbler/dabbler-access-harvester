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

### Stale-Path Scrub On Imported Dev Tools, Even When Plan Says "Import-As-Is"

- **Context:** Importing developer utility scripts (Python tools, side
  scripts, samples) from a sibling repo where the import plan
  dispositions them `import-as-is`. First hit in Session 6 of
  `accessexportertemp1-import` on `tools/validate_against_yaml.py`.
- **Failure or friction:** Session 6 imported the script per its
  `import-as-is` disposition. The script still referenced sibling-repo
  paths and class names (`AccessExporter`, `sample-output/`,
  `AccessExporter.Core/`). Even though the verification prompt
  explicitly disclosed this as deferred-by-design per the import plan,
  round-1 verification flagged it as Major Correctness anyway and
  required a fix-and-reverify round (~$0.11 of extra verifier cost,
  ~5 minutes of orchestrator time).
- **Lesson:** "Import-as-is" applies to behavior, not to repo-local
  strings. Hardcoded sibling paths and stale namespace references in
  imported dev tools are verifier-magnets even with disclosure. The
  cost of a 5-minute mechanical scrub at import time is lower than
  the cost of a re-verification round. Disclosure does prevent
  false-positive hallucinations of missing code, but it does not
  immunize a real correctness gap a fresh reader can see in the file.
- **Action for future sessions:** When importing dev-utility scripts
  (Python, shell, etc.) from a sibling repo, do a mechanical scrub
  for sibling-repo paths and namespace references in the same step
  that imports them — even when the plan says `import-as-is`. Treat
  this as part of the namespace-rewrite mechanical work, not as a
  behavioral edit. Only escalate to the human if the scrub reveals a
  real semantic change.

### Opus At `effort:max` + Adaptive Thinking Timeouts On Large Synthesis-Document Prompts

- **Promoted to `project-guidance.md` → Conventions → Workflow Expectations**
  on 2026-04-25 (after `vba-spec-study` Session 3, second-context
  confirmation). Retained here as the action-detail reference; the rule
  itself is now a Convention. Second context: `vba-spec-study` Session 3
  hit the same timeout pattern on a Sonnet-orchestrated
  `route(architecture)` call, the orchestrator was swapped to Opus, and
  the deliverable was authored in-conversation per the convention.
- **Context:** Session 4 of `vba-parser-session-set-generator`. Two attempts to
  `route(task_type="architecture", complexity_hint=80)` from a Sonnet orchestrator
  both timed out at 600 seconds (raised from 300 after the first timeout).
- **Failure or friction:** Opus at `effort:max` + adaptive thinking on a ~3.8K
  input-token synthesis-document prompt consistently exceeded the provider timeout.
  A fallback to `route(task_type="planning")` produced a meta-plan (how to write
  the document) rather than the document itself, because the `planning` prompt
  template frames the task as "plan how to do X". The playbook was ultimately
  authored in-conversation by the Sonnet orchestrator — a deivation from the spec.
- **Lesson:** For large synthesis-document generation from a Sonnet orchestrator,
  route(architecture) to Opus/max is unreliable above ~3K input tokens due to the
  timeout. Two reliable alternatives: (a) use Opus as the orchestrator and handle
  in-conversation (the in-conversation convention), or (b) break the synthesis into
  focused subsections across multiple shorter route() calls. Do not use
  `task_type="planning"` as a workaround for document generation — it produces
  plans, not documents.
- **Action for future sessions:** If a synthesis-document route() call to Opus times
  out and the orchestrator is Sonnet: escalate to Opus as the orchestrator for the
  next attempt. If that is not possible, author in-conversation and disclose the
  deviation in the verification prompt.

### `effort:xhigh` Is Not A Valid Anthropic API Effort Level

- **Context:** Session 4 of `vba-parser-session-set-generator`. Initial route()
  calls to Opus failed with HTTP 400 Bad Request before the timeout issue was
  discovered.
- **Failure or friction:** `router-config.yaml` had `effort: xhigh` for Opus under
  `task_type_params.architecture`. The Anthropic API rejected this with "This model
  does not support effort level 'xhigh'." Anthropic's supported effort levels for
  `claude-opus-4-6` and `claude-sonnet-4-6` are `low`, `medium`, `high`, and `max`.
  OpenAI's `gpt-5-4` supports `reasoning_effort: xhigh` — the two APIs use
  different enums.
- **Lesson:** When adding or editing `task_type_params` in `router-config.yaml`,
  check the supported effort levels per provider: Anthropic → `low|medium|high|max`;
  OpenAI → `none|minimal|low|medium|high|xhigh`. Do not copy Anthropic values from
  OpenAI entries or vice versa.
- **Action for future sessions:** The router-config.yaml was fixed (effort: xhigh →
  effort: max for Opus). If a route() call returns HTTP 400 immediately (not a
  timeout), check the effort/reasoning_effort value for that model first.

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

### Notify The Human On `route()` Timeout — Consider Switching Orchestrator Rather Than Continuing To Fail

- **Context:** Mid-session `route()` failures (timeout, transient API
  errors) where the orchestrator's first instinct is to retry with the
  same configuration. First hit in Session 3 of `vba-spec-study`
  (2026-04-25).
- **Failure or friction:** Session 3 hit an Opus `route(architecture)`
  timeout on the second call (the first having succeeded but content
  been lost to a print-side bug — see "Persist Routed Output To Disk
  Before Any Display Or Logging Side Effects" below). The Sonnet
  orchestrator silently retried instead of notifying the human; only
  after the second timeout did the human intervene to swap the
  orchestrator. Two paid Opus calls were burned before the swap.
- **Lesson:** When a `route()` call to a tier-3 model times out
  (especially `architecture` / `session-verification`), the orchestrator
  should notify the human and propose either (a) swap to a higher-tier
  orchestrator that can author in-conversation, or (b) split the prompt
  into smaller chunks. Silent retries waste budget. The notification
  should include the failing task type, the prompt size, and the
  `lessons-learned.md` entry it matches.
- **Action for future sessions:** First timeout → log it, retry once.
  Second timeout → stop, notify the human via
  `send_session_complete_notification` (or equivalent) with subject
  "route() timeout — orchestrator change recommended", and wait for
  instruction. Do not retry a third time.

### Persist Routed Output To Disk Before Any Display Or Logging Side Effects

- **Context:** Any orchestrator script that calls `route()` and produces
  synthesis content downstream needs. First hit in Session 3 of
  `vba-spec-study` (2026-04-25).
- **Failure or friction:** Session 3 Batch A's first
  `route(architecture)` call succeeded with 16788 output tokens at
  $1.29. The script then did `print(result.content)` for visibility
  before writing to file. The print failed on Windows `cp1252` codec (a
  directional-arrow character `↵` in the response was unencodable), the
  script crashed, and the entire $1.29 of content was lost. The retry
  timed out (see Opus-timeout convention in `project-guidance.md`).
- **Lesson:** `print()` to a Windows console is fragile against Unicode.
  Orchestrator scripts must save routed output to disk *before* any
  print/log/parse side effects. The pattern is
  `with open(path, "w", encoding="utf-8") as f: f.write(result.content)`
  first, then `print()` second.
- **Action for future sessions:** All `route()`-using scripts must
  follow the persist-then-display pattern. The change-log generator and
  synthesis-dispatcher templates should encode this as the default, not
  a per-script choice. When in doubt, write to a temp path first; if
  the print fails, the content is still recoverable from disk.

### Run Parallel Session Sets In Per-Set Git Worktrees, Not On A Shared Working Tree

- **Promoted to `project-guidance.md` → Conventions → Workflow
  Expectations** on 2026-04-26 (after `vba-lexical-and-expression-grammar`
  Session 4 merge, second-context confirmation). Retained here as the
  action-detail reference for the full step sequence; the rule itself is
  now a Convention.
- **Context:** First hit on 2026-04-26 running
  `vba-lexical-and-expression-grammar` and `access-metadata-extraction` in
  parallel per the `project-guidance.md` -> Token-Window Budget Overrides
  endorsement of parallel session sets. Both orchestrators (initially
  one Codex + one Claude, later two Claudes) checked out the same git
  branch in the same working directory.
- **Failure or friction:** The two sessions did not conflict
  semantically — they touched different code (grammar vs. metadata
  schema). They collided on **disk**: a `dotnet build` of the full
  solution from Session 1 of `access-metadata-extraction` failed at the
  grammar project because `Vba.g4` was mid-edit by the parallel session
  and its regenerated `VbaLexer.cs` did not compile. Concurrent edits
  to `AccessHarvester.sln`, `ai-router/router-metrics.jsonl`, and
  shared `tmp/` artifacts produced confusing partial states. Codex's
  partial synthesis output landed as orphan files inside the metadata-
  schema session-set folder rather than in the canonical
  `session-reviews/` / `issue-logs/` locations.
- **Lesson:** Parallel session sets need **isolated working
  directories** even when their logical scope is independent. Git
  worktrees are the right primitive: one worktree per session set, each
  on its own `session-set/<slug>` branch. Disk state stays separate;
  merges happen explicitly when each set finishes rather than as
  ambient churn during execution.
- **Action for future sessions:** When starting a new session set that
  will run in parallel with another in-progress set:
  1. Create a branch off main: `git checkout -b session-set/<slug>`.
  2. Create a worktree for it: `git worktree add ../<repo>-<slug> session-set/<slug>`.
  3. Run all sessions of that set in the worktree's directory.
  4. On the **last session of the set**, after `change-log.md` is
     generated and the final session commit is pushed, the orchestrator
     also runs `git fetch origin main && git merge --no-ff origin/main`
     into the session-set branch (resolving any conflicts), then
     `git checkout main && git merge --no-ff session-set/<slug> &&
     git push`. The merge commit's subject ("Merge session set
     <slug>") preserves traceability. Delete or tag the branch
     after merge.
  5. Do **not** introduce a separate "merge session set" — the merge
     belongs in Step 8 of the last regular session.
  Two collateral cleanups discovered alongside this lesson: `samples/
  NorthwindDevEdition.accdb` (Access mutates timestamps on open) and
  `ai-router/router-metrics.jsonl` (every router call appends) should
  be treated as merge-noise — gitignore them or expect tiny
  reconciliation commits forever.
  If a second session set hits this same failure mode, propose
  promoting the worktree-per-set rule to a Convention in
  `project-guidance.md`.

### Verify DAG Prereqs Against Actual Completion State Before Registering Session Start, Not Against The Previous Set's Handoff Recommendation

- **Context:** Starting `access-symbol-table-and-types` Session 1 on
  2026-04-26. The previous set (`access-metadata-extraction`) had
  authored a cross-set handoff recommendation in its `change-log.md`
  pointing at `access-symbol-table-and-types` as the next set to start,
  with Opus 4.7 @ effort=high as the recommended orchestrator.
- **Failure or friction:** The orchestrator (Opus, this session)
  followed the handoff recommendation and ran Step 0 to completion —
  worktree created, branch cut, `session-state.json` written — before
  reading the spec's prerequisite list against actual completion
  state. The spec's `Prerequisite:` line listed
  `resolver-default-members-and-inference/` (#9) and
  `access-metadata-extraction/` (#10). Only #10 was done; #7 → #8 → #9
  had not even been started, so `AccessHarvester.Vba.Resolver/` did
  not exist in the repo. Session 1's deliverable (resolver extensions)
  had nothing to extend. The cross-set handoff recommendation was
  authored from the previous set's perspective without DAG-wide
  prereq awareness — it picked the *next downstream slug*, not the
  *next unblocked slug*.
- **Lesson:** Cross-set handoff recommendations in
  `change-log.md` are advisory and authored by a single set's
  end-of-run analysis call. They do not guarantee that the named next
  set's full prereq list is satisfied. The authority for "what is
  startable now" is `session-set-tree.md` (the DAG) cross-referenced
  against per-set `change-log.md` presence (the completion gate). Do
  the DAG check **before** registering session start in Step 0, not
  after.
- **Action for future sessions:** Add a sub-step to the workflow's
  Step 0 (between "identify the active session set" and "register
  this session in `session-state.json`"): for the chosen slug, look
  up its row in `session-set-tree.md`, parse the `Prereqs` column,
  and for each prereq verify a `change-log.md` exists in the
  corresponding `docs/session-sets/<prereq-slug>/` folder. If any
  prereq is missing, stop and surface to the human with the list of
  missing prereqs and a recommendation for the next *actually*
  unblocked slug. Until this is encoded in code, treat it as an
  orchestrator checklist item. If a second session set hits this
  same failure mode, promote it to a Convention in
  `project-guidance.md` and add a `verify_dag_prereqs()` helper to
  `ai-router/session_log.py` so the check is mechanical rather than
  manual.

### Split Session Sets Whose Features Span Independent Prereq Chains

- **Context:** Same incident as the entry above
  (`access-symbol-table-and-types`, 2026-04-26). After surfacing the
  blocker, inspection of the spec showed it bundled two features
  with materially different prereq depths:
  - **Feature 1 — Synthetic Declaration Injection** required #9
    (resolver) and #10 (metadata extractor).
  - **Feature 2 — Type Library Registry** was a hand-curated lookup
    of DAO/ADO/Office/Access type members; its only real dependency
    was #10's project being in the repo, and its consumers
    (the resolver-side integration) were downstream.
- **Failure or friction:** The combined spec implicitly forced the
  whole set to wait on the slowest prereq (#9), even though half the
  work (Feature 2) could have run in parallel as soon as #10 landed.
  This wasted ~5 sessions of potential parallelism in the schedule.
  The split into `access-type-library-registry` (#11a, prereqs `[#10]`)
  and `access-resolver-symbol-table-integration` (#11b, prereqs
  `[#9, #10, #11a]`) recovered that parallelism — #11a became
  startable immediately and #11b's prereq list grew by exactly one
  soft-coupling link (the registry contract).
- **Lesson:** When authoring a session-set spec, list each feature's
  *actual* prereqs in column form, not the union of prereqs across
  the set. If two features have materially different prereq depths
  and there's a clean interface seam between them (e.g., one
  produces a contract the other consumes), split them into separate
  sets. The cost of splitting is one extra change-log + verification
  step; the benefit is unblocking parallel execution and avoiding
  long-blocked-set scheduling churn. The same logic applies in
  reverse: if two unblocked slugs share a prereq frontier and the
  combined session count is small, consider merging — but merging is
  rarely the bug; over-bundling is.
- **Action for future sessions:** When the
  `vba-parser-session-set-generator`-style planner authors a
  multi-feature session set, the planner prompt should require an
  explicit per-feature prereq table (not just a per-set union) and
  flag any spec where features have non-identical prereqs as a
  split candidate. Until the planner prompt is updated, the
  orchestrator authoring `ai-assignment.md` for a multi-feature set
  should perform this check manually as part of Step 3.5 — and
  surface a split recommendation to the human if the features
  diverge. If a second multi-feature set hits this pattern,
  promote both this lesson and the per-feature prereq table to
  Conventions in `project-guidance.md`.

### Require A Trailing JSON Block In Session-Verification Prompts To Prevent Terse Verdicts

- **Promoted to `project-guidance.md` → Conventions → Workflow
  Expectations** on 2026-04-27 (after `resolver-default-members-and-inference`
  Sessions 1-4, second+ context confirmation across 8 verification rounds).
  Retained here as the action-detail reference; the rule itself is now a
  Convention.
- **Context:** End-of-session cross-provider verification (Step 6).
  First hit in `resolver-declarations-and-scopes` Session 3
  (2026-04-27): gpt-5-4 returned only the bare string `ISSUES_FOUND`
  with no issue details on two separate rounds, requiring reprompting
  to get actionable findings.
- **Failure or friction:** Free-form verifier responses can legally
  truncate at the verdict line, leaving the orchestrator with no
  actionable findings. The round costs $0.09-$0.14; a terse response
  wastes the call and triggers an extra round.
- **Lesson:** Append a required structured JSON block to every
  `session-verification` prompt. Structuring the response forces the
  verifier to populate required fields and makes it harder to produce
  a one-line answer. The JSON block should follow the verifier's prose
  analysis (so reasoning still happens first), not replace it.
- **Action for future sessions:** End every `session-verification`
  prompt with the following required-response schema:

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

  The orchestrator can then parse `result.content` for the JSON block
  and pass it directly to `log.save_issue_log()` rather than
  interpreting free-form prose. If the JSON block is absent, treat
  the response as malformed and retry once with an explicit reminder.
  Additionally, open the verification prompt with an explicit prose-first
  instruction (e.g., "Your response MUST begin with a detailed prose
  analysis for each concern below BEFORE the JSON block") placed BEFORE
  the file listings — gemini-pro skips analysis prose when the instruction
  appears only in the trailing JSON-schema section. Confirmed in
  `enrichment-events-and-validation` Sessions 3 and 4 (2026-04-28):
  both rounds returned JSON-only `VERIFIED` (24 output tokens) despite the
  trailing block instruction being present; adding the lead instruction to
  round 2 produced substantive per-section prose analysis.

### Routed Output Truncation — `stop_reason="end_turn"` Is Not Proof Of Completeness

- **Context:** Any high-volume routed call (test-generation, refactoring,
  doc generation). First hit in `jet-sql-grammar-and-parser` Session 4
  (2026-04-27).
- **Failure or friction:** A `route(task_type="test-generation")` call
  asked gemini-pro for 24 xUnit test methods in one shot. The model
  returned ~11 tests at 2,547 output tokens and stopped mid-string
  (last line `var sql = "SELECT Reports`). The provider reported
  `stop_reason: "end_turn"` — not `"max_tokens"` — so the existing
  router escalation path did not fire. The orchestrator only
  discovered the truncation by visual inspection of the saved file,
  after `log.log_step` had already recorded the call as complete.
- **Lesson:** Three takeaways, in order of leverage.
  1. **`stop_reason == "max_tokens"` is one truncation signal but not
     sufficient.** Providers (gemini-pro empirically) can return
     `end_turn` on a cut-off response. Truncation detection must also
     include a syntactic-completeness check (unclosed code fence,
     unbalanced braces).
  2. **Item count is the wrong control variable for batch sizing.**
     Total output tokens drives truncation; tests vary in length
     (a single-assertion test is ~80 tokens; a multi-assertion test
     with multi-line SQL is ~250 tokens). Target ~1,200 output
     tokens per call as the budget; let item count fall out of that.
     A fixed item-count threshold (e.g., "≤8 tests per call") gives
     false confidence — 8 complex tests can still exceed the limit.
  3. **On detected truncation: halve the batch and retry, or fall
     back to in-conversation authoring.** Retrying at the same batch
     size with the same prompt is wasted spend; the model will hit
     the same point again. Geometric halving converges regardless
     of where the actual ceiling sits.
- **Action for future sessions:**
  - The router exposes `RouteResult.truncated` (added 2026-04-27 via
    `ai_router.utils.detect_truncation`). It is True when
    `stop_reason == "max_tokens"` OR an unclosed triple-backtick
    fence is detected OR `{` count exceeds `}` count. Callers should
    check this flag immediately after a route call and *before*
    logging success.
  - On `truncated == True`: halve the batch and retry, or author the
    remainder in-conversation when AST types / patterns are already
    in context. Do not retry at the same size.
  - The descriptor-based two-stage tactic (route compact descriptors,
    assemble boilerplate locally) is project-specific to code-
    generation tasks and is **not** recorded as a convention here. If
    a second session set independently arrives at the same factoring
    on a different task type, propose promotion at that point.
  - If a third session set hits the same truncation pattern, propose
    promoting the dynamic-budget heuristic to a Convention in
    `project-guidance.md`.

---

## Cost Guard On Verification: Skip When Verifier Cost Greatly Exceeds Generator

- **Context:** Any auto-verified task type (`code-review`,
  `security-review`) where the generator is a cheap tier-1 or
  tier-2 model and the rule-based verifier picks an expensive
  tier-3 model from a different provider.
- **Failure or friction:** A cheap Gemini Flash call routed at
  $0.0003 can pull a $0.15 GPT-5.4 verification call — a 500x
  cost ratio that destroys the savings the cheap generation was
  supposed to deliver. Without a guard, batch jobs that loop over
  many small routed calls accumulate verifier costs that dwarf
  the actual work.
- **Lesson:** The router's verification block in
  `router-config.yaml` carries `verification.max_cost_multiplier`
  (default 3.0). When the estimated verifier cost exceeds
  `max_cost_multiplier × generator cost`, the verification is
  skipped and the metrics record `verification_skipped:
  cost_guard`. The session-verification step is exempt from the
  guard — that one is non-negotiable cross-provider review.
- **Action for future sessions:** Trust the guard for
  auto-verification of cheap routed work. If a specific task type
  must always be verified regardless of cost ratio, raise
  `max_cost_multiplier` in the YAML or pin the generator to a
  more expensive model so the ratio drops naturally.

## ASCII-Only Glyphs In Cross-Platform Terminal Output

- **Context:** Any helper that prints a status table, summary, or
  progress indicator to the terminal. Examples:
  `print_session_set_status()`, `print_metrics_report()`, custom
  status helpers a session set adds.
- **Failure or friction:** Emoji glyphs (✅, 🟡, ⏳, etc.) crash
  Windows `cp1252` consoles mid-line, losing the rest of the
  report. Some terminal emulators silently drop the unprintable
  characters; PowerShell and cmd.exe in default Windows
  configurations raise `UnicodeEncodeError` and abort the script.
- **Lesson:** Use ASCII-only glyphs in any output that prints to
  the terminal: `[~]` for in-progress, `[ ]` for not-started,
  `[x]` for done, `*` for bullets, `-` for dashes. Reserve rich
  Unicode for files written to disk (`encoding="utf-8"`) where
  consumers control their own decoding.
- **Action for future sessions:** When porting or authoring a
  status helper, follow `print_session_set_status()`'s pattern in
  `ai-router/__init__.py`. If a richer rendering is desired, write
  it to a file and tell the user the path; do not assume the
  terminal will render it.

## Session-State.json Is The Single Source Of Truth For In-Progress Detection

- **Context:** Any external tool that needs to detect which
  session set is currently being worked on — VS Code Session Set
  Explorer, manager dashboards, parallel-session coordination.
- **Failure or friction:** `find_active_session_set()` historically
  inferred state from `activity-log.json` presence — but the
  activity log is only created on the first `log_step()` call,
  which can be 5–10 minutes after a session actually started.
  External tools showed sets as "not started" while a session was
  visibly underway.
- **Lesson:** Call `register_session_start()` at Step 1, before
  the first `log_step()`. The function writes `session-state.json`
  with `status: "in-progress"`, the orchestrator metadata, and the
  start timestamp. External tools watch this file and flip to In
  Progress immediately. At Step 8, `mark_session_complete()` flips
  status to `complete` and records the verification verdict.
- **Action for future sessions:** Always call
  `register_session_start()` at Step 1 and
  `mark_session_complete()` at Step 8. Do not rely on activity-log
  presence as the in-progress signal.

## Always Route `ai-assignment.md` And Next-Orchestrator / Next-Set Recommendations

- **Context:** Authoring `ai-assignment.md` (Step 3.5) or the
  next-orchestrator / next-session-set recommendations (Step 8).
- **Failure or friction:** When the orchestrator drafts these
  documents from its own opinion, the recommendations are
  systematically biased toward whichever model the orchestrator
  is. A Claude orchestrator predictably recommends Claude for the
  next session even when Gemini Flash or Sonnet would have handled
  it cheaper.
- **Lesson:** Always produce these via
  `route(task_type="analysis")`. The router's task-type defaults
  pick a cheaper model than the orchestrator's own, and the
  recommendation reflects routing economics rather than the
  orchestrator's self-image.
- **Action for future sessions:** Never self-opine on which model
  could have been cheaper. Rule #17 in the workflow doc makes this
  explicit.

## Per-Session-Set E2E/UAT Configuration Is Spec-Declared, Not Inferred

- **Context:** Authoring a new session-set spec, or onboarding a
  set into an upgraded workflow.
- **Failure or friction:** Earlier workflow versions inferred UAT
  applicability at runtime. The orchestrator made the call
  session-by-session, leading to inconsistency: one session in a
  set would author a checklist, the next would not.
- **Lesson:** UAT and E2E gates are declared once per session set
  in a YAML configuration block at the top of the spec
  (`requiresUAT`, `requiresE2E`, `uatScope`). The orchestrator
  reads the block at Step 2 and obeys it for the rest of the
  workflow. Authoring-time heuristics live in
  `docs/planning/session-set-authoring-guide.md`; runtime
  mechanics live in `docs/ai-led-session-workflow.md`. Crossing
  those wires (asking the orchestrator to second-guess the spec)
  is the failure pattern.
- **Action for future sessions:** When authoring a spec, read the
  authoring guide first and pick the flags deliberately. When
  executing a session, trust the spec.

## Inventory Existing Assets Before Designing New Infrastructure

- **Context:** The `vba-symbol-resolution-and-enrichment` set was
  scoped on 2026-04-28 as a 2-session PoC to "build symbol
  resolution infrastructure." Session 1 was attempted by Claude
  Haiku 4.5 and shipped a Python regex extractor. Cross-provider
  verification later returned FAIL with 12 findings (see
  `docs/session-sets/vba-symbol-resolution-and-enrichment/change-log.md`).
  When Claude Opus 4.7 prepared the Session 1 redo, it nearly
  authored a parallel Python rewrite of the same regex extractor
  before stopping to inventory the codebase.
- **Failure or friction:** The orchestrator was about to build a
  parallel implementation of work that already existed in C# with
  1148 passing tests. The harvester's `AccessHarvester.Vba.Parser`
  (ANTLR-driven, full AST), `AccessHarvester.Vba.Resolver`
  (declarations, scope tree, cross-reference index, default-member
  resolution, type inference, Access object-model integration),
  and `AccessHarvester.Pipeline.Enrichment` (FunctionClassification,
  EventHandlerClassification, NavigationGraphBuilder,
  FormComponentSpec, RowSourceSpec, QueryAbstraction, etc.) were
  all complete. Only the AI-interpretation pass that fills the
  existing `Purpose` / `Intent` / `Notes` / `Classification` slots
  was missing. That mismatch was hidden behind the orchestrator
  reaching straight for "design new infrastructure" instead of
  "what do we already have?"
- **Lesson:** Before authoring an architecture document for new
  infrastructure, spend 15-30 minutes inventorying existing assets
  in the codebase. Read the relevant project directories. Read the
  change-logs of recently-completed session sets in adjacent
  problem areas. Read the session-set-authoring-guide's
  prerequisite section. Confirm the new work is genuinely new and
  not a rebuild of something already shipped.
- **Action for future sessions:** When a session calls for "build
  X infrastructure," the orchestrator's first step is an
  inventory pass. Specifically:
  1. List directories under the relevant project namespace
     (`AccessHarvester.*`, `tools/`, `docs/test-cases/`, etc.)
  2. List `docs/session-sets/*/change-log.md` files for completed
     sets in adjacent problem areas; read the ones whose names
     overlap with the new work.
  3. Grep the codebase for the key terms in the proposed design
     (symbol table, resolver, classifier, etc.) — if matches
     exist, read them before designing new ones.
  4. Surface to the human anything that suggests the new work is
     a redo or a wrapper; pause for blessing before proceeding.
  This applies even when the spec is freshly authored. Specs can
  be written without full knowledge of repo state.

## Probe Output Value Earlier; Don't Sink Token Budget Into Unvalidated Substrates

> **Promoted to `project-guidance.md` → Conventions → Workflow
> Expectations** on 2026-04-29 (after `vba-symbol-db-path-b-probe`
> Sessions 1-3, second-context confirmation). Retained here as the
> action-detail reference; the rule itself is now a Convention.

> Companion lesson to "Inventory Existing Assets Before Designing
> New Infrastructure" above. That lesson is for the orchestrator;
> this one is for the human directing the work.

- **Context:** The harvester accumulated four days of high-quality
  parser, resolver, and enrichment work across multiple session
  sets (`parser-foundations`, `vba-lexical-and-expression-grammar`,
  `vba-statement-and-module-grammar`, `vba-grammar-validation`,
  `parser-infrastructure`, `resolver-declarations-and-scopes`,
  `resolver-references-and-with-blocks`,
  `resolver-default-members-and-inference`,
  `access-resolver-symbol-table-integration`,
  `enrichment-events-and-validation`,
  `enrichment-functions-navigation-review`,
  `enrichment-queries-and-forms`). By the time the human paused
  to ask "what does an AI agent actually need to interpret this?",
  the substrate was large and largely unvalidated by an actual
  downstream-AI consumer.
- **Failure or friction:** The work was genuinely good and
  necessary, but its value was never directly validated by feeding
  it to a downstream AI consumer. A comprehension probe — "give
  three AI agents this output, see if they can interpret one
  procedure" — would have answered "is the substrate working?"
  weeks earlier and at a fraction of the cost of building more
  substrate. The human articulated the lesson directly:
  "I should have looked sooner at the output before spending 4
  days worth of tokens on output that had limited value."
- **Lesson:** When building substrate for a downstream AI consumer,
  insert probe checkpoints earlier in the program of work, not
  only at the end. A "give the AI agent the current output and
  see if it can do the downstream task" probe is cheap (a few
  routed calls) compared to another session of substrate-building.
  Substrate that doesn't help the downstream consumer is sunk
  cost no matter how clean it is.
- **Action for future sessions:** When the project arc is
  "build substrate for an AI-driven downstream task," the human
  (or the orchestrator on the human's behalf) should propose a
  probe checkpoint after every 2-3 substrate-building session sets.
  The probe doesn't have to be elaborate; a single procedure or
  artifact, three providers, the human adjudicates. The cost of
  the probe is negligible compared to the cost of discovering the
  substrate-shape is wrong after another four days of token spend.
  The probe in `vba-symbol-db` Session 5 is the first instance of
  this pattern in this repo; future substrate-heavy initiatives
  should incorporate similar probes earlier in their session plans
  rather than waiting for the final session set.

- **First confirmed hit (2026-04-29):** `vba-symbol-db` Session 7
  closed with a PIVOT verdict driven directly by what the probe and
  Session 6's calibration analysis surfaced — routed
  cross-provider rigor was paying high cost for low correlation
  signal (Session 6 found Pearson +0.004 between confidence and
  cross-provider agreement; ~$24 routed across 7 sessions). The
  pivot is to subscription-paid in-IDE orchestrators on the full
  Northwind DB. The probe earned its keep by changing direction
  before another set's worth of token spend went into the same
  pattern. **Promote to a Convention in `project-guidance.md` the
  next time a second substrate-heavy initiative incorporates an
  early probe checkpoint and the probe materially changes
  direction.**

## Verify The Prior Session Actually Committed And Pushed Before Starting A New One

> **Promoted to `project-guidance.md` → Conventions → Workflow
> Expectations** on 2026-04-30 (after `workflow-package-pilot`
> Session 7, second-context confirmation — Session 6 finished
> workflow steps cleanly with VERIFIED across four Opus rounds but
> did not commit; Session 7 caught and bundled). Retained here as
> the action-detail reference; the rule itself is now a Convention.
> The companion `verify_prior_session_committed()` helper in
> `ai-router/session_log.py` is a deliberate follow-up, not yet
> implemented.

- **Context:** Picking up the next session of an in-progress session
  set when the prior session was run by a different in-IDE
  orchestrator. First hit on 2026-04-29 in
  `vba-symbol-db-path-b-probe` Session 3: Session 2 (Codex /
  GPT-5.5) had finished its workflow steps cleanly —
  `session-state.json` flipped to `complete`, verification was
  VERIFIED, `ai-assignment.md` was updated, the verifier review file
  was saved — but Codex did NOT execute Step 8's commit/push.
- **Failure or friction:** All Session 2 deliverables sat untracked
  in the working tree until Session 3 noticed during `git status`.
  Affected files: `tests/AccessHarvester.TranslationProbe.Tests/`
  (the new project), `docs/session-sets/<slug>/translation-packet.md`,
  `docs/session-sets/<slug>/translation/*`,
  `docs/session-sets/<slug>/session-reviews/session-002.md`,
  `tools/symbol-db/probe-builder/` (new C# project), and modifications
  to `AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs`,
  `AccessHarvester.sln`, and `tools/symbol-db/probe/build_packet.py`.
  The recovery cost was small (Session 3 bundled both sessions in
  one commit and noted the catch-up in the message), but the failure
  mode generalizes: any external tool or downstream session that
  reads `session-state.json` to detect "Session N is done" and
  assumes the artifacts are in git will be wrong.
- **Lesson:** External tooling reads `session-state.json` for
  in-progress detection (correct), but the orchestrator picking up
  the next session must NOT trust that signal alone for
  artifact-on-disk-vs-in-git questions. The git working tree is the
  only source of truth for "did the prior session actually commit?"
  Run `git status` against the session-set folder (and any code
  paths the prior session was supposed to touch) as part of Step 0.
- **Action for future sessions:** Add to the Step 0 checklist (after
  `register_session_start`, before the first content-producing
  step): run `git status --short -- docs/session-sets/<slug>/
  tests/ tools/`. If untracked or unstaged files matching the prior
  session's expected deliverables exist (cross-reference against
  the prior session's `## Acceptance Criteria` checked items in
  `ai-assignment.md`), surface to the human:
  "Prior session marked complete but did not commit; should I
  include those files in this session's commit, or push them as a
  separate catch-up commit first?" Default to bundling unless the
  human says otherwise. If a second session set hits this same
  failure mode (any orchestrator, any session), promote to a
  Convention in `project-guidance.md` → Workflow Expectations and
  add a `verify_prior_session_committed()` helper in
  `ai-router/session_log.py` so the check is mechanical rather than
  manual.

## When Routed Cross-Provider Rigor Returns "Your Rigor Isn't Paying For Itself," Drop The Rigor MODEL — Not The Rigor Itself

> **Promoted to `project-guidance.md` → Conventions → Token-Window
> Budget Overrides** on 2026-04-29 (after `vba-symbol-db-path-b-probe`
> Sessions 2-3 successfully executed the in-IDE-orchestrator-pair
> pivot — Codex translated, Claude reviewed, $0.078 routed across
> both sessions). Retained here as the action-detail reference; the
> rule itself is now a Convention. Note: the strict trigger ("a
> second session set hits the calibration → pivot pattern within one
> set") was not met — `vba-symbol-db-path-b-probe` only executed the
> pivot stage, inheriting the calibration result from `vba-symbol-db`.
> The human accepted the looser reading "second session set
> incorporates the in-IDE-orchestrator pivot" at the Step 9
> reorganization review.

- **Context:** A multi-session set built around the
  AI-router's routed cross-provider verification rigor as the primary
  quality gate. First hit in `vba-symbol-db` Sessions 4-7 (2026-04-29).
- **Failure or friction:** The set spent ~$24 routed across 7 sessions.
  The most expensive sessions were the ones designed to *measure*
  whether routed rigor was earning its keep — Session 5 ($7.98,
  84 cross-provider re-interpretations) and Session 6 ($12.46, 126
  cross-provider grading calls). Their headline finding was that
  self-reported `Confidence` is essentially uncorrelated with
  cross-provider interpretation agreement (Pearson +0.004) and mildly
  *negative* against justification agreement (-0.18). In other words,
  the rigor model itself was not paying for itself — the routed
  per-symbol verification was paying high cost for an uncalibrated
  signal. By Session 7 the cumulative cost had reached the human's
  pain threshold and Session 7's migration-readiness round and
  cross-provider session verification were both skipped. The instinct
  to "just drop rigor" was wrong; the right move was to drop the
  *rigor model*.
- **Lesson:** When a calibration session returns "your routed
  cross-provider rigor isn't paying for itself" (low or zero
  correlation between the rigor signal and the thing it is supposed
  to gate), the cost-effective response is to switch to a *cheaper*
  rigor model rather than to abandon rigor. Subscription-paid in-IDE
  orchestrator pairs (Claude Code + Codex/GPT, both billed against
  flat-rate windows the human is paying for anyway) provide
  cross-provider independence at marginal-zero per-call cost. Trade
  is wall-clock (slower per session, parallelizable across two
  orchestrators in different VS Code windows) and the loss of
  fine-grained per-symbol cost telemetry — both acceptable when the
  per-call billing was the binding constraint in the first place.
  Drop `Confidence*` and `Justification*` columns from the writeback
  path at the same time — they were the routed-rigor-era signals
  that Session 6 proved had no calibration value.
- **Action for future sessions:** If a session set's calibration
  output reports a zero-or-negative correlation between
  self-rated confidence (or any AI-self-rated signal) and
  cross-provider agreement, the next set should:
  1. Drop the AI-self-rated columns from the writeback path entirely
     (keep them in the schema if downstream consumers need them, but
     stop populating them).
  2. Re-platform the next iteration of the same work onto
     subscription-paid in-IDE orchestrators (Claude Code + Codex/GPT)
     — the per-call cost goes to zero, the cross-provider independence
     stays.
  3. Use the in-IDE-orchestrator pair as the cross-provider check
     directly (Claude reviews Codex's output and vice versa, in
     batches). Skip the routed grading rubric — the calibration
     session already proved it didn't add signal.
  4. Leave Gemini out of the interpretation/review path unless a
     Gemini coding assistant becomes available in VS Code that
     reads against the same subscription budget.
  If a second session set hits this same pattern (routed-rigor
  calibration that returns near-zero correlation, followed by an
  in-IDE-orchestrator pivot), promote to a Convention in
  `project-guidance.md` under Token-Window Budget Overrides.

## Write Absent-Case Tests Symmetrically Across All Promoted Fields, Not Just The Primary One

- **Context:** Hermetic unit tests for extractors that lift multiple fields out of a raw
  property bag into typed fields (e.g., `RecordSource`, `Filter`, `OrderBy` on
  `FormMetadataExtractor`). First hit in `structured-form-report-extractor` Session 3
  (2026-05-15): verifier (gpt-5-4 Round 1) flagged that `Filter` absent and `OrderBy`
  absent had no explicit tests, only `RecordSource` absent.
- **Failure or friction:** The spec said "present/absent" for the promotion group, but
  the absent-case tests were only written for the primary field (`RecordSource`). The
  extractor could silently return a stale value for `Filter` or `OrderBy` with no test
  to catch it. Fixing required two additional tests per extractor (four total) and a
  re-verification round.
- **Lesson:** When writing promotion tests, pair a "present" AND an "absent" test for
  **every** promoted field — not just the one that motivated the test file. This applies
  equally to reports, forms, tables, and any future extractor that lifts fields.
- **Action for future sessions:** Before marking extractor unit tests complete, scan the
  list of promoted fields and verify that each one has an explicit absent/null test.
  A promotion group with N fields should have at least N absent-case tests.

## Don't Name A Spec Deliverable `session-reviews/session-NNN.md` For Non-Verifier Content

- **Context:** Spike/analysis session sets where the primary deliverable is a written
  analysis rather than code (`access-object-extractor-spike`, Session 1, 2026-05-14).
- **Failure or friction:** The spec named `session-reviews/session-001.md` as the
  deliverable for the gap analysis and integration strategy document. After the
  orchestrator wrote the analysis there, `log.save_session_review()` (called at Step 6
  verification) overwrote it with the verifier's raw output. The analysis was lost and
  had to be recreated at a different path.
- **Lesson:** `log.save_session_review()` unconditionally writes to
  `session-reviews/session-NNN.md` — that path is owned by the session log, not the
  orchestrator. Spike and analysis deliverables must be placed at a session-set root
  file (e.g. `gap-analysis-and-strategy.md`, `findings.md`) or a distinctly named path.
  Only raw verifier output belongs under `session-reviews/`.
- **Action for future sessions:** When authoring a spike spec, name the primary
  deliverable at the session-set root level (alongside `spec.md`), not under
  `session-reviews/`. The `session-reviews/` directory is reserved for
  `log.save_session_review()` output.

## Post-Fix Sweep All Narrative Artifacts When A Rule Or Contract Changes

> **Promoted to `project-guidance.md` → Conventions → Workflow
> Expectations** on 2026-05-17 (after
> `harvester-finish-canonical-parse-success-report` Session 2,
> second-context confirmation — the same multi-round-drift failure
> mode replayed in a different trigger family: a rule-authority
> framing change rather than a confidence-tag change). Retained here
> as the action-detail reference; the rule itself is now a Convention.
> The original promotion-trigger language asked specifically for a
> second confidence-tag-change hit. The Step 9 reorganization review
> accepted the looser reading "same failure mode in a related trigger
> family" because the abstract pattern (narrative summary blocks lag
> body fixes) and the mitigation (grep / sweep before re-verifying)
> are identical across triggers.

- **Original context (confidence-tag changes):** Workflow-package
  sessions where a per-rule confidence tag (`inferred` vs
  `confirmed`) or an open-questions entry changes between a draft
  and a fix round. First hit in
  `generalization-validation-on-non-northwind` Session 2 Rounds
  1-3 (2026-05-15).
- **Second-context confirmation (rule-authority framing changes):**
  `harvester-finish-canonical-parse-success-report` Session 2 Rounds
  1-3 (2026-05-17) authoring `unresolved-reason-narrative.md`. R1
  fixed the body (Major: rewrote the `member-not-found-at-segment-<N>`
  framing from "stable entry" to "dynamic-prefix family"; Major:
  reconciled the `unknown-expression` analyzer-support claim with the
  verbatim suggested-next-steps; Minor: added per-pair
  FunctionClassification entries). R2 caught residual stale framing
  in the opening *Authority* block + the closing *Stability
  commitment* block that still described `SuggestedNextStepsLookup`
  as the full source-of-truth (Minor). R3 caught the *Cross-cutting
  consumer guidance* bullet that still said "narrative growth =
  reason reaching the lookup" (Minor). Three rounds and ~$0.42 of
  verification cost to converge on edits the body fixes implied.
- **Failure or friction:** R1 cross-provider verification fixes the
  body. R2 / R3 catch *narrative summaries* — intros, authority
  declarations, stability statements, cross-cutting consumer
  guidance — that still carry pre-fix framing because the
  orchestrator's natural editing motion targets the body but not the
  framing/summary blocks. The pattern repeats independent of trigger
  family: confidence-tags and rule-authority framing both produce
  the same multi-round drift if the orchestrator does not
  proactively sweep the narrative summary blocks.
- **Lesson:** When a rule, contract, confidence tag, or authority
  framing changes, the artifacts that **narrate** that rule's state
  — generalization findings, change logs, template-fit summary
  tables, intro/authority/stability blocks, cross-cutting guidance,
  count cells in prose — must be swept for stale language at the
  same time the rule itself is updated. The natural site of post-fix
  drift is the *frame* of the narrative (header / authority / closer
  / cross-cutting), not the body. Future fix passes should grep the
  frame blocks specifically, in addition to the body.
- **Action for future sessions:** Maintain a small post-fix
  checklist whenever a rule, contract, or framing is changed:
  1. Update the rule (package Section 3, per-reason entry, etc.) and
     any test that references it.
  2. Grep the *related narrative artifacts* for stale tokens. For
     confidence-tag changes: `inferred`, `not read`, `open question`,
     `not yet`, `Session 2 did not`. For rule/authority changes:
     opening-block phrases like "source of truth", "authority",
     "the lookup", "the closed set"; closing-block phrases like
     "stability commitment", "the reason set above"; cross-cutting
     phrases like "trigger is", "match on Reason exactly". Any
     residue adjacent to the changed rule gets rewritten in the same
     pass.
  3. Re-count any count cells in summary tables (e.g., "N
     questions", "N invariants", "N rules") so the cross-artifact
     totals match.
  4. Only then run the next cross-provider verification round.
  
  The convention text in `project-guidance.md` captures the rule
  itself; this entry holds the verbose action checklist + the two
  confirmation contexts that drove promotion.

## Falsification Tests Must Not Assert Behavior The Package Says It Did Not Verify

- **Context:** Path-b-probe pattern (AI translates a source
  procedure into a stub workflow, AI writes characterization
  tests, translation is discarded). First hit in
  `generalization-validation-on-non-northwind` Session 2 Round 1
  (2026-05-15).
- **Failure or friction:** Session 2 authored
  `FormatPhone_NormalizesToCanonicalShapeOnAfterUpdate()` which
  hardcoded the exact output mask `"(555) 123-4567"`, fallback
  behavior for non-10-digit input, and a `null -> null` return.
  But the package's phone-normalization rule explicitly said
  "Confidence: confirmed (that normalization happens); inferred
  (the exact output mask — Session 2 did not read `FormatPhone`'s
  body)." The test was asserting more than the package claimed to
  have verified — it was, in effect, asserting its own stub's
  shape rather than the source's behavior. The Round 1 verifier
  correctly flagged this as a Major finding.
- **Lesson:** Anti-coupling and evidence are two faces of the same
  rule: a falsification test pins what the package says is
  *confirmed*, not what the test author *thinks* the source
  probably does. A test that asserts more specific behavior than
  the package claims to know is a stub-tautology — it verifies
  the test's own implementation, not a workflow invariant. Two
  valid resolutions: (a) tighten the test to the verified
  portion only, (b) read the referenced source body, upgrade the
  package to `confirmed`, and keep the specific assertion.
- **Action for future sessions:** Before adding a falsification
  test against a workflow-package rule, check the rule's
  confidence tag. If the tag is `confirmed`, the test can assert
  the full specific shape the rule names. If the tag is
  `inferred`, the test must assert only what is `confirmed` —
  either restructure the test, or upgrade the package by reading
  the referenced source before writing the test. Cross-provider
  peer review reliably catches this; treat the convention as a
  pre-review checklist item to avoid spending verification rounds
  on it.
  
  If a second session set hits this same pattern, promote to a
  Convention in `project-guidance.md` under workflow-package
  authoring (next to the existing anti-coupling guidance from
  `workflow-package-pilot`).

## Write `disposition.json` Before The Final Session's Closeout Commit, Not After

> **Promoted to `project-guidance.md` → Conventions → Workflow
> Expectations** on 2026-05-15 (after `generalization-validation-on-non-northwind`
> Sessions 1-2, second-context confirmation — proactive application
> rather than a second failure-mode hit). Retained here as the
> action-detail reference; the rule itself is now a Convention. The
> companion workflow-doc update lists `disposition.json` explicitly
> as a Step 8 closeout artifact so future orchestrators don't have
> to discover this from lessons-learned.

- **Context:** Final-session closeout (Step 8). First hit in
  `form-report-code-and-grouping` Session 3 (2026-05-15).
- **Failure or friction:** The orchestrator generated `change-log.md` and
  `next-session-set-recommendation.md`, made the Session 3 closeout commit,
  pushed, and then called `mark_session_complete()` — which failed the
  `disposition_present` closeout gate with `CloseoutGateFailure`. The fix
  required writing `disposition.json`, staging it, making a second
  follow-up commit (`Session 3 closeout: add disposition.json`), and
  pushing again. The disposition file is a meta-file that summarizes the
  set's completion, lists files changed, and identifies the next session
  set — it logically belongs with the rest of the closeout artifacts in
  the same commit, not in a tail-commit that exists only to satisfy the
  gate.
- **Lesson:** `mark_session_complete()` enforces two checks before
  flipping `session-state.json` to `complete`: (1) `disposition.json`
  must exist; (2) the working tree must be clean (no uncommitted
  changes in the session-set scope). Writing `disposition.json` AFTER
  the closeout commit forces a second commit. The workflow doc's Step
  8 doesn't currently list `disposition.json` as a generated artifact,
  so an orchestrator following the doc verbatim hits the gate on the
  first set they close out.
- **Action for future sessions:** On the final session, generate
  `disposition.json` as part of the "Step 8 closeout artifacts" group,
  alongside `change-log.md` and `next-session-set-recommendation.md`,
  BEFORE staging the closeout commit. The file should include
  `status: "complete"`, a summary string, a `files_changed[]` list, a
  `next_session_set: {slug, summary}` block, and `blockers: []`. See
  `docs/session-sets/structured-form-report-extractor/disposition.json`
  for the canonical shape. If a second session set's last session hits
  this same failure mode, propose promoting this rule to a Convention
  in `project-guidance.md` and updating `docs/ai-led-session-workflow.md`
  Step 8 to list `disposition.json` explicitly.

## Pre-Verification Sweep For Cross-Session Decision Drift

- **Context:** Multi-session sets where Session N's deliverables
  reference open-question resolutions or decisions adjudicated in
  Session 1 (or any earlier session). First hit in
  `remaining-work-planner` Session 3 (2026-05-17).
- **Failure or friction:** Session 3 of `remaining-work-planner`
  authored a strategy.md Phase 4 update whose "Done when" paragraph
  still required golden output for **three** canonical fixtures
  (Northwind + HomeHealthCare + MiniComplex), while Session 1's Open
  Question 1 had resolved MiniComplex OUT of Phase 4 weeks earlier
  (recorded in `gap-audit.md`, the `disposition.json` →
  `post_close_resolutions`, and carried correctly into
  `remaining-work-tree.md` and the new ROADMAP.md). The orchestrator
  authored the strategy.md edit without grepping for the old
  three-fixture language; cross-provider verification (gpt-5-4 R1)
  caught it as a Major finding. Fix was trivial (one-paragraph edit),
  but R1 cost $0.187 and ~5 minutes of orchestrator time that a
  zero-cost grep would have prevented.
- **Lesson:** When a new session's artifacts reference resolutions
  adjudicated in a prior session (open questions, candidate-for-
  unfreezing decisions, scope decisions, fixture-inclusion calls),
  the orchestrator's natural authoring mode draws on the **prior**
  language ("three canonical fixtures" was the pre-Q1 phrasing
  throughout the codebase). The narrative-sweep failure mode that
  the existing "Post-Fix Sweep All Narrative Artifacts" lesson covers
  is intra-session (R1 → R2 drift after a fix); this is the
  cross-session variant. The mitigation pattern is the same — grep
  before verification — but the trigger is different (writing new
  artifacts that reference past decisions, not fixing R1 findings).
- **Action for future sessions:** Before the first cross-provider
  verification call on any session that produces artifacts
  referencing prior-session decisions, run a grep pass on the new
  artifacts for the pre-resolution language. For
  `remaining-work-planner` Session 3, that would have been:

  ```bash
  grep -nE "three.*fixture|MiniComplex" \
    docs/planning/strategy.md \
    docs/session-sets/ROADMAP.md \
    docs/session-sets/remaining-work-planner/change-log.md
  ```

  The grep target is the *old* phrasing the decisions superseded,
  not the new phrasing. For the sets this orchestrator touches in
  the near term: Session 1 Open Questions resolved
  (MiniComplex OUT, macros/property-sheet hybrid inventory-driven,
  packaging self-contained binary) imply
  grep targets like `three.*fixture`, `\\bMiniComplex\\b` (outside
  excluded-fixture context), `dotnet.tool` (out as primary), and
  similar. Cost: zero. Cost of skipping: an R1 retry round.

  If a second session set hits this same pattern (an R1 Major or
  Minor traced to stale pre-resolution language in a new artifact),
  promote this lesson to a Convention in
  `docs/planning/project-guidance.md` and consider whether the
  workflow doc's Step 6 verification setup deserves a "pre-verify
  grep checklist" sub-bullet.

## Bare JSON Verdict From The Verifier (Block Present, Analysis Absent) — Reminder Prompts Fix It

> **Promoted to `project-guidance.md` → Conventions → Workflow
> Expectations** on 2026-05-18 (after `harvester-finish-cli-packaging`
> Session 2's Step 9 reorganization review accepted the
> proactive-application reading of the trigger: this set baked the
> prose-before-JSON lead into Round 1 of the very first verification
> prompt and got a substantive 7-point analysis with empty issues on
> the first try — no Round 2 reminder needed). Retained here as the
> action-detail reference; the rule itself is now a Convention. The
> proactive-application reading mirrors the precedent set by the
> "Write `disposition.json` Before The Final Session's Closeout
> Commit" promotion (2026-05-15).

- **Context:** End-of-session Step 6 cross-provider session-verification
  via `route(task_type="session-verification")` (or `query("gemini-pro",
  task_type="session-verification")` when falling back per the
  Verifier-failure escalation ladder). First hit in Session 2 of
  `harvester-finish-cli-e2e-test-audit` (2026-05-18).
- **Failure or friction:** The existing "Require A Trailing JSON Block
  In Session-Verification Prompts To Prevent Terse Verdicts" convention
  covers the failure mode where the JSON block is *absent*. The new
  variant: gemini-pro returned only the JSON block (`{"verdict":
  "VERIFIED", "issues": []}`) with **no preceding analysis prose**. The
  JSON block was structurally valid, so the convention's literal trigger
  ("treat as malformed and retry once") did not fire — but the
  verification was auditless. Session 1 of the same set, also a
  gemini-pro fallback round, had produced a substantive 7-point
  numbered analysis followed by the JSON block, so the bare-verdict
  pattern is a per-call quality variance, not a systematic gemini-pro
  output mode.
- **Lesson:** The required-JSON-block prompt extension forces verdict
  structure but does not by itself force analysis. When a substantive
  audit trail matters (final session of a session set, work that
  closes a strategy-level criterion, high-stakes refactor), the prompt
  must also explicitly request the analysis section *before* the
  verdict block. The default required-JSON-block hint phrases the JSON
  block as the trailing output; the verifier is free to interpret that
  as the *only* required output.
- **Action for future sessions:** When calling `route()` or `query()`
  with `task_type="session-verification"` on closeout-grade work, add a
  second prompt block that explicitly requires:
  1. A numbered analysis covering each verification question, AND
  2. THEN the JSON verdict block in the required format.
  Phrase the analysis requirement as mandatory (e.g., "Do not skip the
  analysis. Both the analysis and the JSON block are required."). If
  round 1 returns only the JSON block despite the required-JSON-block
  hint, retry once with the "show your reasoning before the JSON"
  reminder — Session 2 R2 took 1559→2851 output chars and produced
  the substantive 5-point analysis. If a second session-set hits this
  same bare-verdict variant, propose promoting this lesson to a
  Convention in `project-guidance.md` and consider extending the
  default verification prompt template in
  `ai-router/prompt-templates/verification.md` to include the
  analysis-required block by default.

## Diff-Gate Sentinel Tests: Extract The Failure-Message Builder Into A Helper Both The Gate And The Sentinel Consume

- **Context:** Authoring a sentinel test for a diff-gate that fails
  when a generated artifact disagrees with a committed golden. The
  diff-gate emits a human-readable failure message at gate-fail
  time; the sentinel test exists to confirm that message stays
  human-readable as the gate evolves. First hit in
  `harvester-finish-canonical-parse-success-report` Session 2
  (2026-05-17) for `ParseSuccessReportTests` (HHC + Northwind
  per-fixture parse-success reports).
- **Failure or friction:** The natural shape of a sentinel test is
  to reconstruct the diff-message in the test itself and assert on
  it — but that means the sentinel is exercising a *parallel*
  implementation of the message text, not the message text the
  production gate actually emits. If the production gate's message
  drifts (a refactor changes a phrase, a CI log harvester picks up
  a different format string), the sentinel silently passes against
  the stale parallel implementation. The R1 verifier explicitly
  flagged this risk in this set's planning phase: "the sentinel
  test cover the same message a human sees when the gate fails —
  not a parallel reimplementation."
- **Lesson:** Extract the diff-message builder into a small helper
  class (`ParseSuccessReportDiff.Build(committed, generated, fixtureName, outPath)
  -> string?` here) that both the production gate and the sentinel
  test consume. Convention: `Build` returns `null` on equality and
  the assembled failure-message string on mismatch; the production
  gate throws on non-null; the sentinel test asserts substrings on
  the non-null result. The helper class can be `internal` to the
  test project so it does not leak into product surface. Byte-
  identical text to the gate's prior inline message is the
  correctness criterion for the refactor itself.
- **Action for future sessions:** When authoring a new diff-gate
  sentinel test:
  1. Extract the diff-message builder into a helper before writing
     the sentinel. Don't write the sentinel against an inline
     message-builder in the production gate.
  2. Have the production gate delegate to the helper. The
     production-gate refactor should be byte-identical to the prior
     inline message-emission (verify by a single `dotnet test`
     pass against the committed golden, then by adding a
     hand-built mutation case to the sentinel that asserts the
     non-null path).
  3. The sentinel covers three shapes at minimum: (a) byte-equal
     returns `null`, (b) a single-character mutation produces a
     message that contains the fixture name, line number, both
     snippets, and the regeneration affordance, (c) any internal
     helpers (e.g., a snippet escaper) round-trip cleanly.
  4. Keep the helper class `internal static` in the test project
     unless the production code itself needs to call it; the
     sentinel testing affordance is a *test-project* concern, not
     a product surface.

  If a second diff-gate sentinel test in this repo follows this
  pattern, promote to a Convention in
  `docs/planning/project-guidance.md` → Conventions → Testing
  Conventions, naming `ParseSuccessReportDiff` /
  `ParseSuccessReportDiffTests` as the reference shape.

### Regenerate Committed Example Files Alongside The Schema Set That Produces Them

- **Context:** Session 3 of
  `harvester-finish-operations-and-interpretation-docs` (2026-05-18).
  The set's spec listed `examples/*.json` as the consumer-facing
  examples gallery the interpretation guide's §9 would point at. The
  three files (`examples/minimal-form.json`,
  `examples/complex-form.json`, `examples/northwind-output.json`) had
  been authored against the pre-import AccessExporterTemp1 schema and
  never regenerated when the canonical-golden-output set normalised
  the harvester output to the current
  `{ Type, Properties, Children, ... }` per-fixture multi-file corpus
  shape.
- **Failure or friction:** The downstream-AI probe (Session 3 step 3)
  surfaced the schema mismatch as the spec's "Out-of-date examples"
  stop rule trip. Resolution required: a WIP commit at the stop
  point, a round of user adjudication, an in-session regeneration
  (workflow deviation from the spec-prescribed tree-amendment to
  `schema-finalization-and-docs`), two additional probe rounds to
  surface gap candidates against the regenerated examples, two
  rounds of guide edits to address the gaps, and a separate
  follow-up tree-amendment session
  (`schema-finalization-and-docs-tree-amendment-001`) recommended
  to formalise the provenance of the in-session regen. Direct cost:
  ~$0.18 in extra probe rounds plus orchestrator time across the
  detour. The schema mismatch had been latent since the
  canonical-golden-output set landed; it surfaced only when a
  downstream docs set actually exercised the example files.
- **Lesson:** Committed example files (anything under `examples/`,
  `samples/GoldenOutput/`, fixture corpora, or similar) are part of
  the schema contract. When a session set produces a new schema or
  modifies a schema-touching corpus, the same set must regenerate
  every committed example file that references the schema — not as a
  later-session amendment. The cost of the regenerate-now step is
  trivial (file copies plus a build / golden-pin run); the cost of
  rediscovering the staleness later is a multi-round detour and a
  procedural-debt amendment. The reverse case — leaving examples
  pointing at the old schema "for backward reference" — is also
  wrong; the consumer-facing examples must match the current
  contract.
- **Action for future sessions:** When authoring or reviewing a spec
  that produces a new schema or touches a corpus consumed by
  examples, add a "regenerate `examples/` and `samples/GoldenOutput/`
  entries that reference this schema" step to the same set's
  Sessions list. When picking up a docs set that consumes existing
  example files, sanity-check the example file shape against the
  current canonical golden corpus *before* the downstream-AI probe
  runs — a quick `diff` of one form file from `examples/` against
  one form file from `samples/GoldenOutput/<fixture>/ir/Forms/` is a
  five-second check that prevents a stop-rule trip. If a second
  context in this repo confirms the staleness pattern, propose
  promotion to a Convention in
  `docs/planning/project-guidance.md` → Conventions → Workflow
  Expectations: "When a session set produces or modifies a schema,
  regenerate every committed example referencing that schema in the
  same set's commit."

### Net-Core xUnit Test Host Does Not Load Legacy Encodings — Register Them Explicitly

- **Context:** `harvester-body-and-query-sql` Session 1, wiring `EmitManifestArtifacts` to parse VBA text from `raw/Forms/*.txt` files produced by Access `SaveAsText`.
- **Failure or friction:** All form VBA parse calls silently failed with `NotSupportedException: No data is available for encoding 1252` in the xUnit test host. The same code worked in the CLI host (which initializes Windows legacy encoding support through Access COM). The exception was caught by the `try/catch` in `EmitManifestArtifacts`, logging a diagnostic and skipping every form module — so the manifest emitted `Forms[].ParserOutput = null` in the test but emitted `ParserOutput: {…}` in the CLI run. The discrepancy was invisible until the golden regression test failed.
- **Lesson:** In .NET Core, `Encoding.GetEncoding(N)` for any codepage outside the platform-default subset (which includes 1252 on non-Windows) throws `NotSupportedException` unless `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` has been called in the current process. The CLI host gains this transitively from Access COM startup; the xUnit test host does not. A caught encoding exception in a manifest-emission loop is a silent data corruption bug: the manifest looks valid (Forms: [] or Forms without ParserOutput) but is structurally incomplete.
- **Action for future sessions:** When reading Access-exported files (`SaveAsText` produces Windows-1252 ANSI text), call `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` at the top of the method before the first `GetEncoding()` call. The provider's `RegisterProvider` is idempotent (repeated calls are no-ops) and thread-safe. Never catch encoding exceptions inside a loop that is supposed to populate a collection and silently continue — log them prominently or rethrow so the failure is visible.

### Always Sort Determinism-Sensitive Collections Emitted To JSON Manifests

- **Context:** `harvester-body-and-query-sql` Session 1, `ParserOutputProjector.BuildBody()`, which iterates `ResolvedModule.References` and emits them into `Body.References[]` and `Body.Calls[]`.
- **Failure or friction:** `Directory.GetFiles()` returns files in NTFS creation order, which varies across different pipeline runs on the same machine and across fresh temp directories. The golden regression test compared manifests produced at different times and hit ordering drift in Forms/Reports arrays when different Access COM extraction runs created ir-files in different NTFS order.
- **Lesson:** Any collection emitted to a determinism-sensitive JSON output (manifest, golden corpus, schema validation fixture) must be explicitly sorted before emission. Relying on "the upstream should be deterministic" is insufficient — upstream ordering may be guaranteed within a single process run but differ across processes, hosts, or fresh extractions.
- **Action for future sessions:** When building a list from `Directory.GetFiles()`, `module.References`, or any ordered-but-not-guaranteed-stable upstream source that feeds a manifest or test fixture, add `.OrderBy(f => f, StringComparer.Ordinal)` (for paths) or the appropriate stable sort key (for AST/resolver results, use location: Line → Column → EndLine → EndColumn → text). For `Body.References`, the total sort key is `(StartLine, StartColumn, EndLine, EndColumn, ExpressionText)` — this is now the established pattern in `ParserOutputProjector.BuildBody()`.

### Sweep Spurious `.accdb` Binary Diffs After `dotnet test` Before Staging The Closeout Commit

- **Context:** `harvester-object-cleanup-identifier` Session 3 (2026-05-28),
  preparing the Session 3 closeout commit after the full sequential
  `dotnet test` run.
- **Failure or friction:** `git status --short` showed two unexpected
  binary modifications — `samples/HomeHealthCare.accdb` and
  `samples/MiniComplex.accdb`. Neither file was touched by any source
  edit in the session, and the explicit `identify-cleanup` pilot was
  run with `--no-com`. The diffs were caused by `dotnet test`:
  Access integration tests that open an `.accdb` via COM cause
  Access to mutate internal timestamps and persistence-state fields
  on close, even when the test is read-only at the SQL/DAO level.
  This is the same Access-COM-mutates-on-open root cause that
  "Run Parallel Session Sets In Per-Set Git Worktrees" documents
  for `samples/NorthwindDevEdition.accdb` in the worktree-merge
  context. But the failure mode is wider: any session that runs
  the test suite before its closeout commit can hit it on any
  `.accdb` under `samples/`.
- **Lesson:** Spurious `.accdb` diffs after `dotnet test` carry no
  semantic content — only Access-managed timestamp metadata — and
  must not be committed. If they sneak into the closeout commit
  they pollute git history with noise that future workers diff
  against, and they conflict-and-rebase miserably across parallel
  worktrees. The session's actual deliverables already live in
  source files (`.cs`, `.md`, `.json`) and emitted golden outputs
  under `samples/GoldenOutput/<db>/`; the `.accdb` binaries
  themselves are inputs, not outputs.
- **Action for future sessions:** As part of the Step 8 closeout
  pre-commit pass, run `git status --short -- samples/*.accdb` and
  `git checkout -- samples/*.accdb` for any `.accdb` shown as
  modified. Do this BEFORE `git add` so the spurious touches are
  never staged. If a second context in this repo confirms the
  pattern as session-set-spanning rather than session-spanning,
  propose promoting it to a Convention in `project-guidance.md` →
  Conventions → Workflow Expectations alongside the existing
  "session-state.json is the single source of truth" entry.
