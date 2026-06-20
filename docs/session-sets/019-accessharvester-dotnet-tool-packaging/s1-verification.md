1. **RID-gating correctness**

The move from an unconditional property group to:

```xml
<PropertyGroup Condition="'$(RuntimeIdentifier)' != ''">
```

is sufficient for the exact Session 1 paths you proved:

- `dotnet build` of the solution, with no `-r`, stays RID-neutral.
- `dotnet pack` of the CLI project, with no `-r`, stays RID-neutral.
- `tools/publish/publish.ps1` passes `--runtime win-x64 --self-contained true`, so `$(RuntimeIdentifier)` is definitely populated for the self-contained release publish.

So for the validated commands, the intended separation holds: normal build/pack stays framework-dependent, and the release publish still gets single-file/self-extract behavior.

Two nuances matter:

- This is **RID-scoped**, not literally **publish-scoped**. If someone later runs `dotnet build -r win-x64`, `dotnet pack -r win-x64`, or injects `RuntimeIdentifier` from elsewhere, the condition will evaluate true. That does **not** create a demonstrated Session 1 failure here, because the properties you gated are publish-oriented properties and your empirical pack result shows the tool package came out correctly in the supported RID-neutral pack path. But the comment saying they activate “ONLY during the self-contained publish” is stricter than the actual condition.
- A RID-less self-contained publish is not a silent failure mode here. In current SDK behavior, self-contained/single-file publishing fundamentally needs a runtime identifier. Since `publish.ps1` explicitly supplies `--runtime win-x64`, your existing release path is covered. If some future ad hoc publish omitted RID, that would fail or diverge obviously rather than silently producing the intended artifact.

Bottom line: **no code defect here for Session 1**. The gating is correct for the proven build/pack/publish flows, though the explanatory comment slightly overstates how narrow the condition is.

2. **Version inheritance**

Your reasoning is correct.

In SDK-style projects:

- `PackageVersion` defaults to `Version` if `PackageVersion` is not set.
- If `Version` is supplied from `Directory.Build.props`, the project inherits it unless overridden locally.

You did not add any second version property in the CLI csproj, and your empirical result:

- `Dabbler.AccessHarvester.Tool.1.2.0.nupkg`
- tool `--version` output showing `AccessHarvester 1.2.0 ...`

is exactly what I would expect from correct inheritance. So there is **no second version scheme** introduced by this diff.

3. **License metadata deferral**

Safe for Session 1.

For local pack/install:

- missing `PackageLicenseExpression` / `PackageLicenseFile` is **not** a pack blocker,
- and it does not prevent install from a local feed.

This is a packaging-quality/publication concern, not a local tool-pack correctness concern. Given Session 2 explicitly owns first-push guardrails, deferring license metadata is acceptable here.

4. **PackageReadmeFile / README pack correctness**

Mechanically, the csproj wiring is correct:

```xml
<PackageReadmeFile>README.md</PackageReadmeFile>
<None Include="..\README.md" Pack="true" PackagePath="\" Visible="false" />
```

That is the standard pattern:

- the package metadata points at `README.md`,
- the file is actually included into the `.nupkg`,
- and it lands at package root where NuGet expects it.

So the **packing mechanics are correct**.

However, there is a real content problem created by pairing the package with this README: the packed README still shows command examples like:

```md
AccessHarvester validate ...
```

while the installed tool command is:

```bash
access-harvester
```

That matters because `PackageReadmeFile` makes this README the package-facing documentation on NuGet. A tool user can install the package successfully and then copy a README example that fails immediately because `AccessHarvester` is not the tool command name.

So on concern (c), the answer is:

- **packaging wiring: correct**
- **packaged README content for the tool channel: not fully correct**

5. **Doc edits: accuracy and internal consistency**

There is a lot here that does check out:

- The distinction between the two channels is conceptually right:
  - `dotnet tool` = framework-dependent
  - GitHub Release zip = self-contained
- The Windows + Access caveat for COM-backed subcommands is correctly preserved across channels.
- The removal of SQLite wording is reasonable and consistent with your stated evidence. Nothing in this diff contradicts the claim that the CLI package closure does not include `Microsoft.Data.Sqlite`.
- The schema-version doc corrections look internally consistent with your empirical runtime output:
  - `--version` reporting `schema v1.1`
  - `validate` handling manifests by declared schema version and supporting both `v1.0` and `v1.1`

I do see one real documentation defect in `install.md`:

The doc repeatedly frames the tool channel as needing a **.NET 10 runtime** on the host. That is incomplete for acquisition. To **install or update** a dotnet tool, the host needs the **.NET SDK** (because `dotnet tool install` / `dotnet tool update` are SDK commands). A runtime-only machine is not enough to follow the installation instructions you added.

Concrete harm path:

- User reads the new table.
- User sees “host already has a .NET 10 runtime” and chooses the tool path.
- User has runtime-only, not SDK.
- `dotnet tool install ...` is unavailable / fails due to missing SDK.

That is within Session 1 scope because this session explicitly asked for the tool installation channel docs and host/runtime caveats.

6. **Any other real defect introduced by this diff within Session 1 scope**

I do **not** see a code-path defect in the actual packaging/installability work:

- tool metadata is sufficient for `dotnet pack`,
- version inheritance is correct,
- README inclusion is technically correct,
- the self-contained publish path remains compatible with the existing script,
- and your empirical pack/install/run proof is consistent with expected .NET tool behavior.

The real issues I found are both documentation-facing:

1. `install.md` understates prerequisites for *installing* the dotnet tool by saying “runtime” where “SDK for install/update; runtime for execution” is the accurate caveat.
2. The README now shipped inside the tool package still uses `AccessHarvester` invocation examples, which is wrong for the packaged tool command `access-harvester`.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/harvester/install.md",
      "summary": "The new dotnet-tool documentation says the host needs only .NET 10 runtime, but `dotnet tool install`/`update` require the .NET SDK, so runtime-only hosts will be sent down a non-working install path.",
      "fix": "Revise the tool-channel prerequisite text to say that installing/updating the tool requires the .NET 10 SDK (or another environment with `dotnet tool` commands available), while running an already-installed tool requires the .NET 10 runtime."
    },
    {
      "severity": "Major",
      "location": "README.md",
      "summary": "The README packed into the NuGet tool package still shows `AccessHarvester ...` command examples even though the installed tool command is `access-harvester`.",
      "fix": "Update the packed README to include dotnet-tool-specific install and invocation examples using `access-harvester`, or create a package-specific README and point `PackageReadmeFile` at that file."
    }
  ]
}
```