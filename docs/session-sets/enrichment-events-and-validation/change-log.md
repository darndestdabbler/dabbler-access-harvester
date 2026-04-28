```markdown
# Change Log: enrichment-events-and-validation

**Summary:** Implemented a comprehensive enrichment pipeline for VBA components, focusing on event handler classification, validation rule extraction, and RowSource specification. This work covers phases 6.3-6.5 of the VBA Parser Migration Harvester Plan.

**Key Deliverables:**
*   **Event Handler Taxonomy:**
    *   `EventHandlerClassification.cs` (record, enums)
    *   `EventHandlerClassifier.cs` (pure-function AST classifier with 7 categories)
    *   59 xUnit tests (`EventHandlerClassifierTests.cs`)
*   **Validation Rule Extraction:**
    *   `ValidationRule.cs` (record, enums, constraint sub-records)
    *   `ValidationRuleExtractor.cs` (extracts rules from tables, forms, BeforeUpdate handlers)
    *   32 xUnit tests (`ValidationRuleExtractorTests.cs`)
*   **RowSource Specification:**
    *   `RowSourceSpec.cs` (record, enum, descriptors)
    *   `RowSourceSpecBuilder.cs` (builds/annotates RowSource specifications)
    *   31 xUnit tests (`RowSourceSpecBuilderTests.cs`)
*   **End-to-End Pipeline & Integration:**
    *   `EnrichmentResult.cs` (composite result record)
    *   `EnrichmentPipelineRunner.cs` (pure-function orchestrator, populates `FormComponentSpec` references)
    *   14 xUnit tests (`EnrichmentPipelineRunnerTests.cs`)

**Output Files:**
*   `EventHandlerClassification.cs`
*   `EventHandlerClassifier.cs`
*   `ValidationRule.cs`
*   `ValidationRuleExtractor.cs`
*   `RowSourceSpec.cs`
*   `RowSourceSpecBuilder.cs`
*   `EnrichmentResult.cs`
*   `EnrichmentPipelineRunner.cs`
*   Associated test files: `EventHandlerClassifierTests.cs`, `ValidationRuleExtractorTests.cs`, `RowSourceSpecBuilderTests.cs`, `EnrichmentPipelineRunnerTests.cs`

**Test Count:** 136 new hermetic xUnit tests.
```