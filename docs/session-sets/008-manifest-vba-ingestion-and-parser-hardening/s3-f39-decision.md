# F-3-9 Conformance-Contract Decision Record

> **Set:** 008-manifest-vba-ingestion-and-parser-hardening, Session 3
> **Decided:** 2026-06-13
> **Mechanism:** decision-time consensus (cross-provider) per
> `docs/ai-led-session-workflow.md` → Decision-time consensus, then
> orchestrator adjudication.
> **Decision:** **Option B** — amend the contract to the truth; do **not**
> build the corpus-diff conformance runner.

## The question (006 review F-3-9, Minor)

`project-guidance.md` (VBA Parser Strategy) declared the `<n>.expected.json`
files "the contract; parser changes are validated by re-running each input and
comparing against the expected output." That comparison does not exist; the only
enforcement is schema-shape conformance (`TestCaseFixtureConformanceTests`) plus
targeted selected-annotation behavioral tests on a subset of cases. The
`expected.json` files were hand-authored before implementation as design intent
and encode fields the harvester never emits (per-reference `confidence`,
`templateCandidates`, `HARV-*` diagnostic-code registry, narrative
`suggestedNextStep`s) in a shape that differs from the operative `DatabaseManifest`
v1.1 output.

- **Option A:** build the runner the guidance promises (execute parser/resolver
  over each input, diff against `expected.json`, with a documented field-mask for
  not-yet-implemented surfaces).
- **Option B:** amend `project-guidance.md` + the test-cases README to state the
  actual enforced contract; keep `expected.json` as design-intent/onboarding
  artifacts, explicitly labeled as non-binding.

## Cross-provider consensus

Routed the trade-off to both cross-provider models (prompt:
`s3-f39-consensus-prompt.md`; raw replies: `s3-f39-consensus-gpt-5.4.md`,
`s3-f39-consensus-gemini.md`).

| Model | Recommendation | Confidence |
|---|---|---|
| gpt-5-4 | **B** | high |
| gemini-pro | **B** | high |

**Unanimous, high-confidence B.** Converging reasons:

1. The field-mask under A would suppress the *majority* of every fixture (the
   intent-only fields the harvester does not and may never emit), so the diff
   asserts little at high maintenance cost — a clumsy re-encoding of the targeted
   assertions that already exist.
2. The fixtures and the operative `DatabaseManifest` v1.1 output have different
   shapes; a true diff is not well-defined without re-authoring all 14 fixtures to
   current output — which would convert design specifications into brittle
   snapshot tests, *weakening* them.
3. The escaped F-3-1/2/3 defects were caught by **new targeted behavioral pins**
   (the mechanism this codebase already uses), not by any corpus diff. Targeted
   assertions are the regression tool that matches parser semantics here.
4. Lean-by-default + consumer-driven posture do not justify a net-new,
   un-requested harness.

**GPT-5.4 nuance adopted:** if stronger corpus-level pinning is ever wanted, build
a *separate, smaller* golden suite in the **actual emitted `DatabaseManifest`
shape**, scoped to the fields the consumer truly relies on — do **not** retrofit
the design-intent `expected.json` files into that role. Recorded as a future
consumer-driven candidate (carried in the next-session-set recommendation), not
work for this set.

## Decision and actions (Option B)

1. **`project-guidance.md` → VBA Parser Strategy:** remove the "source of truth /
   the contract / validated by re-running and comparing" language; state the real
   enforcement (schema-shape conformance + targeted behavioral tests) and label the
   `expected.json` files as design-intent/onboarding artifacts, not an executable
   diff target. Keep test case 08 (`08-unresolvable.bas`) as the canonical
   explicit-unresolved *example* (its role is illustrative, which survives).
2. **`docs/test-cases/README.md`:** correct "The 12 cases" → 14; correct the
   "validated against `harvester-schema.v0.json`" sentence to the actually-pinned
   v1.1 shape; note the case-12 stop-rule exclusion; label the expected outputs as
   illustrative design intent.
3. **No runner built.** No new test harness; the existing schema-shape +
   behavioral suites stand as the enforced contract.

## Risk accepted (disclosed)

There remains no full end-to-end golden-output regression over the entire corpus,
so a broad regression in a non-asserted field across many cases could go unpinned.
Mitigation: the committed HomeHealthCare manifest golden
(`CanonicalGoldenRegressionTests`) is the byte-level full-output regression gate on
the real fixture; the per-feature behavioral pins cover the parser semantics. The
separate-golden-suite option above is the escalation path if a consumer ever needs
more.
