# Symbol DB Measurement — NorthwindDevEdition.accdb (Session 1 fixture: Form_frmCompanyDetail + modStrings)

> **Session:** 1 of 5 (`vba-symbol-db`).
> **Purpose:** Quantify how many AI interpretation calls are needed once dead-
> code, deferred-companion, and MD5-twin skips are applied. The number drives the
> Session 4 cost expectation in `docs/session-sets/vba-symbol-db/spec.md`.

## Totals

| Metric | Count |
|---|---|
| Modules harvested (rows in `Modules`) | 2 |
| Modules deferred (Old companions) | 0 |
| Symbols total | 115 |
| Symbols flagged dead | 0 |
| Procedures (`sub`) | 22 |
| Functions | 6 |
| Properties (Get/Let/Set) | 0 |
| Module variables | 7 |
| Enum declarations | 1 |
| Enum values | 48 |
| MD5-equivalent clusters detected | 0 |
| MD5-twin siblings tagged (interpretation will be copied) | 0 |

## AI-call savings vs. naive baseline

| Strategy | Calls needed | Savings |
|---|---|---|
| Naive (interpret every symbol) | 115 | — |
| With deferral skip | 115 | −0 |
| ... + dead-code skip | 115 | −0 more |
| ... + MD5-twin copy | 115 | −0 more |
| **Net reduction** | **115** | **0.0%** |

Numbers are over-additive in the rare case where dead and twin overlap. Session 4
will replay this table against the actual interpretation pass to produce the
definitive cost reduction.

## Per-module breakdown

| Module | Kind | Symbols | Dead | Procedures | Module vars | Local vars | Enum decls | Enum values |
|---|---|---|---|---|---|---|---|---|
| `Form_frmCompanyDetail` | Form | 51 | 0 | 24 | 7 | 20 | 0 | 0 |
| `modStrings` | Module | 64 | 0 | 4 | 0 | 9 | 1 | 48 |

## Deferred modules

None — `NorthwindDevEdition.accdb` has no `Old`-companion modules in the
Session 1 input set. The deferral filter is exercised by the unit tests
regardless; this is the documented zero-case baseline.
