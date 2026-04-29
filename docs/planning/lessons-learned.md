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

## When Routed Cross-Provider Rigor Returns "Your Rigor Isn't Paying For Itself," Drop The Rigor MODEL — Not The Rigor Itself

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
