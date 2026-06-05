using System.Collections.Generic;
using AccessHarvester.SymbolDb.Extraction;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Hermetic unit tests for the Old-companion deferral filter. The filter is
// exercised with synthetic module names so any fixture (whether or not it
// contains Old-companions) gets correct behaviour without a separate test pass.
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
            "frmSample", "modSample"
        };
        var outcome = Filter.Classify("frmSample", modules);
        Assert.False(outcome.IsDeferred);
    }
}
