# Set 012 — set-wide devil's-advocate review (Sessions 1–3)

> **How to run (operator):** paste this whole prompt into GitHub Copilot twice —
> once with **Gemini Pro**, once with **GPT-5.4** — in the
> `dabbler-access-harvester` workspace so the reviewer can open every file named
> below. Save the raw answers to `…/s3-devils-advocate-gemini.md` and
> `…/s3-devils-advocate-gpt-5-4.md` and hand both back. This is the **primary**
> closeout review for set 012 (a routed cross-provider `session-verification`
> runs additionally as the mandatory automated gate).

---

> **Bias cautions:** This prompt was authored by the session orchestrator
> (Claude Opus 4.8), which has an opinion and a stake in the work passing. Its
> framing may steer you toward in-scope refinements when the right answer is to
> question the scope, and the work may be presented as further along or more
> correct than it is. Before answering as posed, check whether each is the right
> question; if a sharper one exists, answer that too.

**You are a devil's-advocate verifier. Null hypothesis: each concern below names
a real defect.** Your job is to *substantiate* the defect with a concrete
counterexample — a specific VBA/SQL fragment, AST shape, name divergence, or
golden delta — not to issue a verdict. A reasoned "looks fine" is the weakest
possible answer; prefer "here is the input that breaks it" or "here is the line
of code that proves it cannot." **Begin with prose analysis of every concern by
id before the JSON block; both the prose and the JSON are required.**

## Background

Set 012 delivered two Round-2 consumer requests over the Access metadata
harvester (C# / .NET 10, mechanical extraction only — no AI interpretation):

- **R2-1 / DW-9 (Session 1):** materialise the switchboard menu rows (in *no*
  harvested artifact before — `tblSwitchboardItems` shipped `(no data file)`)
  via a shape-gated row-read, and populate `NavigationGraph.EntryPoints` /
  `.Switchboard` / `.Macros`.
- **R2-2 / DW-10 (Sessions 2–3):** derive the cross-object **chain** surface
  `NavigationGraph.ChainEdges` / `.UnresolvedChainTargets` —
  launcher → report → dialog → query → parameter. S2 produced the **mechanical**
  edges (over the IR surfaces: embedded macros, record sources, query
  parameters). **S3 (this session)** added the **code-behind** producer over the
  resolved VBA AST.

The shipped HomeHealthCare golden (`samples/GoldenOutput/HomeHealthCare/`)
reproduces **byte-identically** from a fresh COM run; the full hermetic suite is
886 + 38 green; the golden + schema gates pass. So none of the concerns below is
"does HHC pass" — they are about **correctness beyond the HHC corpus** and about
**claims closed on weak evidence** (a single pin, a reasoning rebuttal, or
golden-neutral behaviour pinned only synthetically).

Primary S3 code to read:
[`NavigationChainBuilder.CodeBehind.cs`](../../../AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.CodeBehind.cs)
and the TempVar-supply logic + `Build` overload in
[`NavigationChainBuilder.cs`](../../../AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs);
wiring at `AccessHarvester.Core/Pipeline.cs` (~line 527); model in
[`NavigationGraph.cs`](../../../AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs);
pins in `tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs`;
design + deviations in
[`s3-regrounding-and-design.md`](s3-regrounding-and-design.md); the consumer
contract in [`interpretation-guide.md`](../../harvester/interpretation-guide.md)
§5.7.

---

### C1 — S3: `acDialog` detection is narrower than the WindowMode semantics

`OpensDialog` is chosen only for `DoCmd.OpenForm` when positional slot **5**
(WindowMode) is the identifier `acDialog`, or a `WindowMode:=acDialog` named arg.
`OpenReport` is **always** `OpensReport` regardless of WindowMode.

*Contrarian hypotheses (pick the strongest you can substantiate against the
code):*
- A report opened modally (`DoCmd.OpenReport "rptX", , , , acDialog` — WindowMode
  is OpenReport positional slot **4**, not 5) is a real parameter-dialog pattern
  that this producer silently records as a plain `OpensReport`, losing the modal
  signal. Is that a correctness gap, or is `OpensDialog` deliberately form-only
  (per the model doc) and therefore correct-by-definition?
- A call that mixes a positional target with other **named** args
  (`DoCmd.OpenForm "frmX", WindowMode:=acDialog, OpenArgs:="x"`) — does
  `PositionalValues` (which drops named args from the positional list) still find
  the `WindowMode:=` named arg? Trace `OpensWithDialog`.
- Slot 5 is hard-coded. Construct an `OpenForm` call where `acDialog` lands in a
  different slot (e.g. an extra/omitted positional) and check whether the
  detection mis-fires or misses.

### C2 — S3: `Forms!`/`Reports!` reads recognised syntactically — coverage vs fabrication

`IsFormsControlRef` matches exactly the two-bang shape
`Forms!obj!member` / `Reports!obj!member` (root identifier `Forms`/`Reports`,
one intermediate bang, the outer bang). This is a deliberate **deviation** from
the spec's "resolve via the Access-integration layer" (see design doc).

*Contrarian hypotheses:*
- **Misses:** real HHC-plausible read shapes that are NOT a bare two-bang —
  `Forms!frm!ctl.Value` (a `MemberAccess` wrapping the bang), `Forms("frmX")!ctl`
  (call-style collection index), `Forms![frm with space]!ctl` (bracketed names),
  or a `With Forms!frm` block + `!ctl` (`ImplicitWithBangExpression`). Do any
  occur in the HHC reports, and does the producer drop them? (Check the report
  `Report_Open` bodies under `ir/Reports/*.ir.json` `VbaCode`.)
- **Fabrication / wrong direction:** the write-target exclusion uses a
  `ReferenceEqualityComparer` set of `BangAccessExpression` nodes. Can a single
  statement contain the *same* `Forms!frm!ctl` node as both a write target and a
  read (or two structurally-equal but distinct nodes) such that the exclusion
  either wrongly drops a read or wrongly keeps a write as a `SuppliesParameter`
  edge? Is `Forms!frm!ctl = x` (a write INTO the dialog) correctly suppressed?

### C3 — S3: module → owning-object mapping by name is collision-prone

A code-behind module is tied to its form/report by `ModuleScope.ModuleName`
matching a `db.Forms`/`db.Reports` name (case-insensitive). The owner map is
built forms-first then reports.

*Contrarian hypothesis:* construct a database where a **standard** module shares
a name with a form, or a form and a report share a name (Access allows a form and
report with the same name). Does the producer then (a) walk a non-code-behind
module as if it owned a form, or (b) assign the wrong `ChainNodeKind` to the
owner (reports overwrite forms in the dictionary)? Is "a module name equals at
most one form/report" actually guaranteed, or merely true on HHC?

### C4 — S3: `tempvar-no-supplier` correctness (cross-producer, golden-neutral)

A record-source `TempVars!x` consumer with no setter becomes an
`UnresolvedChainTarget` (`tempvar-no-supplier`). The "supplied" universe is
`CollectAssignedTempVars` — every `TempVars!x = …` target across **all** modules
(this was widened from the owner-only edge set specifically to avoid a
false-positive when a standard module sets the TempVar). It is golden-neutral on
HHC and pinned only synthetically.

*Contrarian hypotheses:*
- **False negative:** a TempVar is "set" only via `TempVars.Add "x", v` or
  `TempVars("x") = v` (method/indexer forms, not the bang-assignment
  `TempVars!x =`). `CollectAssignedTempVars` keys only off the bang-target shape,
  so those setters are invisible → the consumer is *correctly* flagged? Or is the
  flag then a false "broken chain" because the value IS set, just not via the
  recognised syntax? Which is the right call, and does the interpretation guide
  say?
- **Reason-set leakage:** confirm `non-literal-target` and `tempvar-no-supplier`
  are both in the documented closed `Reason` set (§5.7) and that no producer can
  emit a reason outside it.

### C5 — S3: duplicate-edge collapse drops `WhereCondition`/`OpenArgs` distinctions

Exact-duplicate code-behind edges (same `From`/`To`/`Kind`/`Provenance`) collapse
to one. The `ChainEdge` model carries **no** `WhereCondition`/`OpenArgs`/argument
detail (unlike the VBA `NavigationEdge` in `Edges`).

*Contrarian hypothesis:* two `DoCmd.OpenForm "frmDetail"` calls that differ by
`WhereCondition` or `OpenArgs` (e.g. opening the same detail form filtered two
different ways) collapse to a single chain edge, silently losing that they are
distinct navigations. Is that an acceptable "structural relationship" abstraction
(the detail still lives in `Edges`), or a lossy merge a consumer walking only
`ChainEdges` would be misled by? Does §5.7 tell the consumer that `ChainEdges` is
de-duplicated and argument-free, so they must join back to `Edges` for call-site
detail?

### C6 — S1 (mined): switchboard shape-gated detection + the accepted-by-design Minor

S1's switchboard detection keys off a **column signature**
(`SwitchboardID, ItemNumber, ItemText, Command, Argument`) not the table name,
and the row-read is a bounded ordered SELECT. One R1 Minor
(`s1-verification-round-2.md`, "absent StartUpForm vs real DAO read failure") was
**accepted by design** rather than fixed.

*Contrarian hypotheses:* (a) can a non-switchboard table whose columns *happen*
to match the canonical signature be mis-detected and row-read as navigation
chrome (false positive), or a real switchboard with a renamed/missing canonical
column be missed (false negative)? (b) Re-examine the accepted-by-design Minor:
is "absent StartUpForm" genuinely indistinguishable from a real DAO read failure,
or does the swallow hide a real extraction error a consumer should see? Files:
the switchboard exporter + `BuildNavigationInputs` in `AccessHarvester.Core/Pipeline.cs`;
`s1-verification.md`; `s1-verification-round-2.md`.

### C7 — S2 (mined): is the TempVars/record-source scanner's "class closed" claim true?

S2 took **three** verification rounds to close TempVars false-positives, ending
in a token-aware single-pass scanner (`ExtractTempVars` in
`NavigationChainBuilder.cs`) that skips string-literal and bracketed spans. The
final verdict declared the false-positive *class* closed.

*Contrarian hypothesis:* find a record-source SQL string that still mis-handles
`TempVars!` — e.g. a doubled-quote escaped literal (`'it''s TempVars!x'`), a
bracketed identifier containing a `]` ... (JET brackets don't nest, but check the
`SkipBracket`/`SkipQuoted` edge cases), nested/adjacent quote+bracket spans, or a
legitimate `TempVars![member]` immediately following a skipped span. Separately:
does the inline-SQL FROM/JOIN extraction (`SourceAndRefCollector`) miss targets
in subqueries / derived tables / `UNION` arms that a faithful record-source chain
should surface?

---

After the prose analysis (every concern, by id), output exactly:

```json
{
  "verdict": "VERIFIED",
  "issues": [
    { "severity": "Critical",
      "location": "<file or concern id>",
      "summary": "<one sentence>",
      "fix": "<recommended fix>" }
  ]
}
```

`issues` is `[]` iff you found no Critical/Major issue. `verdict` is `"VERIFIED"`
only when `issues` is empty; otherwise `"ISSUES_FOUND"`. In each finding's
`summary`, distinguish a **code** defect (wrong beyond HHC) from a
**documentation** defect (behaviour acceptable but the interpretation guide
should warn the consumer).
