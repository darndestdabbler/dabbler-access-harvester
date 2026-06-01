# Access COM Graceful Shutdown & Zombie-Process Failsafe Spec

> **Purpose:** Stop the harvester's COM-driven test/extraction runs from leaving
> orphaned `msaccess.exe` (and the `testhost.exe` / `dotnet.exe` that wait on
> them) behind when a graceful `Quit()` fails. Implement the two-layer pattern
> from [`docs/access-com-zombie-cleanup.md`](../../access-com-zombie-cleanup.md)
> — (1) make graceful shutdown actually release the COM RCWs, and (2) capture the
> instance PID up front and force-kill *only that exact PID* if it survives — in
> the harvester's single COM lifecycle chokepoint.
> **Created:** 2026-06-01
> **Session Set:** `docs/session-sets/harvester-access-com-graceful-shutdown/`
> **Implementation guide:** [`docs/access-com-zombie-cleanup.md`](../../access-com-zombie-cleanup.md)
> (added by the maintainer 2026-06-01; the authoritative reference for the pattern,
> the `hWndAccessApp` → `GetWindowThreadProcessId` PID capture, the doubled
> `GC.Collect` + `WaitForPendingFinalizers`, the bounded `WaitForExit` before
> force-kill, and the force-kill / lock-file caveats).
> **Status:** DRAFT — see *Finalization checklist* (maintainer call on the slug and
> the lock-file-cleanup scope).

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
prerequisites: []
totalSessions: 2
```

---

## Why This Set, Why Now

Multiple orchestrators (and the human, on 2026-06-01 during
`harvester-consumer-workflow-instructions` Session 2) have had to **kill hung
tests** — especially the newest PDF / report-baseline COM path — and then hunt
down orphaned `testhost.exe` / `dotnet.exe` / `msaccess.exe`. The
`[Collection("HomeHealthCareCom")]` serialization added earlier removed the
*parallel*-COM races, but it does not address the residual failure mode: a
graceful `Quit()` that does not actually drop the RCW reference count to zero
leaves a true zombie `msaccess.exe`, which no `taskkill`-by-name teardown can
target safely without risking unrelated Access instances.

The root cause and its failsafe are both well understood and captured in
[`docs/access-com-zombie-cleanup.md`](../../access-com-zombie-cleanup.md). The fix
is small and localised because the harvester has a **single COM instantiation
chokepoint**:

- [`AccessHarvester.Core/Com/AccessComSession.cs`](../../../AccessHarvester.Core/Com/AccessComSession.cs)
  is the only class that does `Type.GetTypeFromProgID("Access.Application")` +
  `Activator.CreateInstance`. Every other COM consumer
  (`AccessDateMaximaSampler`, `OleDbDaoSchemaReader`, `ReportBaselineCommand`, and
  the PDF renderer reached through them) goes through `AccessComSession`. Harden
  that one class and every consumer inherits the fix.

`AccessComSession.Dispose()` today calls `Quit(2)` →
`Marshal.FinalReleaseComObject` → a **single** `GC.Collect` /
`WaitForPendingFinalizers`. It does **not**: capture the PID, double the GC pass,
wait a bounded time for exit, or force-kill the exact surviving PID. This set adds
exactly those.

This set addresses, at the root-cause level, the on-deck
`harvester-parallel-com-test-cleanup` candidate noted in
[`ROADMAP.md`](../ROADMAP.md). It does **not** cover the separate
sanitize-against-HomeHealthCare timeout (an investigative spike tracked in
`lessons-learned.md`); that stays out of scope.

---

## Sessions

### Session 1 of 2: Harden `AccessComSession` with PID capture + force-kill failsafe (hermetic)

**Goal:** `AccessComSession` reliably terminates the *specific* Access instance it
created, gracefully when possible and by exact-PID force-kill when not — with the
kill-decision logic covered by hermetic unit tests (no Access required).

**Steps:**
1. **Capture the PID on construction.** After `OpenCurrentDatabase`, read
   `Application.hWndAccessApp`, translate it via a `GetWindowThreadProcessId`
   P/Invoke (`user32.dll`) to a process id, and store it. Confirm `hWndAccessApp`
   is non-zero before trusting it; record a diagnostic / leave the PID unset if it
   is zero (UI-suppressed edge case) so the failsafe degrades safely rather than
   killing PID 0.
2. **Strengthen `Dispose()` per the guide:** keep `Quit(acQuitSaveNone)` in
   try/catch and the `FinalReleaseComObject`; run `GC.Collect()` +
   `GC.WaitForPendingFinalizers()` **twice** (finalizing one RCW can queue
   others); then a **bounded** `WaitForExit(timeout)` on the captured PID before
   any hard kill — a slow exit is not a zombie.
3. **Force-kill only the exact surviving PID.** If the captured PID is still alive
   after the bounded wait, `Process.GetProcessById(pid).Kill()` + `WaitForExit()`;
   treat `ArgumentException` ("no such process") as the success case. Never kill by
   image name.
4. **Extract a testable seam.** Put the "graceful-exited vs. zombie → kill exact
   PID" decision behind an injectable abstraction (e.g. an `IProcessController`
   with `bool IsRunning(int pid)` / `bool WaitForExit(int pid, int ms)` /
   `void Kill(int pid)`), so the decision logic is unit-tested with a fake — the
   real COM path stays Windows/Access-gated. Hermetic tests cover: graceful exit →
   no kill; survives wait → kill exact PID; PID never captured (hWnd was 0) →
   no kill attempt; `ArgumentException` → treated as success.
5. Build, then run **only** the affected hermetic project(s) incrementally per the
   `lessons-learned.md` entry "Run Tests Incrementally By Project" — do **not** run
   the Access-COM/PDF tier in this session.
6. Cross-provider verify.

**Creates:** the `IProcessController` seam + its production (PID/Win32-backed)
implementation and a fake for tests; new hermetic unit tests for the kill-decision
logic.
**Touches:** [`AccessHarvester.Core/Com/AccessComSession.cs`](../../../AccessHarvester.Core/Com/AccessComSession.cs).
**Ends with:** `AccessComSession` captures its PID and force-kills only that PID as
a failsafe; the decision logic is green under hermetic unit tests; the COM/PDF tier
was deliberately not run.

### Session 2 of 2: Verify against real Access, simplify test teardown, document

**Goal:** Prove the hardened session leaves no orphaned `msaccess.exe`, and remove
the now-redundant blunt teardown so the fragile tier stops needing babysitting.

**Steps:**
1. On a host with Access installed, run the Access-COM / PDF integration tier
   (report-baseline / PDF — the newest fragile surface) and, after the run,
   assert there is **no surviving `msaccess.exe`** that the run created (and no
   leaked `testhost`). Capture the before/after process check in the session
   review.
2. **Simplify `CliInvoker` teardown.** Now that `AccessComSession` self-cleans by
   exact PID, replace / narrow the blunt best-effort `taskkill` in
   [`tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs`](../../../tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs)
   so it no longer risks unrelated instances; keep only what is still needed for
   the out-of-process CLI case.
3. **Lock-file hygiene (scope: maintainer call — see Finalization checklist).** If
   in scope, add `.laccdb` / `.ldb` cleanup (or test-DB recreation) after any
   force-kill, per the guide's force-kill caveat, for disposable test databases.
4. Wire the implementation guide into the durable docs: reference
   [`docs/access-com-zombie-cleanup.md`](../../access-com-zombie-cleanup.md) from
   `project-guidance.md` (Conventions) and from the CLAUDE.md / AGENTS.md /
   GEMINI.md "Building & Testing" section's test-lane guidance (apply the edit to
   **all three** agent-instruction files per the keep-in-sync rule).
5. Build + incremental tests; then the COM/PDF tier per step 1. Cross-provider
   verify; closeout (change-log, ROADMAP/state, Step 9).

**Touches:** `CliInvoker.cs`; `project-guidance.md`; `CLAUDE.md` / `AGENTS.md` /
`GEMINI.md`; possibly test-DB setup helpers (if lock-file cleanup is in scope).
**Ends with:** a documented, verified no-zombie COM teardown; the blunt teardown
removed; the implementation guide referenced from durable guidance.

---

## Finalization checklist (maintainer)

- [ ] **Slug.** Authored as `harvester-access-com-graceful-shutdown` (describes the
  root-cause fix). Confirm, or rename to the ROADMAP's on-deck
  `harvester-parallel-com-test-cleanup`. If kept as-is, update the ROADMAP on-deck
  note to point here and mark the old candidate absorbed.
- [ ] **Lock-file cleanup scope (Session 2, step 3).** Decide whether `.laccdb` /
  `.ldb` cleanup + disposable-test-DB recreation is in scope for this short set or
  deferred. Default recommendation: in scope only if it is small; otherwise defer.
- [ ] **`totalSessions: 2`** — confirm the hermetic-core / Access-gated-verify
  split, or collapse to 1 session if you want the whole thing in one pass on an
  Access host.
- [ ] Author `ai-assignment.md` at Session 1 Step 3.5 (routed), per the workflow.
- [ ] Add the ROADMAP row when the set starts.

---

> **Provenance:** `harvester-consumer-workflow-instructions` Session 2
> (2026-06-01), where the whole-solution test run hung on the COM/PDF tier and had
> to be killed + orphan-reaped; the human added
> [`docs/access-com-zombie-cleanup.md`](../../access-com-zombie-cleanup.md) and
> asked for a short set to apply it.
