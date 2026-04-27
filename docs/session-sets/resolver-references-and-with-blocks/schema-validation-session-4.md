## Code Review: ResolverFixtureAnnotationTests.cs (Session 4)

---

### Issue 1 — Missing assertion contradicts test name and comment

**Severity:** Minor
**Location:** `ResolverFixtureAnnotationTests.cs` — `Fixture02_HasResolvedToMeAndBuiltInAndUnresolvedOutcomes`

**Issue:** The test name and inline comment both claim that `ResolvedToBuiltIn` outcomes are verified ("there are also `ResolvedToBuiltIn` outcomes (Format, etc.)"), but no `Assert.Contains` for `ResolvedToBuiltIn` is present. The third `Assert` is merely `Assert.NotEmpty(rm.References)`, which is trivially satisfied by any non-empty module and adds no signal.

**Fix:**
```csharp
Assert.Contains(rm.References, r => r.Outcome is ResolutionOutcome.ResolvedToMe);
Assert.Contains(rm.References, r => r.Outcome is ResolutionOutcome.Unresolved);
Assert.Contains(rm.References, r => r.Outcome is ResolutionOutcome.ResolvedToBuiltIn); // was missing
```

---

### Issue 2 — Test name is semantically inverted

**Severity:** Minor
**Location:** `ResolverFixtureAnnotationTests.cs` — `Fixture03_NoOutcome_HasReason_WithBlockFlatteningDeferred`

**Issue:** The test name reads "has reason WithBlockFlatteningDeferred" but the body asserts `Assert.Empty(deferred)` — i.e., it verifies that *no* such outcomes exist. This is a direct semantic inversion that will mislead anyone diagnosing a failure or tracing the test's intent.

**Fix:** Rename to:
```csharp
public void Fixture03_NoReferences_HaveReason_WithBlockFlatteningDeferred()
```

---

### Issue 3 — Bang-flattening test only proves ≥ 1, not full coverage

**Severity:** Minor
**Location:** `ResolverFixtureAnnotationTests.cs` — `Fixture03_ImplicitWithBangAccess_IsResolvedViaWithFlattening`

**Issue:** The filter `&& r.Outcome is ResolutionOutcome.ResolvedViaWithFlattening` is part of the *query*, so `bangFlattened.Count > 0` only asserts that at least one bang was flattened. Any `ImplicitWithBangExpression` that silently fell through to `Unresolved` or another outcome would be invisible to this test.

**Fix:** Separate the query from the outcome assertion:
```csharp
var allBangs = rm.References
    .Where(r => r.Expression is ImplicitWithBangExpression)
    .ToList();
Assert.True(allBangs.Count > 0,
    "Expected at least one ImplicitWithBangExpression in fixture 03.");
Assert.All(allBangs, r =>
    Assert.IsType<ResolutionOutcome.ResolvedViaWithFlattening>(r.Outcome));
```

---

### Issue 4 — Bang-outcome assertion in fixture 08 is unscoped

**Severity:** Minor
**Location:** `ResolverFixtureAnnotationTests.cs` — `Fixture08_BangAccessOnRs_IsDefaultMemberNotYetResolved`

**Issue:** The test collects *all* `BangAccessExpression` nodes in the module and asserts every one is `DefaultMemberNotYetResolved`. If a future edit to the fixture or registry adds a bang access whose target *does* resolve (e.g., a form control bang on a known object), the test fails for an unrelated reason. The assertion should be scoped to the `rs` target to match the stated intent ("bang-access on rs").

**Fix:**
```csharp
var bangs = rm.References
    .Where(r => r.Expression is BangAccessExpression b
                && b.Target is IdentifierExpression id
                && string.Equals(id.Name, "rs",
                    System.StringComparison.OrdinalIgnoreCase))
    .ToList();
Assert.True(bangs.Count > 0,
    "Expected at least one bang-access reference on rs.");
Assert.All(bangs, r => Assert.Equal(
    UnresolvedReasons.DefaultMemberNotYetResolved,
    Assert.IsType<ResolutionOutcome.Unresolved>(r.Outcome).Reason));
```

---

### Issue 5 — Coverage gap: `Fixture03_ImplicitWithMembers` does not verify the outer-vs-inner With scope is honoured

**Severity:** Minor
**Location:** `ResolverFixtureAnnotationTests.cs` — `Fixture03_ImplicitWithMembers_AreResolvedViaWithFlattening`

**Issue:** Fixture 03 has a nested With (`With Me.sfrmInspections.Form` inside `With rs`). The test only asserts ≥ 1 flattened implicit-with-member and the single-wrap invariant. It does not verify that the correct *enclosing* With is chosen for each member — e.g., that `.RecordsetClone` inside the inner With resolves against `Me.sfrmInspections.Form`, not `rs`. A regression in `FindEnclosingWith` scope-stack ordering would not be caught.

**Fix:** Add targeted checks for at least one known outer-With and one known inner-With member:
```csharp
// .Edit / .Update / .Close are on the outer rs With
var editRef = rm.References.FirstOrDefault(r =>
    r.Expression is ImplicitWithMemberExpression m
    && string.Equals(m.MemberName, "Edit", StringComparison.OrdinalIgnoreCase));
Assert.NotNull(editRef);
Assert.IsType<ResolutionOutcome.ResolvedViaWithFlattening>(editRef!.Outcome);

// .RecordsetClone is on the inner Me.sfrmInspections.Form With
var cloneRef = rm.References.FirstOrDefault(r =>
    r.Expression is ImplicitWithMemberExpression m
    && string.Equals(m.MemberName, "RecordsetClone", StringComparison.OrdinalIgnoreCase));
Assert.NotNull(cloneRef);
Assert.IsType<ResolutionOutcome.ResolvedViaWithFlattening>(cloneRef!.Outcome);
```

---

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "ResolverFixtureAnnotationTests.cs — Fixture02_HasResolvedToMeAndBuiltInAndUnresolvedOutcomes",
      "summary": "Test name and comment promise a ResolvedToBuiltIn assertion that is never made; the third Assert is a trivial NotEmpty check.",
      "fix": "Add Assert.Contains(rm.References, r => r.Outcome is ResolutionOutcome.ResolvedToBuiltIn) and remove the NotEmpty line."
    },
    {
      "severity": "Minor",
      "location": "ResolverFixtureAnnotationTests.cs — Fixture03_NoOutcome_HasReason_WithBlockFlatteningDeferred",
      "summary": "Test name is semantically inverted: the body asserts Assert.Empty (no deferred outcomes) but the name reads as 'has reason WithBlockFlatteningDeferred'.",
      "fix": "Rename to Fixture03_NoReferences_HaveReason_WithBlockFlatteningDeferred."
    },
    {
      "severity": "Minor",
      "location": "ResolverFixtureAnnotationTests.cs — Fixture03_ImplicitWithBangAccess_IsResolvedViaWithFlattening",
      "summary": "The ResolvedViaWithFlattening predicate is inside the Where filter, so the count assertion only proves ≥ 1 bang was flattened; un-flattened ImplicitWithBangExpressions are invisible.",
      "fix": "Separate the query (all ImplicitWithBangExpression nodes) from Assert.All(allBangs, r => Assert.IsType<ResolvedViaWithFlattening>(r.Outcome))."
    },
    {
      "severity": "Minor",
      "location": "ResolverFixtureAnnotationTests.cs — Fixture08_BangAccessOnRs_IsDefaultMemberNotYetResolved",
      "summary": "All BangAccessExpression nodes are asserted, not only those targeting rs; an unrelated bang added to the fixture would break the test unexpectedly.",
      "fix": "Filter on b.Target is IdentifierExpression id && id.Name == \"rs\" before asserting DefaultMemberNotYetResolved."
    },
    {
      "severity": "Minor",
      "location": "ResolverFixtureAnnotationTests.cs — Fixture03_ImplicitWithMembers_AreResolvedViaWithFlattening",
      "summary": "No assertion distinguishes which With block each implicit-with member resolves against; a regression in FindEnclosingWith scope-stack ordering for nested Withs would not be caught.",
      "fix": "Add targeted checks for a known outer-With member (e.g., .Edit against rs) and a known inner-With member (e.g., .RecordsetClone against Me.sfrmInspections.Form)."
    }
  ]
}
```