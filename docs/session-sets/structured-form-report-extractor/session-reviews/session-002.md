# Verification Round 1

{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "tests/AccessHarvester.Tests/SyntheticMetadataExtractorIntegrationTests.cs",
      "summary": "AssertForms/AssertReports unconditionally compare top-level scalar fields via ReadOptionalString, so omitted fixture keys are treated as expected null and the new loose-match path will likely fail when Access writes default/empty values such as DefaultView, Filter, or OrderBy.",
      "fix": "Make each form/report scalar assertion conditional on the fixture actually containing that property (e.g. TryGetPropertyValue(\"RecordSource\"/\"Filter\"/\"OrderBy\"/\"DefaultView\", ...)) instead of comparing missing keys to null; mirror the conditional pattern already used in AssertGroupLevels and control-property assertions."
    }
  ]
}

---

# Verification Round 2

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
