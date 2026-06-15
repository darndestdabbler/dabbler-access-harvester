# Set 017 — Devil's-Advocate Review Prompt (run in GPT-5.4 **and** Gemini Pro, in-IDE)

> Paste this into each in-IDE orchestrator (GitHub Copilot → GPT-5.4, and Gemini
> Pro) **separately**, in a checkout of `dabbler-access-harvester` at the Set 017
> closeout commit. Each reviewer has full repo + local-corpus access. Save each raw
> verdict to `s1-devils-advocate-<engine>.md` (never edit the saved file). This is
> the **primary** closeout review per `project-guidance.md` → the in-IDE pair has a
> blind spot non-overlapping with the routed `gpt-5-4` verifier (which already
> returned ISSUES_FOUND → 2 Minors, both fixed). In particular, the routed verifier
> saw a **password-redacted** packet, so it could **not** check the committed docs
> for a leaked credential — you can and must.

---

## Bias-cautions preamble (read before reviewing)

You are reviewing closeout-grade work. Known failure modes to resist:

- **Sycophancy / confirmation bias.** Do not rubber-stamp. The orchestrator's prose
  is persuasive and internally consistent; that is not evidence it is correct.
- **Your null hypothesis is that each work item below is WRONG.** Your job is to
  *break* it. A finding you can reproduce/demonstrate beats a finding you assert.
- **Steelman the *specific* contrarian hypothesis stated for each item** — do not
  produce generic "be skeptical" commentary (that yields theatrically negative,
  low-value reviews). For each item, actively try to make the stated counter-claim
  true with a concrete reproducer, file reference, or counter-example.
- **Two passes.** Pass 1: independently attempt to falsify each claim below. Pass 2:
  re-read the diff and the test once more and ask "what did pass 1 not look at?" —
  name at least one thing pass 1 missed (even if it turns out benign).

## What the session shipped (the work under review)

A single-session, Full-tier set onboarding the **proprietary, split FE/BE
StudentICTDatabase** as a **local-only** invariant corpus (never committed). Tracked
changes only:

- `tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs` (new,
  skip-gated; mirrors `NorthwindLocalInvariantTests`).
- `docs/planning/deferred-work.md` — new **DW-24** (Connect-string credential/path
  redaction), **DW-25** (`TableMetadata.IsLinked`/`Attributes` absent from
  run-pipeline manifest table records), **DW-26** (`SourceLocation.StartColumn==0`
  0-based leak → schema-invalid manifest).
- `docs/harvester/interpretation-guide.md` §5.5 — split-FE behaviour note
  (`Relationships: []`, detect linked via `Connect`+`SourceTableName`, `Connect` is
  sensitive).
- session-set artifacts (`s1-reground-evidence.md`, `ai-assignment.md`,
  `s1-verification.md`, `s1-issues.json`, state/log).

Validation on the local host: build clean; the new test PASSES 1/1 against the local
corpus; HHC golden proven byte-level neutral (`CanonicalGoldenRegressionTests` 1/1);
`NorthwindLocalInvariantTests` 1/1. Scope rule: **corpus onboarding only** — every
producer defect is recorded as a DW, not fixed.

---

## Items to attack (each with its specific contrarian hypothesis)

### A1 — Credential / machine-path leak into git
**Contrarian hypothesis to prove:** *"This session committed the real BE password or
an absolute machine path into a tracked file."* The orchestrator harvested a corpus
whose `Connect` strings embed a plaintext `PWD=` and an absolute path, then wrote
several docs *about* that. Grep the **committed diff** (not just the test) —
`deferred-work.md`, `s1-reground-evidence.md`, `activity-log.json`, the change-log,
disposition — for the literal password value, for `PWD=<anything-other-than-a-
placeholder>`, and for any absolute `C:\…` / `/Users/…` path to the BE. The claim is
refuted only if **every** occurrence is a placeholder (`<redacted>`,
`<absolute-local-path>`). If you find a real value anywhere tracked, that is a
**Critical** finding.

### A2 — The DW-26 known-limitation pin is dishonest or wrong
**Contrarian hypothesis to prove:** *"Asserting `IsValid == false` is scope-dodging
that masks a second, unrelated real schema violation in this corpus."* The
orchestrator claims `StartColumn==0` is the **sole** real failure and the thousands
of other validator errors (`ResolvedTarget should be "null"`, `ItemKind Expected
"Query"/"Expression"`, `ParserOutput should be "null"`) are losing-`oneOf`-branch
noise. Try to find a hard schema failure on the StudentICT manifest that is **not**
the `StartColumn` minimum and **not** recoverable via a `oneOf`/`null` branch. (Lever:
`ResolvedReference.ResolvedTarget` is `oneOf[object,null]`; Northwind validates with
the same object-valued fields — does that actually hold here, or is there a structural
difference between the two corpora's manifests you can point to?) If you find a second
independent failure class, the pin is incomplete (it would stay green while a real
defect rides along) → **Major**.

### A3 — `StartColumn==0` is correct and the SCHEMA is the bug
**Contrarian hypothesis to prove:** *"The harvester's column model is legitimately
0-based; `minimum:1` in the schema is the real defect, so DW-26 should be a schema fix
and the test pins the wrong artifact."* Test it against the evidence: the committed
HHC golden has 5973 `StartColumn` values, min 1, and line-leading tokens at column 1
(not 0). Is there a code path or doc that says the parser's columns are 0-based? If the
1-based reading is wrong, the whole DW-26 framing (and the test's `IsValid==false`
expectation) is misdirected → **Major**.

### A4 — The linked-table detection heuristic is wrong (DW-25)
**Contrarian hypothesis to prove:** *"Detecting a linked table by a non-empty `Connect`
+ `SourceTableName` produces false positives or false negatives."* `TableMetadata`'s
doc says native local tables get `Connect = ""` (empty), not null. Can a **local**
(non-linked) table ever carry a non-empty `Connect` or `SourceTableName` (e.g. a table
that was once linked, or an Access system table)? If so, the test's `linkedTables`
filter and the interpretation-guide guidance are unsound. Separately: is `IsLinked`
*actually* always null on the `run` path, or does some code populate it that the
orchestrator missed (search beyond `TableMetadataExtractor`)? Either → **Major**.

### A5 — The test passes for the wrong reason (false green)
**Contrarian hypothesis to prove:** *"One or more assertions cannot actually fail, so
the test is a no-op that would stay green even if the producer regressed."* For each
assertion, construct the producer regression it is supposed to catch and check the
assertion would actually flip:
- `HasZeroStartColumn` — would it return true for a `StartColumn:0` that is **not** the
  DW-26 class (e.g. a legitimately-0 field elsewhere), making the pin pass spuriously?
- the capability-level ribbon checks — would they pass even if the ribbon producer
  emitted **no** resolved entry points, or resolved them to the wrong module?
- `Assert.Empty(manifest.Relationships)` — is this an invariant of split FEs, or could
  it pass trivially on a corpus that simply has no relationships for unrelated reasons,
  giving false confidence about split-FE handling?

### A6 — Corpus-independence labeling and staleness
**Contrarian hypothesis to prove:** *"The test still conflates corpus-specific canaries
with corpus-independent invariants, or bakes in counts/values that will silently rot."*
The routed verifier already flagged this (now split into capability invariants vs
labeled canaries). Re-check: is the split honest and complete? Are there remaining
exact-count or exact-name assertions mislabeled as invariants? Do the comments state
observed facts (e.g. "9 linked", "3315") as if they were asserted contract?

### A7 — Scope / spec-fidelity
**Contrarian hypothesis to prove:** *"The session violated its own spec or Stop
Rules."* The spec requires: a skip-gated test of corpus-independent invariants; DW-24 +
split-FE docs; HHC golden byte-neutral; **no** committed StudentICT artifact; **no**
weakening of assertions to force green. Did changing the spec's "manifest
schema-validates" invariant into an `IsValid==false` pin honour the Stop Rule ("a
failing real-corpus invariant is a finding, not an assertion to soften") — or quietly
abandon a required invariant? Is the linked-table count correction (spec said 13;
actual 9) handled honestly?

---

## Required output (per reviewer)

For each item A1–A7: state **UPHELD** (the work is sound — the contrarian hypothesis
failed) or **CHALLENGED** (you made the counter-claim stick), with the concrete
evidence (file:line, reproducer, or counter-example). Then a JSON block:

```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    { "severity": "Critical" | "Major" | "Minor", "item": "A1..A7", "location": "<file>", "summary": "...", "fix": "..." }
  ]
}
```
`VERIFIED` ⇒ `issues: []`. End with **one thing pass 1 missed** (pass-2 result), even if benign.
