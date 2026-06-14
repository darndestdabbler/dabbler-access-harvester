using System.Linq;
using AccessHarvester.Pipeline.Enrichment;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

/// <summary>
/// Hermetic pins for <see cref="SwitchboardStructureBuilder"/> (012 R2-1 /
/// DW-9). Verifies the generic page-grouping rule (ParentID-if-present else
/// SwitchboardID) handles both a canonical Switchboard Manager table and a
/// hand-rolled custom (ParentID-hierarchy) table, that raw Command codes are
/// preserved undecoded, that provenance is populated, and that orphan
/// (null page-key) rows are counted rather than dropped.
/// </summary>
public sealed class SwitchboardStructureBuilderTests
{
    private static SwitchboardRawRow Row(
        int sid, int item, string text, int cmd, string? arg = null, int? parent = null)
        => new()
        {
            SwitchboardID = sid,
            ItemNumber = item,
            ItemText = text,
            Command = cmd,
            Argument = arg,
            ParentID = parent,
        };

    [Fact]
    public void Null_or_empty_artifact_returns_null()
    {
        Assert.Null(SwitchboardStructureBuilder.Build(null));
        Assert.Null(SwitchboardStructureBuilder.Build(new SwitchboardItemsArtifact()));
    }

    [Fact]
    public void Canonical_table_groups_by_SwitchboardID()
    {
        // Wizard-style: SwitchboardID is the (non-unique) page id, ItemNumber 0
        // is the page header. No ParentID column.
        var artifact = new SwitchboardItemsArtifact
        {
            DetectedTables = new[] { "Switchboard Items" },
            Tables = new[]
            {
                new SwitchboardTableRows
                {
                    Table = "Switchboard Items",
                    HasParentId = false,
                    Rows = new[]
                    {
                        Row(1, 0, "Main Menu", 0),
                        Row(1, 1, "Open Clients", 3, "frmClients"),
                        Row(1, 2, "Reports", 1, "2"),
                        Row(2, 0, "Reports Menu", 0),
                        Row(2, 1, "Client Report", 4, "rptClients"),
                    },
                },
            },
        };

        var s = SwitchboardStructureBuilder.Build(artifact)!;
        Assert.Equal(0, s.UnlinkedItemCount);
        Assert.Equal(2, s.Pages.Count);

        var main = s.Pages[0];
        Assert.Equal(1, main.SwitchboardId);
        Assert.Equal("Switchboard Items", main.Table);
        Assert.Equal("Main Menu", main.Name);          // ItemNumber-0 header
        Assert.Equal(3, main.Items.Count);             // header preserved as an item
        Assert.Equal(new int?[] { 0, 3, 1 }, main.Items.Select(i => i.Command)); // raw, undecoded
        Assert.Equal("frmClients", main.Items[1].Argument);

        var reports = s.Pages[1];
        Assert.Equal(2, reports.SwitchboardId);
        Assert.Equal("Reports Menu", reports.Name);
    }

    [Fact]
    public void Custom_table_groups_by_ParentID_and_counts_orphans()
    {
        // HomeHealthCare-shape: autonumber SwitchboardID (unique), ParentID is
        // the real page key, command codes are custom (0=label,1=form,3=report),
        // and null-ParentID rows are non-navigation audit data.
        var artifact = new SwitchboardItemsArtifact
        {
            DetectedTables = new[] { "tblSwitchboardItems" },
            Tables = new[]
            {
                new SwitchboardTableRows
                {
                    Table = "tblSwitchboardItems",
                    HasParentId = true,
                    Rows = new[]
                    {
                        Row(1, 1, "Home Health Care", 0, "", 0),
                        Row(2, 2, "Client Search", 1, "frmClientSearch", 0),
                        Row(11, 1, "Reports Menu", 0, "", 1),
                        Row(12, 2, "Client Profile", 3, "rptClientProfile", 1),
                        Row(21, 99, "Plan 1 status", 0, null, null),
                        Row(22, 99, "Plan 2 status", 0, null, null),
                    },
                },
            },
        };

        var s = SwitchboardStructureBuilder.Build(artifact)!;
        Assert.Equal(2, s.UnlinkedItemCount);          // the two null-ParentID rows
        Assert.Equal(2, s.Pages.Count);

        var pageZero = s.Pages.Single(p => p.SwitchboardId == 0);
        Assert.Equal("Home Health Care", pageZero.Name);
        Assert.Equal(2, pageZero.Items.Count);

        var pageOne = s.Pages.Single(p => p.SwitchboardId == 1);
        Assert.Equal("Reports Menu", pageOne.Name);
        var report = pageOne.Items.Single(i => i.Text == "Client Profile");
        Assert.Equal(3, report.Command);               // raw 3, NOT decoded to canonical "open form browse"
        Assert.Equal("rptClientProfile", report.Argument);
    }

    [Fact]
    public void Item_provenance_traces_to_source_row()
    {
        var artifact = new SwitchboardItemsArtifact
        {
            DetectedTables = new[] { "tblSwitchboardItems" },
            Tables = new[]
            {
                new SwitchboardTableRows
                {
                    Table = "tblSwitchboardItems",
                    HasParentId = true,
                    Rows = new[] { Row(7, 3, "Manage Rate Limits", 1, "frmRateLimits", 0) },
                },
            },
        };

        var item = SwitchboardStructureBuilder.Build(artifact)!.Pages.Single().Items.Single();
        var p = item.Provenance!;
        Assert.Equal("switchboard-row", p.Source);
        Assert.Equal("tblSwitchboardItems", p.Table);
        Assert.Equal(7, p.SwitchboardId);
        Assert.Equal(3, p.ItemNumber);
        Assert.Equal(0, p.ParentId);
    }

    [Fact]
    public void Empty_argument_normalizes_to_null()
    {
        var artifact = new SwitchboardItemsArtifact
        {
            DetectedTables = new[] { "Switchboard Items" },
            Tables = new[]
            {
                new SwitchboardTableRows
                {
                    Table = "Switchboard Items",
                    HasParentId = false,
                    Rows = new[] { Row(1, 0, "Header", 0, "   ") },
                },
            },
        };

        var item = SwitchboardStructureBuilder.Build(artifact)!.Pages.Single().Items.Single();
        Assert.Null(item.Argument);
    }

    [Fact]
    public void Null_command_and_ids_are_preserved_not_coerced_to_zero()
    {
        // A row with no stored Command must stay null, not collapse to 0
        // (which would be indistinguishable from a real command-0 label).
        var artifact = new SwitchboardItemsArtifact
        {
            DetectedTables = new[] { "Switchboard Items" },
            Tables = new[]
            {
                new SwitchboardTableRows
                {
                    Table = "Switchboard Items",
                    HasParentId = false,
                    Rows = new[]
                    {
                        new SwitchboardRawRow
                        {
                            SwitchboardID = 1, ItemNumber = null, ItemText = "Mystery",
                            Command = null, Argument = null,
                        },
                    },
                },
            },
        };

        var item = SwitchboardStructureBuilder.Build(artifact)!.Pages.Single().Items.Single();
        Assert.Null(item.Command);
        Assert.Null(item.Provenance!.ItemNumber);
    }

    [Fact]
    public void Multiple_tables_keep_distinct_page_source_identity()
    {
        var artifact = new SwitchboardItemsArtifact
        {
            DetectedTables = new[] { "Switchboard Items", "tblSwitchboardItems" },
            Tables = new[]
            {
                new SwitchboardTableRows
                {
                    Table = "Switchboard Items",
                    HasParentId = false,
                    Rows = new[] { Row(1, 0, "Wizard Main", 0) },
                },
                new SwitchboardTableRows
                {
                    Table = "tblSwitchboardItems",
                    HasParentId = true,
                    Rows = new[] { Row(5, 1, "Custom Main", 0, parent: 0) },
                },
            },
        };

        var pages = SwitchboardStructureBuilder.Build(artifact)!.Pages;
        Assert.Equal(2, pages.Count);
        // Both pages have page key matching their own table's convention,
        // and Table disambiguates them.
        Assert.Contains(pages, p => p.Table == "Switchboard Items" && p.SwitchboardId == 1);
        Assert.Contains(pages, p => p.Table == "tblSwitchboardItems" && p.SwitchboardId == 0);
    }
}
