using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Cleanup;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Cleanup;

public class NamePatternDetectorTests
{
    private static IReadOnlyList<NamePatternSignal> Run(string name, string type,
        params (string Name, string Type)[] peers)
        => NamePatternDetector.Detect(name, type, peers);

    // ── Vocabulary ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tblClients_OLD")]
    [InlineData("frmBackup")]
    [InlineData("qryReports_Archive")]
    [InlineData("OldReports")]
    [InlineData("frmCopyOfClients")]
    public void Vocabulary_Matches(string name)
    {
        var sigs = Run(name, "Form");
        Assert.Contains(sigs, s => s.Category == "vocabulary");
    }

    [Theory]
    [InlineData("frmclients")]      // no vocabulary token at all
    [InlineData("tblOrders")]
    public void Vocabulary_Does_Not_Match(string name)
    {
        var sigs = Run(name, "Form");
        Assert.DoesNotContain(sigs, s => s.Category == "vocabulary");
    }

    // ── Year ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tblData_2023")]
    [InlineData("Reports_2024")]
    [InlineData("qry-2025")]
    public void Year_Matches(string name)
    {
        var sigs = Run(name, "Form");
        Assert.Contains(sigs, s => s.Category == "year");
    }

    [Theory]
    [InlineData("tblID1234")]
    [InlineData("tbl_3000")]
    public void Year_Does_Not_Match(string name)
    {
        var sigs = Run(name, "Table");
        Assert.DoesNotContain(sigs, s => s.Category == "year");
    }

    // ── Year-Month ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Reports_2024Jan")]
    [InlineData("Jan2024_Backup")]
    [InlineData("qry_2024-01")]
    [InlineData("qry_01-2024")]
    public void YearMonth_Matches(string name)
    {
        var sigs = Run(name, "Query");
        Assert.Contains(sigs, s => s.Category == "year-month");
    }

    // ── Quarter ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Q12025")]
    [InlineData("2025Q1")]
    [InlineData("Q1_2025")]
    [InlineData("2025-Q1")]
    public void Quarter_Matches(string name)
    {
        var sigs = Run(name, "Form");
        Assert.Contains(sigs, s => s.Category == "quarter");
    }

    // ── Numeric-duplicate ──────────────────────────────────────────────────────

    [Fact]
    public void NumericDuplicate_Fires_When_Base_Exists()
    {
        var peers = new[]
        {
            ("frmClients", "Form"),
            ("frmClients2", "Form"),
        };
        var sigs = Run("frmClients2", "Form", peers);
        Assert.Contains(sigs, s => s.Category == "numeric-duplicate");
    }

    [Fact]
    public void NumericDuplicate_Does_Not_Fire_When_Base_Missing()
    {
        var peers = new[]
        {
            ("frmClients2", "Form"),
        };
        var sigs = Run("frmClients2", "Form", peers);
        Assert.DoesNotContain(sigs, s => s.Category == "numeric-duplicate");
    }

    [Fact]
    public void NumericDuplicate_Scope_Is_Same_Object_Type()
    {
        // frmClients exists but as a Form, not a Table — does not count for Table peer.
        var peers = new[]
        {
            ("frmClients", "Form"),
            ("tblClients2", "Table"),
        };
        var sigs = NamePatternDetector.Detect("tblClients2", "Table", peers);
        Assert.DoesNotContain(sigs, s => s.Category == "numeric-duplicate");
    }

    // ── Exclusions ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("MSysObjects", "Table")]
    [InlineData("USysSettings", "Table")]
    public void System_Objects_Are_Excluded(string name, string type)
    {
        Assert.True(NamePatternDetector.IsExcludedSystemObject(type, name));
    }

    [Theory]
    [InlineData("Form_frmClients")]
    [InlineData("Report_rptOrders")]
    public void CodeBehind_Modules_Are_Excluded(string name)
    {
        Assert.True(NamePatternDetector.IsCodeBehindModuleName(name));
    }

    [Fact]
    public void Multiple_Signals_For_Same_Name()
    {
        var sigs = Run("tblClients_OLD_2023", "Table");
        Assert.Contains(sigs, s => s.Category == "vocabulary");
        Assert.Contains(sigs, s => s.Category == "year");
    }
}
