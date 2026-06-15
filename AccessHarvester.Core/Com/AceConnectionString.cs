using System.Data.OleDb;
using System.Runtime.Versioning;

namespace AccessHarvester.Core.Com;

/// <summary>
/// Builds the ACE OLE DB connection string used by every OLE DB reader/sampler
/// in this namespace (006 F-1-5).
/// </summary>
/// <remarks>
/// The four call sites previously interpolated the database path straight into
/// <c>"Provider={provider};Data Source={path};Persist Security Info=False;"</c>.
/// A path containing a <c>;</c> (a legal NTFS filename character) or a stray
/// quote broke connection-string parsing, and the <c>BuildConnectionString</c>
/// catch then conflated "malformed connection string" with "ACE provider
/// missing" — reporting a misleading provider-install error for what is really
/// a path-quoting problem. <see cref="OleDbConnectionStringBuilder"/> sets and
/// quotes <c>Data Source</c> correctly (wrapping in double quotes and doubling
/// embedded quotes as needed), so the value round-trips regardless of the
/// characters in the path. This is robustness against operator-supplied paths,
/// not a security boundary.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class AceConnectionString
{
    /// <summary>
    /// Returns a correctly-quoted ACE OLE DB connection string for
    /// <paramref name="provider"/> against <paramref name="databasePath"/>.
    /// </summary>
    public static string Build(string provider, string databasePath)
    {
        var builder = new OleDbConnectionStringBuilder
        {
            Provider = provider,
            DataSource = databasePath,
        };
        builder["Persist Security Info"] = false;
        return builder.ConnectionString;
    }
}
