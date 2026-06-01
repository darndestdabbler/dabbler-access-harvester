namespace AccessHarvester.Core.Com;

/// <summary>
/// Injectable seam over the handful of OS process operations the COM
/// zombie-failsafe needs. Exists so the "graceful-exited vs. zombie -> kill the
/// exact PID" decision in <see cref="ComProcessReaper"/> can be unit-tested with a
/// fake — the real, <c>System.Diagnostics.Process</c>-backed implementation
/// (<see cref="SystemProcessController"/>) is exercised only on a Windows host.
/// </summary>
/// <remarks>
/// All three members operate on a PID captured up front (while the Access window
/// still existed). Implementations must treat a PID that no longer maps to a live
/// process as the success case — a missing process means it already exited cleanly,
/// which is exactly the outcome the failsafe is trying to reach.
/// </remarks>
public interface IProcessController
{
    /// <summary>True if a process with <paramref name="pid"/> is currently alive.</summary>
    bool IsRunning(int pid);

    /// <summary>
    /// Wait up to <paramref name="timeoutMs"/> for the process to exit. Returns
    /// <c>true</c> if it exited within the window (or was already gone), <c>false</c>
    /// if it is still alive when the timeout elapses.
    /// </summary>
    bool WaitForExit(int pid, int timeoutMs);

    /// <summary>
    /// Force-terminate the exact process <paramref name="pid"/>. A PID that no longer
    /// exists is treated as success (already exited), not an error.
    /// </summary>
    void Kill(int pid);
}
