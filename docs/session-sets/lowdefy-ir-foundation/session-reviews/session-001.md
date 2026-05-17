# Verification Round 1

{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "category": "Completeness",
      "location": "AccessHarvester.Core/Ir/ModalDetailFormSchema.json",
      "summary": "The schema omits the spec-required optional `operators` section and provides no way to reference named reusable operator trees.",
      "details": "Session 1 explicitly required the ModalDetailForm schema to include Requests, Layout, Operators, Events, and Metadata. The current schema only supports inline expressions under `$defs.expression`; it has no top-level `operators` property and no expression form for referencing a named operator. That deviates from the May 2026 synthesis and makes the schema incomplete for the required reusable-operator capability."
    },
    {
      "severity": "Major",
      "category": "Correctness",
      "location": "AccessHarvester.Core/Ir/ModalDetailFormSchema.json#/properties/layout",
      "summary": "The root `layout` is not actually restricted to a container block even though the schema text and session spec require a single container root.",
      "details": "The top-level `layout` property is defined as `$ref: #/$defs/block`, and `$defs/block` allows controls such as `TextBox`, `Button`, `Label`, and `Line` in addition to containers. That means an IR file with a top-level control would validate, which is a silently relaxed constraint. The schema should restrict `layout` to a container-only union (`Section`, `Row`, `Column`, `Group`) or an equivalent dedicated container definition."
    },
    {
      "severity": "Major",
      "category": "Correctness",
      "location": "AccessHarvester.Core/Ir/ModalDetailFormSchema.json#/$defs/controls",
      "summary": "Editable control bindings are over-permissive and allow non-assignable expressions that contradict the documented two-way binding semantics.",
      "details": "For `TextBox`, `ComboBox`, and `CheckBox`, the `binding` property accepts the full `expression` union while `readOnly` defaults to `false`. As written, a writable control can legally bind to a literal or computed expression such as `_sub`, even though the schema description says editing a writable control mutates the referenced request-result row. The schema should distinguish assignable bindings from display expressions, or conditionally require `readOnly: true` when the binding is not a direct field reference."
    },
    {
      "severity": "Minor",
      "category": "Correctness",
      "location": "AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml#/events/onLoad",
      "summary": "The sample uses `onLoad` for the lifecycle fetch event even though the session spec called for `onInit`, and the schema has no explicit lifecycle-event binding.",
      "details": "Feature 2 asked the sample to capture an `onInit` event for loading the record from OpenArgs-equivalent state. Because event names are free-form strings, `onLoad` validates, but it introduces an unstated convention and risks a Session 2 generator mismatch. The sample should use `onInit` consistently, or the schema should define explicit lifecycle trigger mapping instead of relying on arbitrary event names."
    }
  ]
}

---

# Verification Round 2

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
