using System;
using System.IO;
using AccessHarvester.Sql.Parser;
using Xunit;

namespace AccessHarvester.Sql.Grammar.Tests;

// Validates the SqlParserPipeline contract: Strict / Recovering / Lossy
// mode behaviour plus ParseFragment() for fixture-05 dynamic-SQL strings.
// Shape coverage is in Ast/SqlAstBuilderShapeTests.cs.
public class JetSqlParserPipelineTests
{
    // ── simple inputs ─────────────────────────────────────────────────

    private const string SimpleSelect = "SELECT 1";

    private const string MalformedInput = "SELECT @@@bad FROM tbl";

    // ── fixture 04 ────────────────────────────────────────────────────
    // Full SQL from docs/test-cases/04-saved-query-form-params.sql
    // (comments stripped; the grammar handles SQL line-comments natively
    // but we strip here to reduce noise in assertions).
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

    // ── fixture 05 representative SQL ─────────────────────────────────
    // A representative full assembly of what RunFilteredCaseReport
    // constructs at runtime (active-only + overdue filters, BY_DATE sort).
    // Tests that the JET SQL dialect constructs emitted from VBA are
    // parseable as complete statements via ParseFragment().
    private const string Fixture05AssembledSql =
        "SELECT c.CaseID, c.CaseNumber, c.OpenedDate, c.Status " +
        "FROM tblCases AS c " +
        "WHERE 1=1 " +
        "AND c.Status IN ('Open', 'Pending Review') " +
        "AND DateDiff('d', c.OpenedDate, Date()) > 90 " +
        "ORDER BY c.OpenedDate DESC";

    // A subquery fragment as it would appear in the WHERE clause of
    // RunFilteredCaseReport when the caseworker name filter is active.
    private const string Fixture05SubquerySql =
        "SELECT c.CaseID, c.CaseNumber, c.OpenedDate, c.Status " +
        "FROM tblCases AS c " +
        "WHERE 1=1 " +
        "AND c.AssignedCaseworkerID IN (" +
        "SELECT CaseworkerID FROM tblCaseworkers " +
        "WHERE CaseworkerName LIKE '*Smith*')" +
        " ORDER BY c.CaseNumber";

    // ── Strict mode ───────────────────────────────────────────────────

    [Fact]
    public void Strict_SimpleSelect_ProducesNoDiagnostics()
    {
        var result = SqlParserPipeline.Parse(SimpleSelect,
            new ParseOptions { RequestedMode = ParseMode.Strict });

        Assert.Empty(result.Diagnostics);
        Assert.Equal(ParseMode.Strict, result.ModeUsed);
        Assert.NotNull(result.ParseTree);
    }

    [Fact]
    public void Strict_Fixture04_ParsesCleanly()
    {
        var result = SqlParserPipeline.Parse(Fixture04Sql,
            new ParseOptions { RequestedMode = ParseMode.Strict });

        Assert.Empty(result.Diagnostics);
        Assert.Equal(ParseMode.Strict, result.ModeUsed);
    }

    [Fact]
    public void Strict_MalformedInput_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            SqlParserPipeline.Parse(MalformedInput,
                new ParseOptions { RequestedMode = ParseMode.Strict }));
    }

    // ── Recovering mode ───────────────────────────────────────────────

    [Fact]
    public void Recovering_CleanInput_ProducesNoDiagnosticsAndRecoveringMode()
    {
        var result = SqlParserPipeline.Parse(SimpleSelect);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(ParseMode.Recovering, result.ModeUsed);
    }

    [Fact]
    public void Recovering_MalformedInput_ProducesDiagnosticsButNotNullTree()
    {
        var result = SqlParserPipeline.Parse(MalformedInput);

        Assert.NotEmpty(result.Diagnostics);
        Assert.NotNull(result.ParseTree);
    }

    // ── Lossy mode ────────────────────────────────────────────────────

    [Fact]
    public void Lossy_CleanInput_ReportsLossyWarning()
    {
        var result = SqlParserPipeline.Parse(SimpleSelect,
            new ParseOptions { RequestedMode = ParseMode.Lossy });

        Assert.Equal(ParseMode.Lossy, result.ModeUsed);
        Assert.Contains(result.Diagnostics,
            d => d.Code == "SQL0050" && d.Severity == DiagnosticSeverity.Warning);
    }

    // ── ParseFragment — fixture 05 ────────────────────────────────────

    [Fact]
    public void ParseFragment_Fixture05Assembled_ParsesCleanly()
    {
        var result = SqlParserPipeline.ParseFragment(Fixture05AssembledSql,
            new ParseOptions { RequestedMode = ParseMode.Strict });

        Assert.Empty(result.Diagnostics);
        Assert.Equal(ParseMode.Strict, result.ModeUsed);
    }

    [Fact]
    public void ParseFragment_Fixture05Subquery_ParsesCleanly()
    {
        var result = SqlParserPipeline.ParseFragment(Fixture05SubquerySql,
            new ParseOptions { RequestedMode = ParseMode.Strict });

        Assert.Empty(result.Diagnostics);
        Assert.Equal(ParseMode.Strict, result.ModeUsed);
    }

    [Fact]
    public void ParseFragment_Recovering_CleanInput_ProducesRecoveringMode()
    {
        var result = SqlParserPipeline.ParseFragment(Fixture05AssembledSql);

        Assert.Equal(ParseMode.Recovering, result.ModeUsed);
        Assert.Empty(result.Diagnostics);
    }

    // ── BuildAst round-trip ───────────────────────────────────────────

    [Fact]
    public void BuildAst_Fixture04_ReturnsNonNullStatement()
    {
        var result = SqlParserPipeline.Parse(Fixture04Sql);
        var ast = result.BuildAst("04-saved-query-form-params.sql");

        Assert.NotNull(ast);
        Assert.NotNull(ast.Body);
    }

    [Fact]
    public void BuildAst_ParseFragment_Fixture05_ReturnsNonNullStatement()
    {
        var result = SqlParserPipeline.ParseFragment(Fixture05AssembledSql);
        var ast = result.BuildAst("05-dynamic-sql-construction.bas");

        Assert.NotNull(ast);
        Assert.NotNull(ast.Body);
    }

    // ── SourcePath propagation ────────────────────────────────────────

    [Fact]
    public void Strict_SourcePath_AppearsInExceptionMessage()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            SqlParserPipeline.Parse(MalformedInput,
                new ParseOptions
                {
                    RequestedMode = ParseMode.Strict,
                    SourcePath = "test-query.sql"
                }));

        Assert.Contains("test-query.sql", ex.Message);
    }
}
