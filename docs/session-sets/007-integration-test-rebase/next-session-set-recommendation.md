# Next Session Set Recommendation (post-007)

### 1. Next Set Recommendation

Trigger the authored set `008-manifest-vba-ingestion-and-parser-hardening`. It directly addresses F-3-1, a critical-severity defect causing silent, total data loss for all form and report VBA code-behind modules. Fixing this data integrity failure is the highest value work available and unblocks the dependent P3 and P4 sets. The set is fully authored and ready for execution.

### 2. Parallel Work and Future Authoring

We should author and trigger P1b `output-encoding-correctness` (1 session) to run in parallel with 008. It is fully independent and addresses a separate data corruption class (cp1252 mojibake), accelerating value delivery. The scoped investigation set for the quarantined `sanitize` test host wedge should remain low priority; it blocks no work and the CLI is verified healthy.

### 3. Orchestrator Recommendation (008 Session 1)

**Orchestrator:** Claude Code (claude-opus-4-7)
**Rationale:** The task requires precise modification of the core `Pipeline.cs` logic and careful orchestration of COM-tier golden regeneration. Claude's strong context handling and proficiency with complex C# refactoring make it ideal, and this choice maintains the current cross-provider verification setup (gpt-5-4 verifier).

### 4. Hard constraints (verbatim)

- NEVER regenerate, recreate, or commit examples/northwind-output.json or any NorthwindDevEdition-derived artifact - regeneration reintroduces the residue that session sets 003-005 removed. NorthwindDevEdition is proprietary and local-only. All fixtures and goldens derive from committed HomeHealthCare artifacts only.
- Golden regeneration is HomeHealthCare-only (samples/GoldenOutput/HomeHealthCare/).
- COM-tier test runs are deliberate, sequential, foreground, with a TRX logger and the TRX `<Counters>` as the completion signal; never background a dotnet invocation; sweep spurious samples/*.accdb diffs (git checkout -- samples/*.accdb) before staging.
- The quarantined test SanitizeCommandTests.HomeHealthCare_sanitize_... stays quarantined (see docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md); re-enabling belongs to a future scoped investigation set (the wedge only reproduces under a dotnet-test testhost parent; the CLI itself is healthy - 0.55s exit 0 when spawned directly).
- Do not weaken test assertions to force green.