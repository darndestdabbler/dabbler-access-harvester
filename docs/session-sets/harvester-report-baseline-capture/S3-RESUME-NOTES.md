# Session 3 — Resume Notes (rewritten 2026-05-31)

> Supersedes the prior reboot-era notes. **Delete this file at S3
> closeout.** Scratch breadcrumb, not a deliverable.

## TL;DR

S3's substantive work is **done and uncommitted in the working tree**.
The golden-corpus deliverable (decision #7) is **deferred to a new
follow-up set** with maintainer approval. What remains is the routed
**verification + Step 8 close + Step 9** — intentionally handed to a
**fresh session** because tooling was glitching and context was
exhausted when S3 was paused. Do NOT improvise the close; follow the
checklist below.

## Decision of record (maintainer-approved 2026-05-31)

The golden `reports-baseline/` corpus + regression gate (spec decision
#7) and its READ-ME-FIRST (Step 3.4) are **deferred** to a new set,
**`harvester-report-baseline-golden-corpus`** (stub spec written at
`docs/session-sets/harvester-report-baseline-golden-corpus/spec.md`).
Reason: aggregate capture trips an Access design-view / ACE-OLEDB
exclusive-lock contention (root cause in that stub). The fix is invasive
(phase-decoupling, or better: compute aggregates via standalone ACE
OLEDB with no COM design-view read) and was correctly kept out of this
closeout per the spec stop-rule. S3 therefore ships the **COM-free**
scope: property decode (#5) + docs.

## What is DONE (uncommitted in working tree)

- **Property decode + `--include-pdf` opt-in (#5):** shipped in S2-era +
  S3 edits; aggregates-only is the default. Hermetic tests updated.
  (Code: `ReportBaselineRunner.cs`, `ReportBaselineCommand.cs`,
  `Program.cs`; tests in `ReportBaselineRunnerTests.cs`,
  `ReportBaselineCommandIntegrationTests.cs`, `CliUsageTextTests.cs`.)
- **Docs (3.2/3.3):** `docs/harvester/install.md` (subcommand table row +
  full `report-baseline` reference section incl. deferred-golden note)
  and `docs/harvester/interpretation-guide.md` ("Reading report
  baselines" section). Authored in-conversation (disclose this to the
  verifier).
- **Follow-up stub:** `docs/session-sets/harvester-report-baseline-golden-corpus/spec.md`.
- **`disposition.json`:** updated content (summary, files_changed,
  next_session_set, blockers). `status` left `"in-progress"` ON PURPOSE
  — set the final value at close per the validated contract (see step 4).
- **Prevention guard (new, untracked):** `.claude/hooks/guard-dotnet-parallel.ps1`
  + `.claude/settings.json` — a PreToolUse hook blocking
  backgrounded/parallel `dotnet build|test|run` (the root cause of the
  zombie mess). **Not live until `/hooks` is opened once or Claude Code
  restarts** (the settings file didn't exist at session start).
- **Memory:** `feedback_authorized_to_kill_integration_test_zombies`
  written to ~/.claude auto-memory (not part of the repo commit).

## Environment notes / what bit us

- The day's lost time was a self-inflicted **parallel `dotnet test`**
  run: backgrounded test runs spawned COM-RPC-wedged `testhost` zombies
  that held the build DLL locks. They **reaped on their own** once the
  background tasks completed; `dotnet build` is now green, **no reboot
  needed**. The guard hook prevents a recurrence.
- **Tooling was intermittently corrupting** Bash/Glob/Grep output near
  the end (Grep returned "no matches" on patterns that exist). `Read` /
  `Write` / `Edit` stayed reliable. If Grep/Bash look wrong in the fresh
  session, restart first.
- Any COM run drifts `samples/HomeHealthCare.accdb` (and sometimes
  `MiniComplex.accdb`). **Revert before staging:**
  `git checkout HEAD -- samples/*.accdb`.

## REMAINING STEPS (fresh session — do in order)

1. **Build + test, sequential, foreground only.** `dotnet build` then
   `dotnet test`. Confirm green. (The COM `[SkippableFact]` integration
   tests run; revert `samples/*.accdb` drift afterward.)
2. **Step 6 — cross-provider verification (mandatory, routed).**
   `route(task_type="session-verification", complexity_hint=70,
   session_set=..., session_number=3)` → gpt-5-4 for a Claude
   orchestrator (per lessons-learned; do NOT let it resolve to Anthropic).
   In the prompt, DISCLOSE: (a) docs were authored in-conversation;
   (b) the golden corpus (decision #7) is **deferred** to
   `harvester-report-baseline-golden-corpus` — a maintainer-approved
   scope reduction, not an omission; (c) list the in-scope files.
   Include the trailing JSON verdict block + prose-first lead. Save via
   `log.save_session_review(...)`.
3. **Step 7** — only if the verifier disagrees: present the four options
   to the human; `record_adjudication(...)` if it comes to that.
4. **Step 8 — close (final session).**
   - `disposition.json`: set `status: "completed"` (CONFIRMED from
     `ai_router/disposition.py`: `DISPOSITION_STATUSES = ("completed",
     "failed", "requires_review")` — there is NO "complete"/"in-progress"
     value; it currently holds the `"in-progress"` placeholder, which is
     INVALID for close). On the **final** session `next_orchestrator` is
     NOT required (only required when `status=="completed"` AND not
     final). Keep the populated `blockers` array. Add the verification
     verdict after Step 6.
   - **`close_session` does NOT run git or notifications** (confirmed
     from `ai_router/close_session.py`: "does NOT run git commit / push
     or send notifications. The caller commits and pushes before invoking
     this script"; `check_pushed_to_remote` enforces it). So the order is:
     revert `samples/*.accdb` drift → `git add` all S3 work + guard files
     + follow-up stub → `git commit` → **`git push`** → THEN run
     `close_session`.
   - `.venv/Scripts/python.exe -m ai_router.close_session
     --session-set-dir docs/session-sets/harvester-report-baseline-capture`
     (run through the venv interpreter — a bare `python` gives the
     misleading `No module named ai_router.close_session`). It validates
     `disposition.json`, generates `change-log.md`, appends events, and
     flips state on the final close. Flags if needed: `--force`
     (bypass gates, incident only), `--repair --apply` (backfill drifted
     events), `--allow-empty-commit`.
   - **Verify no N−1/N drift:** open `session-state.json` and confirm
     `sessions[2]` (Session 3) is `status:"complete"` with `completedAt`,
     and the set's top-level `status` is `"complete"`. (v4 rules in
     CLAUDE.md.) If the CLI left it short, fix per the schema.
   - `ai-assignment.md` Session 3 Actuals.
   - `send_session_complete_notification(session_set=..., session_number=3,
     verdict=..., total_sessions=3)`.
5. **ROADMAP:** flip the `harvester-report-baseline-capture` row to
   complete (note golden deferred → follow-up); add a row for
   `harvester-report-baseline-golden-corpus`.
6. **Step 9 — reorganization review (post-notify).** Propose a
   `lessons-learned.md` entry capturing: (i) never run `dotnet
   build/test` in parallel/background — the COM-RPC testhost-zombie
   failure + the guard-hook mitigation; (ii) `report-baseline` is a thin
   *verification* layer — the harvester's value is the static substrate
   (metadata + SQL + expression decomposition), so deferring it costs
   little; (iii) the no-COM-aggregates design insight. Also consider the
   testhost-kill authorization. Apply accepted items; commit separately.
7. **Delete this file.**
