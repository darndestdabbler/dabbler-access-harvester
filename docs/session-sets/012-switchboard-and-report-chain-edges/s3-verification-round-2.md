# Set 012 S3 — routed cross-provider session-verification (Round 2)

cost: $0.0000 | tokens(out): 12776 | truncated: False

---

1. **Major (R1 concern 2b) — member/index-wrapped LHS write false-positive.**  
This fix is correct for the covered two-bang control-reference surface. `HandleAssignmentTarget` runs before the normal walk, `UnwrapToControllingBang` strips `.Member` and `(index)` postfix layers, and `_writeTargets` is keyed by reference identity, so the exact LHS bang node is suppressed without muting a structurally similar RHS read elsewhere. For `Forms!frm!ctl.Value = 42` and `Forms!frm!ctl(0) = 42`, the wrong-direction read edge is now blocked; for `x = Forms!frm!ctl.Value`, the read still emits because only the LHS node is marked. I do not see a determinism regression here.

2. **Major (R1 concern 4) — non-bang TempVars setter forms.**  
This is **not complete as implemented**. The collector does broaden the supplied-name universe, but two correctness gaps remain:

   - `RecordFirstLiteralArg` is not actually “first argument” logic; it records the **first positional string literal anywhere in the argument list**. That means `TempVars.Add someDynamicName, "literalValue"` will wrongly record `"literalValue"` as a supplied TempVar name and can suppress a real `tempvar-no-supplier`. The method needs to inspect the name slot only, not scan forward for any later literal.
   - The broadened recognition was added only to `CollectAssignedTempVars`, not to owner-side edge production. In form/report code-behind, `TempVars("x") = ...` and `TempVars.Add "x", ...` still do **not** emit the `Owner -> TempVar` `SuppliesParameter` edge that `TempVars!x = ...` emits. So the false unresolved is suppressed, but the structural chain remains incomplete.

   Determinism is still preserved, but this item is not fully fixed.

3. **Minor (R1 concern 4 doc) — stale closed `Reason` set.**  
The documentation update is fine. Listing `non-literal-target` and `tempvar-no-supplier` in the XML doc for `UnresolvedChainTarget.Reason` resolves the stale-comment issue cleanly.

4. **Major deferrals (DW-14) — named-argument open targets and indexer/`With` control reads.**  
I would accept both deferrals as scope-legitimate for this closeout, with one distinction:

   - **Named-argument `OpenForm`/`OpenReport` target**: legitimate deferral. The gap is shared with the already-shipped VBA-edge layer, the dataset is golden-neutral, and fixing only this chain layer would create cross-layer disagreement on the same call shape. That is a reasonable DW-14 follow-up rather than a must-fix-now closeout blocker.
   - **`Forms("frm")!ctl` / `With ... !ctl` control reads**: also acceptable to defer here because the gap is golden-neutral on HomeHealthCare and does not corrupt already-covered syntax. This one is a pure chain-layer coverage omission rather than a cross-layer problem, so I would want it explicitly tracked/documented, but I would not reopen closeout for it alone.

Overall: item 1 is fixed, item 3 is fixed, item 4’s deferrals are acceptable, but item 2 is still incomplete and still contains a real false-classification bug.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {"severity": "Major", "location": "NavigationChainBuilder.CodeBehind.cs / TempVarSetterCollector.RecordFirstLiteralArg", "summary": "The collector records the first positional string literal anywhere in the argument list, so `TempVars.Add dynamicName, \"value\"` can falsely mark the value literal as a supplied TempVar name and suppress a real `tempvar-no-supplier`.", "fix": "Inspect only the actual TempVar-name slot: for `.Add`, examine the first positional argument only (or an explicit `Name:=` argument if you choose to support it); if that slot is missing or non-literal, do not record a supplied name."},
    {"severity": "Major", "location": "NavigationChainBuilder.CodeBehind.cs / CodeBehindVisitor TempVar supplier emission", "summary": "Form/report code-behind still emits `Owner -> TempVar` supplier edges only for `TempVars!x =`, while equivalent `TempVars(\"x\") =` and `TempVars.Add ...` setters only affect the unmatched-supplier universe and still drop the structural chain edge.", "fix": "Reuse the non-bang TempVar setter recognition in `CodeBehindVisitor` and emit `SuppliesParameter` edges for owner-scoped indexer and `.Add` setters, while keeping standard/class-module recognition collector-only."}
  ]
}
```