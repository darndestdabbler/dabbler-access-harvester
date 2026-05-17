# ModalDetailForm Slice Generator — Validation Notes

> **Session 2 of `lowdefy-ir-foundation`. Author: Opus 4.7 orchestrator.**
> Captures friction surfaced while building the minimal slice generator
> ([`tools/AccessHarvester.IrCodegen/`](../../../tools/AccessHarvester.IrCodegen/))
> and validating its output against the Blazor pilot's csproj.

---

## Validation outcome

- **Generator builds clean:** `dotnet build tools/AccessHarvester.IrCodegen`
  → 0 warnings, 0 errors.
- **Generated output compiles in pilot csproj:**
  `dotnet build output-tests/blazor-add-an-order` → 0 errors, 0 warnings
  with the generated `FrmBudgetServicePlan.razor` +
  `FrmBudgetServicePlan.razor.cs` included.
- **Runtime not exercised.** `ExecuteRequestAsync` and `OnConfirmAsync` are
  emitted as virtual stubs that throw or auto-confirm; running the generated
  component end-to-end requires a hand-written `.Custom.cs` partial that
  binds these to the host app's named-query infrastructure.
- **No hand-written reference component existed in
  [`output-tests/blazor-add-an-order/Ui/Pages/`](../../../output-tests/blazor-add-an-order/Ui/Pages/)**
  — the pilot has only `Home.razor` and `OrderEntryPage.razor`. Per
  [spec.md:128](spec.md), this falls into the alternative validation path:
  code-review the generator's output for reasonableness rather than diff
  against a hand-written target.

## Generator implementation choices (and what those chose against)

- **YAML scalar coercion handled manually.** YamlDotNet's
  `Deserialize<object>` returns strings for every untagged plain scalar
  (a YAML 1.2 Core-schema artifact). The generator walks the YamlNode
  representation directly and applies a custom plain-scalar coercer
  (`true`/`false` → bool, integer/float literals → number, `null`/empty →
  null, everything else → string). Quoted YAML scalars stay strings even
  when their text reads as numeric. Without this layer, `readOnly: true`
  would have been emitted as `string? readOnly` and `_const: false` would
  have written `isDirty = "false"` to a `string?` variable.

- **Request rows modeled as `Dictionary<string, object?>`** (or
  `List<Dictionary<string, object?>>` when a layout block names the
  request via `optionsRequest`). The natural-sounding first instinct
  was `dynamic`, but `@bind-Value="loadPlan.FiscalYear"` on a dynamic
  rvalue emits a setter lambda inside a dynamic invocation, which the C#
  compiler rejects (`CS1977: Cannot use a lambda expression as an
  argument to a dynamically dispatched operation`). Dictionary indexers
  are statically typed, so two-way binding compiles. Cost: every read in
  the emitted C# is `row?["Field"]` (an `object?`); arithmetic and
  comparison operators don't apply natively, requiring
  `System.Convert.ToDouble(...)` wrappers around the arguments.

- **Single-quoted Razor attribute delimiters** are used wherever the C#
  expression body contains double-quoted string literals. Razor does NOT
  honor `\"`-escapes inside double-quoted attributes; the inner `"` ends
  the attribute. Single-quoting the attribute side-steps the issue
  without forcing the C# side to use chars instead of strings.

- **Two-way binding is decomposed into `Value` + `ValueChanged` pairs**
  rather than `@bind-Value`. `@bind-Value` is the prettier shorthand,
  but combined with `Dictionary<,>` indexers it produces a syntax tree
  the Razor compiler cannot parse cleanly. Emitting the pair explicitly
  works at the cost of more verbose markup.

- **`Request` action emits a stub `ExecuteRequestAsync(string)` call**
  rather than wiring up an HTTP/DB client. The stub method on the
  emitted partial class throws `NotImplementedException`; the user
  overrides in `<Class>.Custom.cs`. This is the spec-acknowledged
  "placeholder for hand-written logic" pattern ([spec.md:113](spec.md)).

- **`Confirm` action emits `await OnConfirmAsync(message)`**, which
  defaults to auto-confirm (`Task.FromResult(true)`). MudBlazor 9.x's
  `IDialogService.ShowMessageBox` API was unavailable as an extension
  method by the name the generator initially attempted; rather than
  pin the generator to a specific MudBlazor version's surface, the
  prompt-rendering responsibility was punted to `.Custom.cs`.

## Cross-provider review findings folded in

Sonnet 4.6's Step 4 review surfaced gaps the orchestrator's first pass
missed:

- Schema-declared `visible` / `enabled` block properties were silently
  dropped (SR-H).
- `_op` references generated `NamedOperators.<name>()` against a
  non-existent class; the top-level `operators` section was entirely
  unprocessed (SR-I). Defensive fix landed: emit a TODO + `default!`
  so the generated C# compiles.
- Boolean-context operators (`_iif`, `_not`, `_and`, `_or`) did not
  coerce `object?` operands to `bool`. The current sample evaded the
  bug because every boolean sub-expression bottomed out in `_isnull`
  (already `bool`). Defensive fix landed:
  `System.Convert.ToBoolean(...)` wrapping (SR-J).
- `Line.orientation` was ignored. Fixed inline: vertical orientation
  emits `<MudDivider Vertical="true" />`.
- One factual error in this orchestrator's first refinements draft
  was corrected: SR-G was rewritten to acknowledge that the schema
  does **not** currently constrain event-key shape — the generator's
  PascalCase mapping is a pure generator-side convention.

The remaining findings (debounce ignored, triple-title artifact,
command-type request mis-typing, onValueChanged-as-OnBlur semantic,
trailing `await Task.CompletedTask`, no `dataType` on TextBox) are
captured in `schema-refinements.md` SR-K through SR-P without
defensive emit changes. SR-P specifically covers the trailing-await
issue Step 4 Finding 10 surfaced; an early draft of these notes
overstated the cover and was reworded after the GPT-5.4 verifier round 1
flagged the gap.

## Friction surfaced during authoring (feeds `schema-refinements.md`)

The full list lives in [`schema-refinements.md`](schema-refinements.md).
Cross-reference markers (`SR-N`) match that file's numbering. Items
below are new findings from this session; items inherited from Session
1 Step 3's cross-provider review and the GPT-5.4 verifier are listed
there but not repeated here.

- **SR-A: Request cardinality is implicit (single vs list).** The schema
  treats every named request uniformly; the generator infers list-ness
  by scanning whether a layout block uses the request name as an
  `optionsRequest`. Adding an explicit `cardinality: single | list`
  property to `$defs/request` would remove the heuristic.

- **SR-B: Operator-tree numerics demand value-type guidance.**
  `_sub` / `_gt` etc. on `Dictionary<string, object?>` operands force
  `Convert.ToDouble(...)` coercion in the emitted C#. With a typed DTO
  emitter (a follow-up to this slice) those wrappers go away. Worth
  reflecting in the operator-catalog docs that downstream codegen must
  handle untyped operands.

- **SR-C: Computed bindings on writable controls would have to be
  rejected at schema time.** The schema's Session 1 verifier fix
  (Finding 3, applied in `d71ab21`) requires `readOnly: true` to use
  any non-`_request` expression. That constraint flows through the
  generator: the emit path that builds a `ValueChanged` setter assumes
  the binding is a direct `_request` operator. Without the schema
  constraint, the generator's `ParseRequestRef` would need a "computed
  binding on writable control" error path. Keep the constraint.

- **SR-D: ListBox / table-row controls.** The source form's two
  ListBoxes were explicitly scoped out as MasterDetailListPage
  territory. That decision held up: there is no clean way to put a
  list-of-children inside a ModalDetailForm without overlapping the
  next IR type's responsibilities. The generator is right to refuse
  ListBox as a block type until a `MasterDetailListPage` type exists.

- **SR-E: Generator stubs are explicit but undocumented.** The emit
  contains `ExecuteRequestAsync` and `OnConfirmAsync` virtual stubs
  whose names are not in the schema or the spec. A future schema
  iteration should formalize the "runtime escape hatch" surface so
  consumers know which method names the generator depends on.

- **SR-F: The schema's `state` declares initial values, but those
  values are not emitted as defaults in C# field initializers.**
  The generator declares `protected bool isDirty;` but the IR's
  `state.isDirty: false` should land as `protected bool isDirty = false;`.
  No correctness issue (default(bool) is false) but for non-default
  initial values (`state.fiscalYear: 2026`) this would actually matter.
  Minor.

- **SR-G: Event-name → method-name mapping is a generator convention,
  not a schema rule.** The generator PascalCases event names (`onSave`
  → `OnSave`). Razor attribute values reference the C#-side name; both
  the layout button and the code-behind method must agree. This convention
  is invisible to schema validators; a future schema iteration could
  formalize it (e.g., declare event keys must satisfy
  `^on[A-Z][a-zA-Z0-9]*$`, which they already do, and document the
  PascalCase mapping).

## Production-readiness gap

Two pieces stand between this slice and a production generator:

1. **Typed DTOs.** Replace `Dictionary<string, object?>` with emitted
   record types per request. Requires the schema to either declare each
   request's column shape OR inspect a sibling named-query catalog
   (the future `Requests/*.sql` index).
2. **A real request runner.** A library project at e.g.
   `AccessHarvester.Blazor.Runtime` providing `IRequestRunner`, dialog
   services, validation infrastructure, and a base partial class that
   replaces the per-component stubs.

Both belong to a different (larger) initiative. This slice's job was to
prove the IR shape and surface friction; it has done so.
