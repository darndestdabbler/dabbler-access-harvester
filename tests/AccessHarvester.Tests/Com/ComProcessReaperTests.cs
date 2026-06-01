using System.Diagnostics;
using System.Runtime.Versioning;
using AccessHarvester.Core.Com;
using Xunit;

namespace AccessHarvester.Tests.Com;

// Hermetic unit tests for the COM zombie-process force-kill decision logic
// (ComProcessReaper) and the production IProcessController (SystemProcessController).
// No Access / COM instance is required; the decision logic is driven through a fake,
// and the SystemProcessController cases use only this test runner's own process state.
// Marked Windows-only because AccessHarvester.Core is assembly-gated to Windows.
[SupportedOSPlatform("windows")]
public sealed class ComProcessReaperTests
{
    // A hand-written recording fake for hermetic testing of the decision logic.
    private sealed class FakeProcessController : IProcessController
    {
        public bool IsRunningResult { get; set; }
        public bool WaitForExitResult { get; set; }

        public int IsRunningCalls { get; private set; }
        public int WaitForExitCalls { get; private set; }
        public int KillCalls { get; private set; }
        public int? KilledPid { get; private set; }
        public int? WaitTimeoutMs { get; private set; }

        public bool IsRunning(int pid)
        {
            IsRunningCalls++;
            return IsRunningResult;
        }

        public bool WaitForExit(int pid, int timeoutMs)
        {
            WaitForExitCalls++;
            WaitTimeoutMs = timeoutMs;
            return WaitForExitResult;
        }

        public void Kill(int pid)
        {
            KillCalls++;
            KilledPid = pid;
        }
    }

    // --- ComProcessReaper decision-logic tests ---

    [Fact]
    public void Constructor_NullController_ThrowsArgumentNullException()
    {
        // Arrange
        IProcessController? controller = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>("controller", () => new ComProcessReaper(controller!));
    }

    [Fact]
    public void EnsureTerminated_NullPid_ReturnsFalseAndControllerIsUntouched()
    {
        // Arrange — PID was never captured (e.g. hWndAccessApp was zero).
        var fakeController = new FakeProcessController();
        var reaper = new ComProcessReaper(fakeController);

        // Act
        var result = reaper.EnsureTerminated(null, 5000);

        // Assert — degrade safely: no probing, no kill (PID 0 never targeted).
        Assert.False(result);
        Assert.Equal(0, fakeController.IsRunningCalls);
        Assert.Equal(0, fakeController.WaitForExitCalls);
        Assert.Equal(0, fakeController.KillCalls);
    }

    [Fact]
    public void EnsureTerminated_ProcessAlreadyExited_ReturnsFalseAndDoesNotWaitOrKill()
    {
        // Arrange — graceful Quit + finalizers already dropped the process.
        const int pid = 1234;
        var fakeController = new FakeProcessController { IsRunningResult = false };
        var reaper = new ComProcessReaper(fakeController);

        // Act
        var result = reaper.EnsureTerminated(pid, 5000);

        // Assert
        Assert.False(result);
        Assert.Equal(1, fakeController.IsRunningCalls);
        Assert.Equal(0, fakeController.WaitForExitCalls);
        Assert.Equal(0, fakeController.KillCalls);
    }

    [Fact]
    public void EnsureTerminated_ProcessExitsDuringWait_ReturnsFalseAndDoesNotKill()
    {
        // Arrange — still alive at the probe, but exits within the bounded wait
        // (a slow exit is not a zombie).
        const int pid = 1234;
        const int waitMs = 5000;
        var fakeController = new FakeProcessController { IsRunningResult = true, WaitForExitResult = true };
        var reaper = new ComProcessReaper(fakeController);

        // Act
        var result = reaper.EnsureTerminated(pid, waitMs);

        // Assert
        Assert.False(result);
        Assert.Equal(1, fakeController.IsRunningCalls);
        Assert.Equal(1, fakeController.WaitForExitCalls);
        Assert.Equal(waitMs, fakeController.WaitTimeoutMs);
        Assert.Equal(0, fakeController.KillCalls);
    }

    [Fact]
    public void EnsureTerminated_ProcessDoesNotExitDuringWait_ReturnsTrueAndKillsExactPid()
    {
        // Arrange — true zombie: alive at probe and still alive after the bounded wait.
        const int pid = 1234;
        const int waitMs = 5000;
        var fakeController = new FakeProcessController { IsRunningResult = true, WaitForExitResult = false };
        var reaper = new ComProcessReaper(fakeController);

        // Act
        var result = reaper.EnsureTerminated(pid, waitMs);

        // Assert — kills exactly once, targeting the exact captured PID.
        Assert.True(result);
        Assert.Equal(1, fakeController.IsRunningCalls);
        Assert.Equal(1, fakeController.WaitForExitCalls);
        Assert.Equal(1, fakeController.KillCalls);
        Assert.Equal(pid, fakeController.KilledPid);
    }

    // --- SystemProcessController production-impl tests (Windows host, no Access) ---

    [Fact]
    public void SystemProcessController_IsRunning_CurrentProcess_ReturnsTrue()
    {
        // Arrange
        var controller = new SystemProcessController();
        var currentPid = Process.GetCurrentProcess().Id;

        // Act
        var isRunning = controller.IsRunning(currentPid);

        // Assert
        Assert.True(isRunning);
    }

    [Fact]
    public void SystemProcessController_IsRunning_NonExistentProcess_ReturnsFalse()
    {
        // Arrange — int.MaxValue can never be a live PID, so GetProcessById throws
        // ArgumentException, which the impl treats as already-exited.
        var controller = new SystemProcessController();

        // Act
        var isRunning = controller.IsRunning(int.MaxValue);

        // Assert
        Assert.False(isRunning);
    }

    [Fact]
    public void SystemProcessController_WaitForExit_NonExistentProcess_ReturnsTrue()
    {
        // Arrange
        var controller = new SystemProcessController();

        // Act
        var exited = controller.WaitForExit(int.MaxValue, 10);

        // Assert — a process that does not exist has "exited".
        Assert.True(exited);
    }

    [Fact]
    public void SystemProcessController_Kill_NonExistentProcess_DoesNotThrow()
    {
        // Arrange
        var controller = new SystemProcessController();

        // Act — ArgumentException ("no such process") is the success case.
        var exception = Record.Exception(() => controller.Kill(int.MaxValue));

        // Assert
        Assert.Null(exception);
    }
}
