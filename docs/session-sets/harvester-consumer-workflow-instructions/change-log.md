# Change Log — `harvester-consumer-workflow-instructions`

**Set goal:** Make the harvester output *self-instructing* so a consuming AI
agent learns the full canonical workflow (including the `identify-usage` /
`identify-cleanup` feedback tools) without prior knowledge, and can walk a
non-expert developer through the human-review artifacts — what each JSON file
means, how to act on it, and which zero-install editor opens it.

**Outcome:** Achieved, plus a real one-click-blocking bug fixed along the way.
Full tier; no UAT/E2E (no UI). 2 sessions, both VERIFIED on the first
cross-provider round.

---

## Session 1 — AI-consumer workflow in the self-describing entry point

Orchestrator: claude-code / Opus 4.8 @ effort=medium (2026-06-01).

- Finalized the previously-DRAFT spec (maintainer decisions: `totalSessions: 2`
  confirmed; editor-hosting open decision deferred to Session 2; **no** new
  manifest schema field — `Documentation.Url` already resolves to the
  interpretation guide).
- Added **§3 "Workflow for AI consumers"** to
  [`docs/harvester/interpretation-guide.md`](../../harvester/interpretation-guide.md):
  expanded the former "Reading order" into §3.2 and inserted §3.1 "The canonical
  consumer workflow" (`run` → read `manifest.json` + verify counts →
  `identify-usage` → `object-use.json` (do **not** re-derive the inventory by
  hand) → `identify-cleanup` → `object-cleanup.json` → present `*-checklist.json`
  to the developer). Commands/flags verified against the C# source; prominent
  callout for the correct `--in` target (the per-database dir containing `ir/`,
  not the `--out` root — which silently yields 0 objects).
- Mirrored a concise "AI consumers: read the workflow first" pointer into the
  auto-emitted `READ-ME-FIRST.md` template (`Pipeline.cs`) and both committed
  golden-corpus copies.
- Cross-provider verified VERIFIED on round 2 (gpt-5-4) after fixing a Minor
  self-contradiction and a Major premature-claim from round 1.

## Session 2 — Developer-facing artifact guide, editor wiring, and a bug fix

Orchestrator: claude-code / Opus 4.8 @ effort=medium (2026-06-01).

**Editor-hosting open decision — resolved by cross-provider consensus**
(gpt-5-4 + gemini-pro, bias-cautions preamble; journal in
[`ai_router/consensus-decisions.jsonl`](../../../ai_router/consensus-decisions.jsonl)),
so no `AskUserQuestion` was needed:
- **(a)** Defer classification-editor GitHub Pages hosting (Phase-4 finish-line
  scope; new hosting = new maintenance surface). Docs instruct download +
  local-open of `tools/classification-editor.html`. A raw GitHub `.html` URL is
  **not** a substitute (it won't render as a running app).
- **(b)** Confirm uat-checklist-editor for **both** checklists — *after a bug fix*.

**Bug found and fixed (the scope expansion).** Investigating open-decision (b)
("verify the format matches") revealed that the uat-checklist-editor requires
**PascalCase** `{ "ProjectName", "Review": [...] }` (verified against its
`index.html` and `checklist-schema.json`), and that `object-cleanup-checklist.json`
already matched — but `UsageChecklistWriter` serialized **both** `object-use.json`
and `object-use-checklist.json` through one camelCase options object, so the
use-checklist loaded as an **empty** checklist in the editor. The spec's original
premise ("the editor expects `{projectName, review[…]}`") was backwards.
- Fixed [`UsageChecklistWriter`](../../../AccessHarvester.Pipeline/UsageIdentification/UsageChecklistWriter.cs)
  to emit the checklist in PascalCase (split `_camelOpts` / `_pascalOpts`,
  mirroring `CleanupChecklistWriter`); `object-use.json` stays camelCase.
- Updated the unit test that had encoded the bug (it asserted lowercase `review`)
  to assert PascalCase + absence of camelCase.
- Regenerated the two committed golden `object-use-checklist.json` samples via the
  hermetic `identify-usage` path (reads `ir/`, no COM); confirmed casing-only
  (content identical after lowercasing keys). These golden files are **not** under
  the golden-corpus regression gate, so the fix can't break it.

**Documentation deliverables:**
- New [`docs/harvester/reviewing-the-feedback-artifacts.md`](../../harvester/reviewing-the-feedback-artifacts.md)
  — plain-language developer guide (per-artifact table: what it is, what the
  developer does, which editor). Drafted via `route(documentation)` → gemini-pro,
  then assembled (its own auto-verification flagged two minor misses, folded in).
- Added interpretation-guide **§3.3** (mandatory AI-agent rule: never hand over
  raw JSON — explain each artifact + link the editor; with a copy-paste script),
  updated §3.1 step 4 (removed the stale "forthcoming guide" promise), and §10 +
  `docs/README.md` pointers.
- Wired the editor URLs into the emitted `READ-ME-FIRST.md` template (`Pipeline.cs`)
  and both golden copies, so they travel with the output.

**Validation (incremental, per the new lessons-learned tactic):** `dotnet build`
solution = 0 errors; `AccessHarvester.Pipeline.Tests` (excl. COM probe) =
684 passed / 0 failed. The Access-COM/PDF integration tier was intentionally not
run — all Session-2 changes are hermetic. Cross-provider verification (gpt-5-4):
**VERIFIED round 1**, no issues.

**Mid-session note:** the human flagged that the COM/PDF integration tests are
fragile (other instances have had to kill them). This produced a new
`lessons-learned.md` entry ("Run Tests Incrementally By Project; Isolate the
Access-COM / PDF Integration Tests") and seeded the follow-up set
`harvester-access-com-graceful-shutdown` (since authored, run, and closed) that
hardened `AccessComSession` against zombie `msaccess.exe`.

---

## Files changed (Session 2)

- `AccessHarvester.Pipeline/UsageIdentification/UsageChecklistWriter.cs` (bug fix)
- `tests/AccessHarvester.Pipeline.Tests/UsageIdentification/UsageChecklistWriterTests.cs`
- `AccessHarvester.Core/Pipeline.cs` (READ-ME-FIRST editor section)
- `docs/harvester/reviewing-the-feedback-artifacts.md` (new)
- `docs/harvester/interpretation-guide.md` (§3.1, §3.3, §10)
- `docs/README.md` (index pointer)
- `samples/GoldenOutput/{HomeHealthCare,NorthwindDevEdition}/READ-ME-FIRST.md`
- `samples/GoldenOutput/{HomeHealthCare,NorthwindDevEdition}/usage/object-use-checklist.json`
- `docs/planning/lessons-learned.md` (incremental-testing entry)
- `docs/session-sets/harvester-consumer-workflow-instructions/{spec.md,ai-assignment.md,...}`
- `ai_router/consensus-decisions.jsonl` (consensus journal record)

## Cross-set handoff recommendation

Routed via `route(task_type="analysis")` → gemini-pro (do not self-opine).

**Next set:** the **`sanitize`-against-HomeHealthCare timeout investigative
spike** (documented in `lessons-learned.md`) — reliability before features, the
ordering this arc just validated. **Re-measure first:** the just-closed
`AccessComSession` hardening may have resolved the timeout as a side effect, in
which case the spike closes with a documentation note and no further work. If it
persists, instrument the sanitize CLI path (`OleDbValueSampler` / `OleDbRowSource`
/ COM cleanup) and isolate. No spec required (the goal is already documented). The
`harvester-sql-migration-generator` feature is the logical capstone **after** the
spike (it needs a new spec; confirm it belongs in this repo vs. the future
migration repo). Retire/absorb the now-subsumed `harvester-parallel-com-test-cleanup`
candidate.

The routed recommendation argued for an ultra-cheap diagnostic model on pure
cost grounds; a `sanitize`/OLE-DB/COM diagnostic that must comprehend this repo's
extraction code realistically needs a capable model on a Windows+Access host —
see `disposition.json` `next_orchestrator`.
