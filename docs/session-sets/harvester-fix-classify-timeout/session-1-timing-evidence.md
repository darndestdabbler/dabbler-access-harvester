# classify timing evidence — HomeHealthCare.accdb

- Database: `C:\Users\denmi\source\repos\dabbler-access-harvester\samples\HomeHealthCare.accdb`
- File size: 4,296,704 bytes
- ValueSampleSize: 200
- Captured (UTC): 2026-06-01T18:34:12.5697446+00:00

## Phase A — schema read

- Phase A total (OLE DB rowsets + DAO/Access COM): **6.42 s**
  - A1 OLE DB schema rowsets only: 0.07 s (tables=13, columns=209, pk=18, fk=12)
  - A2 DAO/Access-COM augmentation (A − A1, approx): 6.35 s
- Schema shape: 13 tables (0 linked), 93 columns

## Phase B — value sampling (single reused OLE DB connection)

- Phase B total: **0.16 s**

### By query kind

| Query kind | Count | Total (s) | Avg (ms) | Max (ms) | Slowest location |
|---|---:|---:|---:|---:|---|
| cardinality(DISTINCT) | 93 | 0.06 | 0.7 | 2 | tblClientServiceNeeds.Priority |
| null-count | 93 | 0.04 | 0.5 | 1 | tblClientServicePlan.ClientServicePlanID |
| value-sample(TOP N) | 83 | 0.04 | 0.5 | 1 | tlkpServiceCategories.Description |
| rowcount(COUNT*) | 13 | 0.01 | 0.9 | 7 | tblClientBudgets |

### Tables by total sampling time

| Table | Rows | Cols | Sampling (s) |
|---|---:|---:|---:|
| tblClients | 12 | 18 | 0.03 |
| tblProviders | 6 | 15 | 0.03 |
| tblClientServiceNeeds | 62 | 10 | 0.02 |
| tblClientServicePlan | 52 | 11 | 0.02 |
| tblClientBudgets | 21 | 5 | 0.01 |
| tblServiceRateLimits | 22 | 5 | 0.01 |
| tblProviderServiceCatalog | 53 | 6 | 0.01 |
| tblSwitchboardItems | 52 | 6 | 0.01 |
| tblServices | 12 | 6 | 0.01 |
| tlkpFiscalYears | 4 | 4 | 0.01 |
| tlkpServiceCategories | 7 | 3 | 0.00 |
| tlkpStates | 56 | 2 | 0.00 |
| tlkpPlanStatuses | 2 | 2 | 0.00 |

### 25 slowest individual queries

| Ms | Kind | Location | Table rows |
|---:|---|---|---:|
| 7 | rowcount(COUNT*) | tblClientBudgets | 0 |
| 2 | cardinality(DISTINCT) | tblClientServiceNeeds.Priority | 62 |
| 1 | cardinality(DISTINCT) | tblProviderServiceCatalog.RatePerHour | 53 |
| 1 | cardinality(DISTINCT) | tblServiceRateLimits.FiscalYear | 22 |
| 1 | cardinality(DISTINCT) | tblClientServiceNeeds.FiscalYear | 62 |
| 1 | value-sample(TOP N) | tlkpServiceCategories.Description | 7 |
| 1 | value-sample(TOP N) | tblClientServiceNeeds.FiscalYear | 62 |
| 1 | value-sample(TOP N) | tblClientServiceNeeds.Priority | 62 |
| 1 | cardinality(DISTINCT) | tblServiceRateLimits.MaxRatePerHour | 22 |
| 1 | null-count | tblClientServicePlan.ClientServicePlanID | 52 |
| 1 | null-count | tblClientServiceNeeds.Priority | 62 |
| 1 | null-count | tblProviderServiceCatalog.RatePerHour | 53 |
| 1 | cardinality(DISTINCT) | tblClients.LastName | 12 |
| 1 | cardinality(DISTINCT) | tblClients.EnrollmentDate | 12 |
| 1 | cardinality(DISTINCT) | tblClients.Address2 | 12 |
| 1 | cardinality(DISTINCT) | tblProviders.StateCode | 6 |
| 1 | cardinality(DISTINCT) | tblProviders.Email | 6 |
| 1 | cardinality(DISTINCT) | tblClients.FirstName | 12 |
| 1 | value-sample(TOP N) | tblProviderServiceCatalog.FiscalYear | 53 |
| 1 | cardinality(DISTINCT) | tblProviders.City | 6 |
| 1 | cardinality(DISTINCT) | tblProviders.ProviderID | 6 |
| 1 | cardinality(DISTINCT) | tblServiceRateLimits.Notes | 22 |
| 1 | null-count | tblClients.Address2 | 12 |
| 1 | cardinality(DISTINCT) | tblProviders.ContactLastName | 6 |
| 1 | null-count | tblClientServiceNeeds.PreferredTimeStart | 62 |

## Totals

- Phase A (schema read): 6.42 s
- Phase B (value sampling): 0.16 s
- Overall (A + B + overhead): 6.87 s

