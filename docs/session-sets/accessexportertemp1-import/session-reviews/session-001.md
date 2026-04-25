# Verification Round 1

ISSUES_FOUND
- **SEVERITY:** major  
  **ITEM:** OQ2-FORMANDREPORTDEFAULTS  
  **ISSUE:** The plan makes `samples/FormAndReportDefaults.accdb` contingent on Open question #2, but the spec's Feature 4 explicitly requires importing `samples/FormAndReportDefaults.accdb` in Session 6. This is a direct spec contradiction in both the disposition table and the per-session summary.  
  **SUGGESTION:** Mark `samples/FormAndReportDefaults.accdb` as **import-as-is** in Session 6 unconditionally. Restrict Open question #2 to `samples/MasterDbPropertyExtractor.accdb` and `samples/FormsAndReportsYaml/` only.

- **SEVERITY:** major  
  **ITEM:** WINDOWS-COM-GATING-PLACEMENT  
  **ISSUE:** The plan is internally inconsistent about `[assembly: SupportedOSPlatform("windows")]`. It correctly says the attribute is in `Com/AccessComSession.cs`, but elsewhere says it "travels with the project file" and implies it is retained on `AccessHarvester.Cli/AccessHarvester.Cli.csproj`. The spec/project guidance require the assembly-level Windows gate on `AccessHarvester.Core`.  
  **SUGGESTION:** Normalize the wording to one statement: the attribute lives in `AccessComSession.cs`, so importing that file places the assembly-level gate on `AccessHarvester.Core`; remove any CLI/csproj wording.

- **SEVERITY:** minor  
  **ITEM:** BATON-DRIFT-TEST-COUNT  
  **ISSUE:** The BATON drift table misstates the actual test-tree file count. The provided inventory shows 10 `.cs` files under `tests/AccessExporter.Tests/`; 9 are test files and `UatChecklist.cs` is the helper. The plan says there are "9 test `.cs` files (one of which, `UatChecklist.cs`, is a helper)," which is self-contradictory.  
  **SUGGESTION:** Correct the row to say there are 10 `.cs` files total, of which 9 are test files and `UatChecklist.cs` is a helper.

- **SEVERITY:** major  
  **ITEM:** OQ3-NOT-NECESSARY  
  **ISSUE:** Open question #3 asks the human whether to expand Session 4 to fix two known `AccessTextParser` bugs now versus defer. That is not necessary to approve the import plan: the spec already allows importing code while carrying known issues, and lean-by-default favors deferral unless explicitly requested. Making this a blocking prerequisite for Session 2 adds avoidable human gating.  
  **SUGGESTION:** Remove Open question #3 as a blocking question. Keep the two parser bugs only in "Known issues imported as-is" and default `AccessTextParser.cs` to `import-as-is` for Session 4 unless the human separately asks to pull the fixes forward.

---

# Verification Round 2

ISSUES_FOUND
- SEVERITY: major
  ITEM: WINDOWS-COM-GATING-PLACEMENT
  ISSUE: The Round 1 fix is not fully clean. The Methodology section and the `Com/AccessComSession.cs` row correctly place `[assembly: SupportedOSPlatform("windows")]` in `Com/AccessComSession.cs`, but the `AccessExporter.Core/` section intro still says the assembly-level declaration "travels with the project file." That contradicts the corrected file-level placement and leaves prior wording behind.
  SUGGESTION: Replace that sentence with file-accurate wording (`the assembly-level attribute travels with Com/AccessComSession.cs`) or delete it and rely on the corrected Methodology note plus the `Com/AccessComSession.cs` row.

- SEVERITY: major
  ITEM: DISPOSITION-BUCKET-COMPLETENESS
  ISSUE: The plan still does not classify every listed resource into one of the four required Session 1 buckets. `samples/MasterDbPropertyExtractor.accdb`, both `samples/FormsAndReportsYaml/*.yaml` files, and the five additional hermetic test files are marked `flag — see Open question` instead of `import-as-is`, `import-with-edits`, `rebuild`, or `out-of-scope`. That means the Session 1 deliverable is still incomplete on its own terms.
  SUGGESTION: Keep the human questions, but change each flagged row to a real disposition with a note like `pending human approval` or `recommended import-as-is unless human overrides`. At minimum, the table should use only the four spec-approved disposition labels.

- SEVERITY: minor
  ITEM: SESSION-4-ENRICHER-COUNT
  ISSUE: In `Per-session imports at a glance`, Session 4 says `7 enricher files`, but the listed top-level enricher files are 6 (`GeometryEnricher`, `LabelResolver`, `PropertyDecoder`, `TextCapacityEnricher`, `VbaSignatureEnricher`, `MacroActionsEnricher`), with 3 additional helper/parser files under `Vba/` and `Macros/`. The count does not match the inventory.
  SUGGESTION: Change the wording to `6 enricher entry-point files plus 3 nested helper/parser files`, or remove the numeric count entirely.

---

# Verification Round 3

VERIFIED

Round 3 resolves all three Round 2 findings: the `AccessHarvester.Core` section intro now correctly says the assembly-level Windows platform gate travels with `Com/AccessComSession.cs`, the previously nonconforming disposition cells now use explicit spec-valid bucket labels with pending-OQ qualifiers, and the Session 4 summary now correctly states 6 enricher entry-point files plus 3 nested helper/parser files with names listed. I did not find a new contradiction introduced by those fixes: the disposition table remains internally consistent, the pending human decisions are clearly isolated without breaking bucket labeling, the per-session import breakdown still aligns with the Session 1 spec, and the document still functions as a usable Session 1 import-plan deliverable with BATON drift noted, dispositions assigned, and open questions surfaced for human approval before Session 2.
