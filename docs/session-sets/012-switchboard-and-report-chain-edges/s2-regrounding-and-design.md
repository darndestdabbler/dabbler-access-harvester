# Session 2 — Re-grounding + Chain-Edge Design (R2-2 part 1, DW-10)

> Per the spec Step 1 ("re-ground the R2-2 mechanical-surface claims against
> the tree") and the *verify-consumer-reports-against-fresh-output* /
> *Evidence-Over-Inference* discipline. This note records what the fresh tree
> actually carries, the resulting design, and every deviation from the spec's
> literal wording so the cross-provider verifier sees the reasoning.

## Re-grounding findings (against `samples/GoldenOutput/HomeHealthCare`)

The HHC fixture realises the canonical R2-2 chain almost exactly:

```
frmReportMenu (launcher)
  └─[OnClick embedded macro]→ OpenReport rptServicePlanAndShortfall
        └─[Report_Open VBA: DoCmd.OpenForm "frmReportParam_ClientFY", , , , , acDialog]
              └─[Forms!frmReportParam_ClientFY!cboClient reads]
              └─[TempVars!repCID = m_ClientID  /  Me.RecordSource = "SELECT * FROM tblClients WHERE ClientID = " & m_ClientID]
        rptServicePlanAndShortfall.RecordSource = "SELECT * FROM tblClients WHERE ClientID = 1;"  (design-time placeholder)
  srptServiceShortfall.RecordSource = "SELECT * FROM qryClientNeedsNotMet WHERE ClientID=TempVars!repCID And FiscalYear=TempVars!repFY;"
```

Confirmed surfaces and where they live:

1. **Embedded macros — already enriched.** `MacroActionsEnricher` already parses
   the `_AXL:` XML chunks into a **structured `EventMacros` block** on each
   control in the form IR JSON (named args: `{"Action":"OpenReport",
   "Arguments":{"ReportName":"rptClientProfile"}}`). `frmReportMenu` carries 4
   `OnClick → OpenReport` macros. **Deviation from spec wording:** the spec
   Step 4 says "parse the `OnClickEmMacro` block text". Re-grounding shows the
   block text is *already parsed* by the enricher into `EventMacros`; consuming
   that structured form (the canonical `_AXL`-derived representation, with named
   arguments) is strictly more faithful and DRY than re-parsing the legacy
   `OnClickEmMacro` block-format property string. The producer consumes
   `EventMacros`. (`IrLoader` did **not** previously surface `EventMacros`; this
   session extends `IrControl` to carry it.)

2. **RecordSource — named or inline SQL.** `IrFormOrReport.RecordSource` already
   carries the raw value: a bare table name (`tblClients`), a bare query name
   (`qryProviderServiceCosts_ByFiscalYear`), or inline SQL (`SELECT … FROM …`).
   Inline-vs-named is distinguishable by the leading `SELECT|PARAMETERS|TRANSFORM`
   keyword (existing `RecordSourceResolver` regex).

3. **Query parameters — computed at load, prompt-style in HHC.** `IrQuery.Parameters`
   is **not serialised** in `ir/Queries/*.ir.json`; `IrLoader.ExtractQueryParameters`
   computes it from the SQL via the shared `QueryMetadataExtractor.ParseParameters`
   scan (closed provenance set `declared | form-reference | report-reference`).
   In HHC the only declared parameters are **prompt-style** (`[Enter Provider ID:]`
   in `qryProviderPlansByClient_Param`, `qryClientServicePlan_ByClient`); there are
   **zero `[Forms]!`/`[Reports]!` references in any query SQL**. So the
   form-reference / report-reference → `SuppliesParameter` branch is
   **golden-neutral on HHC** and is covered by **synthetic hermetic pins**.

4. **TempVars are NOT a clean AST node.** The JET SQL grammar models bang-access
   (`!`) **only** for `Forms`/`Reports` roots (`formReference` rule); there is no
   generic postfix-bang in `primaryExpression`. `TempVars!repCID` therefore does
   **not** parse as a structured node. **Deviation from spec wording:** the spec
   Step 3 says emit `TempVars!x` "tokens in the parsed tree"; re-grounding shows
   the parser cannot model them. The record-source producer extracts `[Forms]!`/
   `[Reports]!` from the **parsed AST** (`FormReference` nodes) and `TempVars!<name>`
   via a **textual scan** of the record-source SQL. `TempVars!` appears in 3 report
   record sources (`rptServicePlanAndShortfall`, `srptServicePlanEntries`,
   `srptServiceShortfall`).

5. **Report→dialog is VBA code-behind → S3, not S2.** The `acDialog` open, the
   `Forms!dlg!ctl` reads, and the `TempVars!x = …` **assignments** all live in
   the report's `Report_Open` VBA. They are S3 (code-behind producer). S2 touches
   **no VBA** — its producers are pure functions over the IR surfaces
   (forms/reports/queries). `NavigationGraphBuilder` already emits the
   `DoCmd.OpenForm "frmReportParam_ClientFY"` dispatch as a VBA `NavigationEdge`.

## Design — closed chain-edge model

New on `NavigationGraph` (additive; `NavigationGraph` `$def` is
`additionalProperties`-tolerant, so **no schema-envelope bump** — the additive-
field lesson):

- `ChainEdges: ChainEdge[]` and `UnresolvedChainTargets: UnresolvedChainTarget[]`
  (always-emitted, deterministically sorted; mirrors `Edges` / `UnresolvedTargets`).

`ChainEdge = { From: ChainEndpoint, To: ChainEndpoint, Kind: ChainEdgeKind, Provenance: string }`

- `ChainEndpoint = { Kind: ChainNodeKind, Name: string, Member?: string }`
  - `ChainNodeKind`: `Form | Report | Query | Table | TempVar | Prompt | Unknown`
    (`Member` carries the originating control / referenced control / parameter token).
- `ChainEdgeKind` (closed, PascalCase per spec): `OpensDialog | OpensReport |
  OpensForm | FeedsRecordSource | SuppliesParameter`.
- `Provenance` (closed kebab-case **string** set per spec; mirrors
  `UnresolvedNavigationTarget.Reason`'s raw-string precedent rather than an enum,
  so the on-disk values match the spec verbatim): `vba-call | embedded-macro |
  macro-action | record-source | query-form-reference | query-declared-parameter |
  tempvar-flow`. `vba-call` is emitted by the S3 code-behind producer;
  `macro-action` is **reserved** (no S2/S3 producer in this spec — standalone-macro
  chains are future work). Reserved-but-documented values are intentional: the spec
  fixes the full closed set up front and forbids renaming.

`UnresolvedChainTarget = { Kind: ChainEdgeKind, Consumer: ChainEndpoint,
Provenance: string, Reason: string, Detail?: string }` — the explicit-unresolved
channel (prompt-style declared parameters, unparseable record sources).

### Producers (all pure over `IrDatabase`, in `NavigationChainBuilder`)

| Producer | Input surface | Emits |
|---|---|---|
| Embedded-macro | control `EventMacros` `OpenReport`/`OpenForm` actions | `OpensReport`/`OpensForm`, `From={Form,form,control}`, `embedded-macro` |
| Record-source (named) | `RecordSource` = bare query/table name | `FeedsRecordSource`, `From={Query|Table,name}` → `To={owner}`, `record-source` |
| Record-source (inline) | `RecordSource` = inline SQL (jet-sql parsed) | `FeedsRecordSource` per FROM/JOIN target; `SuppliesParameter` for `[Forms]!`/`[Reports]!` (`query-form-reference`) and `TempVars!` (`tempvar-flow`) |
| Declared-parameter | `IrQuery.Parameters` | `form-reference`/`report-reference` → `SuppliesParameter` (`query-form-reference`); `declared` (prompt) → `UnresolvedChainTarget` (`query-declared-parameter`, reason `prompt-parameter`) |

FROM/JOIN-target object kind (Query vs Table vs Unknown) is resolved against the
`IrDatabase.Queries` / `.Tables` name sets. All collections sorted for byte-stable
output. `Close`/`CloseWindow` embedded macros are **not** in the closed kind set →
ignored (only `Opens*` for `OpenForm`/`OpenReport`).

Every HHC golden delta this produces maps to R2-2 (chain edges over the surfaces above).
