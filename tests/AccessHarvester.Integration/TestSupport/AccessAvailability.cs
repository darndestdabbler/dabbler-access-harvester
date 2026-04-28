using System;
using Xunit;

namespace AccessHarvester.Integration.TestSupport;

internal static class AccessAvailability
{
    public static void SkipIfNoAccess()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Access COM is Windows-only.");
        // CA1416: GetTypeFromProgID is Windows-only; the Skip.IfNot above
        // gates the call at runtime so this is safe.
#pragma warning disable CA1416
        Type? accessType = Type.GetTypeFromProgID("Access.Application", throwOnError: false);
#pragma warning restore CA1416
        Skip.If(accessType is null, "Access.Application COM ProgID not registered on this host.");
    }
}
