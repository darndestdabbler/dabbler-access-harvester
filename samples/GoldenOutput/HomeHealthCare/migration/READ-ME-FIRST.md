# HomeHealthCare — `generate-migration` golden

This directory holds the **golden migration artifacts** for the
`generate-migration` subcommand, pinned against the committed
HomeHealthCare golden IR in the sibling `../ir/` tier.

Unlike `../ir`, `../kb`, and `../reports-baseline`, this directory is **not
produced by the harvester pipeline** (`run` / `report-baseline`) and is **not**
checked by `CanonicalGoldenRegressionTests` — it is a derived golden for the
classify → sanitize → **migrate** chain, regenerated only when the migration
generator's output intentionally changes.

| File | Role |
|---|---|
| `schema.sql` | Golden `CREATE TABLE` + PK + FK DDL emitted from `../ir`. |
| `data.sql` | Golden FK-safe data-load emitted from `sample-data/` (identity, non-identity, and missing-data paths all exercised). |
| `sample-data/*.csv` | A small committed slice of `sanitize` CSV-per-table output (`tlkpStates`, `tlkpPlanStatuses`) used as the data-load input for `data.sql`. The other 11 tables intentionally have no CSV, so `data.sql` also pins the honest "no data file" degradation path. |

`MigrationGoldenScriptTests` (in `AccessHarvester.Pipeline.Tests`) regenerates
these from the committed IR + `sample-data/` and byte-compares (LF-normalized).

To refresh after an intentional generator change:

```
AccessHarvester generate-migration \
  --in   samples/GoldenOutput/HomeHealthCare \
  --data samples/GoldenOutput/HomeHealthCare/migration/sample-data \
  --out  <temp>
cp <temp>/homehealthcare/schema.sql samples/GoldenOutput/HomeHealthCare/migration/schema.sql
cp <temp>/homehealthcare/data.sql   samples/GoldenOutput/HomeHealthCare/migration/data.sql
```
