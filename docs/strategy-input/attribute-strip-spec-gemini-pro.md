### Q1 — Audit scope of call sites

#### 1. Recommendation

**(Q1.a) Audit every VBA-parsing call site and route them all through the new loader.**

Deprecate the existing `VbaParserPipeline.Parse(string)` entry point, replacing it with a new one that requires a `VbaModule` source object, not a raw string. This new entry point will internally use the module loader. This enforces the correct path and eliminates ambiguity.

**Reasoning:**
Creating a "two paths" system (Q1.b) introduces a maintenance liability and violates the "do it right" posture. A future developer will inevitably use the raw, incorrect path and reintroduce parse failures. A one-time, comprehensive refactoring is cheaper than debugging intermittent, consumer-specific parsing bugs later. The loader can be made idempotent: if a header isn't present (as in many test fixtures), it simply returns the original source. This handles all call sites, including tests, without special casing.

#### 2. Spec / Verification

*   **Task:** Create a new `VbaModuleBodyLoader` class. Its primary method accepts a raw module string and returns a `(string Body, ModuleHeaderMetadata? Header)` tuple.
*   **Task:** Refactor all existing call sites of `VbaParserPipeline.Parse` to route through the new loader.
*   **Verification:** The parse-success-report for `HomeHealthCare.accdb` and `NorthwindDevEdition.accdb` must show parse success rates >95%.
*   **Verification:** A static search of the codebase must find no remaining calls to the raw, string-based `Parse` method outside the new loader/pipeline infrastructure. Mark the old method `[Obsolete]` or make it `internal`.

#### 3. Edge Cases to Pin

*   **Malformed Headers:** The header-stripping logic must be deterministic. Define a regex that strictly matches the `VERSION...BEGIN...END...Attribute VB_*` block. If the pattern doesn't match perfectly, the loader treats it as code and does not strip it.
*   **Header-Only Modules:** If a module contains only the header and no subsequent code, the loader should return an empty string for the body.

---

### Q2 — What does "strip" mean structurally

#### 1. Recommendation

**(Q2.b) Preserve the header as parallel `moduleHeader` metadata on the parsed `Module` AST node.**

The loader should parse the header into a strongly-typed record (`ModuleHeaderMetadata`) and the pipeline should attach it to the root `Module` node of the AST.

**Reasoning:**
The downstream AI's task is migration. Attributes like `VB_PredeclaredId`, `VB_Exposed`, and `VB_Creatable` are not housekeeping noise; they are fundamental to the semantics of VBA class/form modules.

*   `VB_PredeclaredId = True` is essential for understanding why code can refer to a form `Form_frmClientEdit` without first instantiating it. Migrating this code correctly requires knowing about this default instance.
*   `VB_Exposed = False` and `VB_Creatable = False` define the module's public contract and instantiation rules. This is critical for generating a correct equivalent in a target like Blazor or React.

Dropping this information (Q2.a) constitutes a fidelity loss that directly contradicts the project's primary goal. The cost of a small AST change is minimal compared to the cost of the AI producing incorrect code due to missing semantic context.

#### 2. Spec / Verification

*   **Task:** Define a `ModuleHeaderMetadata` record with properties for `VbName`, `VbGlobalNameSpace`, `VbCreatable`, `VbPredeclaredId`, `VbExposed`, `MultiUse`, and `Version`. Properties for attributes that may not be present should be nullable.
*   **Task:** The `VbaModuleBodyLoader` must parse the header block into this record.
*   **Task:** Modify the `Module` node in the VBA AST to include a `public ModuleHeaderMetadata? Header { get; }`.
*   **Verification:** AST snapshot tests must be updated. For a form's code-behind, the snapshot must show the `Header` property populated with correct values. For a standard `.bas` module, the `Header` property must be `null`.

#### 3. Edge Cases to Pin

*   **Attribute Presence:** The header-parsing logic must not assume any specific `Attribute VB_*` line is present, except perhaps `VB_Name`. All other attributes should be treated as optional.
*   **Other Module Types:** Class modules (`.cls`) also have these headers. The spec should state that the new logic must correctly handle `.cls` module headers, even though forms/reports are the immediate driver. This prevents future rework.