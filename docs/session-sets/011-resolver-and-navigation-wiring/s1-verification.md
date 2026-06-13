A. **Two-pass correctness**

The two-pass idea itself is correct and, for the cross-module-public case, necessary. If `ReferenceResolver` walks `project.Children`, then resolution must happen only after every sibling module scope has been registered. On that narrow point, the restructure is sound and matches the stated `SymbolDbBuilder` pattern.

Where I do have a concern is the **degradation contract**, not the ordering. In the old per-module flow, a module that failed anywhere in parse/scope-build/resolve was isolated inside its own throwaway `ProjectScope`; it could not affect any sibling. In the new flow, every module scope is inserted into one shared `ProjectScope` during pass 1, and pass 2 merely catches resolution failures per module. That means two leakage cases now exist:

1. **Scope-build failure leakage**: if `BuildModuleScope(...)` mutates `project` before it throws, the catch logs/degrades the module, but any partially-registered scope/declarations can remain visible to later modules.
2. **Resolve failure leakage**: even when scope-building succeeds, a module that later hits `VBA_RESOLVE_FAILURE` is still left in the shared project. Later siblings resolved after that failure can bind to its public members, even though that module is being degraded/omitted.

That is not equivalent to the prior contract. It still avoids whole-manifest abort, but it no longer guarantees that a degraded module is non-participating from other modules’ perspective. If the intent is strict preservation of the old behavior, this needs cleanup or a prune/re-resolve step.

B. **owningClass name alignment**

This is the shakiest part of the session after A. The implementation is relying on `owningClass.Name` being the same identifier the Access object-model registry can use to find the form/report metadata, and the code comments explicitly say that works via the registry’s **by-Name fallback**.

That is probably fine **if and only if** the parse-loop `name`, the kb filename stem, and the Access object’s metadata `Name` are truly identical in all supported inputs. If that invariant holds, then the fact that Access document modules often have internal `ModuleName` values like `Form_<name>` does not matter for `Me`/control binding, because the registry lookup is joining on object name, not class-module name.

But I do not think the implementation proves that invariant. It synthesizes `owningClass` from the loop/file name rather than from authoritative extracted metadata, and there is no evidence here of a pin covering:
- `FormMetadata.Name != ModuleName`
- filename stem != metadata `Name`
- any normalization/sanitization mismatch between filenames and Access object names

So: the current wiring is plausible and will work for the common case, but it is brittle. It can silently miss `Me`/bare-control binding on projects where those names diverge.

C. **Default-member design**

This is mostly a good, conservative design.

Using `ResolvedToObjectModel` plus a `SyntheticDefaultMemberDeclaration` is a reasonable way to avoid changing the closed `ResolutionOutcome` hierarchy. The critical safety property is that it resolves only the indirection (`Recordset.Fields`) and does **not** invent a fake declaration for the bang key (`Notes`). That part is correct and avoids over-claiming.

The `isBang` gate is also correct. Dot access should not start consulting default members here.

The `IsParameterized` gate is also correct and importantly prevents false resolutions on scalar defaults like `TextBox.Value`. So `rs!Notes` can map through `Fields`, but `txtName!Foo` does not incorrectly map through `Value`.

The per-site synthetic declaration is acceptable **if** the cross-reference index truly keys synthetic declarations by identity/reference, as claimed. Given that assertion, per-site instances are fine; they do not need global caching. If that claim were false, this design would be risky, but based on the information provided I do not have enough to call it broken.

My only non-blocking caution here is compatibility: `BuildDeclarationTarget` is now emitting `Kind: "DefaultMember"`, which is a new manifest-level target-kind string. The provided green runs suggest that did not break current validation/consumers, so I would not block on it, but it is the one place in this design that deserves explicit schema/consumer coverage.

D. **SqlStrings omission**

I agree with this change. If production never ran dynamic SQL analysis, then emitting `SqlStrings: []` was asserting a result that the producer did not actually compute. Under the stated convention—only emit what the producer actually wrote, and do not synthesize defaults—omission is the honest encoding.

Since `Body.SqlStrings` is optional in the schema, omission cleanly means “not analyzed / not produced,” while `[]` means “analyzed and none found.” Those are materially different states. So omission is the correct call.

E. **Golden deltas**

The reported deltas are broadly consistent with F-3-4.

- `not-in-scope -> Resolved` is exactly what shared `ProjectScope` should do.
- `Me` and `member-access-on-unresolved-target -> Resolved` is exactly what adding the Access registry + owningClass should do.
- `default-member-not-yet-resolved -> Resolved:DefaultMember` is exactly what the bang-default-member logic should do.
- The `member-access-on-unresolved-target -> default-member-not-yet-resolved` bucket is plausible as a masked-gap surfacing effect: once `Me.control` starts resolving, the next unresolved hop becomes visible instead of the earlier unresolved target masking it.

So I do **not** read the golden numbers, by themselves, as evidence of a semantic regression.

However, those deltas also do not exercise the two structural risks above:
- HomeHealthCare may simply have no pass-2 resolve failures, so the shared-project contamination issue would stay invisible.
- It may also not contain a form/report whose Name/ModuleName/file-basename diverge, so the owningClass lookup brittleness would stay invisible.

So the goldens support the intended wins, but they do not fully discharge A or B.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    { "severity": "Major",
      "location": "AccessHarvester.Core/Pipeline.cs",
      "summary": "Modules that fail scope-building or pass-2 resolution can remain registered in the shared ProjectScope, allowing later modules to resolve cross-module references against modules that are being degraded or omitted.",
      "fix": "Make shared-project registration transactional: only attach a module scope after successful scope-build, and if any module fails resolution, prune failed scopes from the shared project and re-resolve remaining modules (or otherwise ensure degraded modules cannot participate in sibling lookup)." },
    { "severity": "Minor",
      "location": "AccessHarvester.Core/Pipeline.cs",
      "summary": "owningClass is derived from the loop/file name and relies on registry by-Name fallback, which can silently miss Me/control binding when Access object Name, ModuleName, and extracted filename are not identical.",
      "fix": "Derive the owning-class lookup key from authoritative form/report metadata (or have the registry try both Name and ModuleName explicitly) and add a pin covering Name≠ModuleName/file-basename divergence." }
  ]
}
```