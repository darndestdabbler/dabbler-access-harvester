# Change Log — 017-studentict-local-invariant-corpus

**Status:** COMPLETE (1 of 1 sessions) · **Tier:** Full · **Date:** 2026-06-15
**Final verification verdict:** VERIFIED (routed gpt-5-4, round 3) + in-IDE
devil's-advocate pair (GPT-5.4 + Gemini 3.1 Pro) as primary review.

## What shipped

Onboarded the proprietary, split front-end/back-end **StudentICTDatabase** as a
**second local-only invariant corpus** (alongside NorthwindDevEdition) — **never a
committed golden**. The FE, BE, and `samples/*.lnk` are gitignored; the linked-table
`Connect` strings embed a plaintext password + absolute path, so no derived artifact
may ever be committed. The only tracked changes are a skip-gated test, three docs, and
session-set artifacts — **no StudentICT-derived `.accdb`/`raw`/`kb`/`ir`/manifest**.

### Test
- **`tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs`**
  (new, skip-gated on Access COM **and** both FE+BE present; serialised via the
  `HomeHealthCareCom` collection). Mirrors `NorthwindLocalInvariantTests`. Asserts:
  - **Structural invariants (corpus-independent):** clean split-FE pipeline run;
    ≥1 linked table with a non-empty `Connect`+`SourceTableName` whose `Connect`
    references the BE **filename** (never the password/absolute path); **empty
    `Relationships`** (split-FE invariant — relationships live in the BE); a custom
    ribbon present with ≥1 tab; both ribbon callback kinds (onLoad + onActions)
    surfaced and **Resolved** with a non-empty module; `FunctionClassification`
    unresolved refs present (DW-18 witness).
  - **Corpus-specific canaries (labeled):** ribbon name `Help`, tab `tab1`, single
    root onLoad, buttons share `OnActionButton`, all entries resolve into module
    `modRibbonCallbacks`.
  - **DW-26 known-limitation pin:** the manifest does **not** schema-validate; the
    test asserts `IsValid == false`, that the failure is the `StartColumn`
    `[minimum]` class, and (structurally) that a numeric `StartColumn == 0` is
    present — flipping the day DW-26 is fixed.

### Docs / deferred work
- **`docs/planning/deferred-work.md`** — three new items, all out of onboarding scope
  (recorded, not fixed):
  - **DW-24** — linked-table `Connect` strings carry a plaintext password + absolute
    BE path; harvester does no credential redaction (privacy/security; any split DB).
  - **DW-25** — `TableMetadata.IsLinked` (+ raw `Attributes`) absent from
    `run`-pipeline manifest table records (`Pipeline.BuildTableExports` deserialises
    IR that lacks a derived `IsLinked`); detect linked via `Connect`+`SourceTableName`.
  - **DW-26** — manifest source-location columns are emitted **0-based** (the VBA
    AST's ANTLR convention, projected raw by `ParserOutputProjector.BuildLocation`
    with no `+1`) while the schema declares `StartColumn` `minimum: 1` — a convention
    mismatch at projection. Any real split/rich-VBA corpus with a line-leading
    reference/call token yields a **schema-invalid manifest**. Golden-affecting; fix
    is a deliberate decision (normalise projection to 1-based, or relax schema to
    `minimum: 0`).
- **`docs/harvester/interpretation-guide.md`** §5.5 — split-FE behaviour note
  (`Relationships: []` is expected; detect linked via `Connect`+`SourceTableName`,
  not `IsLinked`; `Connect` is sensitive).
- **`spec.md`** — Execution Amendment reconciling the "manifest schema-validates"
  invariant into the DW-26 known-limitation pin (no silent drift).

## Validation
- Build clean. `StudentIctLocalInvariantTests` PASS 1/1 (local corpus, TRX).
- **HHC golden byte-level neutral** — `CanonicalGoldenRegressionTests` PASS 1/1 (this
  set touched no producer). `NorthwindLocalInvariantTests` PASS 1/1 (corroborates that
  prior corpora schema-validate, the basis for DW-26's "first corpus" framing).

## Verification trail
- Routed cross-provider R1 (gpt-5-4): ISSUES_FOUND → 2 Minor (brittle pin matching;
  canary mislabeling) — both fixed.
- In-IDE devil's-advocate pair (PRIMARY): A3 Major (both reviewers — DW-26 root-cause
  reframed), A7 Major (GPT — spec amendment), A6 Minor (Gemini — capability/canary
  split) — all fixed. A1/A2/A4/A5 UPHELD.
- Routed R2: 3 resolutions VERIFIED + 1 new Minor (unbacked canary comment) fixed.
- Routed R3: VERIFIED.
- **Credential near-miss:** the real BE password had been written into 3 tracked docs;
  self-caught and redacted to placeholders **before any commit** (the routed verifier
  could not catch it — its packet was password-redacted; the in-IDE A1 re-check
  confirmed clean).

## Handoff
Repo remains **consumer-driven-only** (no concrete next set authored). See
`next-session-set-recommendation.md` for the open DW landscape; strongest pull-in
candidates if a driver appears: **DW-26** (schema-invalid manifests on real corpora —
newly the highest-value correctness item) and **DW-18** (intrinsic-constant registry;
golden-affecting; huge StudentICT witness, 3315 `FunctionClassification` unresolved).
