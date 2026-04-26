# Verification Round 1

## ISSUES_FOUND

- **Issue:** Missing `DeclareFunction` / `DeclareSub` procedure-kind delta means criterion 1 is not fully met.  
  **Location:** `docs/schema/v0-vs-tiers.md` §§2.1, 2.2, 11, 12; evidence: `ir/ModulesAndClasses/basUtilities.ir.json` has `ProcedureType: "DeclareFunction"`.  
  **Fix:** Add the missing enum/value difference, explain the intended mapping to v0 (`Function`/`Sub` + `Lib`/`Alias` vs new v0 enum members), and include it in the summary/gap tables.

- **Issue:** §9 describes a v0 provenance/tagged-object shape that the current v0 schema does not actually define.  
  **Location:** `docs/schema/v0-vs-tiers.md` §9 vs `docs/test-cases/harvester-schema.v0.json` at `$defs.resolvedReference.properties.resolvedTarget`.  
  **Fix:** Either correct §9 to match the actual schema, or record this as an additional deferred v0 schema gap in §§11-12 if the tagged provenance shape is intended.

- **Issue:** Root/object carrier-field differences are not explicitly reconciled, so the delta doc is incomplete at the envelope/payload boundary.  
  **Location:** `docs/schema/v0-vs-tiers.md` §§1-2; missing explicit comparison of tier root `Name` / `Type` / `VbaCode` / `Procedures[]` against v0 `source.location` / `source.kind` / `source.originalSource` / `items[]`.  
  **Fix:** Add a root-level mapping table for module/class/query/form/report objects so the document covers both wrapper differences and root payload-field differences.

---

# Verification Round 2

## ISSUES_FOUND

- **Issue** → The summary table still misstates the v0 provenance shape as a tagged object (`{ source: "attached"|"unresolved" }`), which contradicts both §9 and `harvester-schema.v0.json` (`resolvedTarget` is `object | null` and unresolved state is carried by `resolutionStatus`).
  **Location** → `docs/schema/v0-vs-tiers.md`, `## 12. Summary Table`, row `D19`.
  **Fix** → Update `D19` so the v0 column reflects the actual v0 shape, e.g. ``resolvedTarget: object | null` with unresolved indicated by `resolutionStatus``; keep `{ source: ... }` scoped to the ir enricher convention only.

---

# Verification Round 3

ISSUES_FOUND
