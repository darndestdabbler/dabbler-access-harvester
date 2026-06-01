# Killing Zombie `msaccess.exe` Processes in COM Automation

Reference and implementation guidance for cleanly shutting down Access instances
created via COM during automated testing. Target language is C# (.NET); the logic
ports to VB.NET, VBA, C++, and Python with equivalent APIs.

## Goal

Reliably terminate the *specific* Access instance a test created, without:
- killing unrelated `msaccess.exe` instances a user or parallel test owns, and
- leaving orphaned zombie processes behind when a graceful `Quit()` fails.

## Strategy (two layers)

1. **Root-cause fix** — make graceful shutdown actually work by letting the GC
   finalize the COM Runtime Callable Wrappers (RCWs) so the reference count reaches
   zero and Access quits on its own.
2. **Failsafe** — capture the instance's PID *immediately* on creation and force-kill
   by that exact PID only if the process survives the graceful path.

Capturing the PID up front is non-negotiable: once an instance is a true zombie you
can no longer query its window handle, so the PID must be recorded while the app is
still alive.

## Targeting the PID via `hWndAccessApp`

The Access `Application` object exposes `hWndAccessApp` (the main window handle).
Translate it to a PID with `GetWindowThreadProcessId` from `user32.dll`.

```csharp
[DllImport("user32.dll", SetLastError = true)]
static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
```

Notes:
- `hWndAccessApp` returns an `int`, so `(IntPtr)accessApp.hWndAccessApp` is valid.
- The handle is only meaningful once the Access window exists. Right after
  `new Application()` it normally is; if you suppress the UI, confirm it is non-zero
  before trusting it.

## Recommended implementation

Scope all COM usage inside a method so its locals become unreachable on return,
then force collection. As the *primary* pattern this is usually simpler and less
error-prone than scattering per-object `Marshal.ReleaseComObject` calls everywhere —
but it is not a substitute for releasing high-risk objects explicitly. Objects that
hold file locks or server-side state — DAO `Recordset`, `Database`, `QueryDef`,
`TableDef`, and similar — should still be closed and released promptly rather than
left to the GC, so locks drop on time. Treat the two as complementary: explicit
release for the objects that matter, GC scope-exit as the catch-all.

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class AccessAutomation
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Do all COM work here. Intermediate objects can be plain locals — the GC
    // will finalize their RCWs once this frame is gone.
    private uint RunAccessWork()
    {
        var accessApp = new Microsoft.Office.Interop.Access.Application();

        IntPtr hwnd = (IntPtr)accessApp.hWndAccessApp;
        GetWindowThreadProcessId(hwnd, out uint processId);

        // ... run tests against accessApp here ...
        //
        // Close and release lock-/state-holding COM objects explicitly as you
        // finish with them — do not wait for the GC for these:
        //     rs.Close();
        //     Marshal.ReleaseComObject(rs);
        // Plain, short-lived locals (and intermediate RCWs) can be left to the GC.

        try
        {
            accessApp.Quit(Microsoft.Office.Interop.Access.AcQuitOption.acQuitSaveNone);
        }
        catch
        {
            // Ignore COM errors during shutdown.
        }

        // Deliberately do NOT keep accessApp alive past this point.
        return processId;
    }

    public void RunAndCleanup()
    {
        uint pid = RunAccessWork();

        // The stack frame holding the COM locals is gone; force the RCWs to
        // finalize so the underlying COM references are released and Access quits.
        // The work is doubled because finalizing one COM object can queue others.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Failsafe: kill only the exact instance, and only if it survived.
        try
        {
            var proc = Process.GetProcessById((int)pid);

            // Give Quit() + finalizers a bounded chance to land before resorting
            // to a hard kill — a slow exit is not the same as a true zombie.
            if (!proc.HasExited && !proc.WaitForExit(5000))
            {
                proc.Kill();
                proc.WaitForExit();
            }
        }
        catch (ArgumentException)
        {
            // No process with that ID — it already exited cleanly. This is the
            // success case.
        }
    }
}
```

### Why the GC approach over the "Two-Dot Rule"

The common advice to "never chain COM calls" and to call
`Marshal.ReleaseComObject` on every object is fragile:

- A single `ReleaseComObject` only decrements the RCW count by one. If the count is
  higher, the object survives anyway — a frequent reason the "graceful" path silently
  fails and leaves a zombie.
- Intermediate RCWs from a property/method chain are **not** "un-killable." The GC
  finalizes them and releases the COM reference; the issue is *timing*, not an
  un-releasable object.

The real cause of a surviving process is a still-rooted reference (a local that has
not gone out of scope, or one the debugger is pinning) keeping the count above zero.
Scoping in a method + `GC.Collect()` addresses that directly.

That said, the hygiene lesson behind the "two-dot rule" is still worth keeping even
though its stated reasoning is wrong: avoid long COM call chains, avoid hidden
temporary wrappers, and keep object ownership obvious. Chained, property-heavy Office
automation is genuinely harder to reason about at cleanup time — not because the
intermediates are immortal, but because it obscures what you still hold.

> Debugger caveat: this can appear to "work in Release but not when stepping through,"
> because the debugger roots references and prevents collection. Test the cleanup path
> outside the debugger.

If a team prefers the disciplined `ReleaseComObject` style, the two approaches
coexist well — explicit release for lock-/state-holding objects, GC scope-exit as the
catch-all. The only claim to drop is the "un-killable reference" justification, which
is incorrect.

## Force-kill caveat

`proc.Kill()` on `msaccess.exe` can leave an orphaned lock file (`.laccdb` / `.ldb`)
and may corrupt or partially write the database. For a disposable test database that is
recreated each run this is acceptable; otherwise treat force-kill as a last resort and
plan for lock-file cleanup / DB recreation.

## Implementation checklist

- [ ] Capture the PID via `hWndAccessApp` → `GetWindowThreadProcessId` immediately after
      `new Application()`, before any test work.
- [ ] Confirm `hWndAccessApp` is non-zero before relying on it (especially if UI is hidden).
- [ ] Keep all COM object usage inside a method; let locals fall out of scope.
- [ ] Explicitly close/release lock- or state-holding COM objects (DAO `Recordset`,
      `Database`, `QueryDef`, `TableDef`) as you finish with them, rather than relying on the GC.
- [ ] Attempt `Quit(acQuitSaveNone)` inside a try/catch.
- [ ] Run `GC.Collect()` + `GC.WaitForPendingFinalizers()` twice after the work method returns.
- [ ] Allow a bounded `WaitForExit(timeout)` before force-killing; a slow exit is not a zombie.
- [ ] Force-kill by the captured PID only if it survives the wait; treat `ArgumentException` as success.
- [ ] Clean up `.laccdb` / `.ldb` lock files (or recreate the test DB) after any force-kill.
- [ ] Verify the cleanup path with the debugger detached, and under parallel runs if tests run concurrently.
- [ ] If zombies persist, audit the test/extraction code for COM references retained outside the work method.
