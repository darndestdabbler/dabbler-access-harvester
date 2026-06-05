# Next Session-Set Recommendation — after 004 Northwind Symbol-DB / SQLite Residue

> Authored at 004 Session 3 closeout. The recommendation below is **routed**
> (cross-provider `analysis`, do-not-self-opine), not the orchestrator's own
> opinion. Raw routed output: [`next-set-analysis-raw.md`](next-set-analysis-raw.md).

## Recommended next set: `005-embedded-residue-redaction` (compliance, not feature)

Set `004` purged the **highest-density** proprietary disclosure — the two binary
symbol-DB SQLite databases and their 5 derived consistency artifacts — from all
history. But it was deliberately scoped to that SQLite/data cluster. The routed
analysis recommends a dedicated follow-on set because proprietary-structure
disclosure is a **compliance liability, distinct from feature work**, so the
repo's "consumer-driven-only" posture (harvester feature arc complete) does **not**
gate it. `003`'s position that "the remediation is not complete while residue
remains" still holds.

What remains deferred (from `003`'s `embedded-residue-findings.md` and `004`'s
Out-of-Scope):

- The **HIGH/MEDIUM/LOW embedded-residue buckets**: probe / translation / workflow
  docs, characterization tests, and name-only references that still disclose
  proprietary NorthwindDevEdition *structure* (form / report / query / table /
  module / control names) inside KEEP-files across the working tree and history.
- The **C# symbol-DB source/test embedded-residue** question (explicitly deferred
  by `004`).

### Proposed shape (from the routed analysis — 3 sessions)

1. **S1 — Triage + redaction/removal map authoring.** Inventory the residue strings
   across the HIGH/MEDIUM/LOW buckets + the C# question; author the definitive
   per-string disposition map.
2. **S2 — Dry-run + verification** on a throwaway `--mirror` clone; prove the
   transformation is exact and side-effect-free.
3. **S3 — Gated execution + closeout** following `004`'s documented destructive-
   rewrite procedure (fresh backup, mirror clone, force-push via `gh` as sole
   credential helper, re-establish checkout, re-register state after `reset --hard`).

> **Scoping note for whoever authors `005`:** the routed analysis assumes a
> **content-redaction** rewrite (rewriting proprietary strings *inside* kept files
> via a `git filter-repo --blob-callback` / replace-text pass). That is materially
> more invasive than `004`'s **path-removal** approach and changes file contents
> across all history. Whether `005` should redact-in-place, delete the offending
> files, or split into per-bucket sub-sets is a **design decision for `005`'s spec**,
> not settled here. Treat the 3-session shape above as a starting point, not a
> mandate.

## Recommended orchestrator (005 Session 1)

The routed analysis frames S1 as an investigative, text-processing/analysis task —
recommending the **cheapest capable engine** at **low–medium** effort (it is
inventory + map authoring, not novel synthesis). Either `claude-code` (Opus 4.8)
for holding the large cross-file inventory in context, or `codex` (GPT-5.x) for the
scripting load, is defensible; let the router pick per `router-config.yaml`.
Cross-provider `session-verification` remains mandatory and must land on a
different provider than the chosen orchestrator (for a Claude orchestrator →
`gpt-5-4`).

## Sequencing caveats

1. **Block new releases until residue remediation is complete.** `main` and history
   still contain proprietary *structural* disclosure in prose/test/name-only form;
   cutting a release would formalize/distribute it. (`v1.2.0` was re-pointed by
   `003` and again by `004`'s tag force-push and is accepted; this governs *future*
   releases.)
2. **Another destructive history rewrite + force-push.** This cannot be a simple
   commit on `main`. Strictly follow `004`'s lessons: fresh backup at the moment of
   the rewrite, throwaway `--mirror` clone, `gh` as the sole credential helper,
   re-register session state after `reset --hard`.
3. **Freeze `main` and avoid parallel sets during `005`'s execution.** Commits
   landing between curation and force-push would be orphaned or need manual re-base.
4. **If content-redaction is chosen,** re-base any characterization/translation
   tests that assert on the proprietary strings onto the synthetic HomeHealthCare
   fixture *before* the destructive session, so they have a valid baseline after
   redaction.
5. **No hard deadline / not auto-started.** This is a maintainer-gated compliance
   set; it does not auto-launch. Surface it as the recommended next action and wait
   for the maintainer to start it.
