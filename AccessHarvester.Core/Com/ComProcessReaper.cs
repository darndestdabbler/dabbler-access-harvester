namespace AccessHarvester.Core.Com;

/// <summary>
/// Decides whether the COM-created Access instance needs to be force-killed and, if
/// so, kills that exact PID — the failsafe layer of the two-layer pattern in
/// <c>docs/access-com-zombie-cleanup.md</c>. The graceful layer (Quit + doubled
/// <c>GC.Collect</c>/<c>WaitForPendingFinalizers</c>) runs in
/// <see cref="AccessComSession.Dispose"/> before this is invoked.
/// </summary>
/// <remarks>
/// Kept free of any COM or Win32 dependency so the kill-decision logic is fully
/// hermetic: it is driven entirely through an injected <see cref="IProcessController"/>.
/// The real session wires in <see cref="SystemProcessController"/>; tests inject a fake.
/// </remarks>
public sealed class ComProcessReaper
{
    private readonly IProcessController _controller;

    public ComProcessReaper(IProcessController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _controller = controller;
    }

    /// <summary>
    /// Give the graceful shutdown a bounded chance to land, then force-kill the exact
    /// captured PID only if it is still alive afterwards.
    /// </summary>
    /// <param name="capturedPid">
    /// The PID captured on construction, or <c>null</c> if it was never captured (e.g.
    /// <c>hWndAccessApp</c> was zero in a UI-suppressed edge case). A null PID means the
    /// failsafe degrades safely — no kill is attempted, so PID 0 is never targeted.
    /// </param>
    /// <param name="waitTimeoutMs">How long to wait for a graceful exit before killing.</param>
    /// <returns><c>true</c> if a force-kill was issued; <c>false</c> if no kill was needed.</returns>
    public bool EnsureTerminated(int? capturedPid, int waitTimeoutMs)
    {
        if (capturedPid is not int pid)
            return false; // PID never captured — degrade safely, never kill PID 0.

        if (!_controller.IsRunning(pid))
            return false; // Already exited (graceful Quit + finalizers did their job).

        if (_controller.WaitForExit(pid, waitTimeoutMs))
            return false; // Exited within the bounded wait — a slow exit is not a zombie.

        _controller.Kill(pid); // Survived the wait: a true zombie. Kill the exact PID only.
        return true;
    }
}
