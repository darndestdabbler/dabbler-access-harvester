# ModalDetailForm Schema — Refinements Backlog

> **Output of `lowdefy-ir-foundation` Session 2.** Consolidates the items
> the Session 1 cross-provider review (Sonnet 4.6) and end-of-session
> verifier (GPT-5.4) deferred, plus the new findings surfaced while
> building the minimal slice generator
> ([`generator-validation-notes.md`](generator-validation-notes.md)).
>
> Each item is a candidate change to
> [`AccessHarvester.Core/Ir/ModalDetailFormSchema.json`](../../../AccessHarvester.Core/Ir/ModalDetailFormSchema.json),
> the sample IR, or the generator. Severity reflects priority for the
> next IR session (probably "Report IR foundation" or
> "MasterDetailListPage IR foundation"). Items the generator confirmed
> are landed in the schema (RefreshRequest, readOnly, container-only
> root, etc.) are not repeated here — see git history of `d71ab21` /
> `9703bfe`.

---

## Carryover from Session 1 Step 3 cross-provider review (Sonnet 4.6)

These were marked "Backlog for `schema-refinements.md`" in
[`session-reviews/session-001-step-03-cross-provider-review.md`](session-reviews/session-001-step-03-cross-provider-review.md).
Verbatim summaries; full details in that review.

### SR-1 (Major): Validation rules need an optional `field` target

**Schema location:** `$defs/action` → `Validate` variant → `params.rules[]`.

Each rule has `expression` + `message` but no `field` name. Rules are
global predicates over the whole IR state. Blazor's standard validation
UX (`ValidationMessage<T>`, MudBlazor's `Error`/`ErrorText`) wants
errors anchored to a specific field. The slice generator emits all
errors via `Snackbar.Add(...)` — a summary banner, not inline. Adding
an optional `field` (a layout block `id`) lets the generator emit a
field-level error indicator when present and fall back to the snackbar
when absent.

**Recommended next session:** Session 3 or whenever the second
ModalDetailForm sample lands.

### SR-2 (Major): `Request` action with per-call parameter overrides

**Schema location:** `$defs/action` → `Request` variant.

Parameters today live on the `$defs/request` definition; a `Request`
action only references the request by name. A future flow that needs
to fire the same request with a different runtime argument has no
choice but to declare a new request. Adding an optional
`paramOverrides: { <name>: <expression> }` on the `Request` action
covers the case without forcing request proliferation. The slice
generator confirmed that the current design works for the sample's
sequence-then-fire pattern but flagged the asymmetry.

**Recommended next session:** Session 3+ (when a flow actually needs
it).

### SR-3 (Major): Operator catalog gaps — string, date, type coercion

**Schema location:** `$defs/expression` → operator `oneOf` branches.

The Sonnet 4.6 reviewer enumerated:

| Category | Missing | Access equivalent |
|---|---|---|
| String | `_concat`, `_trim`, `_left`, `_right`, `_mid`, `_len`, `_instr`, `_ucase`, `_lcase`, `_replace` | `&`, `Trim()`, `Left()`, `Mid()`, `Len()`, `InStr()`, `UCase()`, `LCase()` |
| Date | `_now`, `_date`, `_dateadd`, `_datediff`, `_datepart` | `Now()`, `Date()`, `DateAdd()`, `DateDiff()`, `DatePart()` |
| Type coercion | `_cstr`, `_cint`, `_cdate`, `_ccur`, `_cdbl` | `CStr()`, `CInt()`, `CDate()`, `CCur()` |
| Domain aggregates | `_dmin`, `_dmax`, `_davg` | `DMin()`, `DMax()`, `DAvg()` |
| Comparison | `_like` | `Like` (VBA/SQL) |

The slice generator's expression visitor would absorb each new operator
with a one-line addition; the schema's `expression` `oneOf` grows
accordingly. **Recommended:** Add `_concat`, `_now`, `_dateadd`,
`_cstr`, `_cint`, `_cdate` in Session 3 (the next form sample will
likely hit at least one); the rest land as the migration corpus widens.

### SR-4 (Major): Action catalog — `OpenModal`, `SetFocus`, `ResetForm`

**Schema location:** `$defs/action`.

`RefreshRequest` landed in this set. `OpenModal` (launch a child
ModalDetailForm awaiting result), `SetFocus`, `ResetForm` (Access's
`Me.Undo`), and `SetField` (explicit dictionary write) remain. The
slice generator did not exercise these; the sample's flow is
single-form Save/Cancel/Delete. Add when a flow demands.

### SR-5 (Minor): Flat `_state` pattern adequacy

**Schema location:** `$defs/expression` → `_state` operator pattern
(`^[a-zA-Z0-9_]+$`).

GPT-5.4 verifier round 1 accepted that flat state is right for
ModalDetailForm; a future WizardDetailForm type might want nested
state. Revisit then. No change for this IR type.

### SR-6 (Minor): ListBox scope is documentation, not enforcement

**Schema location:** `description` field of `$defs/block` /
`$defs/containerBlock`.

Document explicitly that ListBox / DataGrid don't belong to
ModalDetailForm; they're the responsibility of MasterDetailListPage.
The slice generator simply refuses unknown block types via the
schema's closed enum, which is the right behavior — the documentation
update is purely a hint to authors so they don't try.

### SR-7 (Minor): `modalType` is intent, not source-mechanical

**Schema location:** `metadata.modalType` description (already updated
in `d71ab21`). Verbatim from the Sonnet review's recommendation:
the harvester's inference rules (Access `acDialog` → `modal`,
standalone navigation → `page`) belong in the harvester spec, not
this schema. No action here.

---

## New findings from Session 2 generator authoring

### SR-A (Major): Request cardinality is implicit

**Schema location:** `$defs/request`.

The schema doesn't declare whether a named query returns a single
record or a list. The slice generator infers list-ness by scanning
whether any layout block names the request as `optionsRequest`; everything
else is single-row. This is a load-bearing heuristic — wrong inference
emits the wrong C# type (`Dictionary<string, object?>?` vs
`List<Dictionary<string, object?>>?`).

**Recommended:** Add `cardinality: single | list` to `$defs/request`,
required. The generator drops its inference logic and reads the
explicit declaration.

### SR-B (Minor): Operator-tree arithmetic on untyped operands

**Schema location:** documentation of `$defs/expression`.

Arithmetic operators (`_add`, `_sub`, `_mul`, `_div`, `_mod`) operate
on `object?` operands when the generator targets `Dictionary<string,
object?>` rows. The generator wraps each operand in
`System.Convert.ToDouble(...)`. With a future typed-DTO emitter this
boilerplate disappears.

**Recommended:** Document in the operator section that downstream
codegen targeting untyped row representations must wrap arithmetic
operands.

### SR-C (Already enforced): Writable bindings must be `_request`

The Session 1 verifier-fix (Finding 3, `d71ab21`) made the schema
require this via `if/then`. The slice generator's writeback emission
relies on this; relaxing it would force the generator to either reject
non-`_request` two-way bindings at runtime or grow a much larger
parser. **No action — keep the constraint.**

### SR-D (Already correct): ListBox absence

Confirmed correct by generator authoring. **No action.**

### SR-E (Minor): Runtime escape-hatch surface

**Schema location:** schema description; possibly a new
`runtimeContract` section.

The generator emits virtual `ExecuteRequestAsync(string)` and
`OnConfirmAsync(string)` stubs. Those method names are a generator
convention invisible to the schema. A future iteration could declare
a `runtimeContract` block in the schema that documents which method
names the generator depends on, so a consumer overriding them in
`.Custom.cs` has an authoritative reference.

**Recommended:** Session 3+ when the runtime library exists; until
then, document on the generator side only.

### SR-F (Minor): State initial values not emitted as field defaults

**Schema location:** generator behavior, not schema.

The IR's `state.X: <initialValue>` declares an initial value; the
generator currently emits the field without a default initializer
(relies on C# `default(T)`). For `state.fiscalYear: 2026` this would
matter. **Recommended:** generator change — emit
`protected long? fiscalYear = 2026;` when the IR provides a non-null
initial value.

### SR-G (Minor): Event-name → method-name mapping is implicit

**Schema location:** `events` definition (no constraint today).

The schema currently imposes **no** pattern constraint on event keys
(`events` is `{type: object, additionalProperties: {$ref: event}}`
with no `propertyNames` clause). The generator assumes keys follow
`^on[a-z][a-zA-Z0-9]*$` and PascalCases them to derive C# method
names (`onSave` → `OnSave`). A key that violated this assumption
(e.g., `save-record` or `1stOpen`) would produce an invalid C#
identifier. **Recommended:** add `propertyNames: { pattern:
"^on[a-z][a-zA-Z0-9]*$" }` to the `events` definition, and document
the PascalCase mapping in the description.

### SR-H (Major): `visible` / `enabled` block properties silently dropped

**Schema location:** `$defs/block` → `properties.visible`, `properties.enabled`.

The schema declares both as expression-valued properties on every
block (containers and controls alike). The generator's `EmitBlock`
switch never reads them. Any IR using `visible: { _state: showAdvanced }`
or `enabled: { _iif: [...] }` renders the control unconditionally with
no compile-time signal. The sample does not exercise either, so the
gap is invisible to the smoke test.

**Recommended:** Wrap each emitted block in `@if (<visible-expr>) { ... }`
when `visible` is present and not literal `true`. Pass `Disabled="!@(<enabled-expr>)"` (MudBlazor uses `Disabled`, not `enabled`) on
controls when `enabled` is present and not literal `true`. Land before
the next sample IR exercises conditional visibility.

### SR-I (Major): `_op` references a non-existent class; `operators` section is unprocessed

**Schema location:** `$defs/expression` → `_op` branch; top-level `operators`
property; generator's `EmitOperator` `case "_op"`.

The generator emitted `NamedOperators.<name>()` for `_op` expressions
before the Step 4 fix, referencing a static class that is never
generated. The `operators` section (top-level map of name → expression)
is never read by the generator. Any IR that used `_op` produced code
that failed to compile; any IR with non-empty `operators` is silently
unrendered.

**Defensive fix applied:** `_op` now emits
`/* TODO: _op '<name>' -- operators section not yet processed */ default!`
so the C# at least compiles.

**Recommended (next session):** Emit a `NamedOperators.<ClassName>`
static C# class alongside `<ClassName>Base`, with one method per entry
in the IR's `operators` section. Each method body is the inlined
expression-tree emit. Update `_op` to call `NamedOperators.<name>()`
once the class exists.

### SR-J (Major): Boolean-context operators need explicit coercion

**Schema location:** `$defs/expression` → `_iif`, `_not`, `_and`, `_or`;
generator's `EmitOperator` for those operators.

`_iif`, `_not`, `_and`, `_or` emit C# constructs that require `bool`
operands. When an operand is itself a `_request` or `_state` ref
(both `object?` in the generator's row model), C# does not coerce
implicitly. The current sample evades this because every boolean
sub-expression bottoms out in `_isnull` (returns `bool` natively).
A second form with `{ _iif: [{ _request: "loadPlan.IsActive" }, "y", "n"] }`
would produce a `CS0029` type error.

**Defensive fix applied:** boolean-context operators now wrap each
operand in `System.Convert.ToBoolean(...)`. Pattern matches the
existing `System.Convert.ToDouble(...)` wrapping for arithmetic
operators.

### SR-K (Minor): `debounce` is silently ignored

**Schema location:** `$defs/event.debounce.delayMs`; generator's
`EmitEventHandler` (no read of `debounce`).

An event declaring `debounce: { delayMs: 300 }` is emitted as an
immediate handler. The intent (collapse rapid fires into one) is lost.

**Recommended:** Either implement debounce via a per-handler
`CancellationTokenSource`/`Task.Delay` pattern, or — at minimum —
emit a `// TODO: debounce <delayMs>ms` comment at the top of the
emitted handler body when `debounce` is present, so the gap is
visible in the generated code.

### SR-L (Minor): Section title is redundant inside a MudDialog

**Schema location:** generator behavior; sample IR's
`layout.title: "FY-Budgeted Service Plan"`.

For `modalType: modal`, the generator emits the form title three
times: once from `metadata.title` inside `<TitleContent>`, once from
the root Section's `title`, and once from the `lblAppTitle` Label
block. Two of these are redundant inside a MudDialog. The Label is
an IR-authoring artifact (the source Access form has an `lblAppTitle`
duplicated for the same reason — Access modal forms often repeat their
title in a Label control).

**Recommended:** Suppress the root Section's title emission when
the enclosing `modalType == "modal"` and the title text matches
`metadata.title`. The Label block is the sample's choice; leave it.

### SR-M (Minor): Command-type requests are mis-typed as row dicts

**Schema location:** `$defs/request`; generator's row-field declaration.

`cmdUpdateBudgetServicePlan` and `cmdDeleteBudgetServicePlan` are
command requests (fire-and-return; no row payload). The generator
declares them as `Dictionary<string, object?>?` and assigns
`savePlan = await ExecuteRequestAsync("savePlan")`, which mis-implies
that an update command returns a row.

**Recommended:** Extend SR-A's `cardinality` enum to three values:
`single | list | command`. `command` emits
`await ExecuteRequestAsync(...)` without an assignment target, and
does not declare a row field.

### SR-N (Minor): `onValueChanged` → `OnBlur` semantic shift

**Schema location:** generator's `EmitTextBox`; documentation.

The schema's `TextBox.onValueChanged` reads as "fire on value change."
The generator maps it to MudBlazor's `OnBlur` (fire on focus exit).
The two fire at different times. The mapping is closer to Access's
field-exit `AfterUpdate` semantics, which is plausibly the migration
intent, but the semantic shift is undocumented in the schema.

**Recommended:** Document the mapping in the schema's
`onValueChanged` description (Access `AfterUpdate` → MudBlazor `OnBlur`).
Optionally: split into `onValueChanged` (per keystroke) and
`onBlur` (focus exit) in a future revision if forms want both.

### SR-P (Minor): Trailing `await Task.CompletedTask` in every emitted handler

**Schema location:** generator behavior; emitted `.razor.cs` event
handlers.

Every emitted event handler ends with `await Task.CompletedTask;`. The
statement is unreachable after a `return` in the Validate guard. In
`OnFiscalYearChanged`, where the body is purely synchronous, the
trailing await exists only to suppress the "async method has no await"
compiler warning — but suppressing it that way is the wrong fix; the
correct fix is to not emit `async` for sync handlers in the first
place.

**Recommended:** Track whether at least one real `await` was emitted
during handler emission. When none was, emit
`return Task.CompletedTask;` and drop the `async` keyword
(`protected Task OnFiscalYearChanged() { isDirty = true; return Task.CompletedTask; }`).
Idiomatic Blazor for sync `EventCallback` handlers.

### SR-O (Minor): No `dataType` on writable controls

**Schema location:** `$defs/controls/textBox`, `comboBox`, `checkBox`.

The schema does not annotate a control's expected value type. The
generator unconditionally emits `<MudTextField T="string">` even for
integer (`FiscalYear`) and currency (`AnnualBudget`) fields, which
would more naturally be `<MudNumericField>`. Without a type hint
the generator cannot dispatch to the right MudBlazor control.

**Recommended:** Add `dataType: "string" | "int" | "decimal" | "date" | "bool"`
(or similar) to writable control branches in the schema. Generator
dispatches MudBlazor control class accordingly. Pairs with SR-A and
SR-3 for the typed-DTO future.

---

## Priority for the next session

| Item | Severity | Trigger to act |
|---|---|---|
| SR-H: emit `@if (visible)` and `Disabled` from block properties | Major | Before any conditional-visibility IR is authored |
| SR-I: implement `operators` section + `_op` codegen | Major | Before any IR uses `_op` |
| SR-A: explicit request cardinality (incl. SR-M command) | Major | Before Session 3's first form sample |
| SR-3: operator catalog (string + date + coercion subset) | Major | Before Session 3's first form sample |
| SR-1: optional `field` on Validate rules | Major | When second ModalDetailForm sample lands |
| SR-J: boolean coercion in `_iif`/`_and`/`_or`/`_not` | Major | Fixed defensively this session; promote to typed-DTO follow-up |
| SR-F: state initial-value emission | Minor | When first form needs a non-default state |
| SR-G: pattern-constrain event keys | Minor | Anytime |
| SR-O: `dataType` on writable controls | Minor | When typed DTOs land |
| SR-K, L, N: debounce / redundant title / onBlur mapping | Minor | When use case materializes |
| Others | Minor | When their use case materializes |
