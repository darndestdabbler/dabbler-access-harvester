# Embedded Proprietary-Residue Findings — 003 Northwind Remediation

> **Authored:** Session 2 (2026-06-05). **Status:** Maintainer findings — NOT
> remediated in this set (per spec Out-of-Scope + Stop Rules). Paths only; no
> proprietary content is reproduced here.

## TL;DR (read this before authorizing Session 3)

The Session 3 scrub allowlist removes the proprietary DB binary, its lock file,
and the harvester's *dedicated* derived artifacts (golden tree, YAML/parse-report
mirrors, symbol-db measurement docs, comprehension drafts, the example manifest).
**It does not, and by spec must not, remove the much larger secondary footprint
of proprietary NorthwindDevEdition structure that is embedded inside keep-files.**

A scan of everything tracked at `HEAD` (excluding the scrub targets) for
distinctive proprietary object identifiers found **123 keep-files** that disclose
real Northwind form / report / query / table / module / control names — and in
many cases full procedure logic, VBA translations, workflow behavior, or
serialized symbol-DB source bodies.

**Consequence:** after the Session 3 rewrite, the repo's *working tree and
history will still contain extensive NorthwindDevEdition structural disclosure.*
The set's "Done Looks Like — no NorthwindDevEdition-derived artifact is tracked
at HEAD" is satisfied *only for the dedicated-artifact category the allowlist
targets*, not for this embedded residue. **This requires a maintainer scope
decision (see Recommendation).**

This is recorded, not acted on, because the spec explicitly scopes embedded
residue out of this set:
> *Out of Scope — Scrubbing proprietary content embedded inside otherwise-keep
> files … If any such residue is found during Session 2 it is recorded as a
> finding for the maintainer, not silently rewritten.*

## How the scan was run

```
git grep -liE '<distinctive Northwind identifiers>' -- \
   ':(exclude)<the 4 judgment-call scrub paths/dirs>' \
   ':(exclude)samples/GoldenOutput/NorthwindDevEdition/**'
```

The identifier set was chosen to be Northwind-specific (form/module/query/table
names that do **not** collide with the synthetic HomeHealthCare fixture's
vocabulary), so HomeHealthCare-only files do not appear. The scan is a *floor*,
not a ceiling: files using only generic terms, or paraphrasing without the exact
identifiers, may also carry residue and would not be caught by this pattern.

## Severity buckets

### CRITICAL — binary/data artifacts embedding full source + interpretations

These are not "a name quoted in prose"; they are serialized harvester/symbol-DB
state for the proprietary DB. `git grep` confirmed proprietary form/module/member
names inside the two SQLite binaries.

- `tools/symbol-db/symbol-db.sqlite` (1,232,896 bytes) — the symbol DB itself:
  module source bodies, symbols, and AI interpretations for the proprietary DB.
- `tools/symbol-db/consistency/grades.sqlite` (671,744 bytes) — consistency-grading
  DB derived from the same.
- `tools/symbol-db/consistency/sample.json`, `correlation-summary.json` — derived
  consistency samples/metrics.

### HIGH — full proprietary procedure source, translations, or descriptions

- `docs/session-sets/vba-symbol-db/probe-results/` — `reference-description.md`
  (definitive prose description of a proprietary BeforeUpdate handler authored
  from its source body), `comprehension-packet.md`, and the three `probe-*.md`
  provider outputs that interpret it.
- `docs/session-sets/vba-symbol-db-path-b-probe/translation/` + `translation-packet.md`
  + `peer-review/` — proprietary procedure translations and reviews.
- `docs/session-sets/workflow-package-pilot/packages/` —
  `add-an-order.md`, `update-an-order.md`, `update-a-product.md`: end-to-end
  workflow logic operationalized from the proprietary DB's forms/queries/VBA.
- `docs/session-sets/dabbler-platform-discovery/smoke-test-frmOrderDetails.md`.
- `tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbe*.cs`
  — translation probe of a proprietary form's procedure.

### MEDIUM — workflow/behavior characterization tests + structural fixtures

- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/*` (5 files) and
  `UpdateAProduct/*` (2 files) — encode proprietary order/inventory/product
  business rules as test expectations.
- `tests/AccessHarvester.Tests/ReportBaseline/*`, `tests/AccessHarvester.SymbolDb.Tests/*`,
  `tests/AccessHarvester.Vba.Resolver.Tests/*` — fixtures/assertions naming
  proprietary objects and structure.
- `docs/session-sets/harvester-report-baseline-*`, `harvester-report-displayed-total-parity`
  specs/change-logs referencing proprietary report objects.

### LOW — incidental name references (fixture named, no structure reproduced)

- C# source naming the fixture: `AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs`,
  `AccessHarvester.SymbolDb/Extraction/LocalVariableExtractor.cs`,
  `AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs`,
  `tests/.../NorthwindFixtureSmokeTests.cs`, `FixtureLoader.cs`, etc.
- `docs/homehealthcare-northwind-comparison.md`,
  `docs/cross-repo/access-to-platform-migration-map.md`,
  `docs/lowdefy-evaluation/prompt-for-codex.md`.
- `docs/session-sets/generalization-validation-on-non-northwind/inventory.md` —
  names two Northwind forms as *comparison analogues* for the HHC fixture. The
  enclosing session-set is otherwise an incidental slug-match KEPT by the
  allowlist rationale; this single file is the lone Northwind-identifier touch in
  that subtree.
- Process bookkeeping that only mentions the `.accdb` path: `activity-log.json`,
  `ai-assignment.md`, `disposition.json`, `*-driver.py`, `*-verify.py`, etc.

(Bucket assignment is a first-pass triage from filenames + sampled contents, not
a line-by-line audit of all 123 files. The maintainer should treat CRITICAL/HIGH
as confirmed and MEDIUM/LOW as "likely, verify if acting".)

## Why this is out of scope for Session 3

1. The spec's allowlist + Stop Rules forbid scrubbing keep-files in this set.
2. These paths are NOT incidental name-matches to "preserve" — they are genuine
   residue — but rewriting 123 files' history (some of which are needed test
   fixtures and process records) is a different, larger remediation requiring its
   own scoping, keep/delete/redact decisions per file, and test-impact analysis.
3. Acting on them inside Session 3 would blow past the reviewed allowlist the
   whole set is built to constrain.

## Recommendation (maintainer decision required)

The repo is **private**, so none of this is publicly exposed today. The decision
is about the maintainer's standing internal policy, not external exposure. Options:

1. **Accept residue, proceed with Session 3 as scoped.** The dedicated-artifact
   scrub still removes the bulk binary + golden footprint. Record that the repo
   retains Northwind *structural* disclosure in keep-files by deliberate choice.
2. **Expand into a follow-up set** (`004-northwind-embedded-residue-*`) that
   triages all 123 files: delete the symbol-DB/consistency tooling artifacts and
   probe/translation/workflow docs outright (they are spent process artifacts),
   re-base the characterization/translation tests onto HomeHealthCare, and redact
   the LOW-bucket name references if desired. This is the path that actually makes
   the repo Northwind-free.
3. **Hybrid:** add the CRITICAL bucket (the two SQLite binaries + the JSON
   consistency samples) to the Session 3 allowlist now — they are pure
   tooling/scratch artifacts with no keep value and the highest disclosure
   density — and defer the HIGH/MEDIUM/LOW buckets to a follow-up set. (This would
   require re-running Session 2's verification + dry-run against the amended
   allowlist before Session 3.)

Per the maintainer's standing preference to honor spec stop rules strictly, the
default is **Option 1 for this set + a named follow-up set** unless the maintainer
directs otherwise at the Session 3 go-ahead.

### Maintainer decision (2026-06-05)

**Defer to a follow-up set** (Option-2 style). Session 3 stays scoped to the
reviewed 9-line allowlist only; the 123-file embedded-residue footprint (incl. the
two binary SQLite DBs) is **not** added to the Session-3 allowlist. A follow-up
session set (suggested slug `004-northwind-embedded-residue-remediation`) will
triage the full residue inventory above with per-file keep/delete/redact decisions
and test-impact analysis. This preserves the reviewed-allowlist discipline of set
`003` and keeps the destructive Session 3 within its verified scope.

## Appendix — full residue inventory (123 paths, sorted)

### tests/ (28)
```
tests/AccessHarvester.CharacterizationTests/AddAnOrder/InventoryAllocationBehaviorTests.cs
tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs
tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderLineItemBehaviorTests.cs
tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3CrossOrderScopeTests.cs
tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3DeletionReallocationTests.cs
tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductReorderPolicyTests.cs
tests/AccessHarvester.CharacterizationTests/UpdateAProduct/UpdateAProductWorkflowModels.cs
tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs
tests/AccessHarvester.Integration/Pipeline/GoldenCorpusRegenerator.cs
tests/AccessHarvester.SymbolDb.Tests/CrossModuleResolutionTests.cs
tests/AccessHarvester.SymbolDb.Tests/DeferralPreFilterTests.cs
tests/AccessHarvester.SymbolDb.Tests/FixtureLoader.cs
tests/AccessHarvester.SymbolDb.Tests/NorthwindFixtureSmokeTests.cs
tests/AccessHarvester.SymbolDb.Tests/PersistenceAndDedupTests.cs
tests/AccessHarvester.SymbolDb.Tests/ReferencesExtractorTests.cs
tests/AccessHarvester.SymbolDb.Tests/Session3ExtractorTests.cs
tests/AccessHarvester.Tests/ReportBaseline/RecordSourceResolverTests.cs
tests/AccessHarvester.Tests/ReportBaseline/ReportAggregateComputerTests.cs
tests/AccessHarvester.Tests/ReportBaseline/ReportAggregatesWriterTests.cs
tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineServiceTests.cs
tests/AccessHarvester.Tests/SyntheticDbIntegrationTests.cs
tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbe.cs
tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbeTests.cs
tests/AccessHarvester.TranslationProbe.Tests/Session3RegressionTests.cs
tests/AccessHarvester.Vba.Resolver.Tests/CrossReference/CrossReferenceFixtureTests.cs
tests/AccessHarvester.Vba.Resolver.Tests/References/ResolverFixtureAnnotationTests.cs
tests/AccessHarvester.Vba.Resolver.Tests/ScopeBuildingFixtureTests.cs
tests/AccessHarvester.Vba.Resolver.Tests/Types/TypeInferenceFixtureTests.cs
```

### tools/ (15)
```
tools/archive/session1_redo_route_architecture.py
tools/archive/session2_verify_session1.py
tools/run-verification-session2-r3.py
tools/run-verification-session2.py
tools/symbol-db/consistency/correlation-report.md
tools/symbol-db/consistency/correlation-summary.json
tools/symbol-db/consistency/grades.sqlite
tools/symbol-db/consistency/sample-report.md
tools/symbol-db/consistency/sample.json
tools/symbol-db/interpret/__main__.py
tools/symbol-db/interpret/summary.py
tools/symbol-db/probe/build_packet.py
tools/symbol-db/probe/run_probe.py
tools/symbol-db/symbol-db.sqlite
tools/symbol-resolver/test_poc.py
```

### AccessHarvester.* source (3)
```
AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs
AccessHarvester.SymbolDb/Extraction/LocalVariableExtractor.cs
AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs
```

### docs/ non-session-set (3)
```
docs/cross-repo/access-to-platform-migration-map.md
docs/homehealthcare-northwind-comparison.md
docs/lowdefy-evaluation/prompt-for-codex.md
```

### docs/session-sets/ (74)
```
docs/session-sets/001-harvester-upstream-feedback-followups/session-3-verify.py
docs/session-sets/001-harvester-upstream-feedback-followups/session-reviews/session-003.md
docs/session-sets/dabbler-platform-discovery/activity-log.json
docs/session-sets/dabbler-platform-discovery/ai-assignment.md
docs/session-sets/dabbler-platform-discovery/disposition.json
docs/session-sets/dabbler-platform-discovery/session-3-changelog-driver.py
docs/session-sets/dabbler-platform-discovery/session-3-driver.py
docs/session-sets/dabbler-platform-discovery/session-3-next-set-driver.py
docs/session-sets/dabbler-platform-discovery/session-3-verify.py
docs/session-sets/dabbler-platform-discovery/session-reviews/session-003.md
docs/session-sets/dabbler-platform-discovery/smoke-test-frmOrderDetails.md
docs/session-sets/generalization-validation-on-non-northwind/inventory.md
docs/session-sets/harvester-report-baseline-capture/ai-assignment.md
docs/session-sets/harvester-report-baseline-capture/change-log.md
docs/session-sets/harvester-report-baseline-capture/spec.md
docs/session-sets/harvester-report-baseline-golden-corpus/activity-log.json
docs/session-sets/harvester-report-baseline-golden-corpus/session-reviews/session-002.md
docs/session-sets/harvester-report-baseline-golden-corpus/spec.md
docs/session-sets/harvester-report-displayed-total-parity/change-log.md
docs/session-sets/harvester-report-displayed-total-parity/disposition.json
docs/session-sets/harvester-report-displayed-total-parity/spec.md
docs/session-sets/resolver-declarations-and-scopes/schema-validation-result.md
docs/session-sets/vba-symbol-db-path-b-probe/activity-log.json
docs/session-sets/vba-symbol-db-path-b-probe/ai-assignment.md
docs/session-sets/vba-symbol-db-path-b-probe/change-log.md
docs/session-sets/vba-symbol-db-path-b-probe/decision.md
docs/session-sets/vba-symbol-db-path-b-probe/peer-review/test-run-session-3.md
docs/session-sets/vba-symbol-db-path-b-probe/peer-review/translation-review.md
docs/session-sets/vba-symbol-db-path-b-probe/session-reviews/session-001.md
docs/session-sets/vba-symbol-db-path-b-probe/session-reviews/session-003.md
docs/session-sets/vba-symbol-db-path-b-probe/spec.md
docs/session-sets/vba-symbol-db-path-b-probe/translation-packet.md
docs/session-sets/vba-symbol-db-path-b-probe/translation/symbol-db-gaps.md
docs/session-sets/vba-symbol-db-path-b-probe/translation/test-run-session-2.md
docs/session-sets/vba-symbol-db-path-b-probe/translation/translation-rationale.md
docs/session-sets/vba-symbol-db/activity-log.json
docs/session-sets/vba-symbol-db/ai-assignment.md
docs/session-sets/vba-symbol-db/change-log.md
docs/session-sets/vba-symbol-db/issue-logs/session-003.json
docs/session-sets/vba-symbol-db/issue-logs/session-004.json
docs/session-sets/vba-symbol-db/probe-results/comprehension-packet.md
docs/session-sets/vba-symbol-db/probe-results/probe-gemini-pro.md
docs/session-sets/vba-symbol-db/probe-results/probe-gpt-5-4.md
docs/session-sets/vba-symbol-db/probe-results/probe-opus.md
docs/session-sets/vba-symbol-db/probe-results/reference-description.md
docs/session-sets/vba-symbol-db/session-reviews/session-001.md
docs/session-sets/vba-symbol-db/session-reviews/session-002-prompt.txt
docs/session-sets/vba-symbol-db/session-reviews/session-002-r2-prompt.txt
docs/session-sets/vba-symbol-db/session-reviews/session-002-r3-prompt.txt
docs/session-sets/vba-symbol-db/session-reviews/session-002-round-3.md
docs/session-sets/vba-symbol-db/session-reviews/session-003-prompt.txt
docs/session-sets/vba-symbol-db/session-reviews/session-003-r2-prompt.txt
docs/session-sets/vba-symbol-db/session-reviews/session-003.md
docs/session-sets/vba-symbol-db/session-reviews/session-004.md
docs/session-sets/vba-symbol-db/spec.md
docs/session-sets/vba-symbol-db/verdict.md
docs/session-sets/vba-symbol-resolution-and-enrichment/SESSION-1-FALSE-START.md
docs/session-sets/vba-symbol-resolution-and-enrichment/activity-log.json
docs/session-sets/vba-symbol-resolution-and-enrichment/ai-assignment.md
docs/session-sets/vba-symbol-resolution-and-enrichment/archive/superseded_architecture.md
docs/session-sets/vba-symbol-resolution-and-enrichment/session-reviews/session-001.md
docs/session-sets/vba-symbol-resolution-and-enrichment/spec.md
docs/session-sets/workflow-package-pilot/_template.md
docs/session-sets/workflow-package-pilot/activity-log.json
docs/session-sets/workflow-package-pilot/ai-assignment.md
docs/session-sets/workflow-package-pilot/cross-workflow-audit.md
docs/session-sets/workflow-package-pilot/inventory.md
docs/session-sets/workflow-package-pilot/packages/add-an-order.md
docs/session-sets/workflow-package-pilot/packages/update-a-product.md
docs/session-sets/workflow-package-pilot/packages/update-an-order.md
docs/session-sets/workflow-package-pilot/session-reviews/session-002.md
docs/session-sets/workflow-package-pilot/session-reviews/session-003.md
docs/session-sets/workflow-package-pilot/session-reviews/session-005.md
docs/session-sets/workflow-package-pilot/session-reviews/session-007.md
```
