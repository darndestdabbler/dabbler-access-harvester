# Root-Cause Findings — classify-against-HomeHealthCare 240 s timeout

> **Session 1 of `harvester-fix-classify-timeout`.** Evidence-backed
> root cause for `ClassifyCommandTests.HomeHealthCare_classify_emits_schema_conformant_json`
> timing out at its 240 s budget. Captured on a Windows + Access host on
> 2026-06-01 by Claude Opus 4.8 (orchestrator). Every claim below is tied
> to a specific observed measurement, per the repo lesson *"Do Not Present
> An Inferred Root Cause As Fact Without Verifying It."*

## TL;DR

The timeout is **not** product compute latency and **not** the
`SELECT DISTINCT` cardinality cost the spec hypothesized. The `classify`
work — schema read + value sampling — completes in **~6.9 s**. The failure
is that the **`classify` CLI *process* never terminates**: after writing its
output it **wedges in native process shutdown** — consistent with COM
un-initialization following the Access COM interop, though the exact native
frame was not captured (see *Confidence* below) — with no surviving
`msaccess.exe`. The xUnit test fails because `CliInvoker.RunWithTimeout`
blocks on `proc.WaitForExit(240000)`, which a never-exiting process blows
through.

**The spec's premise and prime suspect are overturned by the evidence.**

## What the spec assumed vs. what was measured

| Spec hypothesis | Measured reality |
|---|---|
| "real product-latency problem … takes minutes" | In-process work = **6.87 s** total |
| Prime suspect: per-column `DISTINCT`-subquery cardinality, "pathological on any table with substantial row counts" | All 93 cardinality queries = **0.06 s** total, max **2 ms** |
| "value-samples every non-key column across hundreds of tables" (test comment) | Fixture is **13 tables / 93 columns**, largest table **62 rows**; value sampling = **0.16 s** total |

## Evidence

### E1 — Per-phase in-process timing (faithful reproduction)

A throwaway diagnostic reproduced the exact `classify` work in-process
(real `OleDbDaoSchemaReader` + the identical four OLE DB query kinds on one
reused ACE connection, `ValueSampleSize = 200`). Full report:
[`session-1-timing-evidence.md`](session-1-timing-evidence.md). Headline:

- **Phase A — schema read: 6.42 s**
  - A1 OLE DB schema rowsets only: **0.07 s**
  - A2 **DAO / Access-COM augmentation (≈ A − A1): 6.35 s**  ← dominant cost
- **Phase B — value sampling: 0.16 s** (the spec's entire suspect area)
  - `cardinality(DISTINCT)`: 93 queries, **0.06 s**, max 2 ms
  - `null-count`: 93 queries, 0.04 s
  - `value-sample(TOP 200)`: 83 queries, 0.04 s
  - `rowcount(COUNT*)`: 13 queries, 0.01 s
- **Overall: 6.87 s** — three orders of magnitude under the 240 s budget.

So even if the DISTINCT cardinality were optimized to zero, it would move
the needle by 0.06 s. The spec's remediation target (`OleDbValueSampler` /
`ValueSampleSize`) is the wrong place.

### E2 — The failing test reproduces in true isolation … as a process-exit hang

Running only the real test, single test, no parallelism:

```
Failed … HomeHealthCare_classify_emits_schema_conformant_json [4 m]
System.TimeoutException : CLI process did not exit within 240000ms.
  args=[classify --in …\HomeHealthCare.accdb --out …\classify-… --json-status]
  at CliInvoker.RunWithTimeout(...) CliInvoker.cs:line 58
```
Wall clock: **242 s**. The exception is `WaitForExit` returning false — the
CLI *process* did not terminate. (`classify` takes the plain `WaitForExit`
path; only `report-baseline` uses the `RunUntilFilesExist` escape hatch.)

### E3 — The CLI completes all work, then the process hangs with no msaccess child

Running the built CLI exe directly (outside xUnit) against the same fixture:
- It prints `[classify] 13 tables classified.`, writes
  `…/homehealthcare/classification.json`, and emits the full `--json-status`
  envelope with `"ExitCode": 3` — i.e. **all work ran and the output files
  were fully written** (schema-conformance of that output was not separately
  re-asserted here; the point is the process reached the end of its work).
- The **process then stays alive indefinitely**. Repeated runs accumulated
  **four simultaneous live `AccessHarvester.exe` processes** (observed PIDs
  47836 / 58960 / 62740 / 71824), each with **`msaccess.exe` count = 0**.
- This reproduces with stdout redirected to a **file** as well as to a
  pipe, so it is **not** a stdout-pipe-drain artifact.
- The wedged processes **survived `Stop-Process -Force`**; only WMI
  `Win32_Process.Terminate` reaped them — matching the documented signature
  of a COM-RPC-wedged process (cf. `project_wedged_testhost_wmi_terminate`).
- `dotnet-stack` could not attach to the live wedged PIDs, consistent with
  the process being stuck in **native code below the managed runtime**
  (un-initialization of the COM apartment is the most likely such frame,
  but the exact frame was not captured), not in managed code.

Because `AccessComSession` is disposed **mid-work** (inside the schema
read), `msaccess.exe` is already gracefully reaped before `Main` returns.
The surviving wedge is the **CLI's own process shutdown**, not an Access
zombie. The COM-graceful-shutdown hardening (which targets the
`msaccess.exe` child) is working and is orthogonal to this hang.

### E4 — Fix-direction probes (what resolves the wedge)

Two temporary probes on `Program.Main`, each built and run against the
fixture, then reverted:

| Probe at end of `Main` (after flushing stdout/stderr) | Result |
|---|---|
| `Environment.Exit(rc)` | **Still wedges** — 60 s wrapper could not even reap it. `Environment.Exit` runs the managed finalizer / COM-un-init shutdown path; the hang persisting here (while `TerminateProcess`, which skips it, exits cleanly) is consistent with the wedge being in that path, though the exact native frame was not captured. |
| `TerminateProcess(GetCurrentProcess(), rc)` (kernel32, skips finalizers/COM un-init) | **Clean exit in 7 s**, correct exit code **3**, output fully written, **zero** lingering processes. |

This localizes the hang to the **managed→native shutdown path** (the
finalizer / COM-un-init sequence that `Environment.Exit` runs and
`TerminateProcess` skips) and shows the work itself is genuinely complete by
then. The exact native frame stays in the inferred bucket below.

### E5 — Collateral finding (separate from the timeout)

Every field now emits a `SchemaReadError … DAO field-read failure: Property
not found.` degradation (93 of them → exit code 3). This is a **distinct
regression** in `OleDbDaoSchemaReader.TryReadBoolProperty` /
`TryReadStringProperty`: the "Property not found" HRESULT is **not** in
`DaoPropertyAbsentHResults` `{0x800A0CC1, 0x800A0CB3}`, so the absent-property
case escapes the inner `catch … when` and is recorded as an error. It very
likely appeared when `samples/HomeHealthCare.accdb` was rebuilt earlier the
same day (file mtime 2026-06-01 13:41). It does **not** cause the hang (the
read completes in 6.4 s including all 93 exceptions) and is **out of scope**
for this timeout set, but it is worth a tracked follow-up because it makes
`classify` always return exit 3 on this fixture and floods the degradation
list. Whether this same fixture change is what tipped COM shutdown into a
wedge is an open question (not established by evidence here).

## Root cause

The `classify` CLI process **hangs in native process shutdown** after
completing all classification work and writing its output files. The work is
done (~7 s) but the process never terminates, so the test's
`WaitForExit(240 s)` times out.

**What is proven vs. inferred (per the repo norm "Do Not Present An Inferred
Root Cause As Fact"):**

- **Proven by evidence:** the work completes in ~7 s (E1); the process does
  not exit (E2, E3); no `msaccess.exe` survives (E3); the hang is at process
  shutdown after work completion and is consistent with a native/runtime-level
  wedge rather than an ordinary managed-code loop (`dotnet-stack` cannot
  attach, E3); `Environment.Exit`
  (which runs managed finalizers + COM un-init) does **not** resolve it but a
  hard `TerminateProcess` (which skips them) exits cleanly in 7 s (E4).
- **Strong inference, not directly captured:** that the specific native frame
  is **COM apartment un-initialization** following the Access COM interop. The
  E4 contrast (finalizer/COM-uninit path hangs; skipping it does not) and the
  use of Access COM in this path make COM un-init by far the most likely
  mechanism, but no native stack/trace of the wedged frame was captured to
  prove it. A definitive confirmation (a native stack dump of the wedged PID)
  is optional for Session 2 — it would not change the chosen remediation,
  which targets the shutdown sequence regardless of the exact frame.

## Fix-vs-quarantine options for Session 2 (for human decision)

1. **Harness-only, no product change (lowest risk).** Route
   `ClassifyCommandTests` through the existing `CliInvoker.RunUntilFilesExist`
   pattern (already built for "CLI materializes output then hangs in COM
   cleanup"; used by `report-baseline`): treat a stable
   `…/homehealthcare/classification.json` as success, then terminate the
   lingering process. Caveat: its `proc.Kill(entireProcessTree:true)` may
   not reap a COM-wedged process (E3 showed `Stop-Process` could not), so
   the test would go green but could still **accumulate wedged
   `AccessHarvester.exe` zombies** — a CI-host hazard. Would need a WMI
   `Win32_Process.Terminate` fallback to be clean.
2. **Product fix — hard self-terminate after flush (low risk, validated by E4).**
   Have the CLI flush stdout/stderr and `TerminateProcess` itself with the
   computed exit code at the end of `Main`. Validated: 7 s clean exit, right
   exit code, no zombie. Blunt (skips all finalizers) and touches the
   process-exit path of **every** subcommand — but the CLI is short-lived,
   has already written its output, and its only unmanaged dependency
   (`msaccess.exe`) is reaped mid-work. This also fixes the **real
   consumer-facing defect**: a downstream user running `classify` today gets
   a CLI that hangs forever.
3. **Quarantine (Stop-Rule default).** `Skip.If` the test with a specific
   reason + issue link documenting the COM-shutdown wedge, so the COM tier
   goes green-or-skip and the latency is documented, not masked. Honors the
   bounded-spike Stop Rule if options 1–2 are judged too risky, but leaves
   the product hang shipped.

## Cross-provider consensus (Session 1, 2026-06-01)

Both non-Anthropic reviewers confirmed the root cause is **sound**, but
**split on the remediation** (full transcripts:
[`decision-consensus-gpt-5-4.md`](decision-consensus-gpt-5-4.md),
[`decision-consensus-gemini-pro.md`](decision-consensus-gemini-pro.md)):

- **gpt-5-4 → Option 3 (quarantine now, narrower product fix later).**
  Disagrees with the orchestrator's lean. Reason: an unconditional
  `TerminateProcess` at the shared `Main` skips `finally`/`Dispose`/exit
  handlers for **every** subcommand; flushing `Console` does not guarantee
  durability of other buffered sinks (file/log/telemetry, future async) —
  too much blast radius for a bounded spike.
- **gemini-pro → Option 2 (hard terminate).** Agrees with the lean: the only
  option that fixes both the test and the consumer-facing hang; buffer risk
  is minimal in a simple CLI and mitigable by explicit flush; E4 validated it.

Both independently raised the **same** key risk: buffered sinks beyond
`Console` and the cross-subcommand blast radius of a process-wide hard exit.
A synthesis neither fully spelled out: **scope** the hard-terminate so it is
not unconditional for all subcommands (e.g. only after a successful flush on
the COM-using command paths), which narrows Option 2's blast radius toward
gpt-5-4's concern. **Because the reviewers split on a genuine design call,
the path is deferred to the human** (the spec already gates Session 2 on
human confirmation of the path).

## Orchestrator's lean (for the human to confirm)

The spec's Stop Rule
says default to quarantine if the fix "broadly touch[es] the OLE DB / COM
core." Option 2 does **not** touch the COM core — it is a 3-line shutdown
guard whose blast radius is "the process exits hard after it was already
done" — and it is the only option that also repairs the consumer-facing
hang. Recommend **Option 2 as the primary**, with **Option 3 (quarantine)
as the fallback** if cross-provider review flags the hard-terminate as
unacceptable for a shared CLI. Option 1 alone is discouraged (green test,
zombie-accumulating CI host).

## Human decision (2026-06-01) — locks Session 2's path

- **Remediation path: Option 2, *scoped/narrowed*.** Session 2 implements a
  flush-then-`TerminateProcess(exitCode)` guard **scoped to the COM-using
  command path(s)** (not unconditionally at `Main` for every subcommand),
  which addresses gpt-5-4's cross-subcommand blast-radius concern while
  fixing both the failing test and the consumer-facing CLI hang. **Fallback:
  Option 3 (`Skip.If` quarantine + issue link)** if Session 2's verification
  surfaces a blast-radius or buffered-sink problem with the scoped exit.
- **E5 (the `Property not found` DAO degradation) is folded INTO Session 2**,
  not deferred: add the "Property not found" HRESULT to
  `OleDbDaoSchemaReader.DaoPropertyAbsentHResults` so the absent-property case
  is caught at the inner `catch … when` and stops being recorded as 93
  `SchemaReadError` degradations (which currently force exit code 3 on this
  fixture).
