# Verification Round 1

1. **Correctness of the scoped hard-exit.**  
`FinalizeExit` is correctly wired as the single exit funnel from `Program.Main`: every normal return and both top-level catch paths now produce an `exitCode`, then pass through `FinalizeExit`. The guard itself is also correctly scoped to actual Access COM activation, not to command names. `AccessComSession._activatedInThisProcess` only flips to `true` after `Activator.CreateInstance(type)` succeeds and `Application` is assigned, which is the right moment: if Access COM was never activated, the process should take the normal managed return path; if Access COM was activated and a later step fails, the process still needs the hard exit to avoid the shutdown wedge.

`TerminateProcess(GetCurrentProcess(), unchecked((uint)exitCode))` is the right Win32 call pattern here. `GetCurrentProcess()` returns the standard pseudo-handle for the current process, and `TerminateProcess` accepts that. For the stated exit-code domain here (`0`, `1`, `2`, `3`), the `uint` cast is exact and preserves the intended code. This matches the Session 1 probe result that `Environment.Exit(rc)` still wedged while `TerminateProcess` exited cleanly.

On buffered output loss: for this diff, the claim is sound. `Console.Out` and `Console.Error` are the only obvious managed buffered sinks on the CLI surface, and both are explicitly flushed before termination. Output artifacts written by the command before `Dispatch` returns are already closed by the time `FinalizeExit` runs; synchronous file writes do not depend on process finalizers to become visible. The key risk with `TerminateProcess` is skipped finalizers / teardown logic, but that is the intentional tradeoff here, and nothing in the shown change suggests another buffered sink that would be silently lost. If future code adds background logging, async file sinks, telemetry, or deferred writers, that assumption would need revisiting; for the code under review, I do not see a correctness gap.

2. **Blast radius vs. the human's "scoped" requirement.**  
The COM-activation-flag approach is a faithful realization of the requested scope, and in practice it is better than a subcommand-name allowlist. An allowlist hard-codes policy to names; this implementation ties policy to the actual condition that causes the hang: Access COM activation in the current process. That means it correctly covers:
- commands that do activate Access COM, even if they are refactored or indirectly reached through another entry path;
- commands that fail before activation, which should not hard-exit;
- usage errors and non-COM commands, which stay on the normal managed path.

That is narrower and more robust than “if subcommand name is X/Y/Z then kill process”.

On the command set you called out: this approach should hard-exit for any command that actually goes through `AccessComSession` (`extract`, `run`, `classify`, `report-baseline`, `identify-usage`, `identify-cleanup`, assuming those activate `Access.Application` through that wrapper). It should not hard-exit for `parse`, `validate`, `translate`, `generate-docs`, or argument/usage failures, because they do not activate Access COM. That matches the requirement.

`sanitize` remaining outside the guard is acceptable. The established root cause is not “any database-related code” or “any Windows component”; it is specifically the post-work shutdown wedge after Access COM interop. If `sanitize` uses pure OLE DB and does not instantiate `Access.Application`, excluding it is consistent with the evidence and with the “scoped” constraint. If `sanitize` has its own intermittent reliability issue, that is separate from this persistent classify timeout and should not expand blast radius here.

The only theoretical gap would be a hidden `Access.Application` activation path elsewhere in the codebase that does not go through `AccessComSession`. Based on the diff and the reported full COM-tier integration pass, there is no evidence of such a missed path. So on the repo state described, the scoping looks correct.

3. **E5 HRESULT fold-in correctness.**  
Adding `0x800A0CC6` to `DaoPropertyAbsentHResults` is correct for the scenario described. `0x800A0CC6` is DAO error 3270, “Property not found”, and in the context of probing optional DAO field properties, that is semantically the same class of condition as the existing swallowed codes: the field exists, but the requested property does not. Treating that as “property absent” rather than as a schema-read degradation is the right behavior.

Routing both `catch (COMException ex) when (...)` filters through `IsDaoPropertyAbsentHResult` is also a good cleanup. It removes duplication and ensures the bool-property and string-property helpers cannot drift apart in their accepted HRESULT set. The exposure via `InternalsVisibleTo` is appropriately narrow and justified by the need for a hermetic unit test.

I do not see this change as meaningfully masking genuine reader faults. The masking happens only inside the narrow per-property probes, not around broader DAO operations. The false-positive risk would be “a real fault also returns 3270 in a way we would want to surface”, but for these helpers, the contract is already “swallow only absent-property signals and bubble everything else”. Given the captured empirical evidence that all 93 degradations were exactly this HRESULT, folding it in is the right correction.

The hermetic test is adequate for what it is testing:
- it asserts all three accepted absent-property HRESULTs return `true`;
- it asserts several non-absent cases return `false`;
- it tests the exact pure predicate used by both filters.

A live COM regression test for this specific HRESULT would be stronger in the abstract, but not necessary here because the production behavior is just those catch filters delegating to the predicate, and the orchestrator already reports empirical end-to-end confirmation that the HomeHealthCare run went from 93 degradations / exit 3 to 0 degradations / exit 0. That combination is sufficient.

4. **Test isolation safety.**  
The reasoning is correct. The static flag by itself is harmless; the only destructive behavior is in `Program.Main` via `FinalizeExit`. In-process tests that use library code and happen to activate `AccessComSession` inside the test host process will only set the static latch. They will not terminate the host unless that same process subsequently calls the CLI entrypoint path.

That distinction matters here:
- direct command/object invocation in a test host: safe;
- spawning `AccessHarvester.Cli.exe`: safe, because the flag lives in the child process, not the test host;
- calling `Program.Main` in-process after COM activation: unsafe, because it would route through `FinalizeExit` and kill the host.

From your description, the repo’s in-process paths do not call `Program.Main`; they drive command logic directly. `FinalizeExit` is private, so there is no alternate easy path to invoke it. On that basis, current test isolation is safe.

The one caveat is maintainability: the per-process latch is intentionally sticky, so any future test or tool that embeds the CLI assembly and calls `Program.Main` in-process after using `AccessComSession` could self-terminate. That is a future misuse hazard, not a defect in this remediation. For the current test architecture you described, I do not see an isolation bug.

5. **Gaps / overclaims / scope adherence.**  
I do not see a material deviation from the locked remediation path. The hard-exit was implemented as a scoped guard tied to actual Access COM activation, not as an unconditional `Main` kill-switch, and the quarantine fallback was not taken because the product fix appears to work. The E5 collateral fix was folded in as requested.

I also do not see a change to classify’s output contract, `classification-schema.json`, or sampled-statistics semantics. The classify command body and sampler were not changed. The process-behavior change is at exit time after work completes, and the DAO HRESULT change removes spurious degradations caused by misclassifying “property absent” as read errors. That is a correctness fix to error classification, not a schema or statistics contract change.

The only slight overclaim I would temper is wording like “exactly the COM-activating command paths” if taken universally; that is true so long as all `Access.Application` activation is centralized in `AccessComSession`. The evidence you provided, especially the full COM-tier integration pass and lack of repo changes elsewhere, supports that assumption well enough for verification. I do not think it rises to an issue.

On testing, the mix is appropriate:
- build clean;
- hermetic unit test for the new HRESULT predicate;
- direct classify run proving the timeout is gone and exit code is correct;
- full COM integration tier pass showing no widened fallout and no zombies.

A direct automated assertion of “non-COM subcommands still use normal managed exit path” could be nice-to-have, but it is not required to accept this remediation, and nothing in the diff suggests damage there.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
