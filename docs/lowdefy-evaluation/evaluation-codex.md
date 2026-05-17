### 1. Context as Provided

The project aims to migrate Microsoft Access applications to Blazor via a multi-stage pipeline. The current pipeline extracts Access objects into a faithful "Access-shaped" YAML IR. An AI-assisted step is intended to translate this into a "Custom IR YAML" which then deterministically generates a Blazor project.

However, this "Custom IR YAML" and its code generator do not exist in the provided context. Instead, the AI-authored layer is a prose-heavy markdown "Workflow Package," used as guidance for hand-authoring a Blazor pilot application. The central question is how to design the machine-readable Custom IR, specifically evaluating the schema patterns from Lowdefy. My analysis is based on the provided Access-shaped IR, the workflow package format, and the architectural fork presented by the orchestrator, without access to any existing Blazor-target IR or code generator.

### 2. Lowdefy-Alignment Scorecard

This scorecard evaluates a hypothetical, high-level domain DSL (your "Option A") augmented with Lowdefy's conceptual splits, against the current "workflow package" process.

1. **Compactness for AI authoring** - **Divergent-but-justified**. Lowdefy's generic primitives would be verbose for this migration. A domain-specific type like `MasterDetailListPage` is far more compact and has a smaller surface area for the AI to target than a composition of generic `Box`, `Table`, and `Form` blocks.
2. **Reviewability** - **Divergent-but-justified**. A domain-specific IR is more reviewable than Lowdefy's generic blocks because the intent is declared (`type: ModalDetailForm`) rather than inferred from a generic structure. This is superior for human review in a bounded migration context.
3. **Composability** - **Weak**. The current process has no composability mechanism. Adopting a `_ref`-style include for shared IR snippets (for example, a standard search filter block) is a critical, high-value addition that any structured IR must have.
4. **Schema validation surface** - **Weak**. The current prose-based workflow package has no schema. A formal YAML/JSON IR with a strict schema (for example, JSON Schema) is a fundamental requirement for a reliable code generator.
5. **Separation of concerns** - **Weak**. Access conflates data, display, and logic. The workflow packages capture this entanglement in prose. Adopting the conceptual split of Requests (data), Blocks (display), and Actions/Events (behavior) is the single most important architectural improvement, even within a high-level domain DSL.
6. **Escape hatches** - **Weak**. The current escape hatch is "write the Blazor code by hand." A mature IR needs a granular, type-safe escape hatch. For a C# target, this should be invoking a named method on a partial class, not a `_js`-style string evaluation.
7. **Code-gen ergonomics** - **Strong**. A high-level domain DSL makes the code generator simple and declarative (for example, `case "Switchboard": emitSwitchboard(...)`). A Lowdefy-style primitive IR would force the generator to contain complex logic to recognize and synthesize these common Access patterns.
8. **Mapping fidelity from Access** - **Strong**. A domain DSL with first-class types for `ContinuousForm`, `Subreport`, and similar patterns has perfect mapping fidelity by design. This is the primary advantage of a domain-specific IR over a general-purpose one for a migration project.

### 3. Recommendations to Adopt

1. **Adopt the conceptual Block/Request/Action separation.** This is the most valuable Lowdefy pattern. Decouple data operations from UI components. Even within a high-level type, the schema should reflect this separation.

   **Sketch:**

   ```yaml
   # ir/forms/frmBudgetServicePlan.yaml
   type: MasterDetailListPage
   title: "Budget Service Plan"

   requests:
     fetchNeeds:
       query: "SELECT * FROM qryNeeds WHERE ClientID = :clientId"
     fetchPlans:
       query: "SELECT * FROM qryPlans WHERE NeedID = :selectedNeedId"

   layout:
     master:
       type: List
       request: fetchNeeds
       onSelectionChange:
         - action: SetState
           state:
             selectedNeedId: "{{event.data.NeedID}}"
         - action: Request
           requestId: fetchPlans
     detail:
       type: Table
       request: fetchPlans
   ```

2. **Adopt an operator-based expression tree.** Define a catalog of C#-native operators (`Nz`, `IIf`, `DLookup`, standard arithmetic/logical operators) to represent expressions. This avoids string parsing in the code generator and makes logic reviewable and composable.

   **Sketch:**

   ```yaml
   visible:
     _eq:
       - _state: user.role
       - "Admin"

   computedValue:
     _sum:
       - _lookup:
           - _state: lineItems
           - subtotal
       - _state: taxAmount
   ```

3. **Adopt `_ref` for cross-file reuse.** Access applications are notoriously repetitive. A file-inclusion mechanism is essential to keep the IR DRY. This is a straightforward and high-impact addition.

   **Sketch:**

   ```yaml
   layout:
     - _ref:
         path: "shared/standard-header.yaml"
         vars:
           pageTitle: "My Form"
   ```

4. **Adopt formal schema validation.** The IR must be validated against a strict schema at the start of the codegen pipeline. Reject unknown properties and fail loudly on type mismatches. This is non-negotiable for a reliable generator.

### 4. Recommendations to Reject

1. **Reject the generic Block-composition model.** Do not force the AI to describe a `MasterDetailListPage` as a tree of generic `Box`, `Table`, and `Form` blocks. That outsources pattern recognition to the code generator and increases the AI's authoring burden. Use a high-level, domain-specific DSL.
2. **Reject JS-shaped operators and `_js`.** The target is C#, not JavaScript. The operator catalog and escape hatches must be native to the target platform. The escape hatch should be a call to a type-safe, developer-authored C# method in a partial class, not an arbitrary string of code to be evaluated.
3. **Reject the generic connector abstraction.** The migration target is fixed (EF Core on SQL Server/Postgres). A thin abstraction for "named query" and "named command" is sufficient. Do not import a generic connection/provider model that adds complexity with no benefit.
4. **Reject modeling reports as UI blocks.** Reports are print-layout documents, not interactive web pages. Forcing them into a UI-centric block model is a category error. Model them with first-class `Report`, `Subreport`, and `GroupLevel` types, and generate code for a dedicated reporting library rather than Blazor components.

### 5. Open Questions

1. **What should the Blazor-target IR be: Option A, B, or C?**

   Choose **Option A (flat high-level domain DSL)**. A two-stage IR (Option B) introduces a second schema and a desugaring step that adds significant maintenance overhead for flexibility you do not need in a bounded migration project. Start with the simplest thing that can work: a DSL that directly models the source patterns.

2. **Should reports be first-class IR types or a separate domain?**

   Treat reports as a **separate domain with its own first-class IR types**. Their target (paginated PDF or equivalent) and semantics (`LinkMasterFields`, grouping, page headers) are fundamentally different from interactive forms. A separate IR schema and a distinct code generator will produce a cleaner architecture than forcing reports through the same model as form generation.

3. **Should workflow packages be retained, supplemented, or replaced?**

   The workflow package should be **replaced by the YAML IR as the source of truth for code generation**. The prose package is still useful as an upstream AI input and as human-readable design documentation, but the deterministic generator should consume only validated structured IR.

### 6. Suggested Next Steps

1. Define the schema for one high-level type, such as `ModalDetailForm`, including `requests`, `layout`, and event `actions`.
2. Hand-author a single IR file for one concrete Access object, ideally the `frmBudgetServicePlan` detail popup shape.
3. Write a minimal code generator for that one type and emit a single Blazor component pair (`.razor` plus code-behind).
4. Compare the generated output against the hand-written pilot and use that vertical slice to decide whether the Option A shape is ergonomic enough before committing to a broader refactor.
