Concern 1 — SDK vs runtime distinction in `install.md`

The original defect was that the docs implied a runtime-only machine could perform `dotnet tool install`, which is false because the `dotnet tool` command is part of the SDK. In the revised `install.md`, that distinction is now stated correctly in all of the places called out:

- In the channel-comparison table, the tool row explicitly says:
  - install/update requires the **.NET 10 SDK**
  - running an already-installed tool requires only the **.NET 10 runtime**
- In Option A’s “.NET prerequisites” block, the same split is repeated clearly and unambiguously.
- In the shared prerequisite paragraph, the Windows+Access requirement is correctly described as a subcommand/runtime-behavior constraint shared by both channels, not as a replacement for the SDK install prerequisite.

I looked specifically for a remaining harm path where a runtime-only user could still be misled into attempting `dotnet tool install`. I do not see a material one in the revised text. The table and the dedicated prerequisite block are both explicit, and they say the right thing. The shared paragraph is about host capability for COM-based versus hermetic subcommands, which is a separate axis from “what is needed to perform installation.” So the Round 1 fix is correct and complete on the point that mattered: runtime-only is no longer presented as sufficient to install the tool.

Concern 2 — packaged README now tool-specific and command-accurate

The second Round 1 defect was that the package README was the repository overview README, which used `AccessHarvester` executable examples instead of the installed tool command `access-harvester`. The new wiring fixes that cleanly:

- `AccessHarvester.Cli.csproj` now sets:
  - `<PackageReadmeFile>nuget-readme.md</PackageReadmeFile>`
- It also packs the new file from `..\docs\harvester\nuget-readme.md` into the package root:
  - `<None Include="..\docs\harvester\nuget-readme.md" Pack="true" PackagePath="\" ... />`

That is the right pattern: `PackageReadmeFile` names the file as it exists inside the package, and the packed item places that file at the package root with the expected name. The empirical verification confirms the nuspec contains `<readme>nuget-readme.md</readme>`, so the csproj wiring is not just theoretically correct; it produced the correct package metadata.

On content, the new `nuget-readme.md` is tool-specific and uses `access-harvester` throughout. The supplied empirical result that it contains 12 `access-harvester` occurrences and zero stale `AccessHarvester extract` command lines is exactly the evidence I would want here. Since the root `README.md` is no longer the package README, its continued use of `AccessHarvester` examples for source-build / self-contained contexts is no longer a packaging defect.

RID-gating / self-contained publish isolation

This was an important regression risk: adding dotnet-tool packaging must not accidentally turn the NuGet tool into a single-file/self-contained artifact, and the existing GitHub Releases self-contained publish must continue to work.

The revised csproj handles this by moving the single-file/self-extract properties under:

- `<PropertyGroup Condition="'$(RuntimeIdentifier)' != ''">`

That means ordinary RID-neutral `dotnet build` and `dotnet pack` do not see those settings. Empirically, that is exactly what happened:

- `dotnet pack` produced a normal dotnet-tool layout under `tools/net10.0/any/`
- `DotnetToolSettings.xml` is present
- the package is not single-file
- self-contained publish still emits the one-file exe with no loose managed DLLs

That closes the real risk. I checked for a concrete path where the new condition would still contaminate normal pack/build behavior, and I do not see one in the supported flows described here. The fix is sufficient for the session requirement that build/pack remain unaffected while self-contained publish stays intact.

Version inheritance

The session required the tool package version to stay aligned with the existing solution-wide version source rather than introducing a second version scheme. The csproj does not add a local `<Version>` or `<PackageVersion>` override, and the comment explicitly documents inheritance from `Directory.Build.props`.

The empirical result also lines up with that design:

- installed tool reports `1.2.0`
- the docs’ version examples use `1.2.0`
- the channel-alignment text says the tool version maps 1:1 to the GitHub Release tag

So version inheritance remains correct, and I do not see a new divergence introduced by the packaging changes.

`PackageReadmeFile` wiring re-check

Beyond the high-level README fix, the low-level wiring is correct:

- package readme logical name: `nuget-readme.md`
- packed file name at package root: also `nuget-readme.md`
- nuspec readme entry confirms that exact filename
- the source file is outside the project directory, but that is fine because it is explicitly included for packing

I looked for the common failure modes here:
- property points to the wrong filename
- file gets packed under a subfolder instead of the root
- README content is correct but not actually the rendered NuGet README

None of those are present.

New-defect scan

I specifically checked for defects that the fixes might have introduced:

- wrong command name still appearing in the package README: not found
- single-file publish properties leaking into the dotnet tool package: not found
- accidental local package version override: not found
- README wiring mismatch between property and packed file path: not found
- docs drifting away from actual tool behavior: not found; the schema-version wording was also corrected and matches the verified `--version` / `validate` behavior

Net: both Round 1 majors appear fully fixed, and I do not find a new material defect introduced by the changes under review.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```