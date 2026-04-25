# AccessExporterTemp1 → Dabbler Access Harvester — Import Plan

> **Session Set:** `docs/session-sets/accessexportertemp1-import/`
> **Author:** Session 1 of 6 (Discovery + Import Plan)
> **Created:** 2026-04-25
> **Status:** Draft pending human approval of "Open questions" below.

This plan dispositions every top-level folder and significant file in
`../AccessExporterTemp1/` into one of four buckets, per Session 1 of
this set's spec:

- **import-as-is** — copy with namespace rename, no behavioral change.
- **import-with-edits** — copy, then make specific edits.
- **rebuild** — concept stays, code is rewritten in this repo because
  the sibling's implementation conflicts with this repo's principles.
- **out-of-scope** — leave behind.

Sibling-source paths assume `../AccessExporterTemp1/` as the root.
Target paths in this repo follow the project-guidance convention
(`AccessHarvester.{Cli,Core}`, `tests/AccessHarvester.Tests/`, etc.).

---

## Methodology

1. Enumerated the sibling repo file tree directly via `find` to
   `maxdepth 3`, then descended into each subfolder of substance
   (`Enrichment/Vba`, `Enrichment/Macros`, `tests/.../SyntheticDbs`,
   `samples/Reports`, etc.). Verified `../AccessExporterTemp1/docs/`
   contains **only** `sessions/` (4 files, all out-of-scope) — no
   other docs subdirectories present.
2. Read `BATON.md` end-to-end. Treated as informative but not
   authoritative — the spec mandates verification against the actual
   tree.
3. Classified each entry against this repo's principles
   (`docs/planning/project-guidance.md`):
   - Lean by default — no defensive abstractions, no auto-detection
     tools that second-guess curated data, no AI-interpretation.
   - Three-tier output (`raw/` → `kb/` → `ir/`) is canon.
   - No UAT layer — UAT-emission machinery is dropped wholesale.
   - Cross-platform discipline — COM-touching code stays
     `[SupportedOSPlatform("windows")]`.

The assembly-level `[SupportedOSPlatform("windows")]` attribute is
**confirmed present at the top of `Com/AccessComSession.cs`** as
`[assembly: SupportedOSPlatform("windows")]` (line 4). Because
`[assembly: …]` is assembly-scoped regardless of which file declares
it, importing `Com/AccessComSession.cs` in Session 2 places the
Windows-only gate on the entire `AccessHarvester.Core` assembly. No
separate `AssemblyAttributes.cs` exists in the sibling. The gate
then propagates to `AccessHarvester.Cli` via project reference (CA1416
warnings on consumers are expected and non-blocking, per BATON).

---

## BATON drift / discrepancies (per Session 1 standards)

The spec requires noting any drift between BATON.md and the actual
tree.

| BATON claim | Actual | Note |
|---|---|---|
| Line 5: *"Repo is not under git. Changes are not reversible via `git reset`."* | `../AccessExporterTemp1/.git/` exists. | Either someone `git init`'d after BATON was written or BATON drifted. The CLAUDE.md / project-guidance text in **this** repo still echoes the BATON claim; that note may need updating in a later session. Importantly, this repo's import preserves the lean copy-not-symlink discipline regardless. |
| Line 98: *"`tests/Fixtures/` (Reserved; not yet used)"* | No such directory present. | Never created. Treat as out-of-scope (nothing to import). |
| BATON line 131: *"107 hermetic unit tests + 7 integration tests, **all 114 passing**"* | The actual `tests/AccessExporter.Tests/` folder has **10 `.cs` files**: 9 test files + `UatChecklist.cs` (a helper, not a test class). The session spec only names 2 hermetic test files. | Surfacing for human decision before Session 5 — see Open question #1. The "7 integration tests" claim against only 5 `SyntheticDbs/*.accdb` fixtures means some fixtures drive multiple test methods (likely the proximity fixture drives both pipeline + UAT comparisons). Session 5 should audit the actual test method count once the file lands. |

---

## Disposition table

### Top-level

| Sibling path | Disposition | Target path | Notes |
|---|---|---|---|
| `AccessExporterTemp1.slnx` | out-of-scope | — | New solution `AccessHarvester.sln` is created in Session 2. |
| `BATON.md` | out-of-scope | — | Replaced by this repo's `CLAUDE.md` + `docs/planning/*` + `docs/ai-led-session-workflow.md`. Useful as one-shot reference during the import; not committed. Note also that the BATON's "Repo is not under git" claim is stale — see drift table. |
| `.git/` | out-of-scope | — | This repo has its own `.git`. |
| `.vscode/` (`launch.json`, `tasks.json`) | out-of-scope | — | The new repo can establish its own when needed. Settings reference paths under the old solution structure, so a copy would be net-negative. |
| `.vs/` | out-of-scope | — | IDE-private state. |

### `AccessExporter.Cli/` → `AccessHarvester.Cli/`

| Sibling path | Disposition | Target path | Imported in |
|---|---|---|---|
| `AccessExporter.Cli/AccessExporter.Cli.csproj` | import-with-edits | `AccessHarvester.Cli/AccessHarvester.Cli.csproj` | Session 2 (rename project + assembly to `AccessHarvester.Cli` / `AccessHarvester.exe`). The Windows gate is inherited via the project reference to `AccessHarvester.Core`; the CLI csproj does not need its own `[SupportedOSPlatform]` attribute. |
| `AccessExporter.Cli/Program.cs` | import-with-edits | `AccessHarvester.Cli/Program.cs` | Session 2 stub → Session 3 wires `extract` → Session 4 wires `translate` + `run`. Hand-rolled arg parser preserved (per BATON: `System.CommandLine` was rejected to avoid version churn — same call holds here). |

### `AccessExporter.Core/` → `AccessHarvester.Core/`

All files have namespace `AccessExporter.Core.*` rewritten to
`AccessHarvester.Core.*`. The assembly-level
`[SupportedOSPlatform("windows")]` declaration travels with
`Com/AccessComSession.cs` (see Methodology footnote and that file's
disposition row); importing it places the gate on `AccessHarvester.Core`.
**All entries below are confirmed by the sibling filesystem; none are
speculative.**

| Sibling path | Disposition | Target path | Imported in |
|---|---|---|---|
| `AccessExporter.Core/AccessExporter.Core.csproj` | import-with-edits | `AccessHarvester.Core/AccessHarvester.Core.csproj` | Session 2 (project rename, embedded resources re-declared in Session 4). |
| `Abstractions/Abstractions.cs` (`IObjectExporter` / `IObjectTranslator` / `IObjectEnricher`) | import-as-is | `AccessHarvester.Core/Abstractions/Abstractions.cs` | Session 2 |
| `Com/AccessComSession.cs` | import-as-is | `AccessHarvester.Core/Com/AccessComSession.cs` | Session 2. **Carries the assembly-level `[assembly: SupportedOSPlatform("windows")]` attribute on line 4** — verified by file inspection. Importing this file is what places the Windows gate on `AccessHarvester.Core`. No separate `AssemblyAttributes.cs` is needed. |
| `Model/AccessNode.cs` | import-as-is | `AccessHarvester.Core/Model/AccessNode.cs` | Session 2 |
| `Pipeline.cs` | import-with-edits | `AccessHarvester.Core/Pipeline.cs` | Session 3 imports the Extract phase + stubs Translate/Enrich; Session 4 fills Translate/Enrich. |
| `Extraction/SaveAsTextExporter.cs` | import-as-is | `AccessHarvester.Core/Extraction/SaveAsTextExporter.cs` | Session 3 |
| `Extraction/QueryExporter.cs` | import-as-is | `AccessHarvester.Core/Extraction/QueryExporter.cs` | Session 3 |
| `Extraction/TableExporter.cs` | import-as-is | `AccessHarvester.Core/Extraction/TableExporter.cs` | Session 3 |
| `Extraction/RelationshipExporter.cs` | import-as-is | `AccessHarvester.Core/Extraction/RelationshipExporter.cs` | Session 3 |
| `Translation/AccessTextParser.cs` | import-as-is | `AccessHarvester.Core/Translation/AccessTextParser.cs` | Session 4. Two known BATON-flagged bugs (80-char truncation + CR/LF escape leakage) ride along; see "Known issues imported as-is" below. Lean-by-default favors deferring those fixes to a follow-on session set rather than expanding Session 4. |
| `Translation/SaveAsTextTranslator.cs` | import-as-is | `AccessHarvester.Core/Translation/SaveAsTextTranslator.cs` | Session 4 |
| `Translation/JsonPassthroughTranslator.cs` | import-as-is | `AccessHarvester.Core/Translation/JsonPassthroughTranslator.cs` | Session 4 |
| `Enrichment/GeometryEnricher.cs` | import-as-is | `AccessHarvester.Core/Enrichment/GeometryEnricher.cs` | Session 4 |
| `Enrichment/LabelResolver.cs` | import-as-is | `AccessHarvester.Core/Enrichment/LabelResolver.cs` | Session 4 |
| `Enrichment/PropertyDecoder.cs` | import-as-is | `AccessHarvester.Core/Enrichment/PropertyDecoder.cs` | Session 4 |
| `Enrichment/TextCapacityEnricher.cs` | import-as-is | `AccessHarvester.Core/Enrichment/TextCapacityEnricher.cs` | Session 4 |
| `Enrichment/VbaSignatureEnricher.cs` | import-as-is | `AccessHarvester.Core/Enrichment/VbaSignatureEnricher.cs` | Session 4 |
| `Enrichment/Vba/VbaSignatureParser.cs` | import-as-is | `AccessHarvester.Core/Enrichment/Vba/VbaSignatureParser.cs` | Session 4 |
| `Enrichment/Vba/VbaModuleSplitter.cs` | import-as-is | `AccessHarvester.Core/Enrichment/Vba/VbaModuleSplitter.cs` | Session 4 |
| `Enrichment/MacroActionsEnricher.cs` | import-as-is | `AccessHarvester.Core/Enrichment/MacroActionsEnricher.cs` | Session 4 |
| `Enrichment/Macros/MacroXmlParser.cs` | import-as-is | `AccessHarvester.Core/Enrichment/Macros/MacroXmlParser.cs` | Session 4 |
| `Resources/property-defaults.json` | import-as-is | `AccessHarvester.Core/Resources/property-defaults.json` | Session 4 (declared as embedded resource in csproj). |
| `Resources/font-metrics.json` | import-as-is | `AccessHarvester.Core/Resources/font-metrics.json` | Session 4 (declared as embedded resource in csproj). |

### `AccessExporterTemp1/` (original Gemini-era project)

Confirmed out-of-scope per spec ("Original Gemini-era code …
Confirmed out-of-scope — drop without import"). Listed for completeness
so the disposition is auditable.

| Sibling path | Disposition | Notes |
|---|---|---|
| `AccessExporterTemp1/AccessExporter.cs` | out-of-scope | Reference/history per BATON. |
| `AccessExporterTemp1/AccessJsonParser.cs` | out-of-scope | Reference/history. |
| `AccessExporterTemp1/Program.cs` | out-of-scope | Original CLI; replaced by `AccessHarvester.Cli/Program.cs`. |
| `AccessExporterTemp1/AccessExporterTemp1.csproj` (+ `.user`) | out-of-scope | — |
| `AccessExporterTemp1/Properties/launchSettings.json` | out-of-scope | — |
| `AccessExporterTemp1/AccessMetaData/{HomeHealthCare,MiniComplex,NorthwindDevEdition,ProjectBoard}/{Forms,Reports,Macros,ModulesAndClasses}/` | out-of-scope | Per-app run-output dumps from prior runs; we regenerate on demand. |
| `AccessExporterTemp1/{HomeHealthCare,MiniComplex,NorthwindDevEdition,ProjectBoard}.accdb` | out-of-scope | Duplicates of the same `.accdb` files in `samples/`. The `samples/` copies are the canonical source; this folder's are stale. |

### `samples/`

| Sibling path | Disposition | Target path | Imported in / Notes |
|---|---|---|---|
| `samples/HomeHealthCare.accdb` | import-as-is | `samples/HomeHealthCare.accdb` | Session 6 |
| `samples/MiniComplex.accdb` | import-as-is | `samples/MiniComplex.accdb` | **Session 3** (named in spec as the smallest reasonable smoke-test input for `extract`) and Session 6 confirms it's already there. |
| `samples/NorthwindDevEdition.accdb` | import-as-is | `samples/NorthwindDevEdition.accdb` | Session 6 (target of `validate_against_yaml.py`). |
| `samples/ProjectBoard.accdb` | import-as-is | `samples/ProjectBoard.accdb` | Session 6 |
| `samples/FormAndReportDefaults.accdb` | import-as-is | `samples/FormAndReportDefaults.accdb` | Session 6 — **explicitly named in spec Feature 4**, no human gate needed. Ground-truth source for `property-defaults.json`; keeping it lets the human re-run `tools/generate_property_defaults.py` against newer Access versions. |
| `samples/MasterDbPropertyExtractor.accdb` | import-as-is *(pending OQ #2 — drops to out-of-scope if human elects to drop)* | `samples/MasterDbPropertyExtractor.accdb` | Session 6. VBA tool that exports control properties to YAML; only needed to regenerate the YAML dumps. Recommend keep. |
| `samples/FormsAndReportsYaml/FormAndReportDefaults.accdb.yaml` | import-as-is *(pending OQ #2 — drops to out-of-scope if human elects to drop)* | `samples/FormsAndReportsYaml/FormAndReportDefaults.accdb.yaml` | Session 6. Consumed by `tools/generate_property_defaults.py`. |
| `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml` | import-as-is *(pending OQ #2 — drops to out-of-scope if human elects to drop)* | `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml` | Session 6. Consumed by `tools/validate_against_yaml.py`. |
| `samples/Reports/orders-by-customer.rdf.json` | out-of-scope | — | Per BATON: "Target-platform `.rdf.json` examples (user-provided; NOT this tool's output format)". This is downstream-tool target schema, irrelevant to the harvester's mission. |
| `samples/Reports/sales-by-product.rdf.json` | out-of-scope | — | Same reasoning. |

### `tools/`

| Sibling path | Disposition | Target path | Imported in / Notes |
|---|---|---|---|
| `tools/generate_property_defaults.py` | import-as-is | `tools/generate_property_defaults.py` | **Session 4** (per spec — travels with `PropertyDecoder` since the resource it generates is the contract). The script is non-runnable until its YAML inputs land in Session 6 (pending OQ #2). That's acceptable: the embedded `Resources/property-defaults.json` resource itself **is** imported in Session 4 and is the contract. The script's role is regeneration, not first-time generation. |
| `tools/validate_against_yaml.py` | import-as-is | `tools/validate_against_yaml.py` | Session 6 |
| `tools/reports/northwind-validation.md` | out-of-scope | — | Generated artifact. We regenerate when needed; carrying it adds noise without source-of-truth status. |
| `tools/__pycache__/` | out-of-scope | — | Build artifact. |

### `test-builder/`

Per spec: imported as Session 5's Python utility. Not executed as part
of `dotnet test`.

| Sibling path | Disposition | Target path | Imported in |
|---|---|---|---|
| `test-builder/builder.py` | import-as-is | `test-builder/builder.py` | Session 5 |
| `test-builder/create_from_spec.py` | import-as-is | `test-builder/create_from_spec.py` | Session 5 |
| `test-builder/build_synthetic.py` | import-as-is | `test-builder/build_synthetic.py` | Session 5 |
| `test-builder/README.md` | import-as-is | `test-builder/README.md` | Session 5 |
| `test-builder/specs/geometry_basic.json` | import-as-is | `test-builder/specs/geometry_basic.json` | Session 5 |
| `test-builder/specs/labels_attached.json` | import-as-is | `test-builder/specs/labels_attached.json` | Session 5 |
| `test-builder/specs/labels_proximity.json` | import-as-is | `test-builder/specs/labels_proximity.json` | Session 5 |
| `test-builder/specs/labels_from_name.json` | import-as-is | `test-builder/specs/labels_from_name.json` | Session 5 |
| `test-builder/specs/labels_binding.json` | import-as-is | `test-builder/specs/labels_binding.json` | Session 5 |
| `test-builder/__pycache__/` | out-of-scope | — | Build artifact. |

### `tests/AccessExporter.Tests/` → `tests/AccessHarvester.Tests/`

| Sibling path | Disposition | Target path | Imported in / Notes |
|---|---|---|---|
| `AccessExporter.Tests.csproj` | import-with-edits | `tests/AccessHarvester.Tests/AccessHarvester.Tests.csproj` | Session 5 (project + assembly rename; `<None Update>` rules for `SyntheticDbs/` retained; `UatGroundTruth/` rule removed). |
| `GeometryEnricherTests.cs` | import-as-is | `tests/AccessHarvester.Tests/GeometryEnricherTests.cs` | Session 5 (per spec) |
| `LabelResolverTests.cs` | import-as-is | `tests/AccessHarvester.Tests/LabelResolverTests.cs` | Session 5 (per spec) |
| `SyntheticDbIntegrationTests.cs` | import-with-edits | `tests/AccessHarvester.Tests/SyntheticDbIntegrationTests.cs` | Session 5. Edits: (a) drop the proximity-test UAT-emission path that calls `UatChecklist.CollectProximityItems` (the `UatChecklist` helper is out-of-scope per spec); (b) **simplify the proximity test method to a pipeline-runs-and-produces-output assertion** — same shape as the other label integration tests, minus the UAT comparison. The `SyntheticDbs/labels_proximity.accdb` fixture is retained so a future label-quality session set can re-introduce richer assertions. The geometry + three non-proximity label tests (`labels_attached`, `labels_from_name`, `labels_binding`) survive intact. |
| `AccessTextParserTests.cs` | import-as-is *(pending OQ #1 — drops to out-of-scope if human elects to drop)* | `tests/AccessHarvester.Tests/AccessTextParserTests.cs` | Session 5. Hermetic; covers `AccessTextParser` which IS imported. |
| `MacroXmlParserTests.cs` | import-as-is *(pending OQ #1 — drops to out-of-scope if human elects to drop)* | `tests/AccessHarvester.Tests/MacroXmlParserTests.cs` | Session 5. Hermetic; covers `MacroXmlParser` which IS imported. |
| `PropertyDecoderTests.cs` | import-as-is *(pending OQ #1 — drops to out-of-scope if human elects to drop)* | `tests/AccessHarvester.Tests/PropertyDecoderTests.cs` | Session 5. Hermetic; covers `PropertyDecoder` which IS imported. |
| `TextCapacityEnricherTests.cs` | import-as-is *(pending OQ #1 — drops to out-of-scope if human elects to drop)* | `tests/AccessHarvester.Tests/TextCapacityEnricherTests.cs` | Session 5. Hermetic; covers `TextCapacityEnricher` which IS imported. |
| `VbaSignatureParserTests.cs` | import-as-is *(pending OQ #1 — drops to out-of-scope if human elects to drop)* | `tests/AccessHarvester.Tests/VbaSignatureParserTests.cs` | Session 5. Hermetic; covers `VbaSignatureParser` which IS imported. |
| `UatChecklist.cs` | out-of-scope | — | UAT-emission machinery. |
| `UatChecklistTests.cs` | out-of-scope | — | UAT-emission machinery. |
| `UatGroundTruth/labels_proximity.uat.json` | out-of-scope | — | UAT machinery. |
| `UatEmitted/` | out-of-scope (and would not exist after a clean checkout anyway) | — | Generated each test run. |
| `SyntheticDbs/geometry_basic.accdb` | import-as-is | `tests/AccessHarvester.Tests/SyntheticDbs/geometry_basic.accdb` | Session 5 |
| `SyntheticDbs/labels_attached.accdb` | import-as-is | `tests/AccessHarvester.Tests/SyntheticDbs/labels_attached.accdb` | Session 5 |
| `SyntheticDbs/labels_binding.accdb` | import-as-is | `tests/AccessHarvester.Tests/SyntheticDbs/labels_binding.accdb` | Session 5 |
| `SyntheticDbs/labels_from_name.accdb` | import-as-is | `tests/AccessHarvester.Tests/SyntheticDbs/labels_from_name.accdb` | Session 5 |
| `SyntheticDbs/labels_proximity.accdb` | import-as-is | `tests/AccessHarvester.Tests/SyntheticDbs/labels_proximity.accdb` | Session 5 |

### `sample-output/` and `docs/sessions/`

| Sibling path | Disposition | Notes |
|---|---|---|
| `sample-output/AccessMetaData/FormAndReportDefaults/{raw,kb,ir}/...` | out-of-scope | Confirmed in spec — regenerate on demand. |
| `sample-output/AccessMetaData/NorthwindDevEdition/{raw,kb,ir}/...` | out-of-scope | Same. |
| `docs/sessions/01-northwind-yaml-validation.md` | out-of-scope | Sibling-repo session-handoff artifact; this repo has its own session-set workflow. |
| `docs/sessions/02-char-line-capacity.md` | out-of-scope | Same. |
| `docs/sessions/03-vba-signature-enricher.md` | out-of-scope | Same. |
| `docs/sessions/04-macro-actions-enricher.md` | out-of-scope | Same. |

---

## Per-session imports at a glance

A condensed view of which session lands what, derived from the
disposition table above. This is for orchestrator sanity-check; the
spec is the source of truth.

- **Session 2 — Solution skeleton + Core abstractions + COM session:**
  `AccessHarvester.sln`, `AccessHarvester.Core.csproj`,
  `Abstractions/Abstractions.cs`, `Com/AccessComSession.cs`,
  `Model/AccessNode.cs`, `AccessHarvester.Cli.csproj`,
  stub `Program.cs`.
- **Session 3 — Extraction phase:**
  4 extractors, `Pipeline.cs` (extract-only, translate/enrich stubs),
  CLI `extract` subcommand, `samples/MiniComplex.accdb`.
- **Session 4 — Translation + Enrichment phases:**
  3 translators, 6 enricher entry-point files (`GeometryEnricher`,
  `LabelResolver`, `PropertyDecoder`, `TextCapacityEnricher`,
  `VbaSignatureEnricher`, `MacroActionsEnricher`) plus 3 nested
  helper / parser files (`Vba/VbaSignatureParser.cs`,
  `Vba/VbaModuleSplitter.cs`, `Macros/MacroXmlParser.cs`), 2 embedded
  resources, full `Pipeline.cs`,
  CLI `translate` + `run` subcommands, `tools/generate_property_defaults.py`.
- **Session 5 — Tests + Synthetic DB Builder:**
  `AccessHarvester.Tests.csproj`, `GeometryEnricherTests.cs`,
  `LabelResolverTests.cs`, `SyntheticDbIntegrationTests.cs` (with
  proximity-UAT path elided), 5 `SyntheticDbs/*.accdb` fixtures,
  full `test-builder/` Python project. **Pending Open question #1:**
  also `AccessTextParserTests.cs`, `MacroXmlParserTests.cs`,
  `PropertyDecoderTests.cs`, `TextCapacityEnricherTests.cs`,
  `VbaSignatureParserTests.cs`.
- **Session 6 — Samples + Tools + README:**
  remaining `samples/*.accdb` (including `FormAndReportDefaults.accdb`,
  spec-required), `tools/validate_against_yaml.py`, root `README.md`,
  `change-log.md` for this session set. **Pending Open question #2:**
  `samples/MasterDbPropertyExtractor.accdb`, `samples/FormsAndReportsYaml/`.

---

## Open questions for the human

These must be answered before Session 2 begins.

### #1 — Five additional hermetic unit-test files

The spec's Session 5 step list names only `GeometryEnricherTests.cs`
and `LabelResolverTests.cs` as the hermetic unit tests to import. The
sibling actually has **five more** hermetic unit-test files, every one
of which exercises code we are importing in Sessions 3–4:

- `AccessTextParserTests.cs` — covers `Translation/AccessTextParser.cs`
- `MacroXmlParserTests.cs` — covers `Enrichment/Macros/MacroXmlParser.cs`
- `PropertyDecoderTests.cs` — covers `Enrichment/PropertyDecoder.cs`
- `TextCapacityEnricherTests.cs` — covers `Enrichment/TextCapacityEnricher.cs`
- `VbaSignatureParserTests.cs` — covers `Enrichment/Vba/VbaSignatureParser.cs`

Almost certainly a spec oversight. Importing the production code without
its hermetic tests would weaken our regression coverage from the
sibling's claimed 107 hermetic unit tests down to whatever the two
named files provide. **Recommendation: import all five as-is in Session
5.** A no-import answer is fine but should be deliberate.

**Question for human:** Do we import all five additional hermetic
unit-test files, drop them, or pick a subset?

### #2 — `MasterDbPropertyExtractor.accdb` + `FormsAndReportsYaml/`

The dev-tool chain that supports regeneration of the
`property-defaults.json` resource and the Northwind YAML validation
harness:

- `samples/MasterDbPropertyExtractor.accdb` — VBA tool that exports
  control properties to YAML.
- `samples/FormsAndReportsYaml/{FormAndReportDefaults,NorthwindDevEdition}.accdb.yaml`
  — the YAML dumps consumed by `tools/generate_property_defaults.py`
  (defaults source) and `tools/validate_against_yaml.py` (Northwind
  comparison).

(Note: `samples/FormAndReportDefaults.accdb` is **not** part of this
question — the spec's Feature 4 explicitly lists it as a Session 6
import. It travels regardless.)

Two valid stances for the items above:

- **Keep so we can re-run as Access versions evolve.** The defaults
  table becomes regeneratable, and the YAML validation harness keeps
  working. **Recommendation.**
- **Drop and rebuild on demand.** Treat `property-defaults.json` as
  frozen and re-acquire the source artifacts only if a future Access
  version forces regeneration.

**Question for human:** Keep both, drop both, or split (e.g., keep the
YAML dumps but drop `MasterDbPropertyExtractor.accdb` until needed)?

### #3 — Replacement for label-quality UAT review

The sibling's `UatChecklist.cs` / `UatEmitted/` / `UatGroundTruth/`
machinery supports a human-graded review of label-resolution quality
on the proximity test. This repo deliberately has no UAT layer.
**Three stances:**

- **a) Replace with verifier-driven review of a sampled IR run.**
  The end-of-session cross-provider verifier reads a sample of
  `ir/` output and flags label-resolution oddities.
- **b) Drop the concept entirely.** Trust the
  `SyntheticDbIntegrationTests.cs` proximity assertions as the only
  gate; downstream AI consumer tolerates the residual error rate.
- **c) Defer.** Drop here, but track in a future session set if the
  proximity hit rate proves a problem. **Recommendation** —
  the proximity tests already serve as a regression guard, and
  introducing a verifier-driven label-quality pass before we even know
  which `.accdb` inputs land first is engineering ahead of the need.

**Question for human:** a, b, or c?

---

## Cross-reference: confirming questions from the spec

The spec's "Open questions parked for human" section names four:

1. Label-quality review → **Open question #3** above.
2. `MasterDbPropertyExtractor.accdb` + `FormsAndReportsYaml/` →
   **Open question #2** above.
3. Original Gemini-era code → **already decided in the spec's Feature
   1 scope** ("includes the `AccessExporterTemp1/AccessExporterTemp1/`
   original-Gemini-code folder"). Treated here as a confirmatory note
   in the disposition table, not a blocking question — same handling
   as `sample-output/`.
4. `sample-output/` → confirmed out-of-scope; no further question.

This plan adds one new question the spec did not anticipate
(**#1** the five additional hermetic test files surfaced by
file-tree-vs-spec drift). The two known `AccessTextParser.cs` bugs
(80-char truncation + CR/LF escape leakage) are recorded in "Known
issues imported as-is" rather than gated as a blocking question:
lean-by-default favors deferring follow-on fixes to a future session
set unless the human explicitly pulls them forward.

---

## Known issues imported as-is (deferred to future session sets)

These are issues BATON Session 01's validator surfaced that this
session set knowingly inherits without fixing. None block the import.
The natural home for the fix work is the
`docs/session-sets/remaining-work-planner/` set or a follow-on Phase C+
session set.

| # | Issue | Origin | Why deferred |
|---|---|---|---|
| 1 | **80-char free-text truncation** in `AccessTextParser`. `Caption` / `ControlTipText` / `StatusBarText` etc. silently lose the tail past 80 chars (12 hits in Northwind). | BATON line 168 | Lean-by-default — defer to a follow-on session set. Human can pull forward into Session 4 with an explicit ask. |
| 2 | **CR/LF escape leakage** in `AccessTextParser`. SaveAsText writes literal `\015\012` where DAO normalizes to `\n` (~14 free-text properties affected in Northwind). | BATON line 169 | Lean-by-default — defer (same scope as #1; pair them when the fix lands). |
| 3 | **`HasModule` derivation.** Currently falls back to the boolean default (False), but every form with a VBA module shows True via DAO. SaveAsText omits the property, so it should be derived from the presence of the form's `ModulesAndClasses` output rather than from `property-defaults.json`. | BATON line 170 | Small targeted enricher; not in this set's scope. |
| 4 | **Format-mask normalization.** SaveAsText keeps the literal mask (`$#,##0.00;($#,##0.00)`); DAO reports the canonical name (`Currency`). Easier downstream if normalized via a small lookup table. | BATON line 171 | Small string enricher; not in this set's scope. |
| 5 | **`ColumnOrder` 0-vs-1-based.** Convention difference between SaveAsText (0-based) and DAO (1-based). | BATON line 172 | Either skip-list or `+1` normalizer; not in this set's scope. |
| 6 | **`SourceObject` prefix trim.** Subforms/subreports show as `Form.<name>` / `Report.<name>` in the IR; DAO strips the prefix. | BATON line 173 | Small string enricher; not in this set's scope. |

These are all under Phase C+ in the BATON's "What's next" enumeration,
and the BATON itself describes them as "small enough to ship as its
own slice". Carrying them forward as known-issues ledger here means
the `remaining-work-planner` session set can pick them up with full
context.
