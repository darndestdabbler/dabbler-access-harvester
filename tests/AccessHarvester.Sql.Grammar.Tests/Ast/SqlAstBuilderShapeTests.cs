using System.Linq;
using AccessHarvester.Sql.Parser;
using AccessHarvester.Sql.Parser.Ast;
using Xunit;

namespace AccessHarvester.Sql.Grammar.Tests.Ast;

// Validates that representative JET SQL inputs produce the AST shape the
// design doc commits to (docs/session-sets/jet-sql-grammar-and-parser/).
// Each test asserts shape — type, count, and key fields — rather than
// full equality so adding a node carries minimal disruption.
public class SqlAstBuilderShapeTests
{
    private static SqlStatement BuildAst(string source) =>
        SqlParserPipeline.Parse(source, new ParseOptions { RequestedMode = ParseMode.Strict })
                         .BuildAst();

    private static SqlStatement BuildFragmentAst(string source) =>
        SqlParserPipeline.ParseFragment(source, new ParseOptions { RequestedMode = ParseMode.Strict })
                         .BuildAst();

    // ── SELECT * ─────────────────────────────────────────────────────

    [Fact]
    public void SelectStar_BuildsStarSelectList()
    {
        var ast = BuildAst("SELECT * FROM tbl");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.IsType<StarSelectList>(select.SelectList);
    }

    // ── PARAMETERS declaration ────────────────────────────────────────

    [Fact]
    public void ParametersDeclaration_TwoFormReferenceParams_BuiltCorrectly()
    {
        const string sql = """
            PARAMETERS [Forms]![frmX]![cboA] Long, [Forms]![frmX]![txtB] DateTime;
            SELECT 1
            """;
        var ast = BuildAst(sql);

        Assert.NotNull(ast.Parameters);
        Assert.Equal(2, ast.Parameters!.Parameters.Count);

        var p0 = Assert.IsType<FormReferenceParameterName>(ast.Parameters.Parameters[0].Name);
        Assert.IsType<FormsRoot>(p0.Reference.Root);
        Assert.Equal("frmX", p0.Reference.Segments[0].Name);
        Assert.Equal("cboA", p0.Reference.Segments[1].Name);

        Assert.Equal("LONG", ast.Parameters.Parameters[0].TypeName.TypeKeyword);
        Assert.Equal("DATETIME", ast.Parameters.Parameters[1].TypeName.TypeKeyword);
    }

    [Fact]
    public void ParametersDeclaration_BracketedNamedParam_BuiltCorrectly()
    {
        const string sql = "PARAMETERS [Enter ID] Long; SELECT 1";
        var ast = BuildAst(sql);

        Assert.NotNull(ast.Parameters);
        var p0 = Assert.IsType<BracketedParameterName>(ast.Parameters!.Parameters[0].Name);
        Assert.Equal("Enter ID", p0.Name);
    }

    // ── SELECT list ───────────────────────────────────────────────────

    [Fact]
    public void SelectList_NineColumns_Fixture04Shape()
    {
        // The fixture-04 SELECT list has 9 columns.
        const string sql = """
            PARAMETERS [Forms]![frmDistrictReport]![cboDistrict] Long,
                       [Forms]![frmDistrictReport]![txtAsOfDate] DateTime;
            SELECT
                c.CaseID, c.CaseNumber, c.OpenedDate,
                Nz(cw.CaseworkerName, "Unassigned") AS Caseworker,
                cl.ClientLastName & ", " & cl.ClientFirstName AS ClientName,
                DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) AS DaysOpen,
                IIf(DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) > 90,
                    "Overdue",
                    IIf(DateDiff("d", c.OpenedDate, [Forms]![frmDistrictReport]![txtAsOfDate]) > 60,
                        "At Risk", "On Track")) AS StatusFlag,
                c.Priority, c.LastActivityDate
            FROM tblCases AS c
            """;
        var ast = BuildAst(sql);
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var cols = Assert.IsType<ColumnSelectList>(select.SelectList);
        Assert.Equal(9, cols.Items.Count);
    }

    [Fact]
    public void SelectItem_WithAlias_PreservesAlias()
    {
        var ast = BuildAst("SELECT Nz(x, 0) AS [Computed Value] FROM tbl");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var cols = Assert.IsType<ColumnSelectList>(select.SelectList);
        var item = Assert.IsType<ExpressionSelectItem>(cols.Items.Single());
        Assert.Equal("Computed Value", item.Alias);
    }

    [Fact]
    public void SelectItem_TableDotStar_BuildsQualifiedStarItem()
    {
        var ast = BuildAst("SELECT t.* FROM tbl AS t");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var cols = Assert.IsType<ColumnSelectList>(select.SelectList);
        var item = Assert.IsType<QualifiedStarSelectItem>(cols.Items.Single());
        Assert.Equal("t", item.TableQualifier);
    }

    // ── FROM / JOINs ─────────────────────────────────────────────────

    [Fact]
    public void FromClause_SimpleTable_BuildsSimpleTableSource()
    {
        var ast = BuildAst("SELECT 1 FROM tblCases AS c");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.From);
        var src = Assert.IsType<SimpleTableSource>(select.From!.Sources.Single());
        Assert.Equal("tblCases", src.TableName);
        Assert.Equal("c", src.Alias);
        Assert.Empty(src.Joins);
    }

    [Fact]
    public void FromClause_NestedJoins_Fixture04_HasLeftAndInnerJoin()
    {
        // Fixture 04 uses parenthesized nested joins:
        // FROM ((A LEFT JOIN B ON ...) INNER JOIN C ON ...)
        const string sql = """
            SELECT 1
            FROM ((tblCases AS c
                LEFT JOIN tblCaseworkers AS cw ON c.CaseworkerID = cw.CaseworkerID)
                INNER JOIN tblClients AS cl ON c.ClientID = cl.ClientID)
            """;
        var ast = BuildAst(sql);
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.From);

        // Outer parens wrap a middle ParenthesizedTableSource that carries
        // the INNER JOIN chain; the middle's inner SimpleTableSource carries
        // the LEFT JOIN chain. Structure:
        //   outer( middle( tblCases LEFT JOIN cw ) INNER JOIN cl )
        var outer = Assert.IsType<ParenthesizedTableSource>(select.From!.Sources.Single());
        Assert.Empty(outer.Joins);  // outer parens have no trailing join chain

        var middle = Assert.IsType<ParenthesizedTableSource>(outer.Inner);
        Assert.Single(middle.Joins);
        Assert.Equal(JoinKind.Inner, middle.Joins[0].Kind);

        var innerSrc = Assert.IsType<SimpleTableSource>(middle.Inner);
        Assert.Single(innerSrc.Joins);
        Assert.Equal(JoinKind.Left, innerSrc.Joins[0].Kind);
    }

    // ── WHERE clause ─────────────────────────────────────────────────

    [Fact]
    public void WhereClause_AndOfConditions_BuildsBinaryAndExpression()
    {
        var ast = BuildAst("SELECT 1 FROM t WHERE a = 1 AND b = 2");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.Where);
        var and = Assert.IsType<BinaryExpression>(select.Where);
        Assert.Equal(BinaryOperator.And, and.Operator);
    }

    [Fact]
    public void WhereClause_InList_BuildsInListExpression()
    {
        var ast = BuildAst("SELECT 1 FROM t WHERE c.Status IN ('Open', 'Pending Review')");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.Where);
        var inList = Assert.IsType<InListExpression>(select.Where);
        Assert.False(inList.Negated);
        Assert.Equal(2, inList.Values.Count);
    }

    [Fact]
    public void WhereClause_IsNull_BuildsIsNullExpression()
    {
        var ast = BuildAst("SELECT 1 FROM t WHERE c.ClosedDate IS NULL");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.IsType<IsNullExpression>(select.Where);
    }

    [Fact]
    public void WhereClause_IsNotNull_BuildsIsNotNullExpression()
    {
        var ast = BuildAst("SELECT 1 FROM t WHERE c.ClosedDate IS NOT NULL");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.IsType<IsNotNullExpression>(select.Where);
    }

    // ── ORDER BY ─────────────────────────────────────────────────────

    [Fact]
    public void OrderBy_Fixture04_HasTwoItems()
    {
        const string sql = """
            SELECT 1 FROM t
            ORDER BY IIf(c.Priority = 'High', 1, 2), c.OpenedDate
            """;
        var ast = BuildAst(sql);
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.OrderBy);
        Assert.Equal(2, select.OrderBy!.Count);
        Assert.Equal(OrderDirection.Ascending, select.OrderBy[0].Direction);
    }

    [Fact]
    public void OrderBy_DescKeyword_SetsDescendingDirection()
    {
        var ast = BuildAst("SELECT 1 FROM t ORDER BY c.OpenedDate DESC");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.OrderBy);
        Assert.Equal(OrderDirection.Descending, select.OrderBy!.Single().Direction);
    }

    // ── Expressions ───────────────────────────────────────────────────

    [Fact]
    public void IIf_ThreeArgs_BuildsIIfExpression()
    {
        var ast = BuildAst("SELECT IIf(x > 0, 'pos', 'neg') FROM t");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var item = Assert.IsType<ExpressionSelectItem>(Assert.IsType<ColumnSelectList>(select.SelectList).Items.Single());
        Assert.IsType<IIfExpression>(item.Value);
    }

    [Fact]
    public void IIf_Nested_OuterConditionIsIIfExpression()
    {
        // Nested IIf: IIf(cond1, val1, IIf(cond2, val2, val3))
        var ast = BuildAst("SELECT IIf(a > 90, 'Overdue', IIf(a > 60, 'At Risk', 'On Track')) FROM t");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var item = Assert.IsType<ExpressionSelectItem>(Assert.IsType<ColumnSelectList>(select.SelectList).Items.Single());
        var outer = Assert.IsType<IIfExpression>(item.Value);
        Assert.IsType<IIfExpression>(outer.ElseExpr);
    }

    [Fact]
    public void ConcatAmpersand_TwoOperands_BuildsBinaryConcat()
    {
        var ast = BuildAst("SELECT a & ', ' & b AS Name FROM t");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var item = Assert.IsType<ExpressionSelectItem>(Assert.IsType<ColumnSelectList>(select.SelectList).Items.Single());
        // Left-associative: (a & ', ') & b
        var concat = Assert.IsType<BinaryExpression>(item.Value);
        Assert.Equal(BinaryOperator.Concat, concat.Operator);
        var innerConcat = Assert.IsType<BinaryExpression>(concat.Left);
        Assert.Equal(BinaryOperator.Concat, innerConcat.Operator);
    }

    [Fact]
    public void FormReference_FormsCollection_BuildsFormReference()
    {
        var ast = BuildAst("SELECT [Forms]![frmX]![ctlY] FROM t");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var item = Assert.IsType<ExpressionSelectItem>(Assert.IsType<ColumnSelectList>(select.SelectList).Items.Single());
        var formRef = Assert.IsType<FormReference>(item.Value);
        Assert.IsType<FormsRoot>(formRef.Root);
        Assert.Equal("frmX", formRef.Segments[0].Name);
        Assert.Equal("ctlY", formRef.Segments[1].Name);
        Assert.All(formRef.Operators, op => Assert.Equal(RefAccessOperator.Bang, op));
    }

    [Fact]
    public void DateLiteral_PreservesRawText()
    {
        var ast = BuildAst("SELECT #1/1/2024# FROM t");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var item = Assert.IsType<ExpressionSelectItem>(Assert.IsType<ColumnSelectList>(select.SelectList).Items.Single());
        var lit = Assert.IsType<DateLiteralExpression>(item.Value);
        Assert.Equal("#1/1/2024#", lit.RawText);
    }

    [Fact]
    public void DomainFunction_DLookup_BuildsDomainFunctionExpression()
    {
        var ast = BuildAst("SELECT DLookup('Name', 'tblDept', 'ID=1') FROM t");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var item = Assert.IsType<ExpressionSelectItem>(Assert.IsType<ColumnSelectList>(select.SelectList).Items.Single());
        var fn = Assert.IsType<DomainFunctionExpression>(item.Value);
        Assert.Equal("DLOOKUP", fn.FunctionName);
        Assert.NotNull(fn.CriteriaExpr);
    }

    [Fact]
    public void BetweenExpression_BuildsBetweenExpressionNode()
    {
        var ast = BuildAst("SELECT 1 FROM t WHERE x BETWEEN 1 AND 10");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.IsType<BetweenExpression>(select.Where);
    }

    [Fact]
    public void ColumnReference_TableQualified_BuildsColumnReferenceWithTable()
    {
        var ast = BuildAst("SELECT c.CaseID FROM tblCases AS c");
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        var item = Assert.IsType<ExpressionSelectItem>(Assert.IsType<ColumnSelectList>(select.SelectList).Items.Single());
        var col = Assert.IsType<ColumnReference>(item.Value);
        Assert.Equal("c", col.TableQualifier);
        Assert.Equal("CaseID", col.ColumnName);
    }

    // ── TRANSFORM ... PIVOT ───────────────────────────────────────────

    [Fact]
    public void Transform_BasicCrosstab_BuildsTransformStatement()
    {
        const string sql = """
            TRANSFORM Count(o.OrderID)
            SELECT o.CustomerID
            FROM tblOrders AS o
            GROUP BY o.CustomerID
            PIVOT o.Quarter
            """;
        var ast = BuildAst(sql);
        Assert.IsType<TransformStatement>(ast.Body);
    }

    // ── UPDATE / INSERT / DELETE ───────────────────────────────────────

    [Fact]
    public void Update_SingleAssignment_BuildsUpdateStatement()
    {
        var ast = BuildAst("UPDATE tbl SET col = 1 WHERE id = 2");
        var upd = Assert.IsType<UpdateStatement>(ast.Body);
        Assert.Single(upd.Assignments);
        Assert.NotNull(upd.Where);
    }

    [Fact]
    public void Insert_ValuesClause_BuildsInsertWithValuesSource()
    {
        var ast = BuildAst("INSERT INTO tbl (a, b) VALUES (1, 'x')");
        var ins = Assert.IsType<InsertStatement>(ast.Body);
        Assert.Equal("tbl", ins.TableName);
        Assert.IsType<ValuesSource>(ins.Source);
    }

    [Fact]
    public void Delete_WithWhere_BuildsDeleteStatement()
    {
        var ast = BuildAst("DELETE FROM tbl WHERE id = 1");
        var del = Assert.IsType<DeleteStatement>(ast.Body);
        Assert.NotNull(del.Where);
    }

    // ── UNION ─────────────────────────────────────────────────────────

    [Fact]
    public void Union_TwoSelects_BuildsUnionStatementWithTwoSelects()
    {
        var ast = BuildAst("SELECT 1 FROM a UNION SELECT 2 FROM b");
        var union = Assert.IsType<UnionStatement>(ast.Body);
        Assert.Equal(2, union.Selects.Count);
        // AllFlags has one entry per select. AllFlags[i]=true means UNION ALL precedes Selects[i].
        // AllFlags[0] is always false (no UNION before the first select).
        // AllFlags[1] false means plain UNION (not UNION ALL).
        Assert.Equal(2, union.AllFlags.Count);
        Assert.False(union.AllFlags[1]); // UNION not UNION ALL before second select
    }

    [Fact]
    public void UnionAll_SecondSelectHasAllFlagTrue()
    {
        var ast = BuildAst("SELECT 1 FROM a UNION ALL SELECT 2 FROM b");
        var union = Assert.IsType<UnionStatement>(ast.Body);
        Assert.Equal(2, union.AllFlags.Count);
        Assert.True(union.AllFlags[1]); // UNION ALL before second select
    }

    // ── Provenance: SourceProvenance for clean input ──────────────────

    [Fact]
    public void CleanInput_StatementHasSourceProvenance()
    {
        var ast = BuildAst("SELECT 1");
        Assert.IsType<SourceProvenance>(ast.Provenance);
    }

    // ── Fixture 05 — ParseFragment AST shapes ─────────────────────────

    [Fact]
    public void ParseFragment_Fixture05_StatusInFilter_HasInListExpression()
    {
        const string sql =
            "SELECT c.CaseID FROM tblCases AS c " +
            "WHERE c.Status IN ('Open', 'Pending Review') " +
            "ORDER BY c.CaseNumber";

        var ast = BuildFragmentAst(sql);
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.Where);
        var inList = Assert.IsType<InListExpression>(select.Where);
        Assert.Equal(2, inList.Values.Count);
    }

    [Fact]
    public void ParseFragment_Fixture05_DateDiffFilter_HasFunctionCallInComparison()
    {
        const string sql =
            "SELECT c.CaseID FROM tblCases AS c " +
            "WHERE DateDiff('d', c.OpenedDate, Date()) > 90 " +
            "ORDER BY c.OpenedDate DESC";

        var ast = BuildFragmentAst(sql);
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.Where);
        var cmp = Assert.IsType<BinaryExpression>(select.Where);
        Assert.Equal(BinaryOperator.GreaterThan, cmp.Operator);
        Assert.IsType<FunctionCallExpression>(cmp.Left);
    }

    [Fact]
    public void ParseFragment_Fixture05_CompoundWhere_HasAndExpression()
    {
        // WHERE 1=1 AND c.Status IN (...) — the compound WHERE that
        // RunFilteredCaseReport assembles from its fragment template.
        const string sql =
            "SELECT c.CaseID FROM tblCases AS c " +
            "WHERE 1=1 AND c.Status IN ('Open', 'Pending Review') " +
            "ORDER BY c.CaseNumber";

        var ast = BuildFragmentAst(sql);
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.Where);
        var and = Assert.IsType<BinaryExpression>(select.Where);
        Assert.Equal(BinaryOperator.And, and.Operator);
        Assert.IsType<InListExpression>(and.Right);
    }

    [Fact]
    public void ParseFragment_Fixture05_SubqueryInFilter_HasInSubqueryExpression()
    {
        const string sql =
            "SELECT c.CaseID FROM tblCases AS c " +
            "WHERE c.AssignedCaseworkerID IN (SELECT CaseworkerID FROM tblCaseworkers WHERE CaseworkerName LIKE '*Smith*') " +
            "ORDER BY c.CaseNumber";

        var ast = BuildFragmentAst(sql);
        var select = Assert.IsType<SelectCore>(Assert.IsType<UnionStatement>(ast.Body).Selects.Single());
        Assert.NotNull(select.Where);
        var inSub = Assert.IsType<InSubqueryExpression>(select.Where);
        Assert.False(inSub.Negated);
        Assert.NotNull(inSub.Subquery);
    }
}
