# Session 2 Test Run

## Targeted Probe Project

```text
  Determining projects to restore...
  Restored tests\AccessHarvester.TranslationProbe.Tests\AccessHarvester.TranslationProbe.Tests.csproj.
  AccessHarvester.TranslationProbe.Tests -> tests\AccessHarvester.TranslationProbe.Tests\bin\Debug\net10.0\AccessHarvester.TranslationProbe.Tests.dll
Test run for tests\AccessHarvester.TranslationProbe.Tests\bin\Debug\net10.0\AccessHarvester.TranslationProbe.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5
```

No translation ambiguities surfaced strongly enough to block the probe.
The tests supported the chosen shape:

- return `bool` as the translation of the VBA `Cancel` flag
- collapse Access dialog operations into `IDialogService`
- translate `CompanyIsActive()` plus its cached counters

## Full Solution Validation

```text
dotnet build AccessHarvester.sln
Build succeeded.
    283 Warning(s)
    0 Error(s)

dotnet test AccessHarvester.sln --no-build
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5 - AccessHarvester.TranslationProbe.Tests.dll
Passed!  - Failed:     0, Passed:    54, Skipped:     0, Total:    54 - AccessHarvester.Vba.Parser.Tests.dll
Passed!  - Failed:     0, Passed:   294, Skipped:     0, Total:   294 - AccessHarvester.Vba.Grammar.Tests.dll
Passed!  - Failed:     0, Passed:    85, Skipped:     0, Total:    85 - AccessHarvester.Sql.Grammar.Tests.dll
Passed!  - Failed:     0, Passed:   400, Skipped:     0, Total:   400 - AccessHarvester.Vba.Resolver.Tests.dll
Passed!  - Failed:     0, Passed:   355, Skipped:     0, Total:   355 - AccessHarvester.Pipeline.Tests.dll
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14 - AccessHarvester.Integration.dll
Passed!  - Failed:     0, Passed:    70, Skipped:     0, Total:    70 - AccessHarvester.SymbolDb.Tests.dll
Passed!  - Failed:     0, Passed:   328, Skipped:     2, Total:   330 - AccessHarvester.Tests.dll
Skipped! - Failed:     0, Passed:     0, Skipped:     1, Total:     1 - AccessHarvester.Comprehension.Tests.dll
```

The warning profile was unchanged apart from the new test project
participating in the solution build.
