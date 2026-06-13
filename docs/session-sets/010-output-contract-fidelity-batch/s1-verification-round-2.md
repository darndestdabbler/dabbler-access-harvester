The two Round-1 findings are correctly resolved.

For R1-#1, the raw DAO decode no longer collapses 112 and 144 onto the same label. `QueryExporter.QueryTypeName` now emits `SqlPassthrough` for 112 and the distinct `SqlPassthroughBulk` for 144, and `QueryExporterDecodeTests` explicitly pins both values. `QuerySqlProjector.QueryTypeMap` was updated in lockstep so the new raw label still projects to the same manifest schema type, `PassThrough`. That removes the collision without changing the schema-level contract.

For R1-#2, the Q11 classification gap is also closed. `UsageClassifier` now includes `DaoQueryTypeCodes.SptBulk` alongside DDL and standard pass-through, the strategy doc now lists Q11 as `{96,112,144}`, and the new `Query_Q11_PassThroughBulk_Is_StandaloneUtility` test pins the expected behavior for 144. So bulk pass-through queries no longer fall through to the wrong classification path.

I also do not see a regression or a new inconsistency introduced by these fixes. The touched layers now agree on the corrected DAO code table: exporter decode, projector mapping, classifier logic, question generation, strategy docs, and tests are aligned. Centralizing the numeric constants in `DaoQueryTypeCodes` materially reduces future drift risk. Given the passing build/test results and the specific coverage added around 144, this is in a good state for Session 1, with the known golden staleness still appropriately deferred to Session 2.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```