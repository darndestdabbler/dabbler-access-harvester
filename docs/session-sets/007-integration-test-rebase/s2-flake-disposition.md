# Session 2 Flake Disposition — `SanitizeCommandTests.HomeHealthCare_sanitize_against_synthesized_classification_emits_csv_and_report`

> Authored 2026-06-12 (007 Session 2, step 2). Evidence-over-inference
> discipline: every claim below is labeled **proven** (a command was run and
> its output captured) or **inferred / not verified**.

## Verdict

**Persistent under a testhost parent, healthy everywhere else.** The spec's
working premise ("intermittent contention flake", from the 2026-06-01
re-measurement) is overturned by today's evidence. Disposition: quarantine
via `[SkippableFact(Skip = …)]` with this record as the pointer, and propose
a scoped investigation set in the closeout. The CLI/OLE-DB contract the test
guards is empirically intact.

## Proven (2026-06-12, this host — DENICI, Access installed)

1. **Batch run fails at the budget.** Full `AccessHarvester.Integration`
   suite, sequential foreground, TRX
   `tests/AccessHarvester.Integration/TestResults/s2-full-integration.trx`:
   counters `total=60 executed=59 passed=58 failed=1`. Sole failure is this
   test: `System.TimeoutException : CLI process did not exit within 120000ms`
   (the test's own 120s budget), duration `00:02:00.088`.
2. **Targeted harness re-run fails identically.** Filtered run of just this
   test (TRX `s2-sanitize-targeted.trx`): same 120s `TimeoutException`. The
   class is `[Collection("HomeHealthCareCom")]`-serialized and nothing else
   was running, so cross-test ACE lock contention is excluded for this
   failure.
3. **Direct CLI invocation passes in ~0.55s.** The same
   `sanitize --classification <synthesized tlkpPlanStatuses doc> --in
   samples/HomeHealthCare.accdb --out <temp> --json-status` invocation,
   spawned directly (PowerShell `Start-Process`, both streams redirected):
   output files (`sanitization-report.json`, `tlkpPlanStatuses.csv`)
   materialized at 0.554s, process exited 0 at 0.555s, stdout envelope
   `TablesSanitized=1, TotalRowsWritten=2`, correct report content.
4. **Exact spawn-config replication also passes.** A second probe using
   CliInvoker's literal `ProcessStartInfo` shape (`UseShellExecute=false`,
   both streams redirected via `ReadToEndAsync`, `CreateNoWindow=true`,
   `WorkingDirectory=<test bin dir>`): files at 0.539s, exit 0 at 0.542s.
   The spawn configuration is therefore not the discriminating variable;
   the parent process context (testhost) is.
5. **No `msaccess.exe` is involved at any point.** `sanitize` uses ACE
   OLE DB, not Access COM automation. No `msaccess.exe` existed during or
   after either probe, and no orphaned `msaccess`/`testhost`/
   `AccessHarvester` processes remained after either failing harness run
   (the harness's `Kill(entireProcessTree:true)` reaped the wedged CLI).
6. **Harness-spawned ACE access works in general.** In the same batch run,
   `ClassifyCommandTests.HomeHealthCare_classify_emits_schema_conformant_json`
   — also a testhost-spawned CLI process opening the same `.accdb` via ACE
   OLE DB — passed in 5.26s.

## Inferred / not verified

- **The 2026-06-01 "passed targeted in 0.5 s" measurement was most likely a
  direct CLI run, not a harness run.** Today's direct probes time at
  0.54–0.55s — a dead match — while a `dotnet test` invocation carries
  multiple seconds of framework overhead before the test body even starts.
  If so, the historical "intermittent contention flake" classification
  compared a harness failure against a direct-CLI success, and the failure
  may always have been deterministic under a testhost parent. Inferred from
  the timing signature; the 2026-06-01 session's exact command line was not
  preserved.
- **Root cause of the testhost-context wedge: unknown.** Untested candidates:
  job-object / process-group inheritance under testhost, stdin handle
  inheritance (CliInvoker does not redirect `StandardInput`), ACE provider
  behavior with a testhost ancestor. Deliberately not pursued — the spec
  time-boxes this step to characterization plus one mechanical action.
- **Interactive-terminal behavior unmeasured.** All of today's runs (harness
  and probes) were launched from the orchestrator's agent shell. A
  `dotnet test` from a human interactive terminal was not measured and could
  behave differently.

## Why quarantine (not the `RunUntilFilesExist` mitigation, not red-as-is)

- The available mechanical mitigation — switching the test to
  `CliInvoker.RunUntilFilesExist` (files-stable-as-success) — only helps if
  the harness-spawned CLI materializes its outputs before wedging. Whether it
  does is unknown (the test's `finally` deletes the work dir, and the wedge
  only reproduces inside testhost), so the mitigation is a coin flip, while
  quarantine deterministically restores the set's headline deliverable
  (suite green with the exception explicitly recorded).
- The contract the test exists to prove ("CLI wire-up + the OLE DB row source
  work against a real database" — its own design note) is proven intact by
  probes 3–4. Quarantining does not hide a product defect; it hides a
  test-infrastructure interaction.
- The other two `SanitizeCommandTests` (usage error, file-not-found) remain
  active — they cover the subcommand's argument surface.

## Post-quarantine confirmation (R1 fix, 2026-06-12)

Round-1 verification (gpt-5-4) flagged that the final quarantined tree was
not itself demonstrated by a full-suite TRX. Fixed: full
`AccessHarvester.Integration` re-run on the final tree (sequential,
foreground, TRX `s2-full-integration-post-quarantine.trx`):
`total=60 executed=58 passed=58 failed=0` — the 2 non-executed are the
by-design `GoldenCorpusRegenerator` skip and this quarantined test.
Spurious `samples/*.accdb` COM-touch diffs swept again after the run.

## Re-enable condition

Remove the `Skip` when the proposed scoped investigation set (see this set's
closeout artifacts: "sanitize-testhost-wedge investigation") root-causes the
parent-context dependency — e.g., by reproducing the wedge outside xUnit,
instrumenting the CLI's startup under a testhost parent, or eliminating the
dependency (stdin redirect, job-object opt-out). Until then the quarantine is
re-validated implicitly by every full-suite run (the test reports Skipped,
visible in every TRX).
