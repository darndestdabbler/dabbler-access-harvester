# Harvester Fix Classify Timeout Spec

> **Purpose:** Resolve the persistent ~240-second timeout in the `classify`
> CLI path against `HomeHealthCare.accdb` so the
> `AccessHarvester.Integration` COM tier stops carrying a permanent red
> that masks real regressions. Characterize the root cause with evidence
> first, then apply the **minimal correct** remediation — a low-risk
> performance fix if one exists, otherwise an honest `Skip.If` quarantine
> with a documented reason. This is a **reliability spike, not a feature**.
> **Created:** 2026-06-01
> **Session Set:** `docs/session-sets/harvester-fix-classify-timeout/`
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification.
> Full tier; no UI, so no UAT/E2E.
> **Status:** Draft (authored 2026-06-01 from the routed next-set
> recommendation at the close of `harvester-report-displayed-total-parity`).

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites: []
totalSessions: 2
```

> Rationale: an investigative reliability spike — diagnosis + a focused
> code fix or test quarantine + cross-provider verification. No human UAT
> or browser E2E applies.

---

## Why This Set, Why Now

`AccessHarvester.Integration.Cli.ClassifyCommandTests.HomeHealthCare_classify_emits_schema_conformant_json`
times out at its 240-second budget. As re-measured at the close of
`harvester-report-displayed-total-parity` (2026-06-01, **after** the COM
graceful-shutdown hardening landed), the timeout is **persistent and
reproducible** — it fired at 240 s in both a full-tier batch run and a
targeted retry. (The sibling `sanitize`-against-HomeHealthCare test, by
contrast, recovered — it now passes on retry and is treated as an
intermittent flake, explicitly **out of scope** for this set.) A manual
run of the same `classify` invocation against the same fixture also takes
minutes, so this is very likely a **real product-latency problem**, not a
test artifact.

A permanently-red COM test is a standing hazard: it makes every future
COM-tier run ambiguous (`was it my change, or the usual timeout?`) and
risks numbing the orchestrator to a real regression (per
`lessons-learned.md` → "SanitizeCommandTests.HomeHealthCare Timeout…").

**ROOT CAUSE — established by evidence in Session 1 (2026-06-01); this
section's earlier value-sampling hypothesis is OVERTURNED.** See
[`root-cause-findings.md`](root-cause-findings.md) for the full evidence.
Summary: the `classify` work completes in **~6.9 s** (schema read 6.4 s, of
which the DAO/Access-COM augmentation is 6.35 s; value sampling **0.16 s**).
The 240 s timeout is **not** product compute latency and **not** the
`SELECT DISTINCT` cardinality count (all 93 cardinality queries = 0.06 s,
max 2 ms; the fixture is 13 tables / 93 columns, largest table 62 rows).
The real failure: the **`classify` CLI *process* never terminates** — after
writing its output files it **wedges in native/runtime process shutdown**
(consistent with COM un-initialization following the Access COM interop; the
exact native frame was not captured), with **no surviving `msaccess.exe`**
(the graceful-shutdown hardening reaps the Access child mid-work; this is a
separate, CLI-process-level wedge). The test fails because
`CliInvoker.RunWithTimeout` blocks on `proc.WaitForExit(240000)`. Probes
confirmed `Environment.Exit` still wedges (it runs the same managed→native
finalizer / COM-un-init shutdown path) while a hard `TerminateProcess` after
flushing output exits cleanly in 7 s with the correct exit code and no
zombie — consistent with COM un-initialization, though the exact native frame
was not captured. So the remediation target is
**CLI process teardown**, not `OleDbValueSampler` / `ValueSampleSize`.

## Scope

1. **Reproduce + instrument.** Reproduce the timeout deterministically and
   attribute wall-clock across phases: schema read, per-column value
   sampling (count of columns sampled × round-trips × connection
   lifecycle), and COM/OLE DB teardown. Produce a grounded, evidence-backed
   root-cause finding.
2. **Choose the minimal remediation by the evidence:**
   - **Fix** if a tractable, low-risk performance change brings the test
     green within budget — e.g. reuse one OLE DB connection across columns/
     tables instead of connection-per-sample, batch a table's column
     samples into fewer round-trips, or bound the per-table work. The fix
     must not change `classify`'s output contract / `classification-schema.json`
     or the sampled-statistics semantics.
   - **Quarantine** otherwise: gate the test with `Skip.If` + a specific
     reason and an issue link, so the COM tier goes green-or-skip and the
     latency is **documented, not masked**.
3. **Re-establish a green/known COM tier** and update the
   `lessons-learned.md` sanitize/classify-timeout entry with the outcome.

## Out of Scope

- The `sanitize`-against-HomeHealthCare timeout (now an intermittent flake).
- Broad OLE DB / COM rearchitecture beyond the minimal fix.
- Any change to `classify`'s output contract, `classification-schema.json`,
  the heuristics/PII dictionary, or the sampled-statistics semantics.
- Reducing `ValueSampleSize` below a value that would weaken classification
  quality, unless Session 1 shows the larger sample adds no signal (a
  finding the human must bless before it ships).

## Stop Rule

If Session 1's diagnosis shows the root-cause fix is large or risky —
broadly touching the OLE DB / COM core, or risking the `classify` output
contract — **default to the quarantine path** (`Skip.If` + issue link)
rather than over-investing. This set is a bounded spike; honoring the
repo's consumer-driven-only scope discipline beats a deep rewrite.

---

## Sessions

### Session 1 of 2: Reproduce, Instrument, Root-Cause

**Steps:**
1. Reproduce the `classify`-against-HomeHealthCare timeout in isolation
   (single test, no parallelism) and capture the wall-clock.
2. Instrument the `classify` path — `ClassifyCommand` →
   `ClassificationRunner` → `IValueSampler.Sample`
   (`AccessHarvester.Core.Com.OleDbValueSampler`) — to attribute time to
   phases: how many columns are sampled, how many OLE DB connections /
   round-trips that costs, and the per-sample vs. teardown split.
3. Produce a grounded root-cause finding (evidence per claim, no inferred-
   as-fact) and a **fix-vs-quarantine recommendation** with the expected
   blast radius of each.

**Creates:** `docs/session-sets/harvester-fix-classify-timeout/ai-assignment.md`

**Touches (read / diagnostic):**
`AccessHarvester.Cli/Commands/ClassifyCommand.cs`
`AccessHarvester.Pipeline/Classification/ClassificationRunner.cs`
`AccessHarvester.Pipeline/Classification/ScanConfig.cs`
`AccessHarvester.Core/Com/OleDbValueSampler.cs`
`tests/AccessHarvester.Integration/Cli/ClassifyCommandTests.cs`

**Ends with:** An evidence-backed root-cause finding for the classify
timeout and a fix-vs-quarantine recommendation, ready for the human to
confirm the path before Session 2 implements it.

**Progress keys:** `session-1/reproduce-timeout`,
`session-1/instrument-classify-path`,
`session-1/root-cause-and-recommendation`

---

### Session 2 of 2: Remediate + Verify + Closeout

**Path chosen by the human at Session-1 close (2026-06-01):** Option 2,
*scoped* — a flush-then-`TerminateProcess(exitCode)` shutdown guard scoped to
the COM-using command path(s), NOT unconditional at `Main` for every
subcommand (narrows the cross-subcommand blast radius gpt-5-4 flagged).
**Fallback:** Option 3 (`Skip.If` quarantine + issue link) if verification
surfaces a blast-radius / buffered-sink problem with the scoped exit. The
**E5 DAO `Property not found` regression is folded in** (see below).

**Steps:**
1. Implement the scoped CLI-process-teardown fix so `classify` (and the other
   COM-using subcommands sharing the path) flush stdout/stderr and hard-exit
   with the correct exit code instead of wedging in COM un-init. Confirm the
   process exits in ~7 s with the right exit code and leaves no orphaned
   `AccessHarvester.exe`. Must not change `classify`'s output contract,
   `classification-schema.json`, or the sampled-statistics semantics.
2. **Fold-in (E5):** add the "Property not found" HRESULT to
   `OleDbDaoSchemaReader.DaoPropertyAbsentHResults` so the absent-property
   case is caught at the inner `catch … when` and no longer recorded as 93
   `SchemaReadError` degradations (which currently force exit code 3 on the
   rebuilt HomeHealthCare fixture). Add/adjust a hermetic test for the
   absent-property HRESULT mapping.
3. Verify the `AccessHarvester.Integration` COM tier is green-or-skip
   (run it incrementally per the Testing Conventions; read the TRX for
   truth; reap any wedged `testhost` / `AccessHarvester.exe` via WMI
   `Win32_Process.Terminate`).
4. Update `lessons-learned.md` (the sanitize/classify-timeout entry) and
   the ROADMAP with the outcome; run focused validation, closeout, and the
   Step 9 reorganization review.

**Touches (per the chosen path):**
`AccessHarvester.Cli/Program.cs` (scoped flush-then-terminate shutdown guard)
`AccessHarvester.Core/Com/OleDbDaoSchemaReader.cs` (E5 HRESULT fold-in)
`tests/AccessHarvester.Integration/Cli/ClassifyCommandTests.cs` (only if the
quarantine fallback is taken)
`docs/planning/lessons-learned.md`
`docs/session-sets/ROADMAP.md`

**Ends with:** The COM tier no longer carries an unexplained red — the
classify timeout is either fixed within budget or honestly quarantined
with a documented reason — and the outcome is recorded.

**Progress keys:** `session-2/remediate`,
`session-2/verify-com-tier`,
`session-2/closeout`

---

## Deliverables

- An evidence-backed root-cause finding for the `classify`-against-
  HomeHealthCare timeout.
- A green-or-skip `AccessHarvester.Integration` COM tier (focused fix or
  documented `Skip.If` quarantine).
- Updated `lessons-learned.md` + ROADMAP recording the outcome.

## Provenance

The timeout was observed across multiple sets
(`harvester-report-baseline-capture` S2, `harvester-access-com-graceful-shutdown`
S2, and `harvester-report-displayed-total-parity` S2's re-measurement) and
queued as the next-up reliability spike. This is the **first of the two
remaining harvester sets**; after the `harvester-sql-migration-generator`
capstone, the repo goes consumer-driven-only (no self-initiated work).
