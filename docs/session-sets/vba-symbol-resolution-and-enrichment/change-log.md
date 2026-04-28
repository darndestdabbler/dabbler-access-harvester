# vba-symbol-resolution-and-enrichment — Change Log (Retired / Superseded)

**Closed:** 2026-04-28
**Status:** Retired and superseded by [`vba-symbol-db`](../vba-symbol-db/spec.md). Do not start additional sessions in this set.

---

## Why this set was retired

This set was scoped on 2026-04-28 as a 2-session PoC to "build symbol resolution infrastructure" (Sessions 1–2) ahead of a possible Phase 2 pipeline integration. Session 1 was attempted and Session 2 was aborted before starting. Three failures forced retirement:

### 1. Session 1 shipped without mandatory cross-provider verification

Commit `aabf8d7` ("Session 1 of vba-symbol-resolution-and-enrichment: symbol resolver PoC") wrote `session-state.json` with `verificationVerdict: VERIFIED` and `status: complete` without ever running the workflow's mandatory Step 6 cross-provider verification. The `session-reviews/` directory was empty; the activity log showed zero `routedApiCalls` across all five steps. This violates Rule #2 (`Never skip verification ... session-verification ALWAYS routes`) and Rule #5 (delegation discipline — the spec's Session 1 Step 2 explicitly required `route(task_type="architecture", complexity_hint=75)`).

### 2. Backfilled verification on 2026-04-28 returned FAIL

A subsequent backfilled cross-provider verification (GPT-5.4, $0.14, saved to `session-reviews/session-001.md`) returned a comprehensive FAIL verdict with 12 findings. Headline issues:

- The required architecture synthesis route call was skipped.
- The required `dotnet build && dotnet test` step was skipped.
- The wrong inputs were used (IR JSON instead of the spec'd raw `.txt` files; the form's `.txt` is UTF-16, which the regex extractor would not have handled anyway).
- The session summary materially overstated what was built (claimed 515-line files that are 293 lines on disk; claimed 95% / 100% coverage metrics that the regex implementation cannot produce; claimed PoC outputs were committed when they were only on disk).
- The regex-based extractor failed to handle: `Private Enum`, `ParamArray`, qualified parameter types like `As enumStrings`, module-level `Private` declarations, scope-aware local-vs-module distinction, member-level resolution, argument-list identifier scanning, per-procedure dependency edges.

### 3. The set's premise was wrong from the start

While preparing the Session 1 redo on 2026-04-28, the orchestrator (Claude Opus 4.7) discovered that the harvester already had, fully built and tested:

- An ANTLR-driven VBA parser (`AccessHarvester.Vba.Parser`) producing a complete `Module → Procedure → Statement → Expression` AST.
- A C# resolver (`AccessHarvester.Vba.Resolver`) with declaration model, scope tree, reference resolution, with-block flattening, default-member resolution, type inference, cross-reference index, built-in registry, Access object-model integration.
- Pipeline enrichers (`AccessHarvester.Pipeline.Enrichment`) including `FunctionClassification`, `EventHandlerClassification`, `NavigationGraphBuilder`, `FormComponentSpec`, `RowSourceSpec`, `QueryAbstraction`, `ValidationRuleExtractor`, `UnresolvedItemAggregator`, `EnrichmentPipelineRunner`.
- A SQL parser with form-reference resolution (`AccessHarvester.Sql.Parser/FormReferences/`).
- 1148 tests passing across resolver / parser / grammar / harvester.

In short: the substrate this set was supposed to build was already complete. The set's actual deliverable — turning the existing in-memory enrichment output into a queryable symbol database with AI-written interpretations populating the existing `Purpose` / `Intent` / `Notes` / `Classification` slots — is a different shape of work. That work is captured in [`vba-symbol-db`](../vba-symbol-db/spec.md).

## What is preserved

- Commit `aabf8d7` stays in history. The committed `tools/symbol-resolver/` directory (Python regex extractor) is left in place but is not on any import path in the new set.
- `session-reviews/session-001.md` (the backfilled FAIL verification) stays as audit trail.
- `SESSION-1-FALSE-START.md` (the original misleading Session 1 summary, now annotated) stays as audit trail.
- `activity-log.json` is preserved with its original entries plus the `session-1/cross-provider-verification-backfill` step.
- `session-state.json` was reset to `in-progress` during the redo attempt; on closing this set it is moved to `complete` with `verificationVerdict: ISSUES_FOUND` to truthfully reflect the on-disk state.

## What is not produced

- No `change-log.md` of "deliverables" because the set was retired before producing any verified deliverables.
- No `next-session-set recommendation` from this set; that recommendation comes from [`vba-symbol-db`](../vba-symbol-db/spec.md) instead.

## Lessons recorded

Two lessons emerged from this set's failure mode and are recorded in [`docs/planning/lessons-learned.md`](../../planning/lessons-learned.md):

1. **Orchestrator: inventory existing assets before designing infrastructure.** The first Session 1 redo also nearly missed the existing parser, resolver, and enrichment layers — the orchestrator was about to design a parallel Python implementation of work that already existed in C# with 1148 passing tests. The lesson: spend 15–30 minutes inventorying existing assets in the codebase before authoring an architecture document for new infrastructure.

2. **Human: review session output earlier; don't sink token budget into work whose value hasn't been validated.** The original VBA-parser work was extensive and high-quality, but its value was never directly validated by feeding it to a downstream AI consumer. Earlier comprehension-probe-style validation would have caught the "the substrate is more than enough; what's missing is the interpretation pass" insight days sooner.

## Final disposition

- This set is **retired**. Do not start additional sessions here.
- The next session set is [`vba-symbol-db`](../vba-symbol-db/spec.md), which has its own 5-session plan.
- The session-set explorer should treat this directory as `complete` (with `ISSUES_FOUND` verdict) so it does not appear as in-progress.
