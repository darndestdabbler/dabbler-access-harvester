### 1. `LocalVariable_ToFqn_IncludesProcedureSegment`

This test is now fixture-assertive and materially fixes the round-2 problem. The key change is `First(...)` instead of a null-tolerant lookup: the test now fails if the fixture no longer produces a resolved `strDlgMsg` reference from `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`. That makes it a real assertion against observed fixture behavior instead of a silent pass.

The assertions themselves are also aligned with the defect being checked. The regression risk was collapsing a local variable FQN from `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate.strDlgMsg` to the incorrect module-level shape `Form_frmCompanyDetail.strDlgMsg`. The current test rejects that by asserting both:
- the procedure segment is present: `.cboCompanyTypeID_BeforeUpdate.`
- the module/class prefix is correct: `Form_frmCompanyDetail.`

Given the inspection data explicitly says `strDlgMsg` resolves to `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate.strDlgMsg`, this is correct for the fixture and no longer vacuous. It could be made slightly stricter with `Assert.Equal(...)` to the full expected FQN, but that is an optional tightening, not a correctness gap for this round.

### 2. Guard tests for `control_access` and `enum_value_use`

These are acceptable as guard tests given the fixture data. The inspection run establishes that, in this fixture:
- `enum_value_use = 0`
- `control_access = 0`

The comments explain why those counts are zero:
- enum members are unresolved cross-module in this fixture
- the form uses dot notation (`Me.Foo`), not bang notation (`Me!Foo`)

Because of that, these tests should be read as forward-looking shape/invariant checks, not as affirmative coverage that those kinds are currently extracted in Northwind. In that role, they are reasonable: if a future fixture or resolver change starts producing these row kinds, the tests will at least validate that `Identifier` is populated.

So, for the stated purpose, they are acceptable. They are still vacuous against the current fixture’s actual row counts, but that is now documented and intentional rather than accidental. If affirmative coverage is wanted later, the right fix is a dedicated fixture that actually produces `control_access` and resolvable `enum_value_use` rows.

### 3. Remaining issues

There is one remaining minor issue in the test file: `MemberAccesses_BangAccessRows_HaveExclamationInPath` is tautological.

It filters rows with `ma.Path.StartsWith("!")` and then, for those same rows, asserts `Assert.StartsWith("!", ma.Path)`. That adds no signal even when bang rows exist, because the assertion repeats the selection predicate. In the current fixture it also passes vacuously, since the fixture uses dot notation and produces no bang-style access rows.

So the round-2 finding is fixed, but this separate test is still non-assertive and should be corrected. The cleanest fix for this fixture is to assert there are no bang-path member accesses at all. Alternatively, if the intent is to validate bang-path shape, use a fixture that actually contains `Me!Foo` and assert a non-tautological property on the resulting rows.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "ReferencesExtractorTests.MemberAccesses_BangAccessRows_HaveExclamationInPath",
      "summary": "The test is tautological because it filters `Path.StartsWith(\"!\")` and then asserts the same condition, so it provides no real coverage.",
      "fix": "Replace it with a fixture-assertive check such as `Assert.DoesNotContain(result.MemberAccesses, ma => ma.Path.StartsWith(\"!\"))` for this Northwind fixture, or add a dedicated fixture containing `Me!Foo` and assert non-tautological bang-access invariants there."
    }
  ]
}
```