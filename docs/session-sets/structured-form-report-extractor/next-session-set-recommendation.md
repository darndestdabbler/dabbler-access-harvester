Here is the recommendation for the next session set.

---

### **1. Slug**

`form-report-code-and-grouping`

### **2. Purpose**

This session set will close critical gaps in the typed metadata for forms and reports by linking them to their underlying VBA code-behind modules and decoding the detailed data grouping and sorting logic within reports. The previous set established the typed structure; this set populates the key behavioral fields within that structure. Completing this work is essential for the downstream AI to trace UI events to specific code paths and to accurately interpret the presentation logic of complex reports.

### **3. Key Deliverables**

1.  **`ModuleName` Population:** The `ModuleName` property on `FormMetadata` and `ReportMetadata` will be populated by parsing the `Attribute VB_Name` from the raw `VbaCode` string. This provides the canonical link between a form/report object and its code module.
2.  **`GroupLevel` Metadata:** The detailed properties of a report's `GroupLevel`s (`GroupingMode`, `GroupInterval`, `SortOrder`, `KeepTogether`) will be extracted and populated. This requires using a real Access fixture (e.g., Northwind) to confirm the raw property names from the `SaveAsText` output before implementation.
3.  **Updated Typed Schema Documentation:** The `ir-schema-forms-reports.md` document will be updated to reflect the newly-populated fields and their possible values.

### **4. Session Breakdown**

#### **Session 1: Real-Fixture Analysis and ModuleName Implementation**

*   **Scope:**
    1.  **Fixture Analysis:** Run the existing harvester pipeline against a real Access database containing at least one complex, grouped report. Capture and commit the intermediate `kb/` and `ir/` JSON output for that report to serve as a test fixture. Document the raw `SaveAsText` property names and values discovered for `GroupingMode`, `SortOrder`, etc. This unblocks Session 2.
    2.  **ModuleName Extractor:** Implement the logic to extract the `VB_Name` attribute from the `VbaCode` property. Integrate this into the `FormMetadataExtractor` and `ReportMetadataExtractor`. The `VbaCode` property on the typed record should remain untouched.
    3.  **Unit Tests:** Add hermetic unit tests for the `ModuleName` extraction, covering cases where the attribute is present, absent, or malformed.

#### **Session 2: GroupLevel Metadata Extraction**

*   **Scope:**
    1.  **Extractor Logic:** Using the findings from Session 1, enhance `ReportMetadataExtractor` to parse the newly identified properties for each group level. Populate the `GroupingMode`, `GroupInterval`, `SortOrder`, and `KeepTogether` fields on the `GroupLevel` record.
    2.  **Property Bag Cleanup:** Ensure that, like other promoted properties, these raw group-level keys are removed from the main `Properties` bag on the `ReportMetadata` record.
    3.  **Unit Tests:** Add hermetic unit tests to `ReportMetadataExtractorTests.cs` to validate the correct parsing and population of all new `GroupLevel` fields, including handling of missing or default values.

#### **Session 3: Integration Testing and Documentation**

*   **Scope:**
    1.  **Integration Test:** Add a new test to `SyntheticMetadataExtractorIntegrationTests.cs` that uses the real-world report fixture from Session 1. Assert that the end-to-end process correctly populates both the `ModuleName` and the detailed `GroupLevel` fields for the report.
    2.  **Documentation:** Update `docs/ir-schema-forms-reports.md`. Mark the newly implemented fields as "Populated" (removing the "deferred" status) and document their data types and semantics for the downstream AI consumer.
    3.  **Final Review:** Address any code review feedback and ensure all 400+ tests pass cleanly.