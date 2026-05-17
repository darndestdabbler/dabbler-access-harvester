# ModalDetailForm IR — Cross-Provider Design Review

---

## 1. Verdict

**ISSUES_FOUND.**

Two findings are true blockers for Session 2's T4 generator: **(e) write-back binding semantics are unspecified** — the generator cannot emit `@bind-Value` without knowing the target — and **(c) the dual literal-string / `_request`-operator binding surface** gives the generator two incompatible code paths with no schema enforcement to eliminate one. A third finding, **(j) debounce/tryCatch/actions composition order**, is ambiguous enough that two engineers writing the generator independently would produce incompatible Blazor output. All three should be resolved or authoritatively documented before Session 2 starts. The remaining findings are real design gaps but can be captured as backlog without blocking the slice generator.

---

## 2. Findings

### Finding 1 — `_state` pattern disallows dots (evaluation of prompt item a)

**Severity:** Minor  
**Location:** Schema → `$defs/expression` → `_state` variant → `pattern`

**Observation:** The pattern `^[a-zA-Z0-9_]+$` forbids dotted paths such as `currentRecord.ClientID`. The sample worked around this naturally: all state keys are primitive scalars (`budgetId`, `isDirty`).

**Why it matters:** For the Access ModalDetailForm pattern, form-level state is historically simple: one or two IDs passed in via OpenArgs, and a handful of dirty/mode flags. A future `WizardDetailForm` or multi-step modal might want to track `step1.isComplete`, but that is not this form type's contract. The generator's `SetState` / `_state` expression codegen is straightforward with flat keys; supporting dotted paths later would require a path-resolver in the emitter (e.g., `StateDict["step1"]["isComplete"]` vs. `State.step1.isComplete`).

**Recommendation:** Keep the current flat constraint. Add a schema comment: *"Nested state is intentionally out of scope for ModalDetailForm; author should flatten by prefixing, e.g. `step1_isComplete`."* **Defer** nested state to a future `WizardDetailForm` type if one is ever introduced.

---

### Finding 2 — `Request` action carries no per-call parameter overrides (evaluation of prompt item b)

**Severity:** Major  
**Location:** Schema → `$defs/action` → `Request` variant; Sample → `events.onLoad`, `requests.loadNeedsForClient`

**Observation:** The sample demonstrates an implicit ordering dependency: `loadNeedsForClient.parameters.clientId` is `{ _request: "loadPlan.ClientID" }`, which is only resolvable after `loadPlan` has been executed. Nothing in the schema models this dependency. Furthermore, if a future flow needs to call the same query with a *different* runtime argument (e.g., fire `loadNeedsForClient` for a prospective client before saving), the only option is to declare a new request definition, causing request proliferation.

**Why it matters:** Session 2's generator must choose a code strategy for request parameters: evaluate them once at component init, or lazily at call-site. The sample requires lazy evaluation (evaluate at the moment the `Request` action fires). That decision is load-bearing for the emitter but is not stated in the schema. Additionally, the ordering dependency (`loadPlan` must precede `loadNeedsForClient`) is implicit in the event's `actions` array order — correct, but not enforced or documented.

**Recommendation:** (1) Add a schema note to `$defs/action/Request` and `$defs/request` clarifying that parameters are **evaluated lazily at call time** against the current state snapshot. (2) **Defer** per-call parameter overrides (a `paramOverrides` property on the `Request` action) to a later session when a concrete flow demands it — the current pattern of "define a dedicated request per call shape" is workable for this slice.

---

### Finding 3 — Binding accepts raw literal strings with no format enforcement (evaluation of prompt item c)

**Severity:** Major  
**Location:** Schema → `$defs/controls/textBox`, `comboBox`, `checkBox` → `binding`; Sample → `layout` controls

**Observation:** The sample uses two syntactically distinct forms for semantically identical bindings:

```yaml
binding: "loadPlan.ClientName"          # raw string — parsed by convention
binding: { _request: "loadPlan.FiscalYear" }  # explicit operator
```

Both validate against `$defs/expression` (a string literal is a valid expression), but:

1. The literal-string form is **not constrained** to `^[a-zA-Z0-9_]+\.[a-zA-Z0-9_]+$`. An author can write `binding: "some garbage"` and the schema accepts it silently.
2. The generator must contain **two code paths** — one parsing a dotted string, one walking an operator tree — to emit the same `@bind-Value`. This creates a fork that will diverge across engineers.
3. Mixed usage in the same file (`txtClientName` uses a string; `txtTotalPlanned` uses `_request`) is confusing to both human authors and tooling.

**Why it matters:** Session 2's T4 template must branch on `typeof(binding) == string` vs. operator object. Any future refactoring or addition of controls inherits this split. The schema validation gap means typos in literal bindings reach the generator silently.

**Recommendation:** **Before commit**, enforce one canonical form. Two options:

- **Option A (preferred):** Deprecate the raw-string binding form for request-field refs. Require `_request` for request field references, `_state` for state, and `_const` for literals. Reject bare strings in `binding` (or constrain string bindings to a non-dotted literal pattern for constant strings only). Update the sample to use `_request` uniformly.
- **Option B:** Keep both forms but add a `pattern: "^[a-zA-Z0-9_]+\\.[a-zA-Z0-9_]+$"` constraint to the string arm when it appears as a `binding` value — this requires a context-sensitive schema refinement or a distinct `$defs/bindingExpression` type that narrows the string case.

Option A reduces generator complexity and ambiguity; it is the recommended path.

---

### Finding 4 — Validation rules have no `field` target (evaluation of prompt item d)

**Severity:** Major  
**Location:** Schema → `$defs/action` → `Validate` variant → `params.rules[]`

**Observation:** Each rule has `expression` (a boolean predicate) and `message` but no `field` name. Rules are global predicates over the entire IR state. The sample uses this correctly — all three rules are whole-form guards — but the abstraction cannot support field-inline error display.

**Why it matters:** Blazor's standard validation UX (`ValidationMessage<T>`, MudBlazor `MudTextField` `Error`/`ErrorText`) associates errors with specific fields. A generator emitting global-only errors must render them in a summary banner, not inline, which degrades UX compared to the Access equivalent. More concretely: when a future author writes a required-field rule, they will expect the red outline to appear on the field, not in a banner at the top of the modal.

**Recommendation:** Add an **optional** `field` property to rule items:

```json
"field": { "type": "string", "description": "The `id` of the layout control to which this error is anchored, if any." }
```

Optional preserves backward compatibility; the generator emits a field-level `Error` prop when `field` is present, a summary banner entry when absent. **This can land in the schema before Session 2** since the T4 template should design for it now to avoid a breaking change later. Mark it `// optional` in the sample's comment block.

---

### Finding 5 — Two-way binding write-back semantics are unspecified (evaluation of prompt item e)

**Severity:** Critical  
**Location:** Schema → `$defs/controls/textBox` (and `comboBox`, `checkBox`) → `binding`; Sample → `txtFiscalYear`, `savePlan`

**Observation:** The schema and sample both assume that editing a TextBox bound to `"loadPlan.FiscalYear"` writes back into the in-memory result row of the `loadPlan` request — Access-style mutable RecordSource semantics. Evidence:

- `onFiscalYearChanged` only sets `isDirty: true`; it does not copy `FiscalYear` into state.
- `savePlan.parameters.fiscalYear` reads `{ _request: "loadPlan.FiscalYear" }`, implying that field has been mutated by the TextBox edit.

But neither the schema nor any accompanying prose specifies this contract. The schema says `binding` is an `expression`; expressions in the operator catalog are read-only evaluation trees. There is no `SetRequestField` or `WriteBack` action defined anywhere.

**Why it matters:** The T4 generator must produce a concrete C# binding. Two valid interpretations exist:

1. **Request-row mutable**: The generator holds the query result as a `Dictionary<string, object?>` or typed DTO, and `@bind-Value="record.FiscalYear"` mutates it directly. `savePlan` parameters re-read the (now-mutated) row. This matches the sample's intent but requires the generator to model request results as mutable objects.
2. **State-mediated**: Each editable field has a corresponding state key; binding reads from state; `onValueChanged` copies the TextBox value into state; the save request reads from state. This is the explicit Blazor pattern and avoids mutable query rows but requires the sample to be substantially more verbose.

Without a schema-level or design-document declaration of which model is in use, two engineers will produce incompatible generators.

**Recommendation:** **Before commit**, add a `writeback` or `bindingMode` property to the writable control types, **or** add a design-level note to the schema's root `description` declaring the mutable-row semantics. A concrete minimal fix:

- In `$defs/controls/textBox` (and `comboBox`, `checkBox`): add `"readOnly": { "type": "boolean", "default": false }`.
- In the schema description: *"Bindings on writable controls (`readOnly: false`) are two-way: editing the control mutates the in-memory result row of the referenced request. The generator must materialize request results as mutable DTO objects."*
- Update the sample: add `readOnly: true` to `txtClientName` and `txtTotalPlanned` (computed, display-only fields).

The `readOnly` flag also resolves the currently unrepresentable distinction between editable and display-only TextBoxes (see Finding 10).

---

### Finding 6 — ListBox omitted from control catalog (evaluation of prompt item f)

**Severity:** Minor  
**Location:** Schema → `$defs/controls`; Sample → scope comment

**Observation:** The source form's two ListBoxes (`lstClientServiceNeeds`, `lstClientServicePlan`) are absent. The sample's scope comment explains this correctly: inline child-list management belongs to `MasterDetailListPage`.

**Why it matters:** The scope decision is sound for the current form type. A `ModalDetailForm` with an inline editable grid would be better modeled as a `MasterDetailListPage` opened from a parent, not as a `ModalDetailForm` with an embedded `ListBox`. The ComboBox (single-select dropdown) handles the selection use case; a full list belongs to a different IR type.

**Recommendation:** No schema change. **Document the decision** in the schema's `description`: *"A ModalDetailForm does not include a ListBox or DataGrid control; child-list display and editing is the responsibility of `MasterDetailListPage`."* This prevents the next author from attempting to squeeze a list into this form type.

---

### Finding 7 — `modalType: "modal"` semantics are intent-based, not source-mechanical (evaluation of prompt item g)

**Severity:** Minor  
**Location:** Sample → `metadata.modalType`; sample scope comment

**Observation:** The source form has `Modal: "False"`, `PopUp: "False"`. The sample author chose `modalType: modal` to capture migration intent (the form would render as a MudDialog). The schema's enum is `["page", "modal", "drawer"]`.

**Why it matters:** If a future harvester bot reads `Modal: "False"` from Access and mechanically sets `modalType: page`, it will produce a wrong classification for forms that are *functionally* modal (opened by another form, return a result). The schema doesn't distinguish "Access source modal flag" from "Blazor rendering intent."

**Recommendation:** Add a schema-level comment confirming that `modalType` expresses **Blazor rendering intent** for codegen, not a mechanical transcription of Access modal flags. The harvester should apply inference rules (form opened via `DoCmd.OpenForm acDialog` → `modal`; standalone navigation → `page`). **Defer** the formal harvester inference rules to the harvester spec; this schema's responsibility is just the intent declaration.

---

### Finding 8 — Operator catalog is incomplete for the next 5 forms (evaluation of prompt item h)

**Severity:** Major  
**Location:** Schema → `$defs/expression` → operator `oneOf` variants

**Observation:** The current catalog (`_nz`, `_iif`, `_dlookup`, `_dcount`, `_dsum`, `_format`, `_add`, `_sub`, `_mul`, `_div`, `_mod`, `_eq`, `_ne`, `_lt`, `_le`, `_gt`, `_ge`, `_and`, `_or`, `_not`, `_isnull`, `_state`, `_request`, `_const`) will be insufficient within the next 5 forms. Missing operators by category:

| Category | Missing | Immediate Access equivalents |
|---|---|---|
| String | `_concat`, `_trim`, `_left`, `_right`, `_mid`, `_len`, `_instr`, `_ucase`, `_lcase`, `_replace` | `&`, `Trim()`, `Left()`, `Mid()`, `Len()`, `InStr()`, `UCase()`, `LCase()` |
| Date | `_now`, `_date`, `_dateadd`, `_datediff`, `_datepart` | `Now()`, `Date()`, `DateAdd()`, `DateDiff()`, `DatePart()` |
| Type coercion | `_cstr`, `_cint`, `_cdate`, `_ccur`, `_cdbl` | `CStr()`, `CInt()`, `CDate()`, `CCur()` |
| Domain aggregates | `_dmin`, `_dmax`, `_davg` | `DMin()`, `DMax()`, `DAvg()` |
| Comparison | `_like` | `Like` operator in VBA/SQL |

Without these, the next author must use `CallMethod` for any expression containing a date calculation, string concatenation, or type cast. `CallMethod` defeats the purpose of the operator IR (it becomes opaque to the resolver and the generator).

**Why it matters:** The T4 generator's expression visitor is designed once; adding operators later requires schema changes, generator changes, and retesting all existing files. Getting the catalog substantially complete now is cheaper than retroactive expansion.

**Recommendation:** Add at minimum `_concat`, `_now`, `_dateadd`, `_datediff`, `_cstr`, `_cint`, `_cdate` as new operators with appropriate arity constraints before Session 2. The remaining string and domain-aggregate operators can land in a `schema-refinements.md` backlog for Session 3+, since Session 2's slice generator only needs to handle what the sample uses — but the schema should be open for extension without breaking change.

---

### Finding 9 — Action catalog is incomplete; `RefreshRequest` is a near-term blocker (evaluation of prompt item i)

**Severity:** Major  
**Location:** Schema → `$defs/action`

**Observation:** The current catalog (`Validate`, `Request`, `SetState`, `CallMethod`, `CloseModal`, `ShowError`, `Navigate`, `Confirm`) is missing:

| Missing Action | When needed | Access equivalent |
|---|---|---|
| `RefreshRequest` | After a save/delete, reload a named request without navigating | `Me.Requery`, `Me.Refresh` |
| `OpenModal` | Launch a child ModalDetailForm and optionally await its result | `DoCmd.OpenForm acDialog` |
| `SetFocus` | Move keyboard focus to a specific control after validation failure | `Me.ControlName.SetFocus` |
| `ResetForm` | Discard edits, reload from last-committed request data | `Me.Undo` |
| `SetField` | Explicitly write a value into a request row field (alternative to two-way binding) | `Me.FieldName = value` |

`RefreshRequest` is the most critical omission. Almost every CRUD flow is: *validate → execute save command → **refresh the parent list** → close*. Without it, authors must use `CallMethod` to trigger a re-query, which makes the IR opaque. The current sample avoids the problem by closing after save, but the next form probably won't.

`OpenModal` is also near-critical: a ModalDetailForm that needs to open a child lookup modal (e.g., a client picker) has no way to express this in the action catalog.

**Recommendation:** Add `RefreshRequest` before Session 2's commit (it is simple: `params.requestName: string`). Add `OpenModal` with `params: { formId: string, args: object, onClose: action[] }` to the backlog — its semantics are complex (async result passing) and should be designed carefully. `SetFocus` and `ResetForm` can also be backlog items.

---

### Finding 10 — No `readOnly` property on writable controls

**Severity:** Major  
**Location:** Schema → `$defs/controls/textBox`, `comboBox`, `checkBox`

**Observation:** The schema has no way to mark a TextBox as display-only. The sample has `txtClientName` (bound to `loadPlan.ClientName`, clearly display-only — no `onValueChanged`, not referenced in `savePlan`) and `txtTotalPlanned` (a computed field, `_sub` expression binding) that must be read-only. The generator cannot distinguish them from editable fields without this property.

**Why it matters:** Session 2's T4 generator must emit either `<MudTextField @bind-Value="..." />` (two-way) or `<MudTextField Value="..." ReadOnly="true" />` (one-way). Without a `readOnly` flag, the generator must heuristically infer read-only status from the binding expression type (is it a `_sub`? must be computed). This heuristic breaks for any `_request`-bound field that happens to be non-editable.

**Recommendation:** **Before commit**, add `"readOnly": { "type": "boolean", "default": false }` to `textBox`, `comboBox`, and `checkBox`. Update the sample to mark `txtClientName` and `txtTotalPlanned` as `readOnly: true`. This also resolves the ambiguity in Finding 5 (the generator emits two-way binding only when `readOnly: false`).

---

### Finding 11 — Debounce/tryCatch/actions composition order is unspecified (evaluation of prompt item j)

**Severity:** Major  
**Location:** Schema → `$defs/event`

**Observation:** An event may have `actions`, `tryCatch`, and `debounce` simultaneously. The schema does not specify execution semantics when all three are present. Additionally, `tryCatch.tryActions` is optional ("If omitted, the top-level `actions` are used"), but the interaction is only described in prose, not enforced structurally. A subtle gap: if neither `actions` nor `tryCatch` is present, the event object is still valid (no required fields on `event`).

**Why it matters:** Two engineers writing independent generators would likely produce different Blazor output:

- Engineer A: `debounce( tryCatch( actions ) )` — debounce is outermost
- Engineer B: `tryCatch( debounce( actions ) )` — tryCatch catches debounce cancellation exceptions too

The `tryActions` vs `actions` fallback also creates ambiguity: if `tryCatch` is present without `tryActions`, the generator must know to use `actions` as the try body — but this is specified only in a prose description field, not in schema structure.

**Recommendation:** **Before commit**:

1. Add a `minProperties: 1` constraint or require at least one of `actions`/`tryCatch` on the event object (use `oneOf` or `if/then`).
2. Add a prose note to the `event` description formalizing the composition order: *"Execution order when all three are present: (1) debounce delay fires; (2) the try body executes — if `tryCatch.tryActions` is present it is the try body, otherwise `actions` is the try body; (3) on error, `catchActions` runs."*
3. Document that `debounce` wraps the entire try/catch unit (not just the try body).

---

### Finding 12 — Sample's `savePlan` is missing the `NeedID` parameter

**Severity:** Minor  
**Location:** Sample → `requests.savePlan`; Sample → `layout` → `cboClientNeed`

**Observation:** `cboClientNeed` is a ComboBox bound to `"loadPlan.NeedID"`. If the user selects a need, that selection presumably must be persisted. However, `savePlan.parameters` does not include a `needId` (or `NeedID`) parameter. Either (a) the NeedID field is stored on a child join table and saving is handled elsewhere (which should be noted), or (b) this is a sample authoring gap — the ComboBox binding is two-way and the result should be saved.

**Why it matters:** The sample is the reference for Session 2 to verify the generator's output. If the generator emits a ComboBox binding that saves nothing, the smoke test will be incorrect. Even if NeedID is persisted via a different mechanism, the sample should document why.

**Recommendation:** Either add `needId: { _request: "loadPlan.NeedID" }` to `savePlan.parameters`, or add a comment explaining that `NeedID` is saved via a separate child-record mechanism (e.g., a different command). **Fix in sample before commit.**

---

### Finding 13 — Request parameter evaluation timing is unspecified

**Severity:** Minor  
**Location:** Schema → `$defs/request` → `parameters`; Sample → `requests.loadNeedsForClient`

**Observation:** `loadNeedsForClient.parameters.clientId: { _request: "loadPlan.ClientID" }` requires that `loadPlan` has already been executed. The schema says nothing about *when* parameters are evaluated — at component init, at request definition time, or lazily at the moment the `Request` action fires.

**Why it matters:** If the generator evaluates all parameters at init time, `loadNeedsForClient`'s `clientId` would resolve to null (loadPlan hasn't run yet). The correct behavior is lazy evaluation at call time. This is the same root issue as Finding 2 but deserves explicit documentation.

**Recommendation:** Add to the `request` `$defs` description: *"Parameters are evaluated lazily at the moment the `Request` action fires, against the current in-memory state and request-result snapshots at that instant."* This is a documentation-only fix, no schema structure change needed.

---

## 3. Suggested Ordering

### Fix BEFORE committing Session 1 (true blockers for Session 2's T4 generator)

| # | Finding | Why it blocks Session 2 |
|---|---|---|
| 5 | Write-back binding semantics unspecified | Generator cannot emit `@bind-Value` — the fundamental two-way binding expression — without knowing the mutability model. |
| 3 | Binding literal string ambiguity | Generator has two incompatible code paths; sample uses both inconsistently. Fix sample to use `_request` uniformly, or formally define the literal-string convention. |
| 10 | No `readOnly` on writable controls | Generator cannot distinguish display-only from editable TextBoxes; heuristic inference is fragile. |
| 11 | Debounce/tryCatch/actions order unspecified | Two engineers produce incompatible generators; must document execution order before any event-handling code is written. |
| 12 | `savePlan` missing `NeedID` parameter | Sample is the generator smoke-test target; the authoring gap will produce a misleading validation result. |
| 9 | `RefreshRequest` missing from action catalog | Not needed for *this* sample but should be added before Session 2 publishes the schema as stable, or the next form author will be immediately blocked. |

### Backlog for `schema-refinements.md` (later sessions)

| # | Finding | Recommended session |
|---|---|---|
| 4 | Field-scoped validation rules | Session 2 or 3 — should be in schema before the second form is authored |
| 2 | Per-call parameter overrides on `Request` action | Session 3+ — triggered when a concrete flow requires it |
| 8 | Operator catalog gaps (string, date, coercion) | Session 2 (add `_concat`, `_now`, `_dateadd`, `_cstr`, `_cint`, `_cdate`) + Session 3+ for remainder |
| 9 | `OpenModal`, `SetFocus`, `ResetForm` actions | Session 3 |
| 1 | Flat `_state` pattern adequacy | Revisit if/when a WizardDetailForm type is introduced |
| 6 | ListBox scope decision documentation | Add comment to schema before Session 2; no code change |
| 7 | `modalType` intent vs. source semantics | Document in harvester spec; no schema change |
| 13 | Request parameter evaluation timing | Documentation-only; add to schema `$defs/request` description before Session 2 commit |