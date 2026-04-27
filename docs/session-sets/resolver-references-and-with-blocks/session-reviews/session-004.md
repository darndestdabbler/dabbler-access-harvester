```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "ResolverFixtureAnnotationTests.cs / Fixture02_HasResolvedToMeAndBuiltInAndUnresolvedOutcomes",
      "summary": "The test name and comment claim fixture 02 proves built-in resolution, but the assertions never check for any `ResolvedToBuiltIn` outcome.",
      "fix": "Add an explicit assertion such as `Assert.Contains(rm.References, r => r.Outcome is ResolutionOutcome.ResolvedToBuiltIn);`; if fixture 02 does not actually contain a built-in hit, rename the test and comment to remove the built-in claim."
    },
    {
      "severity": "Minor",
      "location": "ResolverFixtureAnnotationTests.cs / Fixture08_LocalVariables_AreResolved",
      "summary": "The test only requires at least one resolved use per local name, so partial resolution regressions for other uses of the same local would go undetected.",
      "fix": "Strengthen the check to assert `hits` is non-empty and that every hit for each listed local has `ResolutionOutcome.Resolved`."
    }
  ]
}
```