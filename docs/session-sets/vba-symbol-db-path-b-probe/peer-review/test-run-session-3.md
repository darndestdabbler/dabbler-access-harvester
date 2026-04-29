# Session 3 Test Run

## Targeted Probe Project (Session 2 + Session 3 Combined)

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test tests/AccessHarvester.TranslationProbe.Tests/...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9, Duration: 19 ms - AccessHarvester.TranslationProbe.Tests.dll (net10.0)
```

Five Session 2 tests + four new Session 3 tests, all green.

The four new Session 3 tests target the under-tested invariants
documented in [`translation-review.md`](translation-review.md):

- `Session3RegressionTests.CompanyIsActive_PopulatesAllFiveCachedCounts_EvenWhenInactive` (U1)
- `Session3RegressionTests.CboCompanyTypeIdBeforeUpdate_DoesNotCloseDialog_WhenAlreadyClosed` (U2)
- `Session3RegressionTests.CompanyIsActive_DelegatesErrorWithDefaultShowUserMessage_OnDaoFailure` (U3)
- `Session3RegressionTests.CboCompanyTypeIdBeforeUpdate_Cancels_WhenActiveButNoVendorProducts` (U4)

## Did A Session 3 Test Force A Translation Choice Session 2 Did Not Make?

**Yes — U2 and U3, partially.**

- **U2 (`...DoesNotCloseDialog_WhenAlreadyClosed`)** would catch a
  translator who collapsed the `IsFormOpen("frmGenericDialog")` guard
  on the assumption that `CloseForm` is idempotent. Session 2's
  blocking-path test sets `IsOpen = true` and asserts CloseForm was
  called; it would still pass if the C# unconditionally called
  `CloseForm`. The Session 2 translation does preserve the guard, so
  U2 passes; the test pins the property prospectively rather than
  retroactively.
- **U3 (`...DelegatesErrorWithDefaultShowUserMessage_OnDaoFailure`)**
  forces the inner `HandleError` call to use
  `showUserMessage == false`. The C# default-parameter mechanism
  happens to deliver this correctly, but a translator who
  hard-coded `true` to "match the outer call" would pass all five
  Session 2 tests. U3 detects that.

U1 and U4 are the same shape — Session 2's translation already does
the right thing, but its tests do not lock the invariant. Session 3
tests lock them prospectively against future regressions.

**No Session 3 test failed against the Session 2 translation.** This
is the right outcome for the probe: it means the translation was
already correct on the points the review surfaced; the value Session
3 added is making those points test-falsifiable.

## Full Solution Validation

```text
dotnet build AccessHarvester.sln
Build succeeded.
    4 Warning(s)
    0 Error(s)

dotnet test AccessHarvester.sln --no-build
Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9   - AccessHarvester.TranslationProbe.Tests.dll
Passed!  - Failed:     0, Passed:    54, Skipped:     0, Total:    54   - AccessHarvester.Vba.Parser.Tests.dll
Passed!  - Failed:     0, Passed:    85, Skipped:     0, Total:    85   - AccessHarvester.Sql.Grammar.Tests.dll
Passed!  - Failed:     0, Passed:   294, Skipped:     0, Total:   294   - AccessHarvester.Vba.Grammar.Tests.dll
Passed!  - Failed:     0, Passed:   400, Skipped:     0, Total:   400   - AccessHarvester.Vba.Resolver.Tests.dll
Passed!  - Failed:     0, Passed:   355, Skipped:     0, Total:   355   - AccessHarvester.Pipeline.Tests.dll
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14   - AccessHarvester.Integration.dll
Passed!  - Failed:     0, Passed:    70, Skipped:     0, Total:    70   - AccessHarvester.SymbolDb.Tests.dll
Passed!  - Failed:     0, Passed:   328, Skipped:     2, Total:   330   - AccessHarvester.Tests.dll
Skipped! - Failed:     0, Passed:     0, Skipped:     1, Total:     1   - AccessHarvester.Comprehension.Tests.dll
```

Combined: **1609 passed, 4 skipped, 0 failed** across all projects.

The four warnings remain `NU1903` package-vulnerability advisories
on `Microsoft.Build.Utilities.Core 17.8.3` (already present before
this session; not introduced by Session 3).
