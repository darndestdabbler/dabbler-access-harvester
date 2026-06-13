# F-3-9 Conformance-Contract Decision — cross-provider consensus prompt

You are advising on a **process/design decision** in the Dabbler Access
Harvester repo (a C#/.NET 10 toolchain that extracts Access metadata + parses
VBA so a downstream AI can interpret it). This is decision-time consensus: give
a clear recommendation with reasoning, not a survey. Default your null
hypothesis to "the cheaper, honest option is better unless the expensive one
buys real coverage."

## Bias cautions
- Do not recommend building a large test harness just because it sounds rigorous.
- Do not assume the hand-authored fixtures are an enforceable contract — judge
  whether they actually are.
- Weigh the repo's stated **Lean By Default** principle (don't add abstractions,
  harnesses, or safety nets beyond what the task requires) and the fact that the
  repo is now **consumer-driven only** (no self-initiated feature work).

## Background (the finding being resolved — 006 review F-3-9, Minor)

`docs/planning/project-guidance.md` (VBA Parser Strategy) currently declares:

> "Test cases in `docs/test-cases/` are the source of truth for parser behavior.
> The `<n>.expected.json` file pinned next to each input is the contract; parser
> changes are validated by re-running each input and comparing against the
> expected output."

**That comparison does not exist.** The 006 review proved (greps + file reads):

- The only behavioral enforcement is:
  - `TestCaseFixtureConformanceTests` — validates each `<n>.expected.json` against
    the **schema shape only** (harvester-schema v1.x), NOT against harvester output.
  - `ResolverFixtureAnnotationTests`, `FixtureCrossCutTests`, `DynamicSqlFixtureTests`,
    grammar corpus tests — parse the `.bas` inputs and assert **selected annotations**
    on a subset of cases (e.g. resolver covers 02, 03, 08, 09).
- **No test diffs actual harvester output against any `<n>.expected.json`.**
- The `<n>.expected.json` files were **hand-authored before implementation as
  design intent**. They encode rich fields the harvester does not emit at all
  (e.g. `confidence` per reference, `templateCandidates` with per-template
  confidence, `suggestedNextStep`, diagnostics with invented stable codes like
  `HARV-W040`, `unresolvedItems` with narrative next-steps). The actual operative
  output schema is the PascalCase `DatabaseManifest` v1.1 (a different shape; the
  v0→v1 ADR migrated the fixtures into a manifest envelope, and 12 of 13 — now 14 —
  validate for shape, with one stop-rule exclusion).
- Practical consequence: 900+ hermetic tests pass while real parser defects shipped
  (the F-3-1/F-3-2/F-3-3 bugs this very set is fixing). The strongest *stated*
  quality gate is aspirational.

## The two options (006 recommendation)

**Option A — Build the conformance runner the guidance promises.** A test that
executes the parser/resolver over each `docs/test-cases/` input and diffs the
produced output against `<n>.expected.json`, with a **documented field-mask** for
not-yet-implemented surfaces (confidence scoring, template candidates, dynamic-SQL
enumeration, the invented diagnostic-code registry, etc.).

**Option B — Amend the contract to the truth.** Edit `project-guidance.md` + the
test-cases README to state the *actual* enforced contract: schema-shape conformance
(`TestCaseFixtureConformanceTests`) + selected-annotation behavioral tests on named
cases. Keep the `<n>.expected.json` files as design-intent specification/onboarding
artifacts, explicitly labeled as such (not an enforced diff target).

Under **either** option, the README is corrected: it says "The 12 cases" and lists
12 pairs, but 14 input/expected pairs now exist on disk (13-preprocessor added
earlier; 14-typed-suffix-and-contextual-names added this set's Session 2). The
README also still says outputs are "validated against harvester-schema.v0.json"
while the conformance test pins v1.1.

## Decisive considerations to weigh and address explicitly

1. **Field-mask size under Option A.** Because the fixtures encode design-intent
   fields the harvester never emits (and may never emit — confidence scoring,
   template enumeration, the HARV-* code registry are not on any roadmap), the
   mask would have to suppress the *majority* of every expected.json. Does a diff
   that masks most fields still assert anything meaningful, or does it just
   re-encode the selected-annotation contract at much higher cost + maintenance?
2. **Structural mismatch.** expected.json fixtures are in the migrated manifest
   envelope but encode the pre-implementation parser-intent shape; the harvester
   emits the operative v1.1 manifest. Is a direct diff even well-defined without
   first re-authoring all 14 expected files to the real output shape (a large task
   that re-pins intent to current behavior — arguably *weakening* them from spec
   to snapshot)?
3. **What the consumer actually relies on.** The downstream consumer is an AI
   agent reading the manifest; its confidence derives from "the pinned cases pass."
   Which option more honestly represents what is guaranteed?
4. **Regression-catching power.** Would Option A actually have caught the
   F-3-1/2/3 defects, or did those need the *new* behavioral pins this set added
   anyway? I.e., is the corpus-diff the right regression mechanism, or are
   targeted behavioral assertions (the current pattern) the right one?
5. **Lean + consumer-driven posture.** Given no self-initiated feature work, is a
   net-new harness justified, or is honest documentation + the existing targeted
   tests the proportionate close?

## Required output

End with a JSON block exactly:

```json
{
  "recommendation": "A" | "B" | "hybrid",
  "confidence": "high" | "medium" | "low",
  "one_line": "<your recommendation in one sentence>",
  "key_reasons": ["<reason>", "..."],
  "if_hybrid_what_minimal_A": "<if you say hybrid, the smallest A-ish slice worth doing, else empty>",
  "risks_of_your_choice": ["<risk>", "..."]
}
```
