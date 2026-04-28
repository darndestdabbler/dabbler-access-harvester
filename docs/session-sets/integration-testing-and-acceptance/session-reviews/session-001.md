# Verification Round 1

### 1. End-to-end coverage vs duplication

**Issue:** Mostly no blocking issue; the new integration project adds genuinely new coverage, but one of its key assertions is weaker than the stated contract.  
**Location:** `tests/AccessHarvester.Integration/` vs existing `AccessHarvester.Tests` and `AccessHarvester.Pipeline.Tests`.  
**Fix:** Keep the project and test split as-is, but tighten the full-pipeline assertion so it fails if the requested category output is missing.

The project is not just re-wrapping existing unit tests. The existing suites are largely in-process and component-oriented: parser/resolver/enrichment tests exercise internal APIs, `SyntheticDbIntegrationTests` focuses on extractor correctness against synthetic databases, and `EndToEndFixtureTests` covers the VBA-side pipeline over anchor fixtures. The new project adds three boundaries that were previously missing: black-box CLI invocation, schema/example contract validation at the consumer boundary, and a real `.accdb` run under a dedicated integration test project.

That means the direction is correct for Feature 1. The only caveat is that the one true full-pipeline test currently proves less than its name and comments claim, because it does not fail when the `Forms` IR directory is absent. So the project itself is additive and appropriate, but one assertion needs strengthening before I would call the full-pipeline structural gate complete.

### 2. Black-box CLI via `Process.Start`

**Issue:** The boundary choice is correct; one test case is just looser than its name implies.  
**Location:** `tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs` and `Cli/CliBootstrapTests.cs`.  
**Fix:** Continue using `Process.Start` for CLI coverage; tighten or rename the unknown-command test so it unambiguously verifies dispatch behavior.

Using `Process.Start` against the built CLI is the right testing boundary here. It validates the actual executable surface: apphost resolution, argument parsing, exit codes, stdout/stderr behavior, and packaging assumptions. That is exactly what an integration test should cover and exactly what in-process `Pipeline.Default()` calls cannot cover. The comment about xUnit’s console capture is also credible: in-process console assertions are often unreliable under test runners, so a child process is the stable boundary.

The implementation is broadly sound. It redirects both streams, drains them concurrently, enforces a timeout, and avoids taking a managed reference on the CLI assembly, which also sidesteps cross-platform analyzer noise. The only weakness is not the process boundary itself but the `UnknownSubcommand_PrintsError_And_ReturnsExitOne` case: it permits either true unknown-subcommand handling or bare-file parse failure, so it does not really pin dispatch semantics. That is a test-specific looseness, not a design problem with `Process.Start`.

### 3. `SkippableFact` and COM-dependent gating

**Issue:** Correct for the two cases you asked about; not a full “Access is definitely automatable” probe.  
**Location:** `tests/AccessHarvester.Integration/TestSupport/AccessAvailability.cs` and `Pipeline/EndToEndPipelineTests.cs`.  
**Fix:** No change is required for the stated requirement; optionally instantiate Access or add a stronger preflight if you want to skip registered-but-unusable installs too.

For the specific question, the skip logic is correct. On non-Windows hosts, `Skip.IfNot(OperatingSystem.IsWindows(), ...)` fires before the Windows-only COM probe, so the test cleanly skips without touching unsupported APIs. On Windows hosts without Access installed, `Type.GetTypeFromProgID("Access.Application", throwOnError: false)` returning `null` correctly causes a skip. That matches the spec’s “skip cleanly on non-Windows CI stages” requirement.

What it does not prove is that Access is fully launchable and usable in the current environment. You can have edge cases where the ProgID exists but automation still fails later due to licensing, profile, bitness, or desktop interaction constraints. That is outside the narrow question you asked, but it is worth calling out. If those failures start showing up in CI, the skip helper should escalate from “is registered” to “can be created and torn down.”

### 4. Fixture-only schema conformance vs authoring a manifest aggregator

**Issue:** Deferring the manifest aggregator is the right Session 1 call, but Gap A remains a real unresolved gap.  
**Location:** `tests/AccessHarvester.Integration/Pipeline/SchemaConformanceTests.cs` and the documented Gap A in the coverage audit.  
**Fix:** Treat this as explicitly deferred work for Session 2/4: either add a manifest-emitter/aggregator later, or mark the acceptance criterion waived-with-rationale if v1 intentionally ships without it.

Session 1 is supposed to consolidate coverage and land integration tests, not silently introduce a new production output layer. A manifest aggregator would be new product behavior, not just test plumbing. Given that the current pipeline emits per-object IR rather than a rolled-up `DatabaseManifest`, it is reasonable that Session 1 stops at validating the committed example manifests against `harvester-schema.v1.0.json`. That at least keeps the schema, examples, and downstream doc-generation contract honest.

What would be wrong is pretending that this closes the “actual pipeline output validates against schema” gap. It does not. Gap A is genuine, and the current disposition correctly says so. So the Session 1 decision is sound, but Session 4 cannot honestly mark the schema-valid-output criterion as “met” unless a manifest-producing step exists by then. This is a defer-with-visibility situation, not a fix-in-Session-1 situation.

### 5. Real correctness issues in the tests

**Issue:** Yes—there is one major assertion hole and at least one smaller coverage hole.  
**Location:** `tests/AccessHarvester.Integration/Pipeline/EndToEndPipelineTests.cs` and `Pipeline/SchemaConformanceTests.cs`.  
**Fix:** Make the pipeline test fail when `ir/Forms` is absent, and enumerate all `examples/*.json` files rather than hardcoding three fixture names.

The major issue is in `Pipeline_RunAgainstMiniComplex_EmitsRawKbIrTreesForFormsCategory()`. The test claims to prove that the pipeline emitted Forms IR output, but it only checks for non-empty `*.ir.json` files if `ir/Forms` already exists. If the pipeline regresses and stops creating the `Forms` directory altogether, the `if (Directory.Exists(formsIrDir))` branch is skipped and the test still passes. That is exactly the kind of silent pass on broken behavior you asked about. Given the test’s purpose, the existence of `ir/Forms` should be mandatory, not optional.

The smaller issue is in `SchemaConformanceTests`. The prose says it validates each committed example manifest, but the test hardcodes three filenames. That is fine only as long as the directory never grows. As soon as a fourth example is committed, the test will silently stop matching its own stated intent. I would also call out the CLI unknown-command test as looser than ideal, but the missing `Forms` directory assertion is the one materially affecting end-to-end confidence.

### 6. `ai-assignment.md` completeness and well-formedness

**Issue:** Unverifiable from the supplied evidence.  
**Location:** `ai-assignment.md`.  
**Fix:** Include the file in the review set; stale model IDs are non-blocking, but completeness and structure cannot be signed off without the actual content.

I cannot positively verify concern 6 from what you provided because `ai-assignment.md` itself is not included among the session artifacts here. The note about stale `claude-3-opus` model IDs being a routed-analysis artifact is plausible and non-blocking, and I would not flag that by itself. But “complete and well-formed” depends on content I cannot inspect: objective framing, prerequisite references, routed tasks, explicit deliverables, acceptance gates, and session handoff details.

So the right conclusion is not “bad” and not “good”; it is “not reviewable from current evidence.” If this verification is meant to sign off all seven concerns, the file needs to be part of the material under review.

### 7. Session 2–5 elaborations in `ai-assignment.md`

**Issue:** Conceptually reasonable from the stub, but not verifiable as actually elaborated.  
**Location:** `ai-assignment.md` Session 2–5 sections.  
**Fix:** Include the elaborated steps and ensure each session has concrete inputs, outputs, gating checks, and fallback decisions.

At the level of the stub, the sequence is sensible: Session 2 expands the corpus and snapshots outputs; Session 3 records benchmark baselines and wires regression thresholds; Session 4 performs the evidence-backed acceptance synthesis; Session 5 handles verifier cleanup and the release change-log. That is a coherent progression and a reasonable starting point.

What I cannot verify is whether `ai-assignment.md` actually elaborated those into actionable step lists. A good elaboration would specify, for example, how sample approval is checked, how snapshot updates are reviewed, how benchmark reproducibility is enforced, what evidence is required for each acceptance criterion, and what happens if Gap A remains unresolved. Without the file, I can say the intended direction is sound, but I cannot approve the actual elaboration.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "tests/AccessHarvester.Integration/Pipeline/EndToEndPipelineTests.cs::Pipeline_RunAgainstMiniComplex_EmitsRawKbIrTreesForFormsCategory",
      "summary": "The test silently passes when the pipeline fails to create the requested `ir/Forms` directory because the non-empty IR assertion is guarded by an optional existence check.",
      "fix": "Replace the conditional with an explicit `Assert.True(Directory.Exists(formsIrDir), ...)` and then assert that at least one `*.ir.json` file exists in that directory."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Integration/Pipeline/SchemaConformanceTests.cs",
      "summary": "The test hardcodes three example manifests even though its stated contract is to validate every committed `examples/*.json` fixture against the schema.",
      "fix": "Discover `*.json` files under `RepoPaths.Examples()` dynamically for theory data so newly added example manifests cannot bypass schema validation."
    },
    {
      "severity": "Minor",
      "location": "ai-assignment.md",
      "summary": "The file was not included in the review materials, so its completeness and the Session 2–5 elaborations could not be verified.",
      "fix": "Include `ai-assignment.md` in the session artifacts and ensure it explicitly captures routing, inputs, outputs, dependencies, and detailed Session 2–5 step lists."
    }
  ]
}
```

---

# Verification Round 2

1. **EndToEndPipelineTests fix**

   This resolves the round-1 **Major** correctly.

   The original defect was that the test only asserted on `*.ir.json` files **if** `ir/Forms` already existed, which meant a complete loss of the requested category directory could still pass. The new version removes that silent-pass path by making both conditions mandatory:

   - `Assert.True(Directory.Exists(formsIrDir), ...)`
   - `Assert.True(irFiles.Any(), ...)`

   That is exactly the contract the round-1 review asked for. If the pipeline regresses in either of the two meaningful ways—dropping the `Forms` directory entirely or creating it but producing no IR files—the test now fails loudly and for the right reason.

   The change also does not distort the test’s intent. It still remains a structural end-to-end gate rather than becoming a fixture-content assertion: it checks pipeline outcome success, expected tier directories, and non-empty category output, but does not overfit to exact form names or counts. That is the correct boundary for this integration test.

2. **SchemaConformanceTests dynamic-discovery fix**

   This resolves the round-1 **Minor** correctly.

   The hardcoded three-file list was the weakness; the new `ExampleFixtures` property now discovers all `*.json` files under `RepoPaths.Examples()` and feeds them into the theory. That means newly committed example manifests can no longer bypass schema validation simply because someone forgot to update test data.

   The implementation is sound for the stated contract from round 1:

   - it enumerates from disk rather than hardcoding names,
   - it uses stable alphabetical ordering,
   - it passes only the filename into the theory and reconstructs the path in the test body,
   - it still asserts both fixture and schema presence before validating.

   The scope also matches the original verifier wording: round 1 called for covering `examples/*.json`, and this implementation does exactly that at the top level. There is no regression introduced here.

3. **ai-assignment.md completeness and Session 2–5 elaboration**

   The round-1 **Minor** here was fundamentally a reviewability problem: the file was missing from the materials. That problem is resolved.

   With the file present, it is reviewable and it is structurally well-formed. Each session includes:

   - a recommended orchestrator,
   - rationale,
   - estimated routed cost,
   - a step table with routing decisions,
   - an “Actuals” section,
   - a next-session recommendation.

   Sessions 2–5 are materially elaborated rather than left as placeholders. They describe concrete activities, expected outputs, and verification points with enough specificity to be actionable. I do not see a blocking completeness problem in the sense raised by round 1. The stale model IDs remain non-blocking per your instruction.

4. **Regressions or new issues introduced by the fixes**

   None are evident in the submitted changes.

   - The `EndToEndPipelineTests` change is a pure tightening of an existing assertion path.
   - The `SchemaConformanceTests` change broadens fixture coverage without weakening validation.
   - The markdown inclusion enables review and does not affect runtime behavior.

   The reported green integration build/test run is consistent with that conclusion. Based on the provided evidence, the round-1 Major and Minors have been resolved, and I do not see a new defect introduced by these fixes.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
