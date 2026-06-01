# Verification Round 1

Step 1 — PID capture on construction: `AccessHarvester.Core/Com/AccessComSession.cs` does implement the required capture path in `AccessComSession.CaptureAccessPid()`, which is called from the constructor after the Access instance is created and the database is opened. The method reads `Application.hWndAccessApp` inside a `try/catch`, so a COM/dynamic read failure leaves `AccessProcessId` unset and records a diagnostic in `PidCaptureWarning`. It explicitly checks `hwndRaw == 0` and returns `(null, warning)` in that case, so it will not later target PID 0. It then calls the `GetWindowThreadProcessId` P/Invoke declared in the same file and checks the returned `pid`; if `pid == 0`, it again leaves the PID unset with a warning. That meets the safety requirement. Minor note only: it does not inspect the function’s return value or `Marshal.GetLastWin32Error()`, but because `pid == 0` is treated as failure, the kill safety behavior is still correct.

Step 2 — strengthened `Dispose()`: this is only partially correct. In `AccessHarvester.Core/Com/AccessComSession.cs`, `AccessComSession.Dispose()` does call `Application?.Quit(2)` (`acQuitSaveNone == 2`), does call `Marshal.FinalReleaseComObject(Application)`, does run `GC.Collect()` + `GC.WaitForPendingFinalizers()` exactly twice, and does invoke the reaper only after those GC passes. That matches most of the acceptance step. The problem is that `Quit(2)` and `FinalReleaseComObject` are inside the same `try` block. If `Application.Quit(2)` throws, `Marshal.FinalReleaseComObject(Application)` is skipped entirely. That weakens the graceful COM shutdown path the session was supposed to strengthen, because the RCW may never be explicitly released in exactly the case where graceful shutdown is already failing. This should be split so `FinalReleaseComObject` still runs even when `Quit(2)` throws.

Step 3 — exact-PID force-kill logic: `AccessHarvester.Core/Com/ComProcessReaper.cs`, method `ComProcessReaper.EnsureTerminated(int? capturedPid, int waitTimeoutMs)`, correctly implements the intended decision tree: `null` PID returns `false` immediately; `!IsRunning(pid)` returns `false`; `WaitForExit(pid, timeout)` returning `true` returns `false`; only the surviving PID is passed to `_controller.Kill(pid)`, once, and the method returns `true`. There is no image-name-based kill anywhere in this code. `AccessHarvester.Core/Com/SystemProcessController.cs`, method `Kill(int pid)`, also uses `Process.GetProcessById(pid)` and `proc.Kill(); proc.WaitForExit();`, so it is targeting the exact PID, not `msaccess.exe` by name. However, there is a concurrency bug here: if the process exits after `GetProcessById(pid)` succeeds but before `proc.Kill()` runs, `Process.Kill()` can throw `InvalidOperationException` for an already-exited process. That exception is not caught. In that race, disposal can fail even though the process is already gone, which is contrary to the “already exited is success” shutdown intent.

Step 4 — testable seam and hermetic tests: `AccessHarvester.Core/Com/IProcessController.cs` is the requested seam with the exact three members: `IsRunning(int)`, `WaitForExit(int,int)`, and `Kill(int)`. `ComProcessReaper` is fully independent of COM and Win32, and `AccessComSession` injects the controller via its constructor overload, so the seam extraction is present. In `tests/AccessHarvester.Tests/Com/ComProcessReaperTests.cs`, the hermetic decision tests cover all of the required logical cases: `EnsureTerminated_NullPid_ReturnsFalseAndControllerIsUntouched` covers the “PID never captured / hWnd 0 equivalent -> no kill” case; `EnsureTerminated_ProcessAlreadyExited_ReturnsFalseAndDoesNotWaitOrKill` covers graceful exit; `EnsureTerminated_ProcessExitsDuringWait_ReturnsFalseAndDoesNotKill` covers bounded wait success; `EnsureTerminated_ProcessDoesNotExitDuringWait_ReturnsTrueAndKillsExactPid` covers exact-PID kill and explicitly asserts `KilledPid == pid`; `Constructor_NullController_ThrowsArgumentNullException` covers the null-guard on the seam consumer. The `SystemProcessController` tests also cover the required `ArgumentException` success behavior for all three methods by using a non-existent PID. So the requested hermetic coverage is present. What is not covered is the already-exited-after-lookup race in `SystemProcessController.Kill`.

Step 5 — build/run only the affected hermetic project: this cannot be independently verified from the supplied source files alone. I can see the new hermetic tests in `tests/AccessHarvester.Tests/Com/ComProcessReaperTests.cs`, and I can see that no Access-gated integration or PDF-tier files were edited in the provided set, which is consistent with the session boundary. But there is no command output, test filter output, or build log here to prove that only the affected hermetic project was run.

Overall: the PID capture and reaper decision logic are implemented correctly, and the seam/tests are in place. The session is not fully acceptable as written because `AccessComSession.Dispose()` can skip `FinalReleaseComObject` if `Quit(2)` throws, and `SystemProcessController.Kill()` has an uncaught already-exited race that can surface during shutdown.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Com/AccessComSession.cs",
      "summary": "AccessComSession.Dispose() puts Application.Quit(2) and Marshal.FinalReleaseComObject(Application) in the same try block, so a Quit failure skips FinalReleaseComObject entirely.",
      "fix": "Move FinalReleaseComObject into a finally block or a separate guarded try so the RCW is always explicitly released even if Quit(acQuitSaveNone) throws."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Com/SystemProcessController.cs",
      "summary": "SystemProcessController.Kill(int) only treats ArgumentException as success; if the process exits after GetProcessById but before Kill, an already-exited race can escape as InvalidOperationException and break Dispose.",
      "fix": "Treat already-exited races as success too by catching InvalidOperationException around Kill/WaitForExit (and optionally re-checking HasExited) so shutdown remains reliable when the target exits between lookup and kill."
    }
  ]
}
```

---

# Verification Round 2

Both Round 1 fixes are correctly present in the reviewed code, and the explicit Session 1 shutdown flow is implemented as intended. However, I do see one additional **Major** resource-leak/correctness issue in the `AccessComSession` constructor failure path, so I cannot give an overall VERIFIED verdict.

**Step 1 – PID capture on construction:**  
In `AccessHarvester.Core/Com/AccessComSession.cs`, the constructor calls `CaptureAccessPid()` after `Application.OpenCurrentDatabase(...)`. In `CaptureAccessPid()`, `Application.hWndAccessApp` is read inside a `try/catch`, so a COM/dynamic read failure degrades safely to `(null, warning)`. The method explicitly checks `hwndRaw == 0` and returns `(null, "hWndAccessApp was zero...")`, which satisfies the “never trust zero / never kill PID 0” requirement. It then calls the `user32.dll` P/Invoke `GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId)` and treats `pid == 0` as failure, again returning `(null, warning)`. That means `AccessProcessId` is left unset and `PidCaptureWarning` records the diagnostic. This is defensive enough for the Session 1 safety goal. It does not inspect the thread-ID return value or last Win32 error, but because it refuses to use PID 0 and preserves a warning, the safe-degradation requirement is met.

**Step 2 – Dispose strengthening and ordering:**  
In `AccessHarvester.Core/Com/AccessComSession.cs`, `Dispose()` now has the correct structure for the first Round 1 fix. `Application?.Quit(2)` is inside an inner `try`, and `Marshal.FinalReleaseComObject(Application)` is in the corresponding `finally`, so a `Quit(2)` failure no longer skips COM release. The outer `catch` suppresses graceful-cleanup errors as intended. After that, `GC.Collect(); GC.WaitForPendingFinalizers();` is executed **exactly twice**, matching the implementation guide. Only after those two GC/finalizer passes does the code invoke `new ComProcessReaper(_processController).EnsureTerminated(AccessProcessId, GracefulExitWaitMs);`, so the reaper is correctly sequenced after the graceful path has had its chance to land. On the specific acceptance criteria for `Dispose()`, this is correct.

**Step 3 – Force-kill only the exact surviving PID:**  
`AccessHarvester.Core/Com/ComProcessReaper.cs` implements the required decision logic cleanly in `EnsureTerminated(int? capturedPid, int waitTimeoutMs)`:  
- `null` PID => immediate no-op / `false`  
- not running => no kill  
- exits within bounded wait => no kill  
- survives wait => `_controller.Kill(pid)` exactly once and return `true`  

There is no image-name-based killing anywhere in this path. In `AccessHarvester.Core/Com/SystemProcessController.cs`, `Kill(int pid)` uses `Process.GetProcessById(pid)`, then `proc.Kill()`, then `proc.WaitForExit()`, so it targets the exact PID only. It catches `ArgumentException` as the “already gone” success case, and now also catches `InvalidOperationException`, which correctly addresses the second Round 1 issue: if the process exits between `GetProcessById` and `Kill`/`WaitForExit`, that race is treated as success. This part is correctly fixed.

**Step 4 – Testable seam and hermetic tests:**  
The seam is present as `AccessHarvester.Core/Com/IProcessController.cs` with the exact three members requested: `IsRunning(int)`, `WaitForExit(int,int)`, and `Kill(int)`. `ComProcessReaper` depends only on this interface, so the decision logic is hermetically testable. The tests in `tests/AccessHarvester.Tests/Com/ComProcessReaperTests.cs` do cover the required cases:  
- null-guard: `Constructor_NullController_ThrowsArgumentNullException`  
- PID never captured / null PID => no-op: `EnsureTerminated_NullPid_ReturnsFalseAndControllerIsUntouched`  
- graceful exit already happened => no kill: `EnsureTerminated_ProcessAlreadyExited_ReturnsFalseAndDoesNotWaitOrKill`  
- exits during bounded wait => no kill: `EnsureTerminated_ProcessExitsDuringWait_ReturnsFalseAndDoesNotKill`  
- survives wait => kill exact PID: `EnsureTerminated_ProcessDoesNotExitDuringWait_ReturnsTrueAndKillsExactPid`  

That last test explicitly asserts `KilledPid == pid`, so the exact-PID requirement is covered. For `SystemProcessController`, the hermetic tests verify `ArgumentException` handling as success for all three methods:  
- `SystemProcessController_IsRunning_NonExistentProcess_ReturnsFalse`  
- `SystemProcessController_WaitForExit_NonExistentProcess_ReturnsTrue`  
- `SystemProcessController_Kill_NonExistentProcess_DoesNotThrow`  

So the seam/testing requirement is satisfied.

**Step 5 – Build/run only the affected hermetic project:**  
This is not inferable from source alone, but your traceability note says `AccessHarvester.Tests` built cleanly and the filtered `ComProcessReaperTests` ran green `9/9`, while the Access-COM/PDF tier was intentionally not run. Nothing in the reviewed code contradicts that, and the test scope matches the Session 1 instructions.

**Additional issue found – constructor failure leaks the created Access instance:**  
In `AccessHarvester.Core/Com/AccessComSession.cs`, inside `AccessComSession(string databasePath, IProcessController processController)`, once `Activator.CreateInstance(accessType)` succeeds, the code assigns `Application = app;` and immediately calls `Application.OpenCurrentDatabase(databasePath, false);` with no cleanup guard. If `OpenCurrentDatabase(...)` throws, the constructor aborts before the object is successfully created, so the caller never gets an instance they can `Dispose()`. In that failure path, there is no `Quit(2)`, no `FinalReleaseComObject`, no doubled GC, and no reaper call. That can leave the COM-created `msaccess.exe` alive until some later nondeterministic GC releases the RCW. Given the stated goal—reliably terminate the specific Access instance created—this is a real resource-leak/correctness gap.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Com/AccessComSession.cs constructor",
      "summary": "If Application.OpenCurrentDatabase(...) throws after Access.Application is created, the constructor exits without releasing or terminating the created Access COM server.",
      "fix": "Wrap post-CreateInstance initialization in a try/catch; on any exception after COM creation, best-effort call Quit(2) and always FinalReleaseComObject(app) in a finally (and optionally capture PID immediately after instantiation so the exact-PID reaper can also be used on constructor-failure paths), then rethrow."
    }
  ]
}
```

---

# Verification Round 3

I reviewed the Session 1 code in:

- `AccessHarvester.Core/Com/AccessComSession.cs`
- `AccessHarvester.Core/Com/ComProcessReaper.cs`
- `AccessHarvester.Core/Com/IProcessController.cs`
- `AccessHarvester.Core/Com/SystemProcessController.cs`
- `tests/AccessHarvester.Tests/Com/ComProcessReaperTests.cs`

Overall, the three previously raised issues do appear to be fixed correctly, and I do not see a new wrong-PID, leak, or teardown-order bug in the Session 1 scope.

1. **PID capture on construction**: In `AccessComSession.AccessComSession(string databasePath, IProcessController processController)`, the PID is now captured immediately after `Activator.CreateInstance(accessType)` and assignment to `Application`, before `OpenCurrentDatabase(...)`. That directly addresses the constructor-leak problem from round 2. The helper `CaptureAccessPid()` in `AccessComSession.cs` reads `Application.hWndAccessApp` inside a `try/catch`, zero-checks the returned handle, calls the `user32.dll` `GetWindowThreadProcessId` P/Invoke, and rejects PID `0` by returning `(null, warning)`. The result is stored in `AccessProcessId`, and a diagnostic is exposed through `PidCaptureWarning`. That means failed capture degrades safely to `null`, and `ComProcessReaper` will no-op rather than ever targeting PID 0. I also confirmed the constructor does a second capture attempt after `OpenCurrentDatabase(...)` only if the early capture was null, which is a sensible recovery for cases where `hWndAccessApp` is not ready immediately after COM activation. One subtle point: if the early capture is null and `OpenCurrentDatabase(...)` throws before the second attempt, the constructor-failure cleanup will still have no PID to kill. That is not a regression or a wrong-PID risk; it is the safe degradation explicitly required by acceptance step 1.

2. **Dispose / teardown strengthening**: `AccessComSession.Dispose()` now delegates to a shared `TeardownCom()` method, and the constructor failure path also uses that same method. In `TeardownCom()`, `_disposed` is checked and set up front, so the teardown is idempotent across `Dispose()` and constructor-failure cleanup. The COM shutdown ordering is now correct: `Application?.Quit(2)` is in an inner `try`, and `Marshal.FinalReleaseComObject(Application)` is in the corresponding `finally`, with an outer `catch` suppressing graceful-cleanup errors. This exactly fixes the original round-1 bug where a `Quit(...)` failure could skip `FinalReleaseComObject(...)`. I also confirmed the code runs:
   - `GC.Collect();`
   - `GC.WaitForPendingFinalizers();`
   - `GC.Collect();`
   - `GC.WaitForPendingFinalizers();`
   
   exactly twice, in the correct order, and only after the COM release attempt. After those two GC/finalizer passes, `TeardownCom()` invokes the reaper: `new ComProcessReaper(_processController).EnsureTerminated(AccessProcessId, GracefulExitWaitMs);`. So the force-kill decision happens after the graceful COM-release phase, which matches the session design.

3. **Exact-PID kill logic**: `ComProcessReaper.EnsureTerminated(int? capturedPid, int waitTimeoutMs)` in `ComProcessReaper.cs` correctly implements the requested decision tree:
   - `null` PID => return `false`, no controller calls
   - not running => return `false`, no kill
   - exits during bounded wait => return `false`, no kill
   - survives wait => `_controller.Kill(pid)` once, then return `true`
   
   There is no image-name lookup anywhere in this logic. The production implementation in `SystemProcessController.Kill(int pid)` uses `Process.GetProcessById(pid)`, then `proc.Kill()` and `proc.WaitForExit()`, so it targets the exact captured PID only.

4. **`SystemProcessController` exception handling**: In `SystemProcessController.cs`, all three methods treat `ArgumentException` as the already-exited success case:
   - `IsRunning(int pid)` catches `ArgumentException` and returns `false`
   - `WaitForExit(int pid, int timeoutMs)` catches `ArgumentException` and returns `true`
   - `Kill(int pid)` catches `ArgumentException` and returns silently
   
   The round-1/round-2 race fix is also present: `Kill(int pid)` now catches `InvalidOperationException`, so if the process exits between `GetProcessById(...)` and `Kill()/WaitForExit()`, that is treated as success. That fix is correct.

5. **Test seam and hermetic coverage**: `IProcessController` is present exactly as required in `IProcessController.cs`, with `IsRunning`, `WaitForExit`, and `Kill`. `ComProcessReaper` depends only on that interface, so the kill-decision logic is cleanly isolated from COM and Win32. In `tests/AccessHarvester.Tests/Com/ComProcessReaperTests.cs`, the fake-based hermetic tests cover the required decision cases:
   - `Constructor_NullController_ThrowsArgumentNullException`
   - `EnsureTerminated_NullPid_ReturnsFalseAndControllerIsUntouched`
   - `EnsureTerminated_ProcessAlreadyExited_ReturnsFalseAndDoesNotWaitOrKill`
   - `EnsureTerminated_ProcessExitsDuringWait_ReturnsFalseAndDoesNotKill`
   - `EnsureTerminated_ProcessDoesNotExitDuringWait_ReturnsTrueAndKillsExactPid`
   
   The exact PID assertion is present in the zombie case via `Assert.Equal(pid, fakeController.KilledPid)`. The production `SystemProcessController` also has no-Access tests for the non-existent-process `ArgumentException` behavior:
   - `SystemProcessController_IsRunning_NonExistentProcess_ReturnsFalse`
   - `SystemProcessController_WaitForExit_NonExistentProcess_ReturnsTrue`
   - `SystemProcessController_Kill_NonExistentProcess_DoesNotThrow`
   
   Those are sufficient for the acceptance criteria. There is not a dedicated test for the `InvalidOperationException` race in `Kill(int)`, but the code fix itself is present and correct, and that specific race is difficult to force deterministically without a more elaborate harness.

6. **Session 1 scope / build-run scope**: From the source snapshot alone, I cannot independently prove which test commands were run, but the changes are consistent with the requested Session 1 scope: the COM/Access consumers outside `AccessComSession` were not edited, there is a hermetic test file focused on `ComProcessReaper`/`SystemProcessController`, and nothing here suggests the Access-COM/PDF tier was touched. That matches the traceability note you provided.

7. **Additional correctness review**: I do not see a path that kills the wrong process. The only kill path is exact-PID based, and a missing PID remains `null` rather than defaulting to `0`. I also do not see the prior COM-release leak anymore: `FinalReleaseComObject` is now structurally guaranteed by `finally` whenever `Application` exists. `_disposed` is set before teardown work begins, which makes the shared teardown idempotent and prevents double-kill/double-release on repeated `Dispose()` calls. I do not see a flaw in the `using var proc = Process.GetProcessById(pid)` pattern as used here.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
