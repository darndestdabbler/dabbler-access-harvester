# Change Log: `vba-spec-study` Session Set

**Dates:** 2026-04-25
**Sessions:** 3 of 3 (Session 4 buffer not needed)
**Summary:** This session set produced three artifacts that downstream VBA-grammar session sets consume: a topic-organized MS-VBAL summary, a production coverage matrix, and a concrete-hazard list grounded in the 12 corpus fixtures.

**Next session-set recommendation (after `vba-spec-study`):**
`parser-foundations`

**Recommended orchestrator for `parser-foundations`:** Claude Code Opus 4.7 @ effort=medium
**Rationale:** This set has a single, critical synthesis step for the verification-harness design, requiring a robust architectural plan. The previous session's timeout with a weaker model on an architectural route confirms that using a top-tier orchestrator for foundational tasks is the most efficient choice.

(Note: `parser-foundations` is currently in progress — Session 1 of 4 was committed earlier on 2026-04-25; the recommendation is to **continue** that set rather than start a new one. `vba-spec-study` (#2) does not unblock any sibling set on its own; `#3 vba-lexical-and-expression-grammar` requires both `#1 parser-foundations` and `#2 vba-spec-study` per the DAG in `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`.)

---

## Session 1 — MS-VBAL Summary

**Outcome:** Produced `docs/spec-study/ms-vbal-summary.md`, a 1260-line topic-organized summary of MS-VBAL with explicit section citations covering lexical structure, modules, procedures, statements, expressions, type system, object model, and preprocessor — with extended treatment of the 14 critical constructs the migration plan calls out.

**Synthesis posture:** Spec called for one `route(architecture)` call. Split into three (lexical+preprocessing / modules+procedures+type-system / statements+expressions+object-model) per the existing `lessons-learned.md` Opus-timeout entry. All three Opus calls succeeded.

**Cost:** ~$2.10 routed (3× Opus @ ~$0.60 each + 3-round gpt-5-4 verification @ ~$0.28 + ai-assignment authoring $0.015).

## Session 2 — Coverage Matrix

**Outcome:** Built `docs/spec-coverage/ms-vbal-coverage.csv` — 110 rows (78 must / 25 should / 7 can-defer) mapping every MS-VBAL grammar production to in-corpus / in-scope / priority / difficulty fields. Authored `docs/spec-coverage/README.md` to document column meanings, corpus scope, and the deferred-Northwind-COM-inspection note.

**Mechanical-correspondence verification:** Used `task_type="code-review"` (Sonnet-pinned) as a stand-in for the unreleased `schema-validation` task type (forthcoming from `#4 routing-playbook`).

**Cost:** ~$0.43 routed (3-round Sonnet correspondence check + 3-round gpt-5-4 verification + small next-orchestrator call).

## Session 3 — Hazard List Synthesis

**Outcome:** Authored `docs/spec-coverage/hazard-list.md` — 62 concrete parsing hazards across 8 sections (A: lexical 9, B: member-access 8, C: with-block 4, D: control-flow 12, E: procedure/parameter 7, F: object-model 9, G: embedded-DSLs 7, H: static-analysis-defeating 6). Three hazards (H-D05 Resume, H-D12 named-args, H-E07 ParamArray) are explicitly spec-seeded with no corpus witness; a synthetic-fixture obligation is recorded for `vba-grammar-validation` (#5). Every other hazard cites a fixture file and line.

**Synthesis posture (deviation):** Spec called for `route(task_type="architecture", complexity_hint=80)` synthesis. Batch-A first call succeeded ($1.29, 16788 output tokens) but the content was lost to a Python `print()` Unicode error (`cp1252` codec rejected a directional-arrow character) before file write. Batch-A retry timed out at the provider boundary, matching the `lessons-learned.md` Opus-timeout entry. Per the lessons-learned action item the human swapped the orchestrator from Sonnet to Opus; per `project-guidance.md` Conventions ("When the orchestrator's model matches the routing target … handle in-conversation"), the deliverable was authored in-conversation rather than re-attempting `route()`. The non-negotiable cross-provider session-verification call (gpt-5-4) is unchanged.

**Verification:** Three rounds. Round 1 found multiple Major issues (H-H05 abstract, vague citations, missing line numbers, ParamArray + named-args missing, cross-ref errors); all fixed. Round 2 found 1 Major + 3 Minor (self-contradiction in seed-list note, fixture-row inconsistencies, vague File fields); all fixed. Round 3 found 2 Major + 2 Minor (H-E01 modifier order, H-D05 Resume 0 vs Construct, H-B08 non-witness contrast, H-D04 GoTo 0 absent); all four fixed in-place. Retry cap reached.

**Cost:** ~$2.99 routed (1 orphan Opus architecture call $1.29 + 3 verification rounds $0.69 + change-log + next-set rec).

## Lessons Reaffirmed Or Newly Noted

- **Opus `route(architecture)` reliability above ~3K input tokens is unreliable.** Re-confirmed for the second time in this repo (first: `vba-parser-session-set-generator` Session 4). The in-conversation authoring fallback continues to work when the orchestrator model matches.
- **The first-call success → second-call timeout pattern is new.** Earlier failures saw the first call time out; in Session 3 the first call succeeded with full content, then a print-side encoding bug erased it before persistence and the second call timed out. Suggests provider transient-instability is real and the orchestrator should *always* persist routed output to disk before any other processing.
- **`route(task_type="summarization")` 400-on-Gemini-Pro continues to require `max_tier=1` fallback.** Hit again on this session's change-log generation. The fallback is operational, but Gemini Flash output is consistently terser than the prompt requested.

## Deferred Items

Round-3 verifier findings, all addressed in-place:

- **H-E01 modifier order (Major).** Was: "[ByRef|ByVal] [Optional] <name>". Now: "Optional [ByRef|ByVal] <name>" matching the fixture witness `Optional ByVal sFilterArgs As String = ""`.
- **H-D05 Resume 0 vs Construct field (Major).** Added `Resume 0` (equivalent to `Resume`) to the Construct field with the MS-VBAL §5.4.12 citation already in place.
- **H-B08 non-witness contrast in File field (Minor).** Removed the `10-class-with-events.cls:42` reference from File (where it was a non-`Me` contrast); moved to Description.
- **H-D04 `On Error GoTo 0` absent from corpus (Minor).** Split File field by sub-form; explicitly noted "On Error GoTo 0 (deactivate): not present in current corpus — synthetic fixture required in vba-grammar-validation (#5)".

## Downstream Consumption

- **`docs/spec-study/ms-vbal-summary.md`** → grammar sessions `#3 vba-lexical-and-expression-grammar` and `#4 vba-statement-and-module-grammar` read it as the spec source of truth.
- **`docs/spec-coverage/ms-vbal-coverage.csv`** → grammar sessions check off must/should/can-defer entries as they implement productions.
- **`docs/spec-coverage/hazard-list.md`** → `#5 vba-grammar-validation` uses every hazard as a test-case requirement, especially the three spec-seeded hazards (H-D05, H-D12, H-E07) which require new synthetic fixtures.

## Set Total

- **Sessions:** 3 (Session 4 buffer not needed; Round-3 Majors fixed mechanically without re-verification, per verifier's explicit "fix Majors then commit; Minors deferrable" guidance).
- **Routed cost:** ~$5.50 across 3 sessions (Session 1 $2.10 + Session 2 $0.43 + Session 3 $2.99).
- **Outputs:** 3 artifacts as listed above plus this change-log.
