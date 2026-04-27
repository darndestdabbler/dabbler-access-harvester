```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/AccessIntegration/ReferenceResolverAccessIntegrationTests.cs",
      "summary": "The new bare-control tests cover positive resolution, Step-1 local shadowing, and standard-module exclusion, but they do not exercise a control name collision with a top-level Access name or built-in, so the claimed Step-2-over-Step-3/4 shadowing order is still unpinned.",
      "fix": "Add a form/report-module test with a control whose name matches `Forms`, `Reports`, or a known built-in and assert the bare identifier resolves to the synthetic control declaration rather than the top-level or built-in candidate."
    }
  ]
}
```