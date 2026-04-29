using System.Collections.Generic;
using AccessHarvester.SymbolDb.Extraction;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Hermetic unit tests for the Old-companion deferral filter. Northwind has
// no Old-companions in the Session 1 input set, but the filter must still
// be exercised so a future fixture (HomeHealthCare in a follow-up scaling
// set) gets correct behaviour without a separate test pass.
public sealed class DeferralPreFilterTests
{
    private static readonly DeferralPreFilter Filter = new();

    [Fact]
    public void Classify_FrmFooOld_FlagsAsDeferredWhenCompanionExists()
    {
        var modules = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "frmFoo", "frmFoo_Old"
        };
        var outcome = Filter.Classify("frmFoo_Old", modules);
        Assert.True(outcome.IsDeferred);
        Assert.Equal("frmFoo", outcome.CompanionName);
        Assert.Contains("frmFoo", outcome.Reason);
    }

    [Fact]
    public void Classify_OldUnderscorePrefix_FlagsAsDeferred()
    {
        var modules = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "frmFoo", "Old_frmFoo"
        };
        var outcome = Filter.Classify("Old_frmFoo", modules);
        Assert.True(outcome.IsDeferred);
        Assert.Equal("frmFoo", outcome.CompanionName);
    }

    [Fact]
    public void Classify_OldNameWithoutCompanion_NotFlagged()
    {
        var modules = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "frmOld" // no "frm" companion exists
        };
        var outcome = Filter.Classify("frmOld", modules);
        Assert.False(outcome.IsDeferred);
        Assert.Null(outcome.Reason);
    }

    [Fact]
    public void Classify_NormalName_NotFlagged()
    {
        var modules = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "frmCompanyDetail", "modStrings"
        };
        var outcome = Filter.Classify("frmCompanyDetail", modules);
        Assert.False(outcome.IsDeferred);
    }
}
