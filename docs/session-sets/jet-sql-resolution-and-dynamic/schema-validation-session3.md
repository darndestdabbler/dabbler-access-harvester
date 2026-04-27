### Fixture 04 Review: `FormReferenceFixtureTests.cs`

The tests correctly validate the primary success and failure paths for form reference resolution described in `04-expected.json`. The coverage of different unresolved reasons (`FormNotInMetadata`, `ControlNotOnForm`) is good. However, there is a gap in assertion precision.

*   **Issue:** Weak assertion for the number of resolved references. The SQL in fixture 04 contains 8 distinct references to form controls. The tests assert that the list of annotations is not empty, but do not verify that all 8 references were found and resolved. This leaves a gap where the resolver could be silently failing on some references (e.g., those in the `PARAMETERS` clause) and the test would still pass.
*   **Location:** `FormReferenceFixtureTests.cs`, method `Fixture04_AllFormReferences_ResolveSuccessfully`, line 105.
*   **Fix:** Strengthen the assertion to check for the exact count of expected annotations.

    ```csharp
    // Original
    Assert.NotEmpty(annotations);
    Assert.All(annotations, a => Assert.IsType<ResolvedFormReference>(a.Resolution));
    
    // Fix
    Assert.Equal(8, annotations.Count); // 2 in PARAMS, 6 in SELECT
    Assert.All(annotations, a => Assert.IsType<ResolvedFormReference>(a.Resolution));
    ```

---

### Fixture 05 Review: `DynamicSqlFixtureTests.cs`

The tests for fixture 05 are excellent at validating the *cardinality analysis* of the dynamic SQL. They correctly confirm that the analyzer identifies a combinatorial blowup, respects the `MaxFullEnumeration` cap, produces the correct number of samples, and can perform a full enumeration when the cap is raised.

However, there is a significant gap in validating the *content* of the generated SQL templates.

*   **Issue:** Lack of validation for conditional SQL fragments. The test `Fixture05_AllTemplates_ContainBaseSelectAndFromClauses` only confirms that the static, non-conditional parts of the SQL string are present in the templates. It does not verify that the conditional fragments (`ACTIVE_ONLY`, `OVERDUE`, form control filters, `ORDER BY` clauses) are correctly assembled into the sample templates. The tests validate the path *counting* mechanism but not the path *content assembly*.
*   **Location:** `DynamicSqlFixtureTests.cs`. A new test method is recommended, or `Fixture05_AllTemplates_ContainBaseSelectAndFromClauses` could be expanded.
*   **Fix:** Add a new test that asserts the representative sample of templates includes examples of the various conditional fragments. This ensures the string concatenation logic is working correctly for all branches.

    ```csharp
    [Fact]
    public void Fixture05_SampledTemplates_ContainConditionalFragments()
    {
        var proc = ParseProcedure();
        var site = new DynamicSqlAnalyzer(BuildRegistry()).Analyze(proc).ExecutionSites[0];

        var templateTexts = site.Templates.Select(t => t.Text).ToList();

        // Check that examples of each conditional fragment exist somewhere in the samples
        Assert.Contains(templateTexts, t => t.Contains("c.Status IN ('Open', 'Pending Review')"));
        Assert.Contains(templateTexts, t => t.Contains("DateDiff('d', c.OpenedDate, Date()) > 90"));
        Assert.Contains(templateTexts, t => t.Contains("c.DistrictID ="));
        Assert.Contains(templateTexts, t => t.Contains("c.AssignedCaseworkerID IN (SELECT CaseworkerID"));
        Assert.Contains(templateTexts, t => t.Contains("ORDER BY c.OpenedDate DESC"));
        Assert.Contains(templateTexts, t => t.Contains("ORDER BY c.CaseNumber"));
    }
    ```