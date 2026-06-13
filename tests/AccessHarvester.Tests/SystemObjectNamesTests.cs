using AccessHarvester.Core.Extraction;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Pins the one shared system/temporary object-name predicate (006 F-1-7) so the raw
/// exporters and the DAO/OLE&#160;DB schema reader agree on which objects to skip:
/// <c>MSys*</c> case-insensitive, <c>~</c> ordinal.
/// </summary>
public class SystemObjectNamesTests
{
    [Theory]
    [InlineData("MSysObjects")]
    [InlineData("MSysQueries")]
    [InlineData("MSYSOBJECTS")]   // case drift must still be caught (OrdinalIgnoreCase)
    [InlineData("msysAccessStorage")]
    [InlineData("~sq_cFoo")]       // temp/hidden query
    [InlineData("~TMPCLP")]
    public void Recognizes_system_and_temp_names(string name)
        => Assert.True(SystemObjectNames.IsSystemObjectName(name));

    [Theory]
    [InlineData("tblClients")]
    [InlineData("qryClientList")]
    [InlineData("MyMSysReport")]   // MSys not at the start is a user object
    [InlineData("Order~Detail")]   // ~ not at the start is a user object
    public void Allows_user_object_names(string name)
        => Assert.False(SystemObjectNames.IsSystemObjectName(name));
}
