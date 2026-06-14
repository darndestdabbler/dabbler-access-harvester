# Set 012 S3 — devil's-advocate review

### C1

The `acDialog` concern is partly the wrong question. The narrow `WindowMode`
logic itself is mostly correct for the currently modelled surface.

`DoCmd.OpenForm "frmX", WindowMode:=acDialog, OpenArgs:="x"` still works.
`HandleDispatch` builds `positional` from only positional and missing arguments,
so slot 0 still holds the literal target, and `OpensWithDialog` separately scans
the full argument list for a named `WindowMode:=` argument. Hard-coding slot 5
for `OpenForm` is also correct because `PositionalValues` preserves omitted
arguments as `null`, so `DoCmd.OpenForm "frmX", , , , , acDialog` still lands
`acDialog` in the sixth positional slot.

I also do not think `DoCmd.OpenReport "rptX", , , , acDialog` is a hidden bug
in the current model. The chain contract defines `OpensDialog` as the
form-dialog case; there is no separate report-dialog edge kind. So modal report
opens being recorded as `OpensReport` is consistent with the published model,
even if it loses the modal flag.

The sharper surviving defect is that named-target opens are dropped entirely.
Concrete counterexample:

```vb
Private Sub Report_Open(Cancel As Integer)
    DoCmd.OpenForm FormName:="frmParam", WindowMode:=acDialog
End Sub
```

or

```vb
Private Sub btnPrint_Click()
    DoCmd.OpenReport ReportName:="rptInvoice"
End Sub
```

`HandleDispatch` reads the target only from `positional[0]`. `NamedArgument`s
are discarded by `PositionalValues`, so these calls produce no `ChainEdge` and
no `UnresolvedChainTarget` at all. That is a real code defect. The only reason
I am not treating the narrower `acDialog` question itself as the main bug is
that the actual failure mode is broader: the builder misses the whole dispatch,
not just the modal bit. The interpretation guide now discloses this as a
coverage boundary, but the omission is still real.

### C2

The fabrication side of this concern is mostly fixed. `Forms!frm!ctl.Value = 42`
used to be able to leak a false read-edge; `UnwrapToControllingBang` now peels
member/index wrappers on an assignment LHS and marks the controlling bang as a
write target, so `Forms!frm!ctl = x` and `Forms!frm!ctl.Value = x` are both
suppressed correctly. A statement like `Forms!frm!ctl = Forms!frm!ctl` still
works the right way under the reference-identity set: the LHS bang is marked,
the distinct RHS bang is not, so only the read side survives.

The coverage side still lands for two real shapes. First, `Forms("frmX")!ctl`
and `Reports("rptX")!ctl` do not match `IsFormsControlRef`, because it demands
an outer `BangAccessExpression` whose target is an inner `BangAccessExpression`
whose target is an `IdentifierExpression` named `Forms` or `Reports`. An
indexer-root shape bottoms out in `IndexOrCallExpression`, so it is dropped.

Second, `With Forms!frmX ... x = !ctl ... End With` is also missed. The VBA AST
has a distinct `ImplicitWithBangExpression` node for `!ctl`, and
`CodeBehindVisitor` overrides `VisitBangAccessExpression` but not
`VisitImplicitWithBangExpression`, so the read never reaches the control-ref
recognizer.

I did not find either of those shapes in the HomeHealthCare report code-behind,
and §5.7 now explicitly calls out named-argument opens, indexer reads, and
`With`-block reads as unsupported coverage. So I would treat C2 as a real but
already-disclosed completeness boundary, not as a newly hidden defect.

### C3

This one still lands.

`CollectCodeBehindEdges` builds a plain
`Dictionary<string, ChainNodeKind>(StringComparer.OrdinalIgnoreCase)`, inserts
every form name as `Form`, then inserts every report name as `Report`. That
means the last insert wins on any bare-name collision.

Concrete counterexample:

- Form name: `Dashboard`
- Report name: `Dashboard`
- Form code-behind:

```vb
Private Sub cmdOpen_Click()
    DoCmd.OpenForm "frmDetail"
End Sub
```

The existing seam already assumes document-module `ModuleScope.ModuleName` is
the bare object name. In that database, both the form module and the report
module arrive as `ModuleName == "Dashboard"`. The owner map will contain only
`Dashboard -> Report`, because the report insert overwrites the form insert.
Both modules are then walked as report owners, so the form's code-behind emits
edges with `From={ Kind = Report, Name = "Dashboard" }`. That is wrong-kind
data, not just a missing edge.

There is a second, related risk if a non-document module can share the same bare
name: the current API takes only `ResolvedModule`, not the source category, so a
same-named standard module would also be promoted as if it were code-behind.
I have not independently verified that Access/VBA allows that standard-module
collision, but the form/report collision is enough to prove the map is relying
on an undocumented uniqueness invariant.

### C4

The direct `tempvar-no-supplier` concern no longer holds in the way the prompt
frames it. `CollectAssignedTempVars` is no longer bang-only: it now recognises
all three common setter forms:

```vb
TempVars!repCID = 1
TempVars("repCID") = 1
TempVars.Add "repCID", 1
```

and the closed `Reason` set is now aligned in both the model comments and the
interpretation guide. So the specific false-positive case "the TempVar was set
via `.Add` or indexer but still reported as `tempvar-no-supplier`" is fixed.

The sharper remaining defect is different: owner-side supplier-edge emission is
still bang-only. `CodeBehindVisitor.HandleAssignmentTarget` emits a
`From={owner} -> To={TempVar}` edge only when the assignment target is a direct
`BangAccessExpression` rooted at `TempVars`.

Concrete counterexample:

```vb
Private Sub Report_Open(Cancel As Integer)
    TempVars("repCID") = m_ClientID
End Sub
```

with a consumer record source like:

```sql
SELECT * FROM qryNeeds WHERE ClientID = TempVars!repCID;
```

In that case, `CollectAssignedTempVars` correctly suppresses
`tempvar-no-supplier`, but the chain surface still lacks the supplier edge
`{ From = rptParent, To = TempVar(repCID), Kind = SuppliesParameter,
Provenance = tempvar-flow }`. A consumer walking only `ChainEdges` sees the
consumer half of the tempvar chain but not the producer half. That is still a
real code defect, and it is not disclosed in §5.7.

### C5

I can construct the lossy case:

```vb
DoCmd.OpenForm "frmDetail", OpenArgs:="ADD"
DoCmd.OpenForm "frmDetail", OpenArgs:="EDIT"
```

Both collapse to one `ChainEdge` because `ChainEdge` has only
`From/To/Kind/Provenance`, and duplicate suppression keys on exactly those
fields.

I do not think that is a hidden correctness bug in the current contract,
though. The published split is that `NavigationGraph.Edges` carries individual
VBA dispatch sites, including raw `WhereCondition` and `OpenArgs`, while
`ChainEdges` is the structural relationship layer. On that reading, dropping the
call-site distinction here is deliberate abstraction, not silent corruption.
If anything, the guide could say the de-duplication more explicitly, but the
argument-free shape of `ChainEdge` already signals that this layer is not a
call-log.

### C6

The old startup-form Minor no longer lands. `ReadStartUpForm` now distinguishes
the expected "property absent" case from a real DAO/property read failure, and
`Export` records an unsuccessful `startup-options` `ExportOutcome` on the
latter. So the prior "accepted by design" swallow is no longer current code.

The shape-gated switchboard tradeoff itself is real, but it is also explicit.
Any non-switchboard business table that happens to carry the full canonical
column set

```text
SwitchboardID, ItemNumber, ItemText, Command, Argument
```

will be harvested as switchboard navigation, and a real switchboard with a
renamed or missing canonical column will be missed. That is the direct cost of
choosing column-signature detection instead of table-name heuristics or deeper
semantic interpretation. I would treat that as accepted design scope rather than
as a hidden regression in this set.

### C7

I could not substantiate the claim that the TempVars scanner's false-positive
class is still open from the current implementation alone.

`ExtractTempVars` now does the two things that mattered in the earlier rounds:
it skips both single-quoted and double-quoted literals with doubled-quote
escapes, and it skips bracketed identifier spans wholesale before attempting a
top-level `TempVars!` match. That directly blocks the previously known false
positives such as `'TempVars!x'`, `"TempVars!x"`, and `[TempVars!fake]`.

On the source-side collector, `SourceAndRefCollector` visits `SimpleTableSource`
and `FormReference` nodes through the SQL AST walker, so nested joins and
subqueries are at least structurally covered. I do not have a concrete current
counterexample showing that a `UNION` arm or derived-table source is dropped by
this walker. There may still be ACE/Jet dialect edges outside the tested corpus,
but I do not have code proof for one here, so I would not elevate C7 to a
finding.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "NavigationChainBuilder.CodeBehind.cs / C1",
      "summary": "Code defect: named-argument OpenForm/OpenReport calls are silently ignored because the target is read only from positional slot 0, so valid literal and non-literal opens can disappear from both the chain and unresolved surfaces.",
      "fix": "Teach HandleDispatch to read FormName:= / ReportName:= named arguments before falling back to positional slot 0, then apply the same literal-vs-dynamic target handling to that path."
    },
    {
      "severity": "Major",
      "location": "NavigationChainBuilder.CodeBehind.cs / C3",
      "summary": "Code defect: code-behind owner mapping keys only on the bare module/object name, so a same-named form/report pair collapses to one owner kind and misclassifies one module's edges.",
      "fix": "Carry enough identity to distinguish form vs report owners when promoting document modules to chain edges, or detect ambiguous bare-name collisions and surface/skip them explicitly instead of letting the later dictionary insert win."
    },
    {
      "severity": "Major",
      "location": "NavigationChainBuilder.CodeBehind.cs / C4",
      "summary": "Code defect: non-bang TempVars setters such as TempVars(\"x\") = ... and TempVars.Add \"x\", ... suppress tempvar-no-supplier but still do not emit the owner-to-TempVar supplier edge, leaving the chain structurally incomplete.",
      "fix": "Reuse the widened TempVar-setter recognition in CodeBehindVisitor itself and emit owner-scoped tempvar-flow supplier edges for indexer and .Add setter forms, while keeping standard/class-module recognition collector-only if those modules still lack a chain owner."
    }
  ]
}
```