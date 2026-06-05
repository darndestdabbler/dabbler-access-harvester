# Disposition Manifest — 005 Northwind Embedded-Residue Remediation (Session 1)

> **Authored:** Session 1 (2026-06-05). **Paths only — no proprietary content.**
> The reviewed per-file disposition for every tracked path carrying a
> NorthwindDevEdition structural (or denser) identifier at `HEAD`, grounded in
> `003`'s 123-path inventory refreshed against the current tree **plus** a fresh
> `HEAD` scan. Companion machine-readable file: [`disposition-manifest.csv`](disposition-manifest.csv).

## How the refreshed inventory was built

1. **Authoritative floor:** the reviewed 123-path appendix in
   `003`'s [`embedded-residue-findings.md`](../003-northwind-proprietary-data-remediation/embedded-residue-findings.md).
   Of those, **117 are still tracked** at `HEAD`; **6 were purged by `004`**
   (the symbol-DB SQLite + consistency artifacts; the 7th, `correlation-scatter.png`,
   was a binary never caught by the text scan).
2. **Fresh `HEAD` scan:** `git grep -liE` over a distinctive Northwind
   structural-identifier set (prefixed Access object names — `frmCompanyDetail`,
   `qryCompanies`, `sfrmCompanyDetail`, `clsErrorHandler`, `modDAO`,
   `EmployeePrivileges`, the `AddAnOrder` / `UpdateAProduct` / `UpdateAnOrder`
   workflow names, etc.), excluding the local-only gitignored Northwind golden/YAML
   trees. The brand token `Northwind` alone was **deliberately excluded** — it
   matches every doc that merely names the fixture and is not itself structural
   disclosure.
3. **Triage of fresh-only hits:** each hit not already in the `003` appendix was
   inspected individually. Genuine structural/logic residue was folded into the
   inventory; **generic-example matches** (e.g. the textbook `[Order Details].[Quantity]`
   in `JetSql.g4`, generic `frmStartup`/`rptInvoice` example names in core source
   and consumer docs) were recorded as **keep-accepted, not residue**.

## Material finding — the order-entry PILOT ARC (maintainer-authorized scope expansion)

The fresh scan surfaced a coupled subsystem absent from both the `003` inventory
and the `005` spec's enumeration: the **4** `workflow-package-pilot` packages (spec
named only 3 — `update-an-employee.md` was missed), the full
`AccessHarvester.CharacterizationTests` tree (**33** files, larger than the spec's
"~28"), the `blazor-add-an-order` + `blazor-update-workflows` session-sets, and
`output-tests/blazor-add-an-order/` — a **buildable MudBlazor project wired into
`AccessHarvester.sln`**. These are **leaf projects** (no harvester-core dependents)
forming one chain — *packages → characterization tests → the Blazor app whose
success criterion is that those tests pass* — that encodes full Northwind
order/product/employee **business logic** (denser than structural names → triggers
the spec Stop Rule). **Maintainer decision (2026-06-05): delete the whole arc** as
in-scope spent proprietary-logic residue. This resolves Session 2's test-coupling
(the characterization tests are deleted with their sole consumer rather than
re-based).

## Summary

| Action | Count | When | Meaning |
|---|---:|---|---|
| **delete** | 198 | Session 1 (this session) | Spent process artifacts, workflow packages, and the order-entry pilot arc — removed at `HEAD` |
| **edit** | 1 | Session 1 | `AccessHarvester.sln` — remove the deleted arc's project entries |
| **re-base** | 26 | Session 2 | Corpus-pinned tests — re-base onto HomeHealthCare or delete; never weaken an assertion |
| **genericize** | 7 | Session 3 | C# source — genericize `RunForNorthwind` / redact comment+string refs; no behavior change |
| **redact** | 20 | Session 3 | Reference/planning docs, live tooling, parser test-pairs, and legit harvester-dev records naming proprietary fixtures |
| **keep-accepted** | 3 | — | Immutable raw verifier output (cannot be edited per workflow) — recorded accepted residue |
| **keep-accepted (generic)** | 25 | — | Generic-example identifiers, evaluated and found **not** to be NorthwindDevEdition structural disclosure |

## Session 1 actions (executed this session)

### DELETE — 198 path(s)

*Session S1 — Order-entry PILOT ARC — full Northwind order/product/employee business logic (packages + characterization tests + blazor pilot app); maintainer-authorized whole-arc delete*

- `docs/session-sets/blazor-add-an-order/activity-log.json`
- `docs/session-sets/blazor-add-an-order/ai-assignment.md`
- `docs/session-sets/blazor-add-an-order/change-log.md`
- `docs/session-sets/blazor-add-an-order/session-reviews/session-001.md`
- `docs/session-sets/blazor-add-an-order/session-reviews/session-002.md`
- `docs/session-sets/blazor-add-an-order/session-reviews/session-003.md`
- `docs/session-sets/blazor-add-an-order/session-state.json`
- `docs/session-sets/blazor-add-an-order/spec.md`
- `docs/session-sets/blazor-update-workflows/session-state.json`
- `docs/session-sets/blazor-update-workflows/spec.md`
- `docs/session-sets/workflow-package-pilot/_template.md`
- `docs/session-sets/workflow-package-pilot/activity-log.json`
- `docs/session-sets/workflow-package-pilot/ai-assignment.md`
- `docs/session-sets/workflow-package-pilot/change-log.md`
- `docs/session-sets/workflow-package-pilot/cross-workflow-audit.md`
- `docs/session-sets/workflow-package-pilot/handoff.md`
- `docs/session-sets/workflow-package-pilot/inventory.md`
- `docs/session-sets/workflow-package-pilot/issue-logs/session-001.json`
- `docs/session-sets/workflow-package-pilot/issue-logs/session-002.json`
- `docs/session-sets/workflow-package-pilot/issue-logs/session-004.json`
- `docs/session-sets/workflow-package-pilot/issue-logs/session-007.json`
- `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
- `docs/session-sets/workflow-package-pilot/packages/update-a-product.md`
- `docs/session-sets/workflow-package-pilot/packages/update-an-employee.md`
- `docs/session-sets/workflow-package-pilot/packages/update-an-order.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-001.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-002.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-003.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-004.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-005.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-006.md`
- `docs/session-sets/workflow-package-pilot/session-reviews/session-007.md`
- `docs/session-sets/workflow-package-pilot/session-state.json`
- `docs/session-sets/workflow-package-pilot/spec.md`
- `output-tests/blazor-add-an-order/BlazorAddAnOrder.csproj`
- `output-tests/blazor-add-an-order/Contracts/CustomerLookupResponse.cs`
- `output-tests/blazor-add-an-order/Contracts/HealthResponse.cs`
- `output-tests/blazor-add-an-order/Contracts/OrderRequest.cs`
- `output-tests/blazor-add-an-order/Contracts/OrderResponse.cs`
- `output-tests/blazor-add-an-order/Contracts/ValidationProblem.cs`
- `output-tests/blazor-add-an-order/Program.cs`
- `output-tests/blazor-add-an-order/Properties/launchSettings.json`
- `output-tests/blazor-add-an-order/Services/Controllers/CustomersController.cs`
- `output-tests/blazor-add-an-order/Services/Controllers/OrdersController.cs`
- `output-tests/blazor-add-an-order/Services/Controllers/ProductsController.cs`
- `output-tests/blazor-add-an-order/Services/Domain/InventoryAllocationService.cs`
- `output-tests/blazor-add-an-order/Services/Domain/OrderCreationService.cs`
- `output-tests/blazor-add-an-order/Services/Domain/OrderDraft.cs`
- `output-tests/blazor-add-an-order/Services/Domain/OrderEnums.cs`
- `output-tests/blazor-add-an-order/Services/Domain/OrderLineItemService.cs`
- `output-tests/blazor-add-an-order/Services/HealthController.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/DatabaseSeeder.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/Entities/CustomerEntity.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/Entities/OrderEntity.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/Entities/OrderLineItemEntity.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/Entities/ProductEntity.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/Mappings/OrderMappings.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/OrdersDbContext.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/OrdersStore.cs`
- `output-tests/blazor-add-an-order/Services/Persistence/PersistenceCatalog.cs`
- `output-tests/blazor-add-an-order/Ui/App.razor`
- `output-tests/blazor-add-an-order/Ui/MainLayout.razor`
- `output-tests/blazor-add-an-order/Ui/Pages/Generated/FrmBudgetServicePlan.razor`
- `output-tests/blazor-add-an-order/Ui/Pages/Generated/FrmBudgetServicePlan.razor.cs`
- `output-tests/blazor-add-an-order/Ui/Pages/Generated/README.md`
- `output-tests/blazor-add-an-order/Ui/Pages/Home.razor`
- `output-tests/blazor-add-an-order/Ui/Pages/OrderEntryPage.razor`
- `output-tests/blazor-add-an-order/Ui/Routes.razor`
- `output-tests/blazor-add-an-order/Ui/_Imports.razor`
- `output-tests/blazor-add-an-order/appsettings.Development.json`
- `output-tests/blazor-add-an-order/appsettings.json`
- `tests/AccessHarvester.CharacterizationTests/AccessHarvester.CharacterizationTests.csproj`
- `tests/AccessHarvester.CharacterizationTests/AddAClient/AddAClientWorkflowModels.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientCreationBehaviorTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientLifecyclePolicyTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientValidationBehaviorTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAClient/Session2FalsificationTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/AddAnOrderWorkflowModels.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/InventoryAllocationBehaviorTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderCreationBehaviorTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/OrderLineItemBehaviorTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/README.md`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3CrossOrderScopeTests.cs`
- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/Session3DeletionReallocationTests.cs`
- `tests/AccessHarvester.CharacterizationTests/ScaffoldTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductCodeFormattingTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductDeletePolicyTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductInventoryAllocationTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductReorderPolicyTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductReorderQuantityTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductReorderRoutingTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/ProductSaveValidationTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/README.md`
- `tests/AccessHarvester.CharacterizationTests/UpdateAProduct/UpdateAProductWorkflowModels.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeeDeletePolicyTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeeIdentityConstraintTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeePrivilegeAssignmentTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/EmployeeSaveValidationTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/README.md`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnEmployee/UpdateAnEmployeeWorkflowModels.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderEditPolicyBehaviorTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/OrderStatusTransitionBehaviorTests.cs`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/README.md`
- `tests/AccessHarvester.CharacterizationTests/UpdateAnOrder/UpdateAnOrderWorkflowModels.cs`
- `tools/session_verify_blazor_session1.py`

*Session S1 — Spent experimental/process arc whose content is proprietary procedure/workflow/comprehension residue*

- `docs/session-sets/dabbler-platform-discovery/activity-log.json`
- `docs/session-sets/dabbler-platform-discovery/ai-assignment.md`
- `docs/session-sets/dabbler-platform-discovery/change-log.md`
- `docs/session-sets/dabbler-platform-discovery/disposition.json`
- `docs/session-sets/dabbler-platform-discovery/next-orc-recommendation-session-2.txt`
- `docs/session-sets/dabbler-platform-discovery/next-orc-recommendation-session-3.txt`
- `docs/session-sets/dabbler-platform-discovery/next-session-set-recommendation.md`
- `docs/session-sets/dabbler-platform-discovery/session-1-driver.py`
- `docs/session-sets/dabbler-platform-discovery/session-1-fixup.py`
- `docs/session-sets/dabbler-platform-discovery/session-2-driver.py`
- `docs/session-sets/dabbler-platform-discovery/session-3-changelog-driver.py`
- `docs/session-sets/dabbler-platform-discovery/session-3-driver.py`
- `docs/session-sets/dabbler-platform-discovery/session-3-next-set-driver.py`
- `docs/session-sets/dabbler-platform-discovery/session-3-verify.py`
- `docs/session-sets/dabbler-platform-discovery/session-events.jsonl`
- `docs/session-sets/dabbler-platform-discovery/session-reviews/session-001.md`
- `docs/session-sets/dabbler-platform-discovery/session-reviews/session-002.md`
- `docs/session-sets/dabbler-platform-discovery/session-reviews/session-003.md`
- `docs/session-sets/dabbler-platform-discovery/session-state.json`
- `docs/session-sets/dabbler-platform-discovery/smoke-test-frmOrderDetails.md`
- `docs/session-sets/dabbler-platform-discovery/spec.md`
- `docs/session-sets/dabbler-platform-discovery/third-opinion-issue-4.md`
- `docs/session-sets/dabbler-platform-discovery/third-opinion.py`
- `docs/session-sets/dabbler-platform-discovery/verify-round-3.py`
- `docs/session-sets/vba-symbol-db-path-b-probe/activity-log.json`
- `docs/session-sets/vba-symbol-db-path-b-probe/ai-assignment.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/change-log.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/decision-plain-english.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/decision.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/peer-review/test-run-session-3.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/peer-review/translation-review.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/session-reviews/session-001.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/session-reviews/session-002.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/session-reviews/session-003.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/session-state.json`
- `docs/session-sets/vba-symbol-db-path-b-probe/spec.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/translation-packet.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/translation/symbol-db-gaps.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/translation/test-run-session-2.md`
- `docs/session-sets/vba-symbol-db-path-b-probe/translation/translation-rationale.md`
- `docs/session-sets/vba-symbol-db/activity-log.json`
- `docs/session-sets/vba-symbol-db/ai-assignment.md`
- `docs/session-sets/vba-symbol-db/change-log.md`
- `docs/session-sets/vba-symbol-db/interpretation-timing-and-cost.md`
- `docs/session-sets/vba-symbol-db/issue-logs/session-001.json`
- `docs/session-sets/vba-symbol-db/issue-logs/session-003.json`
- `docs/session-sets/vba-symbol-db/issue-logs/session-004.json`
- `docs/session-sets/vba-symbol-db/probe-results/comprehension-packet.md`
- `docs/session-sets/vba-symbol-db/probe-results/probe-gemini-pro.md`
- `docs/session-sets/vba-symbol-db/probe-results/probe-gemini-pro.meta.json`
- `docs/session-sets/vba-symbol-db/probe-results/probe-gpt-5-4.md`
- `docs/session-sets/vba-symbol-db/probe-results/probe-gpt-5-4.meta.json`
- `docs/session-sets/vba-symbol-db/probe-results/probe-opus.md`
- `docs/session-sets/vba-symbol-db/probe-results/reference-description.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-001.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002-prompt.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002-r2-prompt.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002-r3-prompt.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002-round-2.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002-round-3.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002-tiebreaker-m2-prompt.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002-tiebreaker-m2.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-002.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-003-prompt.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-003-r2-prompt.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-003.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-004.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-005.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-006.md`
- `docs/session-sets/vba-symbol-db/session-reviews/session-3-rec.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-4-rec.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-5-orchestrator-rec.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-5-rec.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-6-orchestrator-rec.txt`
- `docs/session-sets/vba-symbol-db/session-reviews/session-7-orchestrator-rec.txt`
- `docs/session-sets/vba-symbol-db/session-state.json`
- `docs/session-sets/vba-symbol-db/spec.md`
- `docs/session-sets/vba-symbol-db/verdict.md`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/SESSION-1-FALSE-START.md`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/activity-log.json`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/ai-assignment.md`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/archive/superseded_architecture.md`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/change-log.md`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/session-reviews/session-001.md`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/session-state.json`
- `docs/session-sets/vba-symbol-resolution-and-enrichment/spec.md`

*Session S1 — Spent one-shot driver/probe/PoC script (no live caller)*

- `tools/archive/session1_redo_route_architecture.py`
- `tools/archive/session2_verify_session1.py`
- `tools/run-verification-session2-r3.py`
- `tools/run-verification-session2.py`
- `tools/symbol-db/probe/build_packet.py`
- `tools/symbol-db/probe/run_probe.py`
- `tools/symbol-resolver/test_poc.py`

### EDIT — 1 path(s)

*Session S1 — Remove arc solution entries (AccessHarvester.CharacterizationTests, output-tests folder, BlazorAddAnOrder) via `dotnet sln remove`; all other projects untouched*

- `AccessHarvester.sln`

## Deferred actions (classified now, executed in later sessions)

### RE-BASE (Session 2) — 26 path(s)

*Session S2 — Corpus-pinned test — Session 2 triage: re-base onto HHC or delete; never weaken a live assertion*

- `tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs`
- `tests/AccessHarvester.Integration/Pipeline/GoldenCorpusRegenerator.cs`
- `tests/AccessHarvester.SymbolDb.Tests/AccessHarvester.SymbolDb.Tests.csproj`
- `tests/AccessHarvester.SymbolDb.Tests/BodyHasherTests.cs`
- `tests/AccessHarvester.SymbolDb.Tests/CrossModuleResolutionTests.cs`
- `tests/AccessHarvester.SymbolDb.Tests/DeferralPreFilterTests.cs`
- `tests/AccessHarvester.SymbolDb.Tests/FixtureLoader.cs`
- `tests/AccessHarvester.SymbolDb.Tests/GenerateArtifacts.cs`
- `tests/AccessHarvester.SymbolDb.Tests/IntraProcedureDeadCodeDetectorTests.cs`
- `tests/AccessHarvester.SymbolDb.Tests/NorthwindFixtureSmokeTests.cs`
- `tests/AccessHarvester.SymbolDb.Tests/PersistenceAndDedupTests.cs`
- `tests/AccessHarvester.SymbolDb.Tests/ReferencesExtractorTests.cs`
- `tests/AccessHarvester.SymbolDb.Tests/Session3ExtractorTests.cs`
- `tests/AccessHarvester.Tests/ReportBaseline/RecordSourceResolverTests.cs`
- `tests/AccessHarvester.Tests/ReportBaseline/ReportAggregateComputerTests.cs`
- `tests/AccessHarvester.Tests/ReportBaseline/ReportAggregatesWriterTests.cs`
- `tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineServiceTests.cs`
- `tests/AccessHarvester.Tests/SyntheticDbIntegrationTests.cs`
- `tests/AccessHarvester.TranslationProbe.Tests/AccessHarvester.TranslationProbe.Tests.csproj`
- `tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbe.cs`
- `tests/AccessHarvester.TranslationProbe.Tests/FrmCompanyDetailTranslationProbeTests.cs`
- `tests/AccessHarvester.TranslationProbe.Tests/Session3RegressionTests.cs`
- `tests/AccessHarvester.Vba.Resolver.Tests/CrossReference/CrossReferenceFixtureTests.cs`
- `tests/AccessHarvester.Vba.Resolver.Tests/References/ResolverFixtureAnnotationTests.cs`
- `tests/AccessHarvester.Vba.Resolver.Tests/ScopeBuildingFixtureTests.cs`
- `tests/AccessHarvester.Vba.Resolver.Tests/Types/TypeInferenceFixtureTests.cs`

### GENERICIZE (Session 3) — 7 path(s)

*Session S3 — C# source: genericize RunForNorthwind / redact comment+string references; no behavior change*

- `AccessHarvester.Access/TypeLibraryRegistry.cs`
- `AccessHarvester.Pipeline/Cleanup/NamePatternDetector.cs`
- `AccessHarvester.Pipeline/Manifest/DatabaseInfo.cs`
- `AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs`
- `AccessHarvester.SymbolDb/Extraction/LocalVariableExtractor.cs`
- `AccessHarvester.SymbolDb/Reporting/MeasurementReportBuilder.cs`
- `AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs`

### REDACT (Session 3) — 20 path(s)

*Session S3 — Reference/planning doc, live tooling, or parser test-pair with incidental Northwind names — redact to generic/HHC*

- `docs/cross-repo/access-to-platform-migration-map.md`
- `docs/harvester/install.md`
- `docs/harvester/interpretation-guide.md`
- `docs/homehealthcare-northwind-comparison.md`
- `docs/lowdefy-evaluation/prompt-for-codex.md`
- `docs/planning/strategy.md`
- `docs/session-sets/generalization-validation-on-non-northwind/inventory.md`
- `docs/test-cases/06-calculated-expressions.expected.json`
- `docs/test-cases/06-calculated-expressions.txt`
- `tools/symbol-db/interpret/__main__.py`
- `tools/symbol-db/interpret/summary.py`

*Session S3 — Legit harvester-dev session record naming a proprietary report/form fixture — redact name to generic/HHC; preserves history*

- `docs/session-sets/001-harvester-upstream-feedback-followups/session-3-verify.py`
- `docs/session-sets/harvester-report-baseline-capture/ai-assignment.md`
- `docs/session-sets/harvester-report-baseline-capture/change-log.md`
- `docs/session-sets/harvester-report-baseline-capture/spec.md`
- `docs/session-sets/harvester-report-baseline-golden-corpus/activity-log.json`
- `docs/session-sets/harvester-report-baseline-golden-corpus/spec.md`
- `docs/session-sets/harvester-report-displayed-total-parity/change-log.md`
- `docs/session-sets/harvester-report-displayed-total-parity/disposition.json`
- `docs/session-sets/harvester-report-displayed-total-parity/spec.md`

## Accepted residue (recorded, not removed)

### KEEP-ACCEPTED — immutable verifier/analysis output — 3 path(s)

*Session -- — Immutable raw verifier/analysis output (workflow forbids editing) — accepted recorded residue*

- `docs/session-sets/001-harvester-upstream-feedback-followups/session-reviews/session-003.md`
- `docs/session-sets/harvester-report-baseline-golden-corpus/session-reviews/session-002.md`
- `docs/session-sets/resolver-declarations-and-scopes/schema-validation-result.md`

### Keep-accepted — generic-example identifiers (evaluated, not structural disclosure) — 25 path(s)

These tracked files matched the broad structural-identifier scan but carry only
*generic* Access example names (textbook `[Order Details]`, generic
`frmStartup`/`rptInvoice`/launcher-shell example names), not NorthwindDevEdition
structural disclosure. Recorded so Session 3's final residue scan can account for
them.

- `AccessHarvester.Pipeline/UsageIdentification/SignalIndex.cs`
- `AccessHarvester.Sql.Grammar/JetSql.g4`
- `docs/session-sets/001-harvester-upstream-feedback-followups/change-log.md`
- `docs/session-sets/003-northwind-proprietary-data-remediation/embedded-residue-findings.md`
- `docs/session-sets/003-northwind-proprietary-data-remediation/s2-issues.json`
- `docs/session-sets/004-northwind-symbol-db-sqlite-residue/activity-log.json`
- `docs/session-sets/004-northwind-symbol-db-sqlite-residue/s1-verification.md`
- `docs/session-sets/004-northwind-symbol-db-sqlite-residue/scrub-allowlist-rationale.md`
- `docs/session-sets/ROADMAP.md`
- `docs/session-sets/generalization-validation-on-non-northwind/spec.md`
- `docs/session-sets/harvester-report-displayed-total-parity/activity-log.json`
- `docs/session-sets/harvester-report-displayed-total-parity/ai-assignment.md`
- `docs/session-sets/harvester-report-displayed-total-parity/session-reviews/session-001.md`
- `docs/session-sets/harvester-report-displayed-total-parity/session-reviews/session-002.md`
- `docs/session-sets/jet-sql-grammar-and-parser/issue-logs/session-001.json`
- `docs/session-sets/jet-sql-grammar-and-parser/session-reviews/session-001.md`
- `tests/AccessHarvester.Pipeline.Tests/NavigationGraphBuilderTests.cs`
- `tests/AccessHarvester.Tests/AccessTextParserTests.cs`
- `tests/AccessHarvester.Tests/MacroXmlParserTests.cs`
- `tests/AccessHarvester.Tests/ReportBaseline/SumAggregateEvaluatorTests.cs`
- `tests/AccessHarvester.Vba.Resolver.Tests/AccessIntegration/AccessObjectModelRegistryTests.cs`
- `tests/AccessHarvester.Vba.Resolver.Tests/AccessIntegration/AccessSymbolTableBuilderTests.cs`
- `tools/AccessHarvester.IrCodegen/Program.cs`
- `tools/AccessHarvester.IrCodegen/README.md`
- `tools/session-runner/jetsql_session1_reverify_r3.py`

## Out of scope / untouched

- The local-only gitignored proprietary fixture tree under
  `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/`,
  `samples/GoldenOutput/NorthwindDevEdition/`, and
  `samples/DatabaseYaml/NorthwindDevEdition.accdb/` — never tracked; nothing to remove.
- The harvester / symbol-DB **engine** (already generic) — only the CLI helper
  `RunForNorthwind`, the corpus-pinned test harness, and incidental name references
  are bound to Northwind, and those are classified above.
- **No git-history rewrite and no force-push** — this set is `HEAD`-only by
  maintainer decision; old structural names in pre-rewrite history are accepted residue.
