using System.Linq;
using AccessHarvester.Access;
using AccessHarvester.Sql.Parser.DynamicSql;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using Xunit;
using VbaProcedureDeclaration = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

namespace AccessHarvester.Sql.Grammar.Tests.DynamicSql;

// E2E cross-cut: parse fixture 05 VBA, analyze RunFilteredCaseReport
// through DynamicSqlAnalyzer with frmFilterPanel controls registered,
// and confirm the 5-template combinatorial-blowup result.
// Session 3 of jet-sql-resolution-and-dynamic.
public sealed class DynamicSqlFixtureTests
{
    // Fixture 05 — full VBA source from
    // docs/test-cases/05-dynamic-sql-construction.bas (comment header stripped).
    private const string Fixture05Bas = """
        Public Function RunFilteredCaseReport(Optional ByVal sFilterArgs As String = "") As Long
            Dim sSQL As String
            Dim sWhere As String
            Dim sOrder As String
            Dim db As DAO.Database
            Dim rs As DAO.Recordset
            Dim lCount As Long

            On Error GoTo ErrHandler

            ' Base query
            sSQL = "SELECT c.CaseID, c.CaseNumber, c.OpenedDate, c.Status " & _
                   "FROM tblCases AS c "

            ' Build WHERE clause based on filter args
            sWhere = " WHERE 1=1 "

            If InStr(sFilterArgs, "ACTIVE_ONLY") > 0 Then
                sWhere = sWhere & " AND c.Status IN ('Open', 'Pending Review') "
            End If

            If InStr(sFilterArgs, "OVERDUE") > 0 Then
                sWhere = sWhere & " AND DateDiff('d', c.OpenedDate, Date()) > 90 "
            End If

            ' District filter from form if open
            If CurrentProject.AllForms("frmFilterPanel").IsLoaded Then
                If Not IsNull(Forms!frmFilterPanel!cboDistrict) Then
                    sWhere = sWhere & " AND c.DistrictID = " & _
                             Forms!frmFilterPanel!cboDistrict
                End If

                If Not IsNull(Forms!frmFilterPanel!txtCaseworkerName) And _
                   Forms!frmFilterPanel!txtCaseworkerName <> "" Then
                    sWhere = sWhere & " AND c.AssignedCaseworkerID IN (" & _
                             "SELECT CaseworkerID FROM tblCaseworkers " & _
                             "WHERE CaseworkerName LIKE '*" & _
                             Replace(Forms!frmFilterPanel!txtCaseworkerName, "'", "''") & _
                             "*')"
                End If
            End If

            ' Sort
            If InStr(sFilterArgs, "BY_DATE") > 0 Then
                sOrder = " ORDER BY c.OpenedDate DESC "
            Else
                sOrder = " ORDER BY c.CaseNumber "
            End If

            ' Final assembly
            sSQL = sSQL & sWhere & sOrder

            ' Execute and count
            Set db = CurrentDb()
            Set rs = db.OpenRecordset(sSQL, dbOpenSnapshot)

            If Not rs.EOF Then
                rs.MoveLast
                lCount = rs.RecordCount
            Else
                lCount = 0
            End If

            rs.Close
            Set rs = Nothing
            Set db = Nothing

            RunFilteredCaseReport = lCount
            Exit Function

        ErrHandler:
            MsgBox "Error in RunFilteredCaseReport: " & Err.Description, vbCritical
            RunFilteredCaseReport = -1
            If Not rs Is Nothing Then
                rs.Close
                Set rs = Nothing
            End If
            Set db = Nothing
        End Function
        """;

    private static AccessObjectModelRegistry BuildRegistry() =>
        new(
            new FormMetadata[]
            {
                new()
                {
                    Name = "frmFilterPanel",
                    Children = new AccessControl[]
                    {
                        new()
                        {
                            Type = "Section",
                            Name = "Detail",
                            Children = new AccessControl[]
                            {
                                new() { Type = "ComboBox",  Name = "cboDistrict" },
                                new() { Type = "TextBox",   Name = "txtCaseworkerName" },
                            },
                        },
                    },
                },
            },
            System.Array.Empty<ReportMetadata>(),
            new TypeLibraryRegistry());

    private static VbaProcedureDeclaration ParseProcedure()
    {
        var module = VbaParserPipeline.Parse(Fixture05Bas)
                                      .BuildAst("05-dynamic-sql-construction.bas");
        var proc = module.Items
                         .OfType<VbaProcedureDeclaration>()
                         .FirstOrDefault(p => p.Name == "RunFilteredCaseReport");
        Assert.NotNull(proc);
        return proc!;
    }

    // ── Execution-site shape ───────────────────────────────────────────

    [Fact]
    public void Fixture05_ProducesExactlyOneExecutionSite()
    {
        var proc = ParseProcedure();
        var result = new DynamicSqlAnalyzer(BuildRegistry()).Analyze(proc);

        Assert.Single(result.ExecutionSites);
    }

    // ── Combinatorial-blowup result ────────────────────────────────────

    [Fact]
    public void Fixture05_Site_IsNotAnalyzable_CombinatorialBlowup()
    {
        var proc = ParseProcedure();
        var site = new DynamicSqlAnalyzer(BuildRegistry()).Analyze(proc).ExecutionSites[0];

        Assert.False(site.IsAnalyzable);
        Assert.Equal(UnanalyzableReason.CombinatorialBlowup, site.UnanalyzableReason);
    }

    [Fact]
    public void Fixture05_Site_TemplateCount_EqualsMaxRepresentativeSamples()
    {
        var proc = ParseProcedure();
        var options = DynamicSqlAnalyzerOptions.Default;
        var site = new DynamicSqlAnalyzer(BuildRegistry(), options).Analyze(proc).ExecutionSites[0];

        Assert.Equal(options.MaxRepresentativeSamples, site.Templates.Count);
    }

    [Fact]
    public void Fixture05_Site_FullCardinality_ExceedsMaxFullEnumeration()
    {
        var proc = ParseProcedure();
        var options = DynamicSqlAnalyzerOptions.Default;
        var site = new DynamicSqlAnalyzer(BuildRegistry(), options).Analyze(proc).ExecutionSites[0];

        // CardinalityCapped returns cap+1 as the sentinel when actual cardinality
        // exceeds the cap, so FullCardinality == MaxFullEnumeration + 1 here.
        Assert.True(site.FullCardinality > options.MaxFullEnumeration,
            $"Expected FullCardinality > {options.MaxFullEnumeration}, got {site.FullCardinality}");
    }

    // ── Template content ───────────────────────────────────────────────

    [Fact]
    public void Fixture05_AllTemplates_ContainBaseSelectAndFromClauses()
    {
        var proc = ParseProcedure();
        var site = new DynamicSqlAnalyzer(BuildRegistry()).Analyze(proc).ExecutionSites[0];

        Assert.All(site.Templates, t =>
        {
            Assert.Contains("SELECT c.CaseID", t.Text,
                System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FROM tblCases", t.Text,
                System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE 1=1", t.Text,
                System.StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Conditional-fragment coverage in sample ───────────────────────

    [Fact]
    public void Fixture05_SampledTemplates_ContainConditionalFragments()
    {
        // The representative sample must include examples of each conditional
        // SQL fragment so consumers can see *something* for every code path.
        var proc = ParseProcedure();
        var site = new DynamicSqlAnalyzer(BuildRegistry()).Analyze(proc).ExecutionSites[0];

        var texts = site.Templates.Select(t => t.Text).ToList();

        // ACTIVE_ONLY clause
        Assert.Contains(texts, t => t.Contains("c.Status IN ('Open', 'Pending Review')"));
        // OVERDUE clause
        Assert.Contains(texts, t => t.Contains("DateDiff('d', c.OpenedDate, Date()) > 90"));
        // District filter
        Assert.Contains(texts, t => t.Contains("c.DistrictID ="));
        // Caseworker subquery
        Assert.Contains(texts, t => t.Contains("c.AssignedCaseworkerID IN (SELECT CaseworkerID"));
        // Both ORDER BY variants
        Assert.Contains(texts, t => t.Contains("ORDER BY c.OpenedDate DESC"));
        Assert.Contains(texts, t => t.Contains("ORDER BY c.CaseNumber"));
    }

    // ── Custom cap — full enumeration path ────────────────────────────

    [Fact]
    public void Fixture05_WithLargeCap_ProducesFullEnumeration_AllAnalyzable()
    {
        // With a cap large enough to hold all branches, the site should
        // be analyzable and produce more than MaxRepresentativeSamples templates.
        var proc = ParseProcedure();
        var bigCapOptions = new DynamicSqlAnalyzerOptions(
            MaxFullEnumeration: 256,
            MaxRepresentativeSamples: 5);
        var site = new DynamicSqlAnalyzer(BuildRegistry(), bigCapOptions)
                       .Analyze(proc).ExecutionSites[0];

        Assert.True(site.IsAnalyzable);
        Assert.Null(site.UnanalyzableReason);
        Assert.True(site.Templates.Count > DynamicSqlAnalyzerOptions.Default.MaxRepresentativeSamples,
            $"Expected more than {DynamicSqlAnalyzerOptions.Default.MaxRepresentativeSamples} templates, got {site.Templates.Count}");
    }
}
