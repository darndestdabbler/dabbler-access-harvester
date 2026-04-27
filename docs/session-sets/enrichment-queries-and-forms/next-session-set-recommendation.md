### 1. Recommended Next Set(s) & Rationale

Start both session sets `#15` and `#16` in parallel.

*   **#15 enrichment-events-and-validation:** This set logically extends the form analysis from set #14 by examining control-level behavior like events and validation, which are tightly coupled to the form and query structures already defined.
*   **#16 enrichment-functions-navigation-review:** This set runs in parallel as it addresses a separate domain of global VBA modules and application-level navigation, having no direct dependency on the event/validation outcomes of #15.

### 2. Orchestrator Recommendation (Session 1)

| Session Set | Engine | Model | Effort |
| :--- | :--- | :--- | :--- |
| **#15 enrichment-events-and-validation** | In-conversation | Opus | Synthesis |
| **#16 enrichment-functions-navigation-review** | In-conversation | Opus | Synthesis |

**Reasoning:** Both are "spec stubs" requiring elaboration of the step list for Session 1. This is a synthesis task, which repository conventions route to the Opus model.

### 3. Orchestrator Gotchas

*   **For Set #15 (Events/Validation):** The orchestrator must incorporate the key finding from set #14 regarding "implicit form-reference parameter collection." The specification for `EventHandlerClassification.cs` and `ValidationRule.cs` must explicitly account for dependencies on other form controls that are not passed as formal arguments (e.g., `Me.ControlName` references).
*   **For Set #16 (Functions/Navigation):** The `FunctionClassification.cs` schema must differentiate between pure functions and those with side effects or implicit dependencies on the application's global or UI state (e.g., reading from an open form). This classification is critical for accurate navigation graph generation and identifying code that requires manual review.
*   **For Both Sets:** The primary goal of Session 1 is to transform the high-level "spec stub" into a detailed, multi-step execution plan for the subsequent sessions. The orchestrator must produce this concrete plan as its main output.