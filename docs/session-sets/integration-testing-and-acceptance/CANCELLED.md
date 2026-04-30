# CANCELLED — `integration-testing-and-acceptance`

> **Cancelled:** 2026-04-30
> **Cancelled at:** Session 3 of 5 (Sessions 1-2 closed VERIFIED;
> Sessions 3-5 not run)
> **Reason for cancellation:** Strategic supersession.

## What happened

This set was authored 2026-04-25 to deliver Phase 8.1-8.5 of the
harvester plan: integration tests, regression-suite expansion, perf
benchmarks, corpus expansion, and an acceptance-criteria checkoff.
Mid-set (2026-04-28) it was reframed to substitute an
**AI-comprehension test bench (Layer C)** for the perf-benchmark
work, on the theory that comprehension correctness was a more direct
measure of the harvester's actual purpose than perf criteria.

Sessions 1-2 ran cleanly:

- **Session 1 (VERIFIED):** End-to-end integration test project
  shipped at `tests/AccessHarvester.Integration/`. **Real value;
  remains in the codebase.**
- **Session 2 (VERIFIED after 4 rounds):** Layer C scaffolding
  shipped at `tools/comprehension-bench/` (Python: `draft_questions.py`,
  `run.py`, `scoring.py`) and
  `tests/AccessHarvester.Comprehension.Tests/` (xUnit, gated on
  `HARVESTER_COMPREHENSION_BENCH=1`, dormant by default). **Inert
  scaffolding; remains in the codebase but does not run by default.**

Session 3 was supposed to author `docs/test-cases/comprehension/scenarios.md`
by hand, then curate ~20-40 routed-draft questions per gold-standard
DB, then calibrate the rubric thresholds. The human paused at this
point, observing that Session 3 was a large amount of manual work for
unclear downstream value.

That pause led — over the following two days — to the strategic
inflections captured in [`docs/planning/strategy.md`](../../planning/strategy.md):
the post-`vba-symbol-db` PIVOT (routed cross-provider rigor was
uncalibrated; Pearson +0.004 between AI self-rated confidence and
cross-provider agreement), the `vba-symbol-db-path-b-probe` Hybrid
verdict, and the manager-level reframing that produced the
**workflow-package + characterization-tests** strategy.

Layer C's premise — an AI rates IR-comprehension answers 0.0-1.0
against accepted answers and a graduated rubric calls pass/fail —
is structurally the same self-rated-rigor pattern that the PIVOT
diagnosed as uncalibrated. Characterization tests
([`tests/AccessHarvester.CharacterizationTests/`](../../../tests/AccessHarvester.CharacterizationTests/))
deliver the same epistemic outcome (does the harvester output
support correct downstream understanding?) as falsifiable boolean
assertions instead of opinion-of-opinions scores. The Add-an-Order
Blazor pilot
([`docs/session-sets/blazor-add-an-order/`](../blazor-add-an-order/))
empirically demonstrated this pattern works end-to-end (111
characterization tests pass unmodified through a complete Blazor
implementation).

## What survives

- **`tests/AccessHarvester.Integration/`** — the Session 1
  deliverable. Active, contributing, kept.
- **This set's `coverage-audit.md` and `coverage-audit-routed.md`** —
  one-time test-coverage inventory as of 2026-04-28. Useful as a
  historical snapshot; not actionable forward.

## What's discarded

- **`tools/comprehension-bench/`** — Session 2's Python scaffolding
  (`draft_questions.py`, `run.py`, `scoring.py`, `README.md`).
  **Removed in the same commit that recorded this cancellation.**
  Rationale: the scaffolding is structurally adjacent to the
  uncalibrated-rigor pattern that the strategy walked away from.
  Leaving it in the repo dormant would suggest a viable path the
  strategy explicitly rejects. If a future session ever needs an
  AI-comprehension oracle as a debug aid, recreate from scratch
  with awareness of the post-`vba-symbol-db` PIVOT lesson.
- **`tests/AccessHarvester.Comprehension.Tests/`** — Session 2's
  xUnit wrapper. **Removed in the same commit, including its
  `AccessHarvester.sln` registration.**
- The unrun Sessions 3-5 plans for scenarios authoring, question
  curation, rubric calibration, acceptance-criteria checkoff, and
  v1 release change-log. Each is replaced by something in the new
  strategy (workflow packages with confidence-tagged rules and
  open-questions logs replace the question-and-rubric framing;
  the per-set change-log convention replaces the v1 monolithic
  change-log).
- The recommended follow-up set
  `performance-benchmarks-and-baseline`. Not currently planned;
  may resurface if performance becomes a real-world friction
  point.
- The corpus expansion (5-10 additional `.accdb` files). The
  `workflow-package-pilot` Phase 1 confirmed Northwind alone is
  sufficient for proving the package shape; generalization to
  `MiniComplex.accdb` and `HomeHealthCare.accdb` is now Phase 3c
  of the strategy, on a workflow-package basis rather than a
  bulk-corpus basis.

## Pointer to the superseding work

For the strategic reasoning that produced the cancellation:

- [`docs/planning/strategy.md`](../../planning/strategy.md) — the
  current strategic frame.
- [`docs/session-sets/vba-symbol-db/verdict.md`](../vba-symbol-db/verdict.md)
  — PIVOT verdict that diagnosed the uncalibrated-rigor pattern.
- [`docs/session-sets/vba-symbol-db-path-b-probe/decision.md`](../vba-symbol-db-path-b-probe/decision.md)
  — Hybrid verdict that pointed at characterization tests as the
  better falsification mechanism.
- [`docs/session-sets/workflow-package-pilot/`](../workflow-package-pilot/)
  — empirical proof on four NorthwindDevEdition workflows.
- [`docs/session-sets/blazor-add-an-order/`](../blazor-add-an-order/)
  — empirical proof of downstream consumption (111 tests pass
  unmodified).

## A note for the next strategic-review session

If a future planning session considers reviving an
AI-comprehension-bench-style approach (Layer C or its
descendants), the cancellation rationale above is the operative
counter-argument. The pattern is structurally adjacent to the
post-`vba-symbol-db` PIVOT failure mode; before reinvesting,
that adjacency should be addressed explicitly.
