# vba-symbol-db Session 4 — Interpretation Summary

Fixture: `samples/NorthwindDevEdition.accdb`  
Modules: `Form_frmCompanyDetail` and `modStrings`.  

This report is generated from the symbol DB after the AI
interpretation pass. It is the bookend to Session 1's
`measurement-northwind.md` — Session 1 measured the
dedup/dead/deferred savings on paper; this report measures
what the leaves-first interpretation actually cost in
dollars and routed calls.

## Symbol coverage

- Total symbols in DB: **115**
- Schedulable (interpreted or queued for interpretation): **84**
- Interpreted (Interpretation1 populated): **84**
  - via routed call: **84**
  - via MD5-twin copy: **0**
- Skipped (audit rows in InterpretationHistory):
  - `skipped-dead`: 62
  - `skipped-procedure-local-var`: 31
  - `skipped-low-lookup-score`: 14

## Macros

- Macros in DB: 1
- Interpreted: 1

## Lookup tables

- Tables in DB: 29
- Interpreted (score >= threshold): 15
- Skipped (low lookup score): 14

## Queries

- Queries in DB: 65
- Interpreted: 3
- Skipped dead: 62
- Twin copies: 0

## Cost

- Total routed cost (this run, all build runs combined): **$1.4677**
- BuildRuns recorded: 1

| run_id | session | started_at | interpretations | cost |
|---|---|---|---|---|
| 1 | 4 | 2026-04-29T13:06:20+00:00 | 103 | $1.4677 |

## Confidence distribution (Interpretation1)

| Bucket | Count | % |
|---|---|---|
| 0.90 - 1.00 | 80 | 95% |
| 0.75 - 0.89 | 4 | 5% |

Total interpreted with confidence: 84.

## Escalation rate

- Symbols with opinion 1 only: 84
- Symbols where opinion 2 fired (Confidence1 < 0.75): 0
- Symbols where opinion 3 fired (avg < 0.70): 0
- Escalation rate: 0.0% (vs. spec estimate ~20%)

## Cost-savings vs. naive baseline

The naive baseline interprets every Symbol/Macro/Table/
Query once at full procedure-prompt cost. The current
policy skips procedure-local variables, dead symbols,
low-score tables, and copies MD5-twin interpretations,
and orders interpretation leaves-first so callee summaries
substitute for callee source.

- Naive symbol interpretations: 115
- Actual routed interpretations: 84
- Reduction: 27.0% of symbol calls avoided by the policy filters above

## Notes for Session 5

- The probe target `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`
  has its full direct-callee interpretation set populated:
  the procedure itself (to be blanked at packet build time),
  the seven `m_lng*` / `m_str*` module-level vars, the two
  local `Dim` strings (skipped as proc-local — Session 5's
  packet builder should reconstruct them inline from
  `source_body`), `CompanyIsActive`, `GetString`, and
  `sCompanyDelete_CompanyTypeChange`.
- `DLookup`, `DCount`, `Nz`, `MsgBox`, `DoCmd.OpenForm`, etc.
  resolve to built-ins or the Access object model and stay
  outside the symbol-table interpretation set.
