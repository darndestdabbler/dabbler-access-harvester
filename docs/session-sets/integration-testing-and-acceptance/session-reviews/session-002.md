Core alignment is good. The Session 2 artifacts match the reframed Layer C shape: `draft_questions.py`, `run.py`, `scoring.py`, and the opt-in xUnit wrapper all correspond to the amended plan. The routed calls are correctly using `task_type="analysis"` in the draft, answer, and judge paths. `draft_questions.py` follows the persist-before-print lesson correctly by writing the raw routed response to disk before any console output. `run.py` also writes per-question records before printing its final summary. The xUnit gating is correct on the main checks you called out: env flag, Windows/Access COM availability, sample DB existence, and curated `questions.yaml` existence.

Issue → `run.py` crashes in the intended xUnit flow when `--out` is an absolute temp path outside the repo.  
Location → `tools/comprehension-bench/run.py` (`summary["scores_path"]` construction).  
Fix → Guard `args.out.relative_to(REPO_ROOT)` with `try/except ValueError` and fall back to `str(args.out)`, or only relativize when `args.out.is_relative_to(REPO_ROOT)`.

Issue → The bench can falsely pass with an empty question bank. `compute_pass([])` returns `overall_passed=True`, and `run.py` does not reject zero loaded questions.  
Location → `tools/comprehension-bench/run.py`, `tools/comprehension-bench/scoring.py`.  
Fix → After loading/applying `--limit`, fail fast with exit code 2 if `questions` is empty; optionally also reject runs that produce zero scored records.

Issue → The README workflow example is not runnable as written: it omits the required `--questions` argument and passes `--ir-root <outRoot>` instead of the DB-specific IR root the runner actually expects.  
Location → `tools/comprehension-bench/README.md` (`Workflow` block).  
Fix → Update the example to use the real invocation shape, e.g. `--ir-root <outRoot>/AccessMetaData/<DB> --questions docs/test-cases/comprehension/<DB>/questions.yaml`, and align the harvester CLI flags in the example with the actual CLI syntax.

Issue → The spec amendment is mostly honest, but a few stale references still contradict the mid-session deferral. In particular, some text still says Session 2 ends with drafted banks ready for curation and still describes descoped perf/corpus work as active set scope.  
Location → `docs/session-sets/integration-testing-and-acceptance/spec.md` (`Project Overview`, `Routing notes` for question drafting, and `Open questions for the human`).  
Fix → Update those sections to consistently reflect the reframe: Session 2 ends with a scenarios-input handoff, question drafting moves to Session 3, and corpus expansion/perf are descoped/deferred from this set.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "tools/comprehension-bench/run.py",
      "summary": "The runner raises when summarizing scores if --out is an absolute path outside the repo, which is exactly how the xUnit wrapper invokes it.",
      "fix": "Wrap args.out.relative_to(REPO_ROOT) in try/except ValueError and fall back to the absolute path, or only relativize when the path is under REPO_ROOT."
    },
    {
      "severity": "Major",
      "location": "tools/comprehension-bench/run.py / tools/comprehension-bench/scoring.py",
      "summary": "An empty questions.yaml currently yields a passing bench because zero scored questions produce overall_passed=true.",
      "fix": "Fail fast with a non-zero exit when no questions are loaded or no scores are produced."
    },
    {
      "severity": "Major",
      "location": "tools/comprehension-bench/README.md",
      "summary": "The workflow example is incorrect because it omits --questions and uses the wrong --ir-root shape for the runner.",
      "fix": "Update the README example to pass the DB-specific IR root and the required questions.yaml path, and align the CLI example with the actual AccessHarvester command syntax."
    },
    {
      "severity": "Minor",
      "location": "docs/session-sets/integration-testing-and-acceptance/spec.md",
      "summary": "A few stale statements still contradict the documented mid-session deferral by implying Session 2 ends with drafted banks and by restating descoped perf/corpus work as active scope.",
      "fix": "Revise the Project Overview, Routing notes, and Open questions sections so they consistently state that Session 2 ends with the scenarios-input handoff and Session 3 absorbs drafting/curation."
    }
  ]
}
```