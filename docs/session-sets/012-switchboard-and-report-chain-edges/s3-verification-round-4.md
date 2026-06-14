# Set 012 S3 — routed session-verification (Round 4, post devil's-advocate)

---

A. **C3 owner-kind fix:** Confirmed. Moving owner-kind selection from a name-keyed lookup to each module’s own `ModuleScope.Kind` is the correct fix for form/report same-name collisions. It removes the overwrite failure mode entirely, because attribution is now per harvested module instance, not per shared bare name. On the shown code, I do not see a new determinism risk: the `HashSet` use is membership-only, while emission order remains driven by the input module/procedure walk. I also do not see a new misclassification path in the fix itself; non-form/report modules are explicitly skipped, and form/report modules are attributed from their authoritative harvested kind.

B. **C4 TempVars setter widening:** Confirmed. The collector now covers the relevant static setter forms you listed: `TempVars!x = …`, `TempVars("x") = …`, `TempVars.Item("x") = …`, and `TempVars.Add ...` with either positional or `Name:=` naming. The `RecordNameSlot` logic is correctly constrained to the name slot only: explicit `Name:=` wins anywhere in the argument list; otherwise only the first positional argument is considered. That avoids the prior false negative cases and also avoids the false positive of mistaking a later value literal for the name when the first slot is dynamic.

C. **DW-14 deferrals:** Acceptable for closeout. As described, they are documented coverage gaps rather than sources of incorrect emitted data, and the newly widened supplied-universe scan specifically reduces false `tempvar-no-supplier` reports without inventing unsupported owner-side supplier edges. Given they are disclosed and tracked, they do not block verification of these two fixes.

```json
{"verdict":"VERIFIED","issues":[]}
```