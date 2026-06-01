using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace AccessHarvester.Core.Com;

public static class AcObjectType
{
    public const int acTable = 0;
    public const int acQuery = 1;
    public const int acForm = 2;
    public const int acReport = 3;
    public const int acMacro = 4;
    public const int acModule = 5;
}

public sealed class AccessComSession : IDisposable
{
    // How long to let a graceful Quit() + finalizers land before force-killing.
    // A slow exit is not a zombie; see docs/access-com-zombie-cleanup.md.
    private const int GracefulExitWaitMs = 5000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public dynamic Application { get; }
    public string DatabaseName { get; }

    /// <summary>
    /// PID of the exact <c>msaccess.exe</c> this session created, captured on
    /// construction. <c>null</c> if it could not be captured (e.g. <c>hWndAccessApp</c>
    /// was zero) — in which case the force-kill failsafe degrades to a no-op rather than
    /// risk targeting PID 0.
    /// </summary>
    public int? AccessProcessId { get; }

    /// <summary>
    /// Set when PID capture could not be completed, so callers/tests have a diagnostic
    /// signal instead of a silent failure. <c>null</c> when the PID was captured cleanly.
    /// </summary>
    public string? PidCaptureWarning { get; }

    private readonly IProcessController _processController;
    private bool _disposed;

    public AccessComSession(string databasePath)
        : this(databasePath, new SystemProcessController())
    {
    }

    // The IProcessController is injectable so the production failsafe path is explicit
    // and seam-tested; construction itself still requires Access/COM and so stays
    // Windows-gated. The kill-decision logic is unit-tested via ComProcessReaper directly.
    public AccessComSession(string databasePath, IProcessController processController)
    {
        ArgumentNullException.ThrowIfNull(processController);
        _processController = processController;

        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"Database not found: {databasePath}");

        Type accessType = Type.GetTypeFromProgID("Access.Application")
                          ?? throw new InvalidOperationException("Microsoft Access is not installed or registered.");

        var app = Activator.CreateInstance(accessType)
                  ?? throw new InvalidOperationException("Failed to instantiate Access.Application.");
        Application = app;

        // Capture the PID as early as possible — right after the COM server is created —
        // so that even a failure during OpenCurrentDatabase can be cleaned up by the
        // exact-PID failsafe rather than leaking the msaccess.exe we just spawned.
        (AccessProcessId, PidCaptureWarning) = CaptureAccessPid();

        try
        {
            Application.OpenCurrentDatabase(databasePath, false);
            DatabaseName = Path.GetFileNameWithoutExtension(databasePath);

            // Re-capture once the database/window is definitely up if the early attempt
            // came back empty (e.g. hWndAccessApp was not yet valid right after creation).
            if (AccessProcessId is null)
                (AccessProcessId, PidCaptureWarning) = CaptureAccessPid();
        }
        catch
        {
            // Construction failed after the COM server was created. Tear it down through
            // the same graceful + exact-PID failsafe path Dispose() uses, so we never
            // leak the instance, then rethrow so the caller sees the original failure.
            TeardownCom();
            throw;
        }
    }

    private (int? pid, string? warning) CaptureAccessPid()
    {
        int hwndRaw;
        try
        {
            hwndRaw = (int)Application.hWndAccessApp;
        }
        catch (Exception ex)
        {
            return (null, $"hWndAccessApp could not be read ({ex.GetType().Name}); PID not captured.");
        }

        if (hwndRaw == 0)
            return (null, "hWndAccessApp was zero (UI-suppressed?); PID not captured.");

        GetWindowThreadProcessId((IntPtr)hwndRaw, out uint pid);
        if (pid == 0)
            return (null, "GetWindowThreadProcessId returned PID 0; PID not captured.");

        return ((int)pid, null);
    }

    public void Dispose() => TeardownCom();

    // Shared two-layer teardown used by both Dispose() and the constructor-failure path.
    private void TeardownCom()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            try
            {
                Application?.Quit(2); // acQuitSaveNone
            }
            finally
            {
                // Always release the RCW, even when Quit threw — that is exactly the
                // case where the graceful path is failing and the explicit release
                // matters most for dropping the COM reference count toward zero.
                if (Application is not null)
                {
                    Marshal.FinalReleaseComObject(Application);
                }
            }
        }
        catch
        {
            // Suppress cleanup errors — the graceful path is best-effort; the
            // exact-PID failsafe below guarantees termination regardless.
        }

        // Graceful layer: force the RCWs to finalize so the COM ref count reaches zero
        // and Access quits on its own. Doubled because finalizing one RCW can queue others.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Failsafe layer: if the exact instance we created survived the graceful path,
        // force-kill that PID only. A null PID degrades to a no-op.
        new ComProcessReaper(_processController).EnsureTerminated(AccessProcessId, GracefulExitWaitMs);
    }
}
