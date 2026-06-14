# Set 012 — set-wide devil's-advocate review (Sessions 1–3)

### C1 — S3: `acDialog` detection is narrower than the WindowMode semantics

The implementation correctly adheres to valid VBA without causing data loss. `OpensWithDialog` locates `acDialog` in positional slot 5 or as the named argument `WindowMode:=`, flawlessly mirroring VBA invocation syntax where positional arguments cannot follow named arguments or shift. The exclusion of `OpenReport` from `OpensDialog` is a deliberate and correct architectural choice; reports are consumption ends, not interactive dialogs designed to supply parameters, aligning precisely with the `ChainNodeKind.Form` specification. Null hypothesis disproved. 

### C2 — S3: `Forms!` / `Reports!` reads recognised syntactically — coverage vs fabrication

The null hypothesis holds regarding false negatives. `IsFormsControlRef` explicitly restricts matching to nested `BangAccessExpression` instances rooted by an `IdentifierExpression` ("Forms" or "Reports"). As a result, robust VBA structures like `Forms("frmX")!ctl` (which roots as an `IndexOrCallExpression`) and explicitly qualified indexing like `Forms.Item("frmX")!ctl` are dropped completely, fabricating blind spots in parameter-supply edges. (On the write-avoidance side, reference-equality validation on distinct `BangAccessExpression` correctly separates read/write targets even on visually identically-formed nodes.)

### C3 — S3: module → owning-object mapping by name is collision-prone

The null hypothesis is substantiated. Modual ownership uses a case-insensitive dictionary assignment overwriting Forms with Reports (`owners[r.Name] = ChainNodeKind.Report`). Since Access permits a Form and a Report to share identical names, any collision will cause the Form to be entirely overwritten in the dictionary. Consequently, the Form's code-behind is walked under the misattributed `ChainNodeKind.Report` identity, fabricating incorrect origin endpoints for every chain edge stemming from that Form. 

### C4 — S3: `tempvar-no-supplier` correctness

The null hypothesis holds regarding false negatives in supply verification. The `TempVarSetterCollector`'s syntactic check is overly restrictive. It misses `TempVars.Item("x") = val`, a valid VBA assignment, because `TempVars.Item` represents a `MemberAccessExpression` that fails the rigid `IsTempVarsRoot` assertion. Further, `RecordNameSlot` erroneously asserts slot 0 is strictly a `PositionalArgument`. If a developer uses a named argument list (`TempVars.Add Name:="x", Value:=v`), it resolves to a `NamedArgument`, which the collector ignores. This triggers false-positive "broken chain/unsupplied" warnings despite valid setters existing.

### C5 — S3: duplicate-edge collapse drops `WhereCondition`/`OpenArgs` distinctions

The null hypothesis is disproved by the domain contract. `ChainEdges` is modeled as a structural relationships diagram connecting cross-object paths (launcher → report → dialog). Granular invocation parameters (`WhereCondition`, `OpenArgs`) accurately reside within the separate `Edges` facet, fulfilling a different analytical need. Squelching exact path duplicates preserves structural purity without semantic data loss, as interpretation guidelines strictly direct consumers to join back to `Edges` for detailed call-site semantics.

### C6 — S1 (mined): switchboard shape-gated detection + accepted Minor

The null hypothesis is weak. A non-switchboard table that replicates the highly specific 5-column signature (`SwitchboardID`, `ItemNumber`, `ItemText`, `Command`, `Argument`) is functionally serving as navigation chrome irrespective of table moniker, making its extraction useful. Concerning the 'absent StartUpForm' exception matching a DAO failure: DAO functionally requires the user to explicitly elect a startup form before the property physically exists in the Properties collection. Confusing its natural absence with environmental read failure is moot because the result (no startup element) remains programmatically accurate. 

### C7 — S2 (mined): TempVars/record-source scanner's "class closed" claim true?

The null hypothesis holds against the scanner's comprehensiveness. The `ExtractTempVars` manual parse strictly searches for the bang (`!`) operator, rendering valid alternative syntaxes like dot notation (`TempVars.x` or `TempVars.[x]`) entirely invisible within SQL. Additionally, for the `SourceAndRefCollector` parsing JetSQL `FROM/JOIN` targets, if a query uses deeply-nested subqueries with arbitrary derived sources or unions acting as the target expression payload, the surface parse skips emitting true lineage targets when the AST defaults/recovers locally.

```json
{
  "verdict": "VERIFIED",
  "issues": [
    {
      "severity": "Critical",
      "location": "C3",
      "summary": "Form/Report name collisions overwrite module ownership identity because owners dictionary uses name keys.",
      "fix": "Differentiate dictionary keys by appending Kind to name or iterate modules verifying object presence."
    },
    {
      "severity": "Critical",
      "location": "C2",
      "summary": "Forms(\"frmX\") parameter accesses are ignored because the AST structural detection strictly requires IdentifierExpression.",
      "fix": "Expand IsFormsControlRef to allow an IndexOrCallExpression root for the initial Forms/Reports selection."
    },
    {
      "severity": "Major",
      "location": "C4",
      "summary": "TempVars setter collection misses TempVars.Item indexers and Name:= named argument method invocations.",
      "fix": "Extend TempVarSetterCollector to support MemberAccessExpression (.Item) and NamedArguments within indexers/method calls."
    },
    {
      "severity": "Major",
      "location": "C7",
      "summary": "Record-source TempVars extraction strictly mandates bang (!) operator notation, overlooking dot notations.",
      "fix": "Update TryMatchTempVar manual sweep to respect dot operator (.x and .[x]) boundary patterns."
    }
  ]
}
```