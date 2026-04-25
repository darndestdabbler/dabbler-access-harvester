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
    public dynamic Application { get; }
    public string DatabaseName { get; }
    private bool _disposed;

    public AccessComSession(string databasePath)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException($"Database not found: {databasePath}");

        Type accessType = Type.GetTypeFromProgID("Access.Application")
                          ?? throw new InvalidOperationException("Microsoft Access is not installed or registered.");

        var app = Activator.CreateInstance(accessType)
                  ?? throw new InvalidOperationException("Failed to instantiate Access.Application.");
        Application = app;

        Application.OpenCurrentDatabase(databasePath, false);
        DatabaseName = Path.GetFileNameWithoutExtension(databasePath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            Application?.Quit(2); // acQuitSaveNone
            if (Application is not null)
            {
                Marshal.ReleaseComObject(Application);
            }
        }
        catch
        {
            // Suppress cleanup errors — match existing behavior.
        }
        _disposed = true;
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
