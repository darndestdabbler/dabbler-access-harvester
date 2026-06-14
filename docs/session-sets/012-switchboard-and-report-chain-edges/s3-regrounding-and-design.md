# Session 3 — Re-grounding + Code-Behind Chain-Edge Design (R2-2 part 2, DW-10)

> Per the spec Step 1 ("ground the code-behind hops against a fresh tool run")
> and the *verify-consumer-reports-against-fresh-output* / *Evidence-Over-Inference*
> discipline. Records what the fresh tree actually carries, the resulting design,
> and every deviation from the spec's literal wording so the cross-provider
> verifier (and the in-IDE devil's-advocate pair) sees the reasoning.

## Re-grounding findings (against `samples/GoldenOutput/HomeHealthCare`)

The canonical R2-2 chain, confirmed in the fresh IR, runs:

```
frmReportMenu (launcher)
  └─[OnClick embedded macro]→ OpenReport rptServicePlanAndShortfall          (S2: embedded-macro)
        └─[Report_Open: DoCmd.OpenForm "frmReportParam_ClientFY", , , , , acDialog]   (S3: vba-call OpensDialog)
              └─[Forms!frmReportParam_ClientFY!cboClient / !cboFiscalYear reads]       (S3: vba-call SuppliesParameter)
              └─[TempVars!repCID = m_ClientID ; TempVars!repFY = m_FiscalYear]         (S3: tempvar-flow setter)
        srptServiceShortfall.RecordSource = "… WHERE ClientID=TempVars!repCID And FiscalYear=TempVars!repFY"  (S2: tempvar-flow consumer)
```

Fresh-tree facts that drove the design:

1. **A form/report code-behind module's name IS the bare object name.**
   `NavigationGraph.Edges` already carries `SourceModule = "rptServicePlanAndShortfall"`
   (not `Report_rptServicePlanAndShortfall`), confirming `ResolvedModule.ModuleScope.ModuleName`
   for code-behind is the bare object name — the same seam `NavigationGraphBuilder`
   walks. So a module is tied to its owning application object by a name lookup
   against `db.Forms` / `db.Reports`.

2. **`acDialog` is the WindowMode positional arg (OpenForm slot 5).** e.g.
   `DoCmd.OpenForm "frmReportParam_ClientFY", , , , , acDialog`. Two of the three
   `acDialog` text occurrences in `frmClientServiceNeedDetail` are in **comments**,
   not code — so an AST-based detection (only the real dispatch's positional arg)
   is strictly more correct than a text scan, which would have produced two
   false dialog opens.

3. **`Forms!frm!ctl` reads are on the RHS of `Let` assignments** (e.g.
   `m_ClientID = Forms!frmReportParam_ClientFY!cboClient`). HHC has **11** such
   control reads across 8 reports; **zero** `Forms!`-write targets and **zero**
   `Reports!` reads.

4. **TempVars setters live only in `rptServicePlanAndShortfall.Report_Open`**
   (`repCID`, `repFY`). Both are consumed by the two shortfall sub-reports'
   record sources (the S2 consumer-side edges), so the supply chain is **complete**
   on HHC — the `tempvar-no-supplier` unresolved branch is golden-neutral.

5. **No dynamic (`non-literal`) open in form/report code-behind.** The only
   non-literal `OpenForm` is in `basListHelpers` (a standard module, no
   form/report owner → not a chain edge). So the `non-literal-target` unresolved
   branch is also golden-neutral. Both golden-neutral branches are covered by
   **synthetic hermetic pins**.

## Design — code-behind producer (`NavigationChainBuilder.CodeBehind.cs`)

A fourth producer extends the S2 `NavigationChainBuilder` via an optional
`IReadOnlyList<ResolvedModule>? modules` parameter on `Build` (kept in a partial
file so the VBA-AST dependency stays isolated from the SQL-AST producers — the
two share type names like `Expression`/`LiteralExpression`). All four producers
collect into one edge/unresolved list, sorted once for byte-stable output.

| Surface (per form/report code-behind module) | Emits | Provenance |
|---|---|---|
| `DoCmd.OpenForm` literal target, WindowMode `acDialog` | `OpensDialog` `From={owner}→To={Form,target}` | `vba-call` |
| `DoCmd.OpenForm` literal target, no `acDialog` | `OpensForm` | `vba-call` |
| `DoCmd.OpenReport` literal target | `OpensReport` | `vba-call` |
| `DoCmd.OpenForm/OpenReport` **non-literal** target | `UnresolvedChainTarget` reason `non-literal-target` | `vba-call` |
| `Forms!obj!ctl` / `Reports!obj!ctl` **read** | `SuppliesParameter` `From={obj,Member=ctl}→To={owner}` | `vba-call` |
| `TempVars!x = …` **setter** | `SuppliesParameter` `From={owner}→To={TempVar,x}` | `tempvar-flow` |
| record source references `TempVars!x` with **no** setter (cross-producer) | `UnresolvedChainTarget` reason `tempvar-no-supplier` | `tempvar-flow` |

`Close`/`BrowseTo` are not chain-open kinds (consistent with S2 ignoring
`Close`). Only modules whose name matches a harvested form/report are walked.
Exact-duplicate code-behind edges (same `From`/`To`/`Kind`/`Provenance` from two
dispatch sites, e.g. `frmServiceList` opening `frmServiceDetail` from both a
button click and a double-click) collapse to one edge — a chain edge is a
structural relationship, not a per-call-site record.

## Deviations from the spec's literal wording (and why)

- **Spec Step 2 says `Forms!dlg!control` reads resolve "via the resolver's
  now-wired Access-integration layer."** Re-grounding: the reference is
  recognised **syntactically** (the bang-access root identifier is `Forms` /
  `Reports`), not via the resolver's symbol table. This matches the
  textual-recognition posture `NavigationGraphBuilder` already uses for the
  `acForm`/`acReport`/`acDialog` enum constants; the resolver adds no precision
  for this literal pattern and would couple the chain builder to the symbol
  table for no gain (the *lean-by-default* principle). The Access-integration
  layer remains available; it is simply unnecessary for this detection.

- **Spec Step 3 lists "unmatched TempVars" in the explicit-unresolved channel.**
  Implemented as `tempvar-no-supplier`: a record-source `TempVars!x` consumer
  with no harvested code-behind setter. Gated to runs that supply modules (the
  S2-only path must not falsely flag consumed TempVars as unsupplied, since the
  setters live in code-behind that path never sees). Golden-neutral on HHC;
  pinned synthetically.

## Validation

- 12 new hermetic pins in `NavigationChainBuilderTests` (code-behind opens,
  `acDialog`→`OpensDialog`, control reads, write-target exclusion, TempVars
  setter, non-literal-target unresolved, standard-module skip, dispatch dedup,
  tempvar matched / unmatched / S2-only-no-false-flag). Full hermetic suite:
  886/886 green.
- Coordinated HHC golden regen: `NavigationGraph.ChainEdges` 37 → **63**
  (+24 `vba-call` = 13 deduped opens + 11 control reads; +2 `tempvar-flow`
  setters); `UnresolvedChainTargets` unchanged at 3 (both new unresolved reasons
  golden-neutral). `kb/` and `ir/` byte-identical (chain edges live only in the
  manifest `NavigationGraph`). Every delta maps to R2-2.
- `CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests`: 6/6 green.

## Routed verification R1 disposition (gpt-5-4, ISSUES_FOUND → addressed)

The mandatory routed cross-provider `session-verification` (gpt-5-4, R1) returned
3 Major + 1 Minor. Disposition:

- **Major (concern 2b) — member/index-wrapped LHS write false-positive.**
  `Forms!frm!ctl.Value = 42` (a `MemberAccess` over the control bang) was not
  marked as a write, so the bang walker would emit a wrong-direction read edge.
  **Fixed:** `HandleAssignmentTarget` now peels `.Member`/`(index)` postfix
  layers to the controlling bang (`UnwrapToControllingBang`) and marks it.
  Pinned (`CodeBehind_MemberWrappedFormsControlWrite_DoesNotEmitReadEdge` +
  a read-still-emits guard). Golden-neutral on HHC.
- **Major (concern 4) — non-bang TempVars setter forms.** `CollectAssignedTempVars`
  only recognised `TempVars!x =`, so `TempVars.Add "x", v` / `TempVars("x") = v`
  setters would yield a false `tempvar-no-supplier`. **Fixed:** the collector now
  recognises all three forms (bang / indexer / `.Add`). Pinned
  (`TempVarSupply_NonBangSetterForm_SuppressesUnmatched` theory). Golden-neutral.
- **Minor (concern 4 doc) — stale closed `Reason` set.** `UnresolvedChainTarget.Reason`
  XML-doc omitted `non-literal-target` / `tempvar-no-supplier`. **Fixed** (both
  listed now).
- **Major (concern 1) + Major (concern 2a) — named-argument open targets and
  indexer/`With` control reads not surfaced.** **Deferred → DW-14.** Golden-neutral;
  the named-argument gap is shared with the already-shipped VBA-edge layer
  (`NavigationGraphBuilder`), so a correct fix spans both layers (011's
  navigation-extraction surface) and is beyond R2-2 / this set's Stop Rule. A
  consumer-facing coverage-boundary note was added to interpretation-guide §5.7.

Post-fix: full hermetic suite green, golden reproduces byte-identically (the
fixes are golden-neutral). Routed re-verified R2 → R3 **VERIFIED**.

## Set-wide devil's-advocate disposition (Gemini Pro + GPT-5.4, in-IDE pair)

The primary closeout review (`s3-devils-advocate-gemini.md`,
`s3-devils-advocate-gpt-5-4.md`) raised seven concerns (C1–C7). Disposition:

- **C3 — form/report name collision overwrites owner kind (BOTH providers,
  Critical/Major). FIXED.** The owner map was name-keyed
  (`owners[r.Name] = Report` after `owners[f.Name] = Form`), so a form and
  report sharing a name collapsed to one owner kind and the colliding module's
  edges were misattributed (wrong-kind data, not just missing). The producer now
  derives the owner kind from each module's **own** `ModuleScope.Kind`
  (`Form`/`Report`/skip), which the pipeline sets authoritatively from the
  harvest category. Pinned by
  `CodeBehind_FormReportNameCollision_AttributesEachModuleToItsOwnKind`. This
  also closes the latent "standard module shares a form's name" risk GPT noted.
  Golden-neutral (HHC has no collision; form/report modules carry the correct
  Kind). **This is a real defect the routed R1–R3 verifier missed — the value of
  the adversarial pair.**
- **C4 setter-syntax coverage (Gemini Major). FIXED.** The supplied-universe scan
  now recognises the full finite TempVars setter API — `TempVars!x =`,
  `TempVars("x") =`, `TempVars.Item("x") =`, `TempVars.Add "x", …`, and the
  `Name:=` named-argument form — so a value set via any of them no longer
  false-flags `tempvar-no-supplier`. Pinned (theory, four forms).
- **C1 (named-argument open targets) + C2 (`Forms("frm")!ctl` indexer / `With`
  reads) + C4 owner-side supplier-EDGE emission for non-bang setters — DEFERRED
  to DW-14.** Both providers (and the routed R3) accepted these as legitimate
  closeout-scope deferrals: golden-neutral, no false data (coverage gaps, not
  wrong data), and the named-arg gap is shared with the out-of-scope VBA-edge
  layer. The non-bang setter-edge gap is now also disclosed in
  interpretation-guide §5.7 (GPT's only added ask on C4).
- **C5 (duplicate-edge collapse), C6 (S1 switchboard shape-gating + the
  now-already-fixed startup-form Minor), C7 (S2 TempVars scanner) — NO ACTION.**
  Both providers judged C5 a deliberate-and-correct abstraction (call-site detail
  lives in `Edges`); C6 accepted-design-scope (GPT confirmed the startup-form
  Minor is fixed in current code; the shape-gating tradeoff is explicit); C7 was
  **not substantiated** — GPT could not reproduce a current false positive, and
  Gemini's `TempVars.x` dot-notation is not a documented Access record-source
  reference form (the canonical form is the bang `TempVars!x`, which the S2
  token-aware scanner handles and which closed across S2's three rounds).

Post-disposition: 46 chain pins green; golden byte-identical; routed R4 confirms
the C3/C4 fixes.
