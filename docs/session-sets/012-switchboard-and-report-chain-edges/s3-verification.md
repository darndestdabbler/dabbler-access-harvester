# Set 012 S3 — routed cross-provider session-verification

Model: ?  |  cost: $0.0000  |  tokens(out): 11923  |  truncated: False

---

1. **acDialog detection**

The `acDialog` part is mostly modeled correctly. For `DoCmd.OpenForm`, `WindowMode` is the sixth positional parameter in VBA terms, so zero-based index `5` is right, and checking a named `WindowMode:=` argument is also the right second path. Treating `OpensDialog` as **form-only** is defensible: the chain model is clearly using “dialog” to mean an Access parameter-dialog form, and the emitted kind set has no separate report-dialog concept. Keeping `OpenReport` as `OpensReport` is therefore a reasonable modeling choice, not a gap by itself.

The real completeness problem is adjacent to that logic: `HandleDispatch` only reads the open target from positional slot `0`. If the call is written with named arguments — e.g. `DoCmd.OpenForm FormName:="frmParam", WindowMode:=acDialog` or `DoCmd.OpenReport ReportName:="rptX"` — the current code returns early and emits nothing. That is a mechanical miss, and it directly affects the same call family this concern is about. In other words, the **WindowMode** detection is fine, but the **dispatch recognition as a whole is not complete** for named-argument forms.

A smaller residual limitation is that `IsAcDialog` only matches a bare `IdentifierExpression` named `acDialog`. If the parser can surface qualified enum/member forms, those would also be missed. I would treat that as secondary; the named-target omission is the concrete blocker.

2. **Syntactic `Forms!` / `Reports!` recognition**

The chosen syntactic approach is acceptable in principle. For the canonical HHC shape `Forms!frm!ctl`, `IsFormsControlRef` is correct. It also still works for reads like `Forms!frm!ctl.Value` because the walker will visit the underlying `BangAccessExpression` node for `Forms!frm!ctl`, so `.Value` on the RHS does **not** inherently cause a miss.

There are two real gaps. First, the recognizer is narrower than realistic Access VBA usage: it misses `Forms("frm")!ctl` / `Reports("rpt")!ctl`, and it will also miss implicit `!ctl` references inside `With Forms!frm ... End With` unless the AST shape happens to reduce to the same two-bang pattern. Those are plausible real-world forms, so the deviation from resolver-based handling does cost coverage.

Second, and more importantly, the write-target suppression is only sound for a **direct** bang-expression LHS. `HandleAssignmentTarget` only marks the target when `target is BangAccessExpression`. A write like `Forms!frmParam!cboClient.Value = 42` has a `MemberAccessExpression` target whose child is the bang expression; that child is not marked, so `VisitBangAccessExpression` can incorrectly emit a read-edge from `frmParam.cboClient` to the owner. `ReferenceEqualityComparer` itself is fine; the problem is that the code is not marking all LHS shapes that semantically write to a control reference.

So: the syntactic strategy is not inherently wrong, but the current recognizer is both **incomplete** and **capable of false positives**.

3. **module → owner mapping by name**

Using the bare resolved module name as the seam is well-grounded by the evidence you supplied, and it aligns with the existing `NavigationGraphBuilder` behavior. On HHC and on the stated document-module model, this is the right join key.

I do not see strong evidence that a standard module and a document module can legally share the same VBA component name in a real Access project; likewise, if form/report code-behind module names are truly bare object names, Access/VBA likely enforces uniqueness strongly enough that a same-named form and report would already be problematic. So I would not call this a demonstrated defect from the current evidence set.

That said, the implementation is relying on that invariant silently. If a cross-provider environment ever yields duplicate harvested owner names across forms/reports, the `owners` dictionary will let the later insert win, and report entries currently overwrite form entries. I would classify this as a low-risk hardening point, not a present blocker: a duplicate-name assertion or explicit ambiguity handling would make the assumption explicit.

4. **`tempvar-no-supplier`**

The gating is correct: only performing unmatched-supplier checks when modules are supplied avoids false failures on the S2-only path. Also, using “all harvested modules” as the supplied universe is a defensible interpretation of “is there any harvested setter?”, even if no chain edge is representable for a standard module.

The bigger problem is coverage of setter syntax. `CollectAssignedTempVars` only records direct bang-assignment targets: `TempVars!x = ...` or `Set TempVars!x = ...`. It does **not** recognize other realistic Access setter forms such as `TempVars.Add "x", value` or `TempVars("x") = value` (and likely not property-wrapped variants either). That means `FlagUnsuppliedTempVars` can emit false `tempvar-no-supplier` unresolveds even when harvested VBA does supply the value. Given how common `TempVars.Add` is in Access code, this is a real correctness gap, not just a theoretical edge case.

There is also a contract/documentation mismatch: `UnresolvedChainTarget` says `Reason` is a “closed kebab-case string set”, but its comment block does not list the new `non-literal-target` or `tempvar-no-supplier` values. If the code comments are part of the consumer contract, that closed-set documentation is currently stale.

5. **duplicate-edge collapse**

At the abstraction level, I think this is acceptable. `ChainEdge` is explicitly a structural relationship, not a per-call-site event log, and it does not model `WhereCondition`, `OpenArgs`, or source locations. Given that, collapsing exact duplicates from multiple dispatch sites is consistent with the model and should not mislead a consumer who already understands `ChainEdges` as the higher-level layer. The lower-level VBA edge layer remains the place for per-call-site detail.

The only implementation caveat is that “exact duplicate” is currently keyed via delimiter-joined strings. If raw names or members can contain the delimiter character, you could theoretically collapse distinct edges accidentally. That is an implementation fragility, not a problem with the decision to dedupe structurally. I would harden it eventually with a typed comparer, but I would not make that the main closeout blocker here.

6. **determinism / golden**

From the code shown, I do not see new nondeterminism being introduced. The output lists are accumulated imperatively and then sorted once at the end. The `owners` dictionary is only used for lookup, not for output enumeration; the `HashSet`s are only used for membership/dedup checks. That means dictionary/hash iteration order should not leak into the manifest.

This point does depend on `SortEdges` and `SortUnresolved` being total, byte-stable orders for the now-expanded data set. Those implementations are not shown here, but you already have two strong pieces of evidence: S2’s prior verification history and the stated byte-identical fresh golden reproduction after S3. Given that, I do not see a determinism issue in the S3 additions themselves.

So my read is: determinism looks sound; the main risks I found are correctness/completeness, not output stability.

7. **scope discipline**

I do not see evidence of scope creep or test weakening to force green. The optional `modules` parameter, the partial-file isolation, the form/report-owner restriction, and the S2-only gating of `tempvar-no-supplier` are all aligned with the stated R2-2 scope and with the deterministic/mechanical extraction posture.

The synthetic tests also look like appropriate pins for golden-neutral branches rather than suspicious broad assertions. The places where I think the delivery falls short are not “forced green” smells; they are straightforward completeness/correctness gaps in pattern coverage.

So on scope discipline specifically, I would say the implementation stays inside DW-10. My objections are to a few missed shapes and a stale closed-set doc, not to overreach.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "NavigationChainBuilder.CodeBehind.cs / concern 1",
      "summary": "OpenForm/OpenReport calls that supply the target via named arguments (for example FormName:= or ReportName:=) are silently ignored, so valid literal and non-literal opens can be missed.",
      "fix": "Extend HandleDispatch to read the target name from named arguments (FormName/ReportName) before returning, and apply the same literal/non-literal resolution logic to that path."
    },
    {
      "severity": "Major",
      "location": "NavigationChainBuilder.CodeBehind.cs / concern 2",
      "summary": "Forms!/Reports! control recognition is narrower than realistic VBA usage and the LHS suppression only marks direct bang targets, allowing false read-edges for writes like Forms!frm!ctl.Value = 42.",
      "fix": "Generalize control-reference detection to cover indexer and With-based forms where possible, and propagate write-context suppression through member/index wrappers over a control reference instead of only direct BangAccessExpression targets."
    },
    {
      "severity": "Major",
      "location": "NavigationChainBuilder.CodeBehind.cs + NavigationChainBuilder.cs / concern 4",
      "summary": "TempVar supply matching only recognizes TempVars!x assignment targets, so common setter forms such as TempVars.Add and TempVars(\"x\") = can be misreported as tempvar-no-supplier.",
      "fix": "Broaden TempVar setter collection to detect additional Access TempVars mutation forms (at minimum TempVars.Add and indexer assignments) and reuse that broader detection for both supplier-edge emission and unmatched-supplier suppression."
    },
    {
      "severity": "Minor",
      "location": "NavigationGraph.cs / concern 4",
      "summary": "UnresolvedChainTarget documents Reason as a closed set but its comment block omits the newly emitted non-literal-target and tempvar-no-supplier values.",
      "fix": "Update the model documentation (and any parallel schema comments if applicable) so the declared closed reason set matches the runtime values exactly."
    }
  ]
}
```