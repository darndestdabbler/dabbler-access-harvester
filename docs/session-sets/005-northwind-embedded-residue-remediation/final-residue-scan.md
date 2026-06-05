# Final `HEAD` Residue Scan — 005 Session 3

> **Run:** Session 3 (2026-06-05), after the source genericization + doc/tooling
> redaction. **Verdict: PASS** — no tracked file outside accepted-and-recorded
> paths carries a NorthwindDevEdition **structural identifier**. This set is
> `HEAD`-only; pre-rewrite history is accepted residue by maintainer decision.

## How the scan was run

`git grep -lIE '<distinctive NorthwindDevEdition structural-identifier set>'` over
the whole tracked tree, refreshed from `003`'s
[`embedded-residue-findings.md`](../003-northwind-proprietary-data-remediation/embedded-residue-findings.md)
"How the scan was run" and Session 1's
[`disposition-manifest.md`](disposition-manifest.md). The identifier set is the
**distinctive** prefixed object names that do not collide with the synthetic
HomeHealthCare vocabulary or generic Access examples — e.g. `frmCompanyDetail`,
`frmCompanyList`, `sfrmCompanyDetail_*`, `qryCompanies`, `qryCompanyList`,
`clsErrorHandler`, `modDAO`, `modStrings`, `modInventory`, `modOrders`,
`frmOrderDetails`, `sfrmOrderLineItems`, `qryOrderLineItems`, `qryProductList_Export`,
`qrySalesByRegion`, `rptSalesByProduct`, `rptSalesByEmployee`, `rptSalesByPeriod`,
`srptOrderForm`, `frmSelectVendorDialog`, `EmployeePrivileges`, `CompanyIsActive`,
`sCompanyDelete_CompanyTypeChange`, `frmCredentials`, `Catalog_TableOfContents`,
the `AddAnOrder` / `UpdateAProduct` / `UpdateAnOrder` / `UpdateAnEmployee` workflow
names, and the coupled `OrderCreationWorkflow` / `BlazorAddAnOrder` /
`ShipperEntity` code identifiers.

Deliberately **excluded** from the identifier set (per the manifest, and confirmed
this session):

- The brand/fixture token **`Northwind` / `NorthwindDevEdition`** alone — it is the
  fixture *identity*, not structural disclosure, and is required wherever the
  local-only fixture policy is documented (`.gitignore`, `project-guidance.md` →
  Canonical Test Fixtures, the symbol-db README, and the `003`/`004`/`005` process
  records).
- The local-only gitignored fixture trees (`tmp/comprehension-ir/.../NorthwindDevEdition/`,
  `samples/GoldenOutput/NorthwindDevEdition/`, `samples/DatabaseYaml/NorthwindDevEdition.accdb/`)
  — never tracked; confirmed `git ls-files` returns nothing under them.

## Result

**Outside accepted-and-recorded paths: zero matches.** The genericization +
redaction removed every distinctive structural identifier from production source,
live tooling, reference docs, and planning docs.

### Accepted residue (recorded, not removed) — 11 tracked files

All remaining matches are in paths that *must* retain the identifiers:

| Path | Why accepted |
|---|---|
| `003-northwind-proprietary-data-remediation/embedded-residue-findings.md` | `003` process record — the inventory of what was removed |
| `003-…/s2-issues.json` | `003` verifier issue log |
| `004-…/activity-log.json`, `004-…/scrub-allowlist-rationale.md` | `004` process records |
| `005-…/activity-log.json`, `disposition-manifest.md`, `disposition-manifest.csv`, `s2-issues.json`, `s2-verification.md` | **this set's** process records — must name what is deleted/re-based/redacted |
| `001-…/session-reviews/session-003.md` | Immutable raw verifier output (workflow forbids editing) — recorded in the manifest's keep-accepted list |
| `harvester-report-baseline-golden-corpus/session-reviews/session-002.md` | Immutable raw verifier output — recorded in the manifest's keep-accepted list |

### Generic-example identifiers evaluated and kept (not NorthwindDevEdition disclosure)

- `rptInvoice` — generic invoice report (the parser test-pair
  `docs/test-cases/06-calculated-expressions.*` uses it as an illustrative
  invoice total; it is coupled to a live fixture-conformance test via the
  `.expected.json` `Container` field, so renaming would risk the test for no
  compliance gain). Recorded keep-accepted in Session 1's manifest.
- `frmPatients`, `frmCaseManagement`, `frmCaseDetail`, `tblCases`, `qryCaseworkerList`,
  `frmDashboard`, `frmGenericDialog` — invented case-management / generic example
  names in `docs/test-cases/`, `docs/ir-schema-forms-reports.md`, and the
  migration-map, not NorthwindDevEdition objects.
- HomeHealthCare (synthetic) objects — `frmReportParam_*`, `frmServiceFilter`,
  `modReportParameters`, `qryProviderPlansByClient_Param`, `tblClients`,
  `basListHelpers`, etc. — out of scope (synthetic fixture), kept verbatim.

## Scope gaps found and fixed this session (beyond the Session-1 manifest)

The Session-1 inventory missed several tracked files; the final scan surfaced them
and they were handled at `HEAD`:

1. **`docs/lowdefy-evaluation/` (5 files) — DELETED.** `prompt-for-codex.md`
   embedded the full deleted `add-an-order` workflow package verbatim (proprietary
   order/inventory business logic, denser than structural names → spec Stop Rule).
   Surfaced to the maintainer; maintainer chose to delete the whole spent
   evaluation arc. See [`s3-stop-rule-finding.md`](s3-stop-rule-finding.md).
2. **`docs/ir-schema-forms-reports.md`** — not in the manifest; carried the
   `rptByRegion` / `qrySalesByRegion` worked example. Redacted to generic.
3. **`tools/AccessHarvester.IrCodegen/` (Program.cs + README.md)** — default
   namespace `BlazorAddAnOrder.Ui.Pages.Generated` (referencing the deleted Blazor
   pilot). Genericized to `GeneratedApp.Ui.Pages.Generated`.
4. **`generalization-validation-on-non-northwind/spec.md`** — an `AddAnOrder` test
   path reference. Redacted.
5. **`form-report-code-and-grouping/{activity-log,disposition}.json`** — a
   `rptByRegion` reference in a description field. Redacted.

## Dead-reference cleanups (side effects of the `003`/`004`/`005` deletions)

- `examples/northwind-output.json` was referenced by `install.md` and
  `interpretation-guide.md` but is **not tracked** (dead link). Both references
  were rewritten to point at a live `manifest.json` from a harvester run.
- `interpretation-guide.md` claimed a committed `samples/GoldenOutput/NorthwindDevEdition/`
  golden corpus; only `samples/GoldenOutput/HomeHealthCare/` is committed. Repointed
  the golden-corpus references and the worked example to the committed HomeHealthCare
  corpus / generic shapes.

## Conclusion

The tracked working tree is **NorthwindDevEdition-free at `HEAD`** for structural
identifiers, with only the accepted-and-recorded process records and immutable
verifier outputs retaining them, plus evaluated generic-example names. No
git-history rewrite or force-push was performed.
