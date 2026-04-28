## 1. Recommendation

**Engine:** Claude Code (claude-code) · **Model:** Claude Opus 4.7 · **Effort:** `high`

## 2. Rationale

Session 3 is the most judgment-intensive session in the set — threshold calibration against real bench results requires the orchestrator to interpret score distributions, decide whether rubric adjustments are warranted vs. masking real IR gaps, and document rationale inline, all of which reward Opus-class reasoning at high effort. The plan is well-elaborated (8 explicit steps, clear progress keys), so the high-effort cost lands on judgment calls rather than plan discovery, which is the right trade-off for a session whose baseline JSONL becomes the v1 acceptance evidence.

## 3. Estimated routed cost

**Moderate-to-high (~$4–$8 total).**

Breakdown:
- Question drafting: 2 `route(task_type="analysis")` calls (one per DB, large context) — ~$0.80–$1.50
- Bench run answer+judge: ~40 questions × 2 calls each ≈ 80 `route(task_type="analysis")` calls (small-to-medium context each) — ~$2.50–$5.00
- Verification: 1–2 rounds expected (Session 2's scaffolding already passed the gauntlet; Session 3 is exercising it, not building it) — ~$0.20–$0.50
- Miscellaneous (YAML validation, optional synthetic fixture) — ~$0.10–$0.30

This is 3–6× Session 2's routed cost, driven almost entirely by the bench run's per-question answer+judge loop. Not reducible without cutting questions.

## 4. Notes

| Decision point | Recommendation |
|---|---|
| **Question-drafting `task_type`** | `analysis` as spec states — not `documentation`. The spec already calls this out explicitly; just confirm the orchestrator doesn't deviate. |
| **Bench answer+judge `complexity_hint`** | Omit or leave at default for the answer leg (the bench runner controls this). For the judge leg, the `run.py` prompt should request a numeric score + one-sentence justification — keep the judge prompt short to minimize per-call cost across 80 calls. |
| **Verification `complexity_hint`** | Start at `70` (spec default). Session 2 needed 4 rounds because it was building new infrastructure; Session 3 is mostly running existing scripts + committing artifacts, so expect fewer rounds. If round 1 returns VERIFIED, accept it — don't escalate complexity_hint looking for problems that aren't there. |
| **Curation handoff** | The human-in-the-loop curation pause (step 3) is a hard stop. The orchestrator should commit `questions.draft.yaml` files and issue a clear handoff message *before* the user curates, exactly as Session 2 did for the scenarios handoff. Do not attempt to auto-curate or skip the pause. |
| **Threshold calibration** | If the first bench run shows a difficulty tier consistently failing (e.g., all `high` questions score below 0.40), the orchestrator should inspect the failing questions' `supporting_ir_paths` for IR completeness *before* relaxing the rubric. A missing IR path is a bench-config bug, not a rubric problem. |
| **Change-log / summarization** | No `summarization` routing in Session 3 — that's Session 5 only. Flag this because Session 3's expanded scope might tempt the orchestrator to summarize early. |