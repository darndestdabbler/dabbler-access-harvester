## Analysis

Spec/artifact coherence is strong.

- The spec amendment is honest about the reframe and the mid-session deferral. Session 2 now claims only scaffolding, xUnit wiring, and pipeline smoke coverage; it does **not** pretend that `scenarios.md`, drafted banks, curated `questions.yaml`, or `comprehension-baseline.jsonl` were delivered.
- `tools/comprehension-bench/README.md` matches that story: `draft_questions.py` is described as Session-agnostic tooling used in Session 3, while `run.py` assumes curated `questions.yaml`.
- The routing choices are correct. `draft_questions.py`, the answer leg in `run.py`, and the judge leg in `run.py` all use `task_type="analysis"`, which matches the lessons-learned requirement.
- The persist-before-print requirement is satisfied:
  - `draft_questions.py` writes the raw routed output to disk before any summary print.
  - `run.py` writes per-question records containing `answer_text` and `judge_text` to JSONL before printing the final summary.
- xUnit gating is mostly correct:
  - skipped by default unless `HARVESTER_COMPREHENSION_BENCH=1`
  - skips on non-Windows
  - skips when Access COM is unavailable
  - skips when required sample DBs or curated `questions.yaml` files are absent

Remaining findings are implementation edge cases rather than spec-honesty or route-selection problems.

### Issue → `supporting_ir_paths` validation does not enforce “relative IR file under --ir-root”
**Location →** `tools/comprehension-bench/run.py` (`validate_questions`, `load_ir_snippets`)  
**Fix →** Reject absolute paths, path traversal, and non-files up front. Resolve each candidate against `ir_root`, verify it stays under `ir_root`, and require `is_file()` before accepting it. Optionally require a `.ir.json` suffix as part of the schema contract.

Why this matters:
- The spec and README say `supporting_ir_paths` are relative to the DB IR root.
- Current validation only checks `exists()`.
- A question bank entry like an absolute path, `../...` traversal, or an existing directory will pass validation today.
- That can make the bench read non-IR content or crash later at `read_text()`.

### Issue → Question-file load failures still crash with traceback and wrong exit semantics
**Location →** `tools/comprehension-bench/run.py` (`load_questions`, `main`)  
**Fix →** Wrap `load_questions(args.questions)` in `try/except` and convert YAML parse errors, top-level-shape errors, missing `pyyaml`, and file I/O problems into a concise stderr message plus exit code `2`.

Why this matters:
- Session docs and README position malformed/misconfigured question banks as setup errors.
- Today, invalid YAML, a non-list top level, or missing `pyyaml` raises uncaught exceptions and exits with a traceback instead of a clean misconfiguration result.
- That breaks the intended “fail fast, exit 2 on misconfiguration” behavior.

### Issue → Skip paths after temp-dir creation are treated like failures for cleanup/diagnostic preservation
**Location →** `tests/AccessHarvester.Comprehension.Tests/ComprehensionBenchTests.cs` (`Bench_GraduatedRubricPasses_AcrossGoldStandardDbs`)  
**Fix →** Either create `outRoot` only after all skip-only preconditions are satisfied, or track a separate “actual failure happened” flag so the `finally` block preserves/logs `outRoot` only for real failures, not skips.

Why this matters:
- If `HARVESTER_COMPREHENSION_BENCH=1` is set before Session 3 and `questions.yaml` is still absent, the test skips as intended.
- But because `outRoot` was already created and `succeeded` stays false, the `finally` block preserves that temp directory and prints a failure-style diagnostic message.
- That is misleading and leaks temp directories on skipped runs.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "tools/comprehension-bench/run.py",
      "summary": "Question-bank validation accepts absolute, traversing, and non-file supporting_ir_paths instead of enforcing IR files relative to --ir-root.",
      "fix": "In validate_questions(), resolve each path against ir_root, reject absolute or escaping paths, require the resolved path to remain under ir_root, and require is_file() before accepting it; then treat any violation as exit 2 misconfiguration."
    },
    {
      "severity": "Major",
      "location": "tools/comprehension-bench/run.py",
      "summary": "Malformed YAML, non-list top-level content, or missing pyyaml still crash the runner with a traceback instead of returning exit code 2 for misconfiguration.",
      "fix": "Wrap load_questions() in try/except for YAML parse errors, ValueError, RuntimeError, and OSError, print a concise stderr message naming the question file, and return 2."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Comprehension.Tests/ComprehensionBenchTests.cs",
      "summary": "A skip after outRoot creation is treated like a failure for cleanup/logging, so skipped runs can preserve temp directories and print misleading diagnostics.",
      "fix": "Delay outRoot creation until after skip-only preconditions pass, or track a separate failure flag so preservation/logging only happens for real failures, not skips."
    }
  ]
}
```