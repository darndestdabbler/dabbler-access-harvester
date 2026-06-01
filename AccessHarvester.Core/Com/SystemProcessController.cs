using System.Diagnostics;

namespace AccessHarvester.Core.Com;

/// <summary>
/// Production <see cref="IProcessController"/> backed by <see cref="System.Diagnostics.Process"/>.
/// Every operation treats an <see cref="ArgumentException"/> from
/// <see cref="Process.GetProcessById(int)"/> — i.e. "no process with that ID" — as the
/// success case: the process already exited, which is the outcome the failsafe wants.
/// </summary>
public sealed class SystemProcessController : IProcessController
{
    /// <inheritdoc/>
    public bool IsRunning(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        }
        catch (ArgumentException)
        {
            return false; // No such process — it already exited.
        }
    }

    /// <inheritdoc/>
    public bool WaitForExit(int pid, int timeoutMs)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return proc.WaitForExit(timeoutMs);
        }
        catch (ArgumentException)
        {
            return true; // No such process — already exited.
        }
    }

    /// <inheritdoc/>
    public void Kill(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            proc.Kill();
            proc.WaitForExit();
        }
        catch (ArgumentException)
        {
            // No such process — it already exited cleanly. This is the success case.
        }
        catch (InvalidOperationException)
        {
            // The process exited between GetProcessById and Kill/WaitForExit — it is
            // already gone, which is exactly the outcome we wanted. Treat as success.
        }
    }
}
