# Verification Round 1

1. **Spec fidelity for Finding #4 (`--clean`).**  
The core implementation is directionally correct and mostly faithful to the spec. `Extract`, `Translate`, and `Enrich` now each delete their own target tier before recreating it when `clean: true`, and `Run` threads that same flag through all three phases. Concretely:

- `Extract(...)` deletes `<base>/raw/` before export.
- `Translate(...)` deletes `<base>/kb/` before translation.
- `Enrich(...)` deletes `<base>/ir/` before enrichment.

That is the right place to enforce stale-file cleanup, because it removes the entire tier’s prior contents rather than trying to infer per-object deletions after the fact. The default of `clean = true` also matches the session’s stated final behavior: clean-by-default, opt out via `--no-clean`.

The CLI wiring is also correct in the narrow sense: `--no-clean` is parsed as a flag, `HasNoClean(opts)` flips the effective `clean` boolean, and the effective mode is surfaced back in the status envelope as `Clean`.

The stale orphan semantics exercised by the new tests are correct for the path they actually cover. The two hermetic `Enrich(...)` tests prove the key invariant:

- with `clean: true`, deleting `kb/TestCategory/frmOld.json` between runs removes `ir/TestCategory/frmOld.ir.json`;
- with `clean: false`, the old IR file remains.

That directly closes the original “orphan IR survives rerun” concern at the IR tier.

Two caveats remain:

- The tests prove the IR-side invariant, but they do **not** directly exercise stale-file removal for `raw/` and `kb/` or an end-to-end `Run(...)` rerun after a source deletion. The code for `Extract` and `Translate` is simple and symmetric, so I do not think this is a functional bug, but the coverage is narrower than the prose spec implied.
- Default-clean is intentionally destructive for existing outputs. That is the intended new contract, but it becomes especially significant when combined with `--types` filtering; I cover that under item 6.

2. **Spec fidelity for Finding #6 (status-envelope provenance).**  
Functionally, the new envelope fields are present where they should be:

- `RunExtract` emits `HarvesterVersion`, `StartedAt`, `DurationMs`, and `Clean`.
- `RunTranslate` emits the same.
- `RunRun` emits the same.

`StartedAt` is serialized as UTC ISO-8601 (`"o"` with invariant culture), and `DurationMs` comes from a `Stopwatch`, which is a reasonable duration form for a machine-readable status envelope.

The new integration test also does the right determinism check for the volatile fields: it parses two `run --json-status` outputs, strips `StartedAt` and `DurationMs`, and compares the remaining JSON for equality. That mirrors the repo’s existing determinism posture around `ExportedAt`.

However, there is one real provenance inconsistency in the implementation:

- `AccessHarvester.Core/Pipeline.cs` resolves manifest `HarvesterVersion` from `typeof(Pipeline).Assembly` and does **not** strip the `+<commit>` SourceLink suffix.
- `AccessHarvester.Cli/Program.cs` resolves status-envelope `HarvesterVersion` from `typeof(DatabaseManifest).Assembly` and **does** strip the `+<commit>` suffix.

So the new status envelope’s `HarvesterVersion` is stable across two runs of the same build, yes — but it is not guaranteed to match the `HarvesterVersion` written into `manifest.json` for that same run. That weakens the “auditable provenance” story the session was supposed to improve. This is the main issue I would fix before calling the session fully done.

3. **Optional cross-DB roll-up index.**  
The optional feature is implemented in a sensible place and in a deterministic way.

What is correct:

- `EmitRootIndex(string outputRoot)` scans `<outputRoot>/AccessMetaData/*/manifest.json`.
- It writes `<outputRoot>/AccessMetaData/index.json`, which is the correct root-level location for a cross-DB roll-up.
- Entries are sorted by the embedded manifest database name (`Name`) rather than directory enumeration order.
- Paths are emitted as forward-slash relative paths like `Northwind/manifest.json`.
- Missing manifests are skipped.
- Unparseable manifests are skipped by catching `JsonException`.
- The method is idempotent and deterministic given the same input tree.
- Calling it from `Run(...)` after manifest emission is the right lifecycle point.

The hermetic tests cover the important behaviors well:

- shape and sort order,
- determinism across two invocations,
- skipping missing/unparseable manifests.

Single-DB and multi-DB cases are handled by the same code path; nothing about the implementation is special-cased for count > 1, which is good.

The location is also correct relative to the golden gate. Because the new file sits at `<outputRoot>/AccessMetaData/index.json`, outside each per-DB `BaseDir`, it does not pollute the existing per-database golden corpus.

One minor hardening note: the implementation only silently skips `JsonException`. A transient `IOException` / file lock while reading a manifest would still bubble and fail the run. That is not a spec violation, but if the intent is “one bad entry should never poison the batch index,” broadening the catch around read+parse would make that truer in practice.

4. **Determinism gate impact.**  
The Session 3 changes do **not** materially alter the files that `CanonicalGoldenRegressionTests` compares.

That gate compares:

- `manifest.json` after `GoldenNormalizer.NormalizeManifest(...)`,
- `unresolved-items.json` after normalization,
- the `kb/` tree verbatim,
- the `ir/` tree with per-file normalization.

The new clean semantics do not change bytes when the output root is fresh, which is how the golden regression test runs. The new root index file is outside the compared paths. And the new CLI status-envelope fields are stdout-only, not part of the golden corpus.

The envelope determinism test also mirrors the repo’s existing normalization philosophy correctly: it strips the intentionally volatile fields (`StartedAt`, `DurationMs`) before comparison, analogous to how `GoldenNormalizer` strips `Database.ExportedAt`.

So from a determinism-gate perspective, Session 3 is sound.

5. **Test coverage adequacy.**  
The new tests are good and targeted.

The 5 hermetic `PipelineCleanTests` cover the key pipeline-level invariants introduced in this session:

- stale IR removal with `clean: true`,
- stale IR preservation with `clean: false`,
- root-index shape,
- root-index sort determinism,
- root-index robustness against missing/unparseable manifests.

That is solid unit-level coverage for the new filesystem mechanics, and it avoids COM, which is exactly right for these concerns.

The new `CliRunTests.Run_JsonStatusEnvelope_CarriesProvenance_AndIsDeterministicMinusVolatileFields` also correctly exercises the public contract end-to-end:

- out-of-process CLI invocation,
- real `run --json-status`,
- presence/type assertions for provenance fields,
- determinism check after stripping volatile fields.

Keeping that method inside the existing `CliRunTests` class is also the right call. Given the repo’s documented Access COM contention history, preserving xUnit’s class-level sequential behavior for the HomeHealthCare-backed CLI tests is prudent.

My only coverage critique is that there is still no direct test for:

- CLI `--no-clean` flipping `Clean` to `false`, or
- end-to-end stale cleanup through a full `Run(...)` rerun.

Those are not blockers, but they would tighten confidence.

6. **Backward compatibility / silent behaviour changes.**  
This session intentionally changes behavior, and that change is only partly “safe.”

For the mainstream case — full `run` into a batch output root — default-clean is the right new default. It fixes the stale-artifact problem and makes repeated batch execution safer.

For any existing script that relied on the old merge-overwrite behavior, though, this is a silent semantic change. The command still succeeds, but prior outputs are now deleted tier-by-tier unless the caller adds `--no-clean`. So:

- yes, old workflows can change behavior without a hard failure;
- yes, `--no-clean` is the intended escape hatch;
- but no, that does not make the change entirely “silent-safe.”

The sharpest edge is `--types`. With the new default-clean behavior, a partial-category run like `run --types Forms` against an existing output tree will clear `raw/`, `kb/`, and `ir/` and then repopulate only Forms-related content. Any previously harvested unselected categories disappear unless the caller uses `--no-clean`. That is consistent with the new implementation, but it is a materially different outcome from the legacy merge-overwrite mode and deserves more explicit documentation than it currently has.

So I would describe backward compatibility as: **acceptable if intentional, but not transparent**.

7. **Anything else.**  
Overall, the session is strong: the stale-file mechanics are implemented sensibly, the optional root index is well placed and deterministic, the golden gate stays clean, and the testing strategy is thoughtful.

The two things I would still fix or at least tighten before finalizing are:

- **Unify HarvesterVersion provenance resolution.** Right now status and manifest can disagree for the same run because they pull from different assemblies and apply different normalization. That is the most concrete correctness issue.
- **Call out the `--types` + default-clean interaction more explicitly.** Even if you keep the behavior exactly as implemented, existing incremental scripts deserve a clearer warning.

A smaller, optional hardening improvement would be to let `EmitRootIndex` skip read failures (`IOException`, maybe `UnauthorizedAccessException`) as well as parse failures, since the method’s comments frame it as best-effort aggregation.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Pipeline.cs; AccessHarvester.Cli/Program.cs",
      "summary": "The new status-envelope HarvesterVersion is resolved from a different assembly and with different normalization than manifest.json, so the two provenance surfaces can disagree for the same run.",
      "fix": "Centralize version resolution in one shared helper and use the same authoritative assembly and string normalization for manifest emission, --json-status, and --version."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Cli/Program.cs",
      "summary": "Default-clean combined with --types now deletes previously harvested unselected categories, which is a silent behavior change that the current help text does not call out explicitly.",
      "fix": "Document the --types plus default-clean interaction directly in usage/help text and consider an explicit warning or confirmation path when running --types against a non-empty output tree without --no-clean."
    }
  ]
}
```

---

# Verification Round 2

1. **R1 Major fix correctness.**

The major finding is correctly closed.

The important part of the original issue was not just “get a version string somewhere,” but “make all provenance surfaces report the same version for the same build.” The new `DatabaseManifest.ResolveHarvesterVersion()` does that cleanly:

- `AccessHarvester.Core/Pipeline.cs` now delegates manifest emission to `DatabaseManifest.ResolveHarvesterVersion()`.
- `AccessHarvester.Cli/Program.cs` now delegates the `--json-status` envelope to the same helper.
- `AccessHarvester.Cli/Program.cs` also uses the same helper for `--version`.

That removes the prior split-brain behavior where manifest emission and CLI status/version were reading from different assemblies and normalizing differently.

The normalization itself is also correct and deterministic. The helper:

1. reads `AssemblyInformationalVersionAttribute.InformationalVersion`,
2. falls back to `AssemblyName.Version`,
3. falls back again to `"0.0.0"`,
4. then strips everything from the first `+` onward.

That gives byte-stable behavior in both relevant cases:

- If the informational version is `1.2.3`, the result stays `1.2.3`.
- If it is `1.2.3+abcdef`, the result becomes `1.2.3`.

Using `IndexOf('+')` is fine here. It is deterministic, and `+` is exactly the SemVer build-metadata separator that SourceLink-decorated versions use.

The assembly choice is also reasonable. Since the field being populated is `DatabaseManifest.HarvesterVersion`, sourcing the canonical value from `typeof(DatabaseManifest).Assembly` is a defensible authority: the manifest model owns the field, and all other surfaces are now explicitly aligned to it. That is better than the old arrangement, where the manifest and CLI were free to drift.

There is one subtle architectural tradeoff worth acknowledging, but I do **not** consider it a remaining issue: if `AccessHarvester.Core`, `AccessHarvester.Cli`, and the manifest assembly were ever intentionally stamped with different informational versions, this fix now chooses the manifest assembly as the single source of truth. For this repo and this session, that is the right choice, because the session goal was provenance consistency, not per-assembly version introspection.

2. **Manifest content change risk.**

The manifest bytes do change: `HarvesterVersion` now emits the normalized semver without the `+<commit>` suffix. In this repo, that looks safe.

On the determinism gate specifically, this is already absorbed correctly. You explicitly note that `CanonicalGoldenRegressionTests` continue to pass because `GoldenNormalizer` replaces `HarvesterVersion` before byte comparison. That is exactly the right normalization boundary: the field is provenance, not content the golden corpus should pin byte-for-byte.

I also do not see any in-repo consumer, from the code shown, that depends on the `+commit` suffix being present. The visible usages treat `HarvesterVersion` as an opaque string:

- schema validation only cares that it is a string,
- CLI status tests only check presence/non-empty,
- `--version` output is human-facing and already covered by bootstrap tests,
- nothing shown parses build metadata out of the field.

So within the codebase presented, I see no evidence of a downstream parser, validator, or telemetry surface that requires the suffix.

The only residual risk would be external, out-of-repo consumers that had informally started scraping build metadata from `HarvesterVersion`. That would be a compatibility consideration, but not a correctness problem in this session, and it is consistent with the stated intent to normalize away SourceLink build provenance.

3. **R1 Minor fix sufficiency.**

The minor finding is also adequately closed.

The new `IMPORTANT:` paragraph in `PrintUsage()` is clear, specific, and materially better than the prior help text. It now does all the things it needed to do:

- explicitly names the hazardous combination: `--types` with default clean,
- explains the mechanism: the tier directory is deleted before writing,
- explains the consequence: previously harvested unselected categories disappear,
- gives a concrete example: `run --types Forms`,
- gives the right mitigation: add `--no-clean` for incremental partial re-harvests.

That is the right level of operator-facing warning for this behavior change.

The remediation advice is also accurate. With `--no-clean`, the pipeline reverts to the old merge/overwrite behavior, which is exactly what preserves prior categories during a partial-category run. The help text is framed around `run`, which is the sharpest case because it affects all three tiers, but the warning is still directionally correct for the narrower `extract` and `translate` commands as well.

I do not see a lingering documentation gap large enough to keep the original minor issue open.

4. **Newly-introduced concerns.**

I do not see a new issue introduced by the fix.

The main candidate concern is the one you called out: switching from `typeof(Pipeline).Assembly` to `typeof(DatabaseManifest).Assembly` could, in principle, change the reported version if those assemblies were ever stamped differently. That is real in the abstract, but in context I do not think it is material:

- the entire purpose of the fix was to choose one canonical authority,
- the manifest assembly is a sensible authority because the provenance field lives there,
- the repo appears to version these assemblies coherently anyway,
- and the previous state was demonstrably worse because it allowed manifest and CLI surfaces to disagree.

So I would describe this as an intentional consolidation tradeoff, not a new defect.

On the API shape: making this a `public static` method on `DatabaseManifest` is acceptable. A dedicated `VersionInfo` or `HarvesterVersionResolver` helper could be marginally cleaner from a layering standpoint, but the current placement is understandable: the type already owns `SchemaVersionLiteral` and the `HarvesterVersion` field semantics, so colocating the canonical resolver there is not unreasonable. I would not block the session on that stylistic point.

5. **Remaining R1 caveats.**

I still view the three leftover R1 caveats as non-blocking.

**(a) No direct test for Extract/Translate orphan cleanup.**  
Still true, but still not material. The `Extract` and `Translate` implementations are very simple:

- `Extract` deletes `rawDir` when `clean` is true, then recreates it.
- `Translate` deletes `kbDir` when `clean` is true, then recreates it.

There is no tricky branching there. The `Enrich` tests cover the behavioral pattern, and the other two phases are symmetric and simpler.

**(b) `EmitRootIndex` only catches `JsonException`.**  
This remains an optional hardening opportunity, not a correctness issue. If the desired posture is “best effort under any bad entry condition,” broadening the catch around manifest read/parse to include `IOException` and maybe `UnauthorizedAccessException` would make the method more robust. But the current behavior still meets the implemented contract and the tests written for it.

**(c) No direct test that `--no-clean` flips `Clean` to `false` in the envelope.**  
Still a coverage gap, but not a material risk. The wiring is straightforward:

- `FlagOptions` includes `"no-clean"`,
- `HasNoClean(opts)` checks `opts.ContainsKey("no-clean")`,
- `clean = !HasNoClean(opts)`,
- the envelope emits `Clean = clean`.

That is not where I would expect a latent bug after reading the code. A test would be nice, but I would not reopen the session over it.

6. **Anything else.**

I do not see anything else that should block closure.

The corrected code now does the two important things Round 1 asked for:

- it truly unifies provenance version resolution across manifest, status envelope, and `--version`;
- it explicitly documents the `--types` plus default-clean hazard in operator-facing help.

I would classify the remaining opportunities as polish only:

- optional end-to-end test asserting `manifest.HarvesterVersion == envelope.HarvesterVersion == --version`,
- optional CLI test for `--no-clean` yielding `"Clean": false`,
- optional broader exception handling in `EmitRootIndex`.

None of those rise to the level of a fresh finding given the code in front of me.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
