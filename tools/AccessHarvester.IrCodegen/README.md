# AccessHarvester.IrCodegen

Minimal slice generator for the `ModalDetailForm` IR type. Reads a
schema-conformant IR file (YAML or JSON) and emits a Blazor / MudBlazor
component pair (`.razor` + `.razor.cs`).

Built in
[`lowdefy-ir-foundation`](../../docs/session-sets/lowdefy-ir-foundation/)
Session 2 to validate the Lowdefy-as-IR design concretely. The output is
a deliberately MVP-shaped emit — `Dictionary<string, object?>` row model,
explicit `Value` + `ValueChanged` pairs instead of `@bind-Value`, virtual
escape-hatch stubs for the runtime request runner and confirmation
prompt. The friction surfaced while authoring this generator is in
[`generator-validation-notes.md`](../../docs/session-sets/lowdefy-ir-foundation/generator-validation-notes.md);
the backlog of schema changes it implies is in
[`schema-refinements.md`](../../docs/session-sets/lowdefy-ir-foundation/schema-refinements.md).

## Build

```bash
dotnet build tools/AccessHarvester.IrCodegen
```

Targets `net10.0`, depends on `YamlDotNet`. The output assembly is
`tools/AccessHarvester.IrCodegen/bin/Debug/net10.0/AccessHarvester.IrCodegen.dll`.

## Run

```bash
dotnet run --project tools/AccessHarvester.IrCodegen -- \
    <input-ir.yaml-or-json> \
    <output-directory> \
    [<namespace>]
```

**Important:** invoke from the **repo root**, not from the project
directory. The output-directory argument is interpreted relative to the
current working directory; when `dotnet run --project ...` is run from
the project directory the relative path resolves against the project,
which is never the right place for the generated files. The
[.gitignore](../../.gitignore) blocks `tools/AccessHarvester.IrCodegen/output-tests/`
to prevent the stray-output footgun from re-committing.

### Default namespace

If the namespace argument is omitted, the generator emits
`BlazorAddAnOrder.Ui.Pages.Generated` (matches the pilot at
[`output-tests/blazor-add-an-order/`](../../output-tests/blazor-add-an-order/)).
Override for any other target project.

### Reference invocation

The canonical invocation used in Session 2:

```bash
dotnet run --project tools/AccessHarvester.IrCodegen -- \
    AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml \
    output-tests/blazor-add-an-order/Ui/Pages/Generated
```

Result: two files at
`output-tests/blazor-add-an-order/Ui/Pages/Generated/FrmBudgetServicePlan.razor`
and `FrmBudgetServicePlan.razor.cs`. The pilot csproj picks both up
automatically through its `Microsoft.NET.Sdk.Web` glob.

## What the generator understands

Driven by
[`AccessHarvester.Core/Ir/ModalDetailFormSchema.json`](../../AccessHarvester.Core/Ir/ModalDetailFormSchema.json).
Closed catalogs (validated by the schema, dispatched by the generator):

- **Layout blocks:** `Section`, `Row`, `Column`, `Group`, `TextBox`,
  `Label`, `ComboBox`, `CheckBox`, `Button`, `Line`.
- **Actions:** `Validate`, `Request`, `RefreshRequest`, `SetState`,
  `CallMethod`, `CloseModal`, `ShowError`, `Navigate`, `Confirm`.
- **Operators:** `_state`, `_request`, `_const`, `_op` (currently
  emits a TODO; the `operators` section is not yet codegen-aware),
  `_nz`, `_iif`, `_dlookup`/`_dcount`/`_dsum` (TODO stubs), `_format`,
  `_add`, `_sub`, `_mul`, `_div`, `_mod`, `_eq`, `_ne`, `_lt`, `_le`,
  `_gt`, `_ge`, `_and`, `_or`, `_not`, `_isnull`.

Operator coercion: arithmetic operands are wrapped in
`System.Convert.ToDouble(...)`; boolean-context operands are wrapped in
`System.Convert.ToBoolean(...)`. Both are necessary because the row
model is `Dictionary<string, object?>`; a future typed-DTO emitter will
remove the wrappers.

## What the generator does **not** yet do

See [`schema-refinements.md`](../../docs/session-sets/lowdefy-ir-foundation/schema-refinements.md).
Highlights:

- **SR-H:** `visible` / `enabled` expressions on blocks are silently
  dropped (no `@if` wrap, no `Disabled` attribute).
- **SR-I:** the top-level `operators` section is unprocessed; `_op`
  emits a TODO comment.
- **SR-K:** `debounce` on events is silently dropped.
- **SR-M:** command-type requests (`cmdXxx`) are mis-typed as row
  dictionaries.

Each is a candidate for the next IR session.
