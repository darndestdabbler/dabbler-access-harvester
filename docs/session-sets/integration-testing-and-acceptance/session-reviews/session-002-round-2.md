## Analysis

The spec amendment is largely coherent with the shipped artifacts.

- The reframe in `spec.md` is honest: it clearly says Session 2 only scaffolded Layer C, explicitly defers `scenarios.md`, drafted question banks, and the first green baseline to Session 3, and does not pretend the bench is runnable end-to-end yet.
- The Python route usage is correct where it matters:
  - `draft_questions.py` uses `route(task_type="analysis")` for question drafting.
  - `run.py` uses `route(task_type="analysis")` for both the answer leg and the judge leg.
- The persist-before-print lesson is followed in `draft_questions.py`: raw routed output is written before summary output. `run.py` also writes per-question JSONL before printing its final summary.
- The xUnit gating is mostly correct:
  - opt-in env flag gate: correct
  - Windows / Access COM gate: correct
  - sample DB existence gate: correct
  - curated `questions.yaml` existence gate: correct

The remaining problems are in edge-case correctness and operational robustness rather than in the overall design.

### Issue → Location → Fix

**Issue:** `run.py` does not validate the question-bank schema or `supporting_ir_paths` before spending routed calls. A malformed YAML entry can silently degrade into nonsense input instead of a clean exit-2 misconfiguration. Examples: a scalar string in `accepted_answers` or `supporting_ir_paths` becomes a per-character list via `list(...)`, and missing IR files are injected as `[FILE NOT FOUND ...]` text instead of failing fast. That conflates bench misconfiguration with model-performance failure.  
**Location:** `tools/comprehension-bench/run.py` (`main`, `load_ir_snippets`)  
**Fix:** Add upfront validation before any routing:
- required keys: `id`, `question`, `accepted_answers`, `supporting_ir_paths`, `difficulty`
- `accepted_answers` and `supporting_ir_paths` must be non-empty `list[str]`
- `difficulty` must be one of `low|medium|high`
- IDs should be unique
- every `supporting_ir_path` must exist on disk under `--ir-root`
On any violation, print a clear error and return exit code `2`.

**Issue:** The xUnit wrapper deletes the temp output directory unconditionally in `finally`, even when the bench fails. The assertion message tells the user to inspect `scores.jsonl`, but that file is deleted immediately on failure, which destroys the main diagnostic artifact for an expensive opt-in run.  
**Location:** `tests/AccessHarvester.Comprehension.Tests/ComprehensionBenchTests.cs` (`finally` cleanup of `outRoot`)  
**Fix:** Preserve `outRoot` on any failure or exception. Only delete it on full success, or gate cleanup behind an explicit env var. Also print the preserved temp path to test output when a failure occurs.

**Issue:** `draft_questions.py` still has the same outside-repo path bug that was fixed in `run.py`: if `--out` points outside `REPO_ROOT`, the script writes the files and then crashes while building the summary JSON because it calls `relative_to(REPO_ROOT)` without handling `ValueError`.  
**Location:** `tools/comprehension-bench/draft_questions.py`  
**Fix:** Wrap both `out_path.relative_to(REPO_ROOT)` and `raw_path.relative_to(REPO_ROOT)` in `try/except ValueError` and fall back to absolute-path strings.

**Issue:** The spec honestly moved question drafting to Session 3, but the drafting artifacts still label it as Session 2. That leaves artifact docs out of sync with the reframe and will mis-group router metrics by default.  
**Location:** `tools/comprehension-bench/draft_questions.py`, `tools/comprehension-bench/README.md`  
**Fix:** Change the default `--session-number` in `draft_questions.py` from `2` to `3`, and update README/doc wording from “Session-2 helper” to Session 3 or session-agnostic phrasing.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "tools/comprehension-bench/run.py",
      "summary": "The runner does not validate question-bank entry types or supporting_ir_paths up front, so malformed YAML and missing IR files are treated as bench data instead of exit-2 misconfiguration.",
      "fix": "Add pre-routing schema validation for required fields, list[string] types, non-empty accepted_answers/supporting_ir_paths, allowed difficulty values, unique IDs, and on-disk existence of every supporting_ir_path; return exit code 2 on any violation."
    },
    {
      "severity": "Major",
      "location": "tests/AccessHarvester.Comprehension.Tests/ComprehensionBenchTests.cs",
      "summary": "The test always deletes the temp output directory in finally, which destroys scores.jsonl even when the bench fails and the assertion tells the user to inspect it.",
      "fix": "Preserve the temp output directory on any failure or exception, and only clean it up after a fully successful run (or behind an explicit cleanup flag)."
    },
    {
      "severity": "Minor",
      "location": "tools/comprehension-bench/draft_questions.py",
      "summary": "The script still crashes when --out is outside REPO_ROOT because summary generation uses relative_to(REPO_ROOT) without handling ValueError.",
      "fix": "Wrap relative_to(REPO_ROOT) calls in try/except ValueError and fall back to absolute paths in the printed summary."
    },
    {
      "severity": "Minor",
      "location": "tools/comprehension-bench/draft_questions.py, tools/comprehension-bench/README.md",
      "summary": "Drafting is still labeled as Session 2 in the script defaults and README even though the spec moved that work to Session 3.",
      "fix": "Update the default session number to 3 and revise the README/doc wording so drafting is described as a Session 3 step or as session-agnostic tooling."
    }
  ]
}
```