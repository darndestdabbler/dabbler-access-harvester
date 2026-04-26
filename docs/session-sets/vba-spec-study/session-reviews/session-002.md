## Verification Summary

| Check | Verdict |
|---|---|
| 1. Every summary-table critical construct has at least one `Priority=must` row | **PASS** |
| 2. Column meanings are consistent throughout | **FAIL** |
| 3. Priority assignments respect Lean By Default | **PASS** |
| 4. CSV is well-formed | **PASS** |
| 5. `ms-vbal-summary.md` heading says `15 Critical Constructs` | **PASS** |

## Issues

- **Major** Issue → Corpus cross-references are not mechanically consistent with the stated standard: `Corpus_Fixtures` mixes explicit fixture IDs, `all`, `all 12`, and `various`, and does not reference Northwind anywhere.  
  **Location** → `docs/spec-coverage/ms-vbal-coverage.csv`, `In_Corpus` / `Corpus_Fixtures` columns throughout.  
  **Fix** → Normalize corpus notation to explicit fixture references and include Northwind in the matrix (either in `Corpus_Fixtures` or a dedicated column), then recompute affected `In_Corpus` values.

- **Major** Issue → Event coverage is internally contradictory: one row says `Event declaration (Event keyword)` is absent and `should`, while another says `Event declaration + RaiseEvent` is present in fixture `10` and `must`.  
  **Location** → `docs/spec-coverage/ms-vbal-coverage.csv` rows `§4.4 Event declaration (Event keyword)` and `§6.1 Event declaration + RaiseEvent`.  
  **Fix** → Reinspect fixture `10`; if it contains custom events, mark the declaration row as present and align priority. If it only uses one construct, split `Event declaration` and `RaiseEvent` into distinct non-overlapping rows with accurate corpus evidence.