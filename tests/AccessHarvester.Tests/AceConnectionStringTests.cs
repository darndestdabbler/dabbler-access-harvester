using System.Data.OleDb;
using System.Runtime.Versioning;
using AccessHarvester.Core.Com;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="AceConnectionString"/> (006 F-1-5). No COM
/// is involved — <see cref="OleDbConnectionStringBuilder"/> is a pure parser.
/// Before this fix the four OLE DB readers interpolated the database path raw
/// into <c>Data Source={path}</c>, so a <c>;</c> (a legal NTFS filename char) or
/// a quote in the path broke connection-string parsing and the
/// <c>BuildConnectionString</c> catch then misreported it as "ACE provider
/// missing."
/// </summary>
[SupportedOSPlatform("windows")]
public class AceConnectionStringTests
{
    private const string Provider = "Microsoft.ACE.OLEDB.16.0";

    [Fact]
    public void Build_OrdinaryPath_RoundTripsDataSourceAndProvider()
    {
        string path = @"C:\data\HomeHealthCare.accdb";
        string cs = AceConnectionString.Build(Provider, path);

        var parsed = new OleDbConnectionStringBuilder(cs);
        Assert.Equal(path, parsed.DataSource);
        Assert.Equal(Provider, parsed.Provider);
        Assert.Equal(false, parsed["Persist Security Info"]);
    }

    [Fact]
    public void Build_SemicolonBearingPath_IsQuotedAndRoundTrips()
    {
        // A ';' is a legal NTFS filename character. Raw interpolation
        // ("Data Source=C:\a;b.accdb;...") would split the path across two
        // connection-string tokens; the builder quotes it so it round-trips.
        string path = @"C:\weird;name.accdb";
        string cs = AceConnectionString.Build(Provider, path);

        var parsed = new OleDbConnectionStringBuilder(cs);
        Assert.Equal(path, parsed.DataSource);
    }

    [Fact]
    public void Build_QuoteBearingPath_IsEscapedAndRoundTrips()
    {
        string path = "C:\\has\"quote.accdb";
        string cs = AceConnectionString.Build(Provider, path);

        var parsed = new OleDbConnectionStringBuilder(cs);
        Assert.Equal(path, parsed.DataSource);
    }
}
