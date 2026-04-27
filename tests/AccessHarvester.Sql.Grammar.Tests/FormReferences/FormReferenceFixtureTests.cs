using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Access;
using AccessHarvester.Sql.Parser;
using AccessHarvester.Sql.Parser.FormReferences;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.Declarations;
using Xunit;

namespace AccessHarvester.Sql.Grammar.Tests.FormReferences;

// E2E cross-cut: parse fixture 04 SQL, resolve every [Forms]!... reference
// through an AccessObjectModelRegistry that mirrors frmDistrictReport's
// two declared controls, and confirm the resolver produces the expected
// annotations.  Session 3 of jet-sql-resolution-and-dynamic.
public sealed class FormReferenceFixtureTests
{
    // Fixture 04 — full SQL from docs/test-cases/04-saved-query-form-params.sql
    // (comment header stripped; SQL body preserved verbatim).
    private const string Fixture04Sql = """
        PARAMETERS [Forms]![frmDistrictReport]![cboDistrict] Long,
                   [Forms]![frmDistrictReport]![txtAsOfDate] DateTime;
        SELECT
            c.CaseID,
            c.CaseNumber,
            c.OpenedDate,
            Nz(cw.CaseworkerName, "Unassigned") AS Caseworker,
            cl.ClientLastName & ", " & cl.ClientFirstName AS ClientName,
            DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) AS DaysOpen,
            IIf(DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) > 90,
                "Overdue",
                IIf(DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) > 60,
                    "At Risk",
                    "On Track")) AS StatusFlag,
            c.Priority,
            c.LastActivityDate
        FROM ((tblCases AS c
            LEFT JOIN tblCaseworkers AS cw ON c.AssignedCaseworkerID = cw.CaseworkerID)
            INNER JOIN tblClients AS cl ON c.ClientID = cl.ClientID)
        WHERE
            c.DistrictID = [Forms]![frmDistrictReport]![cboDistrict]
            AND c.CaseStatus IN ('Open', 'Pending Review', 'Awaiting Documents')
            AND c.OpenedDate <= [Forms]![frmDistrictReport]![txtAsOfDate]
            AND (c.ClosedDate IS NULL OR c.ClosedDate > [Forms]![frmDistrictReport]![txtAsOfDate])
        ORDER BY
            IIf(c.Priority = 'High', 1, IIf(c.Priority = 'Medium', 2, 3)),
            c.OpenedDate;
        """;

    private static AccessObjectModelRegistry BuildRegistry() =>
        new(
            new FormMetadata[]
            {
                new()
                {
                    Name = "frmDistrictReport",
                    Children = new AccessControl[]
                    {
                        new()
                        {
                            Type = "Section",
                            Name = "Detail",
                            Children = new AccessControl[]
                            {
                                new() { Type = "ComboBox", Name = "cboDistrict" },
                                new() { Type = "TextBox", Name = "txtAsOfDate" },
                            },
                        },
                    },
                },
            },
            System.Array.Empty<ReportMetadata>(),
            new TypeLibraryRegistry());

    [Fact]
    public void Fixture04_AllFormReferences_ResolveSuccessfully()
    {
        var ast = SqlParserPipeline.Parse(Fixture04Sql)
                                   .BuildAst("04-saved-query-form-params.sql");
        var annotations = FormReferenceResolver.Resolve(ast, BuildRegistry());

        // 2 in PARAMETERS + 3 in SELECT + 3 in WHERE = 8 total
        Assert.Equal(8, annotations.Count);
        Assert.All(annotations, a => Assert.IsType<ResolvedFormReference>(a.Resolution));
    }

    [Fact]
    public void Fixture04_ResolvedTargets_AreControlsOnFrmDistrictReport()
    {
        var ast = SqlParserPipeline.Parse(Fixture04Sql)
                                   .BuildAst("04-saved-query-form-params.sql");
        var annotations = FormReferenceResolver.Resolve(ast, BuildRegistry());

        foreach (var a in annotations)
        {
            var resolved = Assert.IsType<ResolvedFormReference>(a.Resolution);
            var control = Assert.IsType<SyntheticControlDeclaration>(resolved.ResolvedTarget);
            Assert.True(
                control.Name == "cboDistrict" || control.Name == "txtAsOfDate",
                $"Unexpected control name: {control.Name}");
        }
    }

    [Fact]
    public void Fixture04_BothDeclaredControls_AppearInAnnotations()
    {
        var ast = SqlParserPipeline.Parse(Fixture04Sql)
                                   .BuildAst("04-saved-query-form-params.sql");
        var annotations = FormReferenceResolver.Resolve(ast, BuildRegistry());

        var resolvedControlNames = annotations
            .Select(a => a.Resolution)
            .OfType<ResolvedFormReference>()
            .Select(r => r.ResolvedTarget.Name)
            .ToHashSet();

        Assert.Contains("cboDistrict", resolvedControlNames);
        Assert.Contains("txtAsOfDate", resolvedControlNames);
    }

    [Fact]
    public void Fixture04_OriginalText_ContainsFrmDistrictReport()
    {
        var ast = SqlParserPipeline.Parse(Fixture04Sql)
                                   .BuildAst("04-saved-query-form-params.sql");
        var annotations = FormReferenceResolver.Resolve(ast, BuildRegistry());

        Assert.All(annotations, a =>
            Assert.Contains("frmDistrictReport", a.Resolution.OriginalText,
                System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Fixture04_EmptyRegistry_AllReferencesUnresolved_FormNotInMetadata()
    {
        var ast = SqlParserPipeline.Parse(Fixture04Sql)
                                   .BuildAst("04-saved-query-form-params.sql");
        var emptyRegistry = new AccessObjectModelRegistry(
            System.Array.Empty<FormMetadata>(),
            System.Array.Empty<ReportMetadata>(),
            new TypeLibraryRegistry());

        var annotations = FormReferenceResolver.Resolve(ast, emptyRegistry);

        Assert.NotEmpty(annotations);
        Assert.All(annotations, a =>
        {
            var unresolved = Assert.IsType<UnresolvedFormReference>(a.Resolution);
            Assert.Equal(UnresolvedReason.FormNotInMetadata, unresolved.Reason);
        });
    }

    [Fact]
    public void Fixture04_RegistryMissingControl_ProducesControlNotOnFormReason()
    {
        // Registry has frmDistrictReport but with no controls. All
        // second-segment Bang resolutions fail with ControlNotOnForm.
        var registryNoControls = new AccessObjectModelRegistry(
            new FormMetadata[]
            {
                new()
                {
                    Name = "frmDistrictReport",
                    // No children — no controls registered.
                },
            },
            System.Array.Empty<ReportMetadata>(),
            new TypeLibraryRegistry());
        var ast = SqlParserPipeline.Parse(Fixture04Sql)
                                   .BuildAst("04-saved-query-form-params.sql");

        var annotations = FormReferenceResolver.Resolve(ast, registryNoControls);

        Assert.NotEmpty(annotations);
        Assert.All(annotations, a =>
        {
            var unresolved = Assert.IsType<UnresolvedFormReference>(a.Resolution);
            Assert.Equal(UnresolvedReason.ControlNotOnForm, unresolved.Reason);
        });
    }
}
