1. **Correctness of the publish pipeline / build-once-promote-artifacts / idempotence.**  
The core publish shape is sound. `resolve-context` derives the intended version, `validate` performs the only `dotnet pack`, uploads the resulting `.nupkg`/`.snupkg`, and `publish-nuget-org` downloads and pushes that exact artifact. That satisfies the material “build once, promote artifacts” requirement within a workflow run. The `--skip-duplicate` flag gives the expected idempotent retry behavior for reruns after a successful push. I do not see a second pack in the publish job, so there is no same-run rebuild drift. The only caveat is that determinism across separate reruns still depends on ordinary .NET build determinism and fixed dependency resolution; that is normal and not a spec miss here.

2. **Irreversibility guardrails: real or paper-only?**  
Most of the guardrails are real, not decorative. The tag/input-to-`Directory.Build.props` equality check is enforced in code; the package-ID typo guard is enforced by checking the produced package filename against the expected `PackageId + Version`; the package is smoke-installed from the packed artifact before publish; and the publish job is downstream of validation and artifact promotion. The GitHub Packages manual path is also a real no-nuget.org exercise path. Two caveats reduce strength but do not, in my view, rise to a blocking defect for the permanent package ID:  
- the smoke test only executes `access-harvester --help`, despite comments implying a broader validation path; that proves installability and basic runtime startup, but not deeper command paths like schema/ANTLR usage;  
- the install uses `--add-source` rather than isolating sources, so it is not a fully hermetic proof against all future source-resolution edge cases.  
Those are weaknesses in depth, not failures of the first-push guardrail model.

3. **Risk #1: single-file/self-extract property leakage into `dotnet pack`.**  
I do not see a material leak path in the shown work. The self-contained publish properties are still gated on `$(RuntimeIdentifier) != ''`, and the workflow’s pack step is RID-neutral and targets the CLI project directly without `--runtime`. That is exactly the right shape to prevent `PublishSingleFile` / self-extract behavior from contaminating the tool package. On the evidence shown, risk #1 is adequately addressed.

4. **Risk #3: version single-sourcing and tag namespacing.**  
This also looks correct. The version remains single-sourced from `Directory.Build.props`; the CLI project does not set its own `Version`; and the workflow does not override version at pack time. `resolve-context` verifies that the triggering tag or manual input equals the props value, so there is no second versioning scheme hiding in the pipeline. The tag namespace split is also correct: `nuget-v*` for tool publishing versus the existing `v*.*.*` release channel means the two workflows should not double-fire on the same tag.

5. **License correctness: MIT + root LICENSE + ANTLR BSD notice + third-party obligations.**  
The first-party licensing pieces are fine: `PackageLicenseExpression` is `MIT`, the repo-root `LICENSE` is MIT text, and the ANTLR BSD-3-Clause notice is reproduced in `THIRD-PARTY-NOTICES.txt`, which is packed into the package root. That satisfies the specific ANTLR attribution concern.  
However, there is one material problem: the notice file’s own framing says the tool “redistributes” additional runtime components, specifically `System.Data.OleDb` and `JsonSchema.Net`, but it does **not** include their MIT copyright/license notices—only an informational mention. If those assemblies are in the shipped tool package/runtime payload, this is incomplete for a public binary redistribution. The current commentary (“their own copyright notices … travel inside their own packages”) is not a safe assumption for the top-level tool package you are publishing. This is the one issue I consider material enough to block the round until corrected or disproven by showing those MIT-licensed components are not actually redistributed in the produced tool package.

6. **OIDC trusted publishing usage (`NuGet/login@v1`, `id-token: write`, environment).**  
Structurally, this is the right pattern. The publish job has `id-token: write`, the login happens immediately before push, and the API key is short-lived and not stored as a long-lived repo secret. If the nuget.org trusted-publishing policy or `user:` value is wrong, the job should fail explicitly rather than silently succeed. I do not see a silent no-op path in the OIDC wiring itself. The environment is useful for audit/protection, but it should be treated as an optional human gate, not the primary technical control.

7. **Honesty of the cross-repo pin.**  
The cross-repo wording is appropriately honest. It says the packaging and pipeline landed upstream, but the package is not yet live on nuget.org pending one-time maintainer setup and the first `nuget-v<semver>` push. It keeps the `<CONFIRM>` placeholder in place for the live-install command. That matches reality and does not overclaim availability.

8. **Broken internal doc links / missing referenced files.**  
From the material shown, the new links and paths line up: `docs/harvester/nuget-publish.md` links correctly to `install.md` and the workflow path, `install.md` links back to `nuget-publish.md`, and the csproj paths for `nuget-readme.md` and `THIRD-PARTY-NOTICES.txt` are consistent with the stated file locations. I do not see an obvious broken internal link in the deliverables provided.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "THIRD-PARTY-NOTICES.txt",
      "summary": "The notice file is incomplete for the tool package's claimed redistributed runtime closure because it reproduces ANTLR's BSD text but only mentions, rather than includes, the MIT notices for System.Data.OleDb and JsonSchema.Net.",
      "fix": "Inspect the actual contents of the produced .nupkg/runtime payload and add the full MIT copyright and license notices for every redistributed MIT-licensed component (at least System.Data.OleDb and JsonSchema.Net if they are bundled), or prove those components are not redistributed and revise the notice file/comments accordingly."
    }
  ]
}
```