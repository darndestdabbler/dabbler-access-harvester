### 1. Context as Provided

The project aims to migrate Microsoft Access applications to Blazor via a multi-stage pipeline. The current pipeline extracts Access objects into a faithful "Access-shaped" YAML representation (`FormMetadata`/`ReportMetadata` records), which is intended to be consumed by an AI. The goal is to define a second, "Custom IR YAML" that the AI will write to, which will then deterministically generate a Blazor project. This target IR and its code generator do not yet exist in the provided codebase.

The immediate task is to evaluate Lowdefy's schema as a model for this new IR. The de facto AI-authored layer today is a set of structured markdown "workflow packages" which guide manual Blazor development, rather than a machine-readable IR for codegen. The central architectural decision is how to structure the IR: as a high-level domain-specific language (DSL) mirroring Access patterns, as a low-level composition of generic primitives like Lowdefy's, or a two-stage combination. My analysis is based solely on the provided documents and does not assume knowledge of any code in sibling repositories.

### 2. Lowdefy-Alignment Scorecard

1.  **Compactness for AI authoring** — `divergent-but-justified`.
    A high-level domain DSL with types like `MasterDetailListPage` is far more compact and requires less inferential work from the AI than composing the same pattern from generic Lowdefy blocks. The Lowdefy pitch ("50 lines vs 500") assumes a generic target; for a bounded migration, the domain-specific IR will be more compact.

2.  **Reviewability** — `strong`.
    I disagree with the orchestrator; a high-level domain DSL is *more* reviewable by humans than a Lowdefy-style schema. A reviewer can immediately grasp the intent of `pageType: ModalDetailForm`. A Lowdefy equivalent requires the reviewer to mentally parse a tree of generic blocks to recognize the pattern, which is more error-prone and less direct.

3.  **Composability** — `aligned`.
    The need for a `_ref`-style mechanism to include shared configurations (e.g., a common page header, a data request) is critical and directly applicable. The current Access-to-YAML flow suffers from the same lack of reuse as Access itself.

4.  **Schema validation surface** — `aligned`.
    Any formal IR is an improvement over prose. A JSON Schema-validated IR that rejects unknown properties is a mandatory baseline for this project.

5.  **Separation of concerns** — `aligned`.
    The conceptual separation of data (Requests), UI (Blocks), logic (Operators), and behavior (Actions/Events) is the most valuable idea to borrow. These concepts should be applied *within* the high-level domain types, not as a replacement for them.

6.  **Escape hatches** — `divergent-but-justified`.
    Lowdefy's `_js` operator is inappropriate for a C# target. The proposed escape hatch of invoking a named method on a partial class is the correct, type-safe .NET idiom.

7.  **Code-gen ergonomics** — `strong`.
    A domain-specific IR makes for a simpler generator. The generator becomes a dispatch mechanism mapping IR types like `ModalDetailForm` to their specific emitters. A Lowdefy-style IR forces the generator to contain complex logic to recognize patterns from generic primitives, effectively pushing the "Access-to-Blazor" translation problem from the AI into the generator.

8.  **Mapping fidelity from Access** — `strong`.
    A high-level DSL provides a direct, 1:1 mapping for canonical Access patterns (subforms, modals, continuous forms). This preserves the original application's structure, making the migration more transparent and easier to validate. Lowdefy primitives would obscure this structure.

### 3. Recommendations to Adopt

1.  **Adopt the Block/Request Conceptual Split.** This is the highest-value concept. Separate data operations from UI rendering within every page or component definition. An IR for a detail form should have a distinct `requests` section for loading and saving data and a `layout` section defining the UI controls.
    ```yaml
    # Example for a ModalDetailForm
    pageType: ModalDetailForm
    title: "Edit Budget Service Plan"
    requests:
      loadPlan:
        query: "qryBudgetServicePlanByID"
        params:
          - { _state: "openArgs.planID" }
      savePlan:
        command: "cmdUpdateBudgetServicePlan"
    layout:
      # ... Block definitions for form controls ...
    ```

2.  **Adopt a C#-Idiomatic Operator Catalog.** Use a JSON-tree representation for expressions to avoid string parsing in the generator. The operators must map directly to C# constructs or well-known Access functions.
    ```yaml
    # Example: Visible: =IIf([Status]="Complete", False, True)
    visible:
      _iif:
        - _eq:
            - { _state: "currentItem.Status" }
            - "Complete"
        - false
        - true
    ```

3.  **Adopt Declarative Event Handling.** Model UI events as triggers for an ordered list of typed actions. This provides a clean, testable structure for behavior that maps well from Access's event procedures.
    ```yaml
    # Example: onClick event on a save button
    onClick:
      - action: Validate
      - action: Request
        params:
          requestId: "savePlan"
      - action: CloseModal
        params:
          result: "ok"
    ```

4.  **Adopt a Reference (`_ref`) Mechanism.** Implement a pre-processing step for the IR that resolves file-level includes. This is essential for reusing UI fragments, request definitions, and other common configuration, dramatically reducing IR size and maintenance.

### 4. Recommendations to Reject

1.  **Reject General-Purpose Primitives as the IR.** Do not force the AI to author low-level block trees. The migration's scope is bounded by Access patterns. A high-level DSL that names these patterns is more efficient for the AI to author, easier for humans to review, and simpler for the code generator to consume. The flexibility of generic primitives is a solution for a problem this project does not have.

2.  **Reject the Two-Stage IR (Option B).** The orchestrator's proposed two-stage IR (High-level DSL -> Low-level primitives -> Blazor) is over-engineered. It introduces the significant maintenance overhead of two schemas and a "desugaring" transformation step for a benefit—flexibility for one-offs—that is better and more cheaply served by a well-defined escape hatch ("drop to hand-written Blazor"). Stick to a single, high-level IR.

3.  **Reject a `_js`-style Escape Hatch.** Reaffirming the scorecard, a mechanism to invoke arbitrary C# code must be idiomatic. A partial class (`MyPage.Custom.cs`) where developers can add event handlers or helper methods referenced by name from the IR (`action: CallMethod`, `method: "CalculateTotals"`) is type-safe and fits the Blazor model perfectly.

### 5. Open Questions

1.  **IR Shape (A vs. B vs. C): Which architectural path to choose?**
    *   **Decision:** Choose **Option A: Flat high-level domain DSL.**
    *   **Justification:** This approach is the most direct path to value. It optimizes for the specific problem—migrating known Access patterns. It yields a compact, highly reviewable IR, simplifies both AI authoring and code generation, and provides the best mapping fidelity. The perceived inflexibility is a feature, enforcing consistency. Outliers should be handled by the designated escape hatch (hand-written Blazor), not by complicating the primary IR for 95% of cases.

2.  **Workflow Packages: Retain, supplement, or replace?**
    *   **Decision:** The YAML IR must become the **single source of truth for code generation**, replacing the workflow packages for that purpose.
    *   **Justification:** The workflow packages excel at capturing business intent, invariants, and ambiguity—information that is difficult to encode formally. They should be retained as a **human-centric documentation artifact** that complements the IR. An ideal future state might even generate the prose sections of the workflow package *from* the validated IR and associated metadata, ensuring they stay in sync. They must not be a parallel input to codegen.

3.  **Reports: Part of the UI IR or a separate domain?**
    *   **Decision:** Model reports in a **separate, dedicated IR schema.**
    *   **Justification:** Interactive web UI (Blazor) and paginated print media (PDFs, etc.) are fundamentally different domains with different concerns (layout, data binding, rendering targets). Forcing report-specific concepts like `GroupLevel`, `PageHeader`, and `LinkMasterFields` into the UI IR will pollute it. A separate `Report.ir.yaml` schema allows for a clean, focused UI IR and a purpose-built Report IR that can be consumed by a different generator targeting a PDF library (e.g., QuestPDF, IronPDF).

### 6. Suggested Next Steps

1.  **Define a Schema for a Single, High-Value Pattern.** Draft a JSON Schema for the `ModalDetailForm` type only. Include subsections for `requests`, `layout` (with a few basic controls like `TextBox` and `Button`), and `events`.
2.  **Hand-Author a Sample IR File.** Manually translate the `frmBudgetServicePlan` Access YAML into a new `frmBudgetServicePlan.blazor.yaml` file conforming to the schema from step 1. This will immediately surface friction in the proposed schema.
3.  **Build a "Slice" Generator.** Create a minimal T4 template or C# Source Generator that reads *only* `ModalDetailForm` IR files and generates the corresponding Blazor component (`.razor` and `.razor.cs`). Do not try to build the full generator yet.
4.  **Evaluate and Iterate.** Compile and run the generated component. Compare the hand-authored IR against the resulting code. This small, reversible experiment will provide concrete data to validate the high-level DSL approach (Option A) before committing to the full architecture.