# comprehension-bench

AI-comprehension test bench for the harvester (Layer C of the test pyramid).

## Purpose

The harvester exists so downstream AI can reliably interpret raw VBA, JET-SQL,
and Access expressions. Layer A (`SyntheticFixtureSnapshotTests`) catches
parser-output shape regressions cheaply. Layer C — this bench — measures the
actual goal: *can an AI engine, fed the harvester's IR, correctly answer
questions about a source database's intended behavior?*

A regression in IR clarity manifests as a drop in answer accuracy here.

## Files

- `draft_questions.py` — Session-agnostic drafting helper (used in
  `integration-testing-and-acceptance` Session 3 per the 2026-04-28 spec
  amendment). Routes `task_type="analysis"` against a DB's IR + the user's
  `scenarios.md` priority list, emits drafted candidate questions YAML for
  human curation.
- `run.py` — Bench runner. Loads curated `questions.yaml`, slices IR by each
  entry's `supporting_ir_paths[]`, calls `route(task_type="analysis")` for
  the answer, calls a second `route(task_type="analysis")` (the *judge*) to
  score `actual` vs `accepted_answers[]` 0.0–1.0, emits per-question JSONL.
- `scoring.py` — Graduated-rubric pass/fail computation + baseline-diff
  helper.

## Workflow

```
samples/<DB>.accdb
  └─ AccessHarvester.exe run --db samples/<DB>.accdb --out <outRoot>
       └─ <outRoot>/AccessMetaData/<DB>/ir/<category>/<object>.ir.json

docs/test-cases/comprehension/scenarios.md          (human-authored)
docs/test-cases/comprehension/<DB>/questions.yaml   (human-curated, drafted by draft_questions.py)

python tools/comprehension-bench/draft_questions.py \
    --db <DB> \
    --ir-root <outRoot>/AccessMetaData/<DB>
  └─ writes docs/test-cases/comprehension/<DB>/questions.draft.yaml
     (header makes the "draft, not curated" status loud)

# After human curation: rename questions.draft.yaml → questions.yaml,
# then run the bench:

python tools/comprehension-bench/run.py \
    --db <DB> \
    --ir-root <outRoot>/AccessMetaData/<DB> \
    --questions docs/test-cases/comprehension/<DB>/questions.yaml \
    --out <outRoot>/<DB>.scores.jsonl
  └─ exit 0 if graduated rubric passes (low ≥ 0.85, medium ≥ 0.65, high ≥ 0.40)
  └─ exit 1 on rubric failure
  └─ exit 2 on misconfiguration (no questions loaded, missing --questions, etc.)
```

`--ir-root` points at the **DB-specific** IR directory
(`<outRoot>/AccessMetaData/<DB>/`), not the parent `<outRoot>` — that's the
root the runner walks when slicing IR by each question's
`supporting_ir_paths[]`.

## xUnit wrapper

`tests/AccessHarvester.Comprehension.Tests` shells out to `run.py` only when
`HARVESTER_COMPREHENSION_BENCH=1`. Plain `dotnet test` skips by default — the
bench is expensive (routed calls per question) and flaky against
provider-rate-limit conditions, so opt-in is the right default.

## Question-bank schema

Each entry in `questions.yaml`:

```yaml
- id: nw-form-customers-001
  question: "What happens when the user clicks the Save button on the Customers form?"
  accepted_answers:
    - "It validates required fields then writes to the Customers table; ..."
  supporting_ir_paths:
    - "ir/forms/Customers.ir.json"
  difficulty: low   # low | medium | high
```

`accepted_answers` is a list — multiple phrasings are allowed; the judge picks
the closest match. `supporting_ir_paths` are relative to the DB's IR root
(`<outRoot>/AccessMetaData/<DB>/`); the runner loads only those files into the
prompt to keep token cost bounded.
