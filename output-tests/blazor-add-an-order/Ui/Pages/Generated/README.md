# Generated Blazor components

> **Do not hand-edit.** This directory is the output target of the
> ModalDetailForm slice generator
> ([`tools/AccessHarvester.IrCodegen/`](../../../../../tools/AccessHarvester.IrCodegen/)).
> Files here are overwritten each time the generator runs against an IR
> sample. Any manual edits will be lost.

## What's here

Components emitted from the IR samples under
[`AccessHarvester.Core/Ir/samples/`](../../../../../AccessHarvester.Core/Ir/samples/).
Each emitted component is a pair:

- `<ClassName>.razor` — the layout markup
- `<ClassName>.razor.cs` — the partial-class code-behind (state, request
  result rows, event handlers, runtime escape-hatch stubs)

The escape-hatch stubs (`ExecuteRequestAsync`, `OnConfirmAsync`) throw
or auto-confirm at runtime. To wire the component to a real backend,
add a sibling `<ClassName>.Custom.cs` partial that overrides those
methods.

## Regenerating

From the repo root:

```bash
dotnet run --project tools/AccessHarvester.IrCodegen -- \
    AccessHarvester.Core/Ir/samples/<sample>.ir.yaml \
    output-tests/blazor-add-an-order/Ui/Pages/Generated
```

See [`tools/AccessHarvester.IrCodegen/README.md`](../../../../../tools/AccessHarvester.IrCodegen/README.md)
for the full generator invocation and options.

## Why these are committed

The Session 2 spec called for the generated output to be reviewable
alongside the hand-written pilot pages. Committing them lets reviewers
diff against schema and template changes in future sessions without
having to re-run the generator. Per the
[`lowdefy-ir-foundation` Session 2 decision](../../../../../docs/session-sets/lowdefy-ir-foundation/spec.md),
generated artifacts ship with the pilot rather than living in `tmp/`.
