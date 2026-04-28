## 1. Spec amendment ↔ built artifact coherence

The reframe is internally consistent and reads honest.

- `spec.md` clearly separates the **original tree-row scope** from the **2026-04-28 mid-set reframe**, explicitly descopes corpus expansion and perf benchmarking, and moves scenarios capture/question drafting/baseline creation to Session 3.
- The actual Session 2 artifact set matches that boundary exactly:
  - `tools/comprehension-bench/` contains only scaffolding (`README.md`, `draft_questions.py`, `run.py`, `scoring.py`).
  - `tests/AccessHarvester.Comprehension.Tests/` contains only the opt-in wrapper.
  - No in-scope file falsely claims that `scenarios.md`, curated `questions.yaml`, or `comprehension-baseline.jsonl` already exist.
- `README.md` reinforces the same story by describing `draft_questions.py` as a **Session-agnostic helper used in Session 3** and `run.py` as the curated-bank runner.

## 2. Python scaffolding correctness

### `draft_questions.py`

- Uses `route(..., task_type="analysis")`, which matches the lessons-learned requirement and the spec’s routing notes.
- Requires `docs/test-cases/comprehension/scenarios.md` and exits `2` with a clear message if missing, which matches the documented Session 2/3 handoff.
- Correctly follows the persist-before-print rule:
  - routed output is written to `questions.draft.raw.txt`
  - then the drafted YAML is written
  - only after that is the JSON summary printed
- The emitted YAML header loudly marks the output as auto-drafted and uncurated, which matches the provenance/human-curation expectations in the spec.

### `run.py`

- Uses `route(..., task_type="analysis")` for both:
  - the answer leg
  - the judge leg
- The answer/judge prompting shape is coherent with the bench design:
  - answer prompt is constrained to the provided IR snippets
  - judge prompt compares `actual` against `accepted_answers` and requests a machine-parseable trailing `score: <float>`
- The misconfiguration handling added in Round 3 is correct:
  - `load_questions()` failures are converted to concise stderr + exit `2`
  - no traceback leakage for YAML parse / missing-pyyaml / top-level-shape / file I/O problems
- `validate_questions()` is now materially robust:
  - required-field checks
  - list typing checks
  - allowed difficulty enforcement
  - duplicate ID detection
  - absolute/drive-rooted path rejection
  - traversal escape rejection after `resolve()`
  - `is_file()` requirement for each supporting IR path
- The runner preserves outputs before summary display:
  - each per-question record is written to JSONL immediately
  - final console output is only the end summary

### `scoring.py`

- Thresholds match the spec and README exactly:
  - low `>= 0.85`
  - medium `>= 0.65`
  - high `>= 0.40`
- Empty score sets fail closed instead of vacuously passing.
- Tier means are computed only for populated tiers, which is consistent with the module docstring and avoids treating absent tiers as zero.

## 3. xUnit gating correctness

`ComprehensionBenchTests.cs` now gates in the right order and with the right semantics.

- Bench is opt-in via `HARVESTER_COMPREHENSION_BENCH=1`.
- Non-Windows hosts skip before doing any work.
- Access COM availability is checked before pipeline prep.
- Per-DB fixture checks (`samples/<DB>.accdb` and curated `questions.yaml`) are performed **before** temp-dir creation, fixing the earlier skip/leak issue.
- Failure preservation logic is now correct:
  - `failed` is set on actual failures/exceptions
  - successful runs clean up by default
  - failures preserve `outRoot` and print the preserved path for diagnosis
  - skips no longer produce failure-style preservation output

The wrapper also stays honest about the current Session 2 state: because curated `questions.yaml` is deferred, an opted-in run would skip on missing curated banks rather than pretending the bench is runnable already.

## 4. Honesty about done vs deferred

The spec amendment is honest and specific.

- It does not pretend the original tree-row outputs were delivered.
- It explicitly records the reframe and why it happened.
- It explicitly states that Session 2 stops at **scenarios input handoff**.
- It explicitly defers:
  - `docs/test-cases/comprehension/scenarios.md`
  - drafted/curated question banks
  - `comprehension-baseline.jsonl`

The codebase matches that honesty: the tooling exists, but the actual bench content is still intentionally absent.

## 5. Lessons-learned compliance

The implementation matches the requested workflow lessons.

- Question drafting uses `task_type="analysis"` rather than `documentation`.
- Bench answer and judge both use `task_type="analysis"`.
- Persist-before-print is correctly followed in `draft_questions.py`.
- `run.py` persists per-question routed outputs to JSONL before any end-of-run console summary.
- Nothing in the reviewed artifacts contradicts the trailing-JSON-block or send-related-files workflow conventions described in the spec/process text.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```