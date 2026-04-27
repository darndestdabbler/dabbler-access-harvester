using System.Linq;
using System.Text.Json;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Sql.Parser;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic unit tests for QueryAbstractionBuilder. Inputs are SQL
// strings parsed via SqlParserPipeline -> BuildAst, so the tests
// exercise both the visitor logic and the contract between the SQL
// parser AST and the pipeline.
public class QueryAbstractionBuilderTests
{
    // Fixture-04 SQL, dropping the leading SQL comment block. Single-
    // quoted strings appear as JET-style strings in WHERE / ORDER BY;
    // the grammar accepts both single- and double-quoted strings.
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

    private static QueryAbstraction Build(string sourceName, string sql)
    {
        var result = SqlParserPipeline.Parse(sql, new ParseOptions { RequestedMode = ParseMode.Strict });
        var ast = result.BuildAst("test");
        return QueryAbstractionBuilder.Build(sourceName, ast);
    }

    // ── Method-name suggestion ─────────────────────────────────────────

    [Theory]
    [InlineData("qryActiveCasesByDistrict", "GetActiveCasesByDistrict")]
    [InlineData("qActiveCases", "GetActiveCases")]
    [InlineData("qselOpenCases", "GetOpenCases")]
    [InlineData("Customers By Region", "GetCustomersByRegion")]
    [InlineData("simple", "GetSimple")]
    public void SuggestMethodName_StripsHungarianAndPascalCases(string source, string expected)
    {
        Assert.Equal(expected, QueryAbstractionBuilder.SuggestMethodName(source, QueryShape.Select));
    }

    [Fact]
    public void SuggestMethodName_ReturnsNullForPunctuationOnlyName()
    {
        Assert.Null(QueryAbstractionBuilder.SuggestMethodName("---", QueryShape.Select));
    }

    [Theory]
    [InlineData(QueryShape.Insert, "InsertX")]
    [InlineData(QueryShape.Update, "UpdateX")]
    [InlineData(QueryShape.Delete, "DeleteX")]
    public void SuggestMethodName_PrependsActionVerbForActionShapes(QueryShape shape, string expected)
    {
        Assert.Equal(expected, QueryAbstractionBuilder.SuggestMethodName("X", shape));
    }

    // ── Fixture-04 SELECT ──────────────────────────────────────────────

    [Fact]
    public void Build_OnFixture04_ProducesSelectShapeWithSuggestedMethodName()
    {
        var qa = Build("qryActiveCasesByDistrict", Fixture04Sql);
        Assert.Equal(QueryShape.Select, qa.Shape);
        Assert.Equal("GetActiveCasesByDistrict", qa.SuggestedMethodName);
    }

    [Fact]
    public void Build_OnFixture04_LiftsTwoFormReferenceParametersInSourceOrder()
    {
        var qa = Build("qryActiveCasesByDistrict", Fixture04Sql);
        Assert.NotNull(qa.Parameters);
        Assert.Equal(2, qa.Parameters!.Count);

        var p0 = qa.Parameters[0];
        Assert.Equal("cboDistrict", p0.Name);
        Assert.Equal(ParameterOrigin.FormReference, p0.Origin);
        Assert.Equal("long", p0.Type);
        Assert.Contains("frmDistrictReport", p0.SourceName);

        var p1 = qa.Parameters[1];
        Assert.Equal("txtAsOfDate", p1.Name);
        Assert.Equal(ParameterOrigin.FormReference, p1.Origin);
        Assert.Equal("DateTime", p1.Type);
    }

    [Fact]
    public void Build_OnFixture04_DescribesPrimaryFromAndTwoJoins()
    {
        var qa = Build("qryActiveCasesByDistrict", Fixture04Sql);
        Assert.NotNull(qa.PrimaryFrom);
        Assert.Equal("tblCases", qa.PrimaryFrom!.TableName);
        Assert.Equal("c", qa.PrimaryFrom.Alias);

        Assert.NotNull(qa.Joins);
        Assert.Equal(2, qa.Joins!.Count);

        var join0 = qa.Joins[0];
        Assert.Equal(JoinDescriptorKind.Left, join0.Kind);
        Assert.Equal("tblCaseworkers", join0.RightSource.TableName);
        Assert.Equal("cw", join0.RightSource.Alias);

        var join1 = qa.Joins[1];
        Assert.Equal(JoinDescriptorKind.Inner, join1.Kind);
        Assert.Equal("tblClients", join1.RightSource.TableName);
        Assert.Equal("cl", join1.RightSource.Alias);
    }

    [Fact]
    public void Build_OnFixture04_ReturnsAllNineSelectListColumnsWithExpectedOrigins()
    {
        var qa = Build("qryActiveCasesByDistrict", Fixture04Sql);
        Assert.NotNull(qa.ReturnShape);
        Assert.Equal(9, qa.ReturnShape!.Count);

        // Direct columns: CaseID, CaseNumber, OpenedDate, Priority, LastActivityDate.
        Assert.Equal(ReturnColumnOrigin.Direct, qa.ReturnShape[0].Origin); // c.CaseID
        Assert.Equal("CaseID", qa.ReturnShape[0].Name);
        Assert.Equal(ReturnColumnOrigin.Direct, qa.ReturnShape[1].Origin); // c.CaseNumber
        Assert.Equal(ReturnColumnOrigin.Direct, qa.ReturnShape[2].Origin); // c.OpenedDate
        Assert.Equal(ReturnColumnOrigin.Direct, qa.ReturnShape[7].Origin); // c.Priority
        Assert.Equal(ReturnColumnOrigin.Direct, qa.ReturnShape[8].Origin); // c.LastActivityDate

        // Computed columns: Caseworker (Nz), ClientName (concat), DaysOpen (DateDiff), StatusFlag (IIf).
        Assert.Equal("Caseworker", qa.ReturnShape[3].Name);
        Assert.Equal(ReturnColumnOrigin.Computed, qa.ReturnShape[3].Origin);
        Assert.Equal("ClientName", qa.ReturnShape[4].Name);
        Assert.Equal(ReturnColumnOrigin.Computed, qa.ReturnShape[4].Origin);
        Assert.Equal("DaysOpen", qa.ReturnShape[5].Name);
        Assert.Equal(ReturnColumnOrigin.Computed, qa.ReturnShape[5].Origin);
        Assert.Equal("StatusFlag", qa.ReturnShape[6].Name);
        Assert.Equal(ReturnColumnOrigin.Computed, qa.ReturnShape[6].Origin);
    }

    [Fact]
    public void Build_OnFixture04_FlattensWhereIntoAndPredicateWithFourOperands()
    {
        var qa = Build("qryActiveCasesByDistrict", Fixture04Sql);
        var and = Assert.IsType<AndPredicate>(qa.Where);
        Assert.Equal(4, and.Operands.Count);

        // Operand 0: c.DistrictID = [Forms]!...!cboDistrict
        var cmp = Assert.IsType<ComparisonPredicate>(and.Operands[0]);
        Assert.Equal("=", cmp.Operator);
        Assert.Equal(ExpressionKind.Column, cmp.Left.Kind);
        Assert.Equal(ExpressionKind.FormReference, cmp.Right.Kind);

        // Operand 1: c.CaseStatus IN (...)
        var inPred = Assert.IsType<InPredicate>(and.Operands[1]);
        Assert.Equal(3, inPred.Values.Count);
        Assert.False(inPred.Negated);

        // Operand 3 (the parenthesised OR clause): OR with IsNull + comparison
        var or = Assert.IsType<OrPredicate>(and.Operands[3]);
        Assert.Equal(2, or.Operands.Count);
        Assert.IsType<IsNullPredicate>(or.Operands[0]);
        Assert.IsType<ComparisonPredicate>(or.Operands[1]);
    }

    [Fact]
    public void Build_OnFixture04_OrderByHasTwoEntriesBothAscending()
    {
        var qa = Build("qryActiveCasesByDistrict", Fixture04Sql);
        Assert.NotNull(qa.OrderBy);
        Assert.Equal(2, qa.OrderBy!.Count);
        Assert.All(qa.OrderBy, o => Assert.Equal(OrderDirection.Ascending, o.Direction));
        // First sort key is the IIf priority mapping — function call.
        Assert.Equal(ExpressionKind.FunctionCall, qa.OrderBy[0].Expression.Kind);
        // Second is the simple column c.OpenedDate.
        Assert.Equal(ExpressionKind.Column, qa.OrderBy[1].Expression.Kind);
    }

    [Fact]
    public void Build_OnFixture04_AggregationMarkersAreNullBecauseSelectIsNonAggregate()
    {
        var qa = Build("qryActiveCasesByDistrict", Fixture04Sql);
        // Fixture has no DISTINCT, no TOP, no aggregates, no GROUP BY.
        Assert.Null(qa.Aggregation);
    }

    // ── Smaller smoke-tests covering shapes the fixture does not exercise

    [Fact]
    public void Build_OnSelectStarSetsReturnShapeNullByDesign()
    {
        var qa = Build("qAll", "SELECT * FROM tblCases");
        Assert.Null(qa.ReturnShape);
        Assert.Equal(QueryShape.Select, qa.Shape);
    }

    [Fact]
    public void Build_OnDistinctSelectMarksDistinctOnAggregationMarkers()
    {
        var qa = Build("qDistinct", "SELECT DISTINCT Region FROM tblClients");
        Assert.NotNull(qa.Aggregation);
        Assert.True(qa.Aggregation!.IsDistinct);
        Assert.False(qa.Aggregation.HasAggregates);
    }

    [Fact]
    public void Build_OnTopSelectCarriesTopNAndMarksAggregation()
    {
        var qa = Build("qTop", "SELECT TOP 10 c.CaseID FROM tblCases AS c");
        Assert.NotNull(qa.Aggregation);
        Assert.Equal(10, qa.Aggregation!.TopN);
        Assert.False(qa.Aggregation.TopIsPercent);
    }

    [Fact]
    public void Build_OnAggregateSelectMarksHasAggregatesAndOriginsTheColumn()
    {
        var qa = Build("qCount", "SELECT COUNT(*) AS N FROM tblCases");
        Assert.NotNull(qa.Aggregation);
        Assert.True(qa.Aggregation!.HasAggregates);
        Assert.NotNull(qa.ReturnShape);
        var col = Assert.Single(qa.ReturnShape!);
        Assert.Equal("N", col.Name);
        Assert.Equal(ReturnColumnOrigin.Aggregate, col.Origin);
    }

    [Fact]
    public void Build_OnGroupBy_RecordsGroupByAndHasGrouping()
    {
        var qa = Build(
            "qByRegion",
            "SELECT Region, COUNT(*) AS N FROM tblClients GROUP BY Region");
        Assert.NotNull(qa.GroupBy);
        Assert.Single(qa.GroupBy!);
        Assert.NotNull(qa.Aggregation);
        Assert.True(qa.Aggregation!.HasGrouping);
    }

    [Fact]
    public void Build_OnDeleteShapeLeavesReturnShapeNull()
    {
        var qa = Build("qDel", "DELETE FROM tblCases WHERE c.Status = 'Closed'");
        Assert.Equal(QueryShape.Delete, qa.Shape);
        Assert.Null(qa.ReturnShape);
        Assert.NotNull(qa.Where);
    }

    [Fact]
    public void Build_OnUpdateShapeCarriesPrimaryFromAndWhere()
    {
        var qa = Build("qUpd", "UPDATE tblCases AS c SET c.Status = 'Closed' WHERE c.CaseID = 1");
        Assert.Equal(QueryShape.Update, qa.Shape);
        Assert.NotNull(qa.PrimaryFrom);
        Assert.Equal("tblCases", qa.PrimaryFrom!.TableName);
        Assert.Equal("c", qa.PrimaryFrom.Alias);
        Assert.NotNull(qa.Where);
    }

    [Fact]
    public void Build_OnInsertShape_LeavesStructuralFacetsNullByDesign()
    {
        // Insert structure (column list, value source) is not in the
        // first-session synthesis scope — Session 3's end-to-end pass
        // wires it in. Verify that we at least classify the shape
        // correctly without throwing.
        var qa = Build("qIns", "INSERT INTO tblCases (CaseID, Status) VALUES (1, 'Open')");
        Assert.Equal(QueryShape.Insert, qa.Shape);
        Assert.Null(qa.ReturnShape);
        Assert.Null(qa.Where);
    }

    [Fact]
    public void Build_OnLikePredicate_ProducesLikeNode()
    {
        var qa = Build(
            "qLike",
            "SELECT c.CaseID FROM tblCases AS c WHERE c.CaseNumber LIKE 'A*'");
        var like = Assert.IsType<LikePredicate>(qa.Where);
        Assert.False(like.Negated);
        Assert.Equal(ExpressionKind.Literal, like.Pattern.Kind);
    }

    [Fact]
    public void Build_OnBetweenPredicate_ProducesBetweenNode()
    {
        var qa = Build(
            "qBetween",
            "SELECT c.CaseID FROM tblCases AS c WHERE c.OpenedDate BETWEEN #2025-01-01# AND #2025-12-31#");
        var between = Assert.IsType<BetweenPredicate>(qa.Where);
        Assert.False(between.Negated);
        Assert.Equal(LiteralKind.Date, between.Low.LiteralKind);
        Assert.Equal(LiteralKind.Date, between.High.LiteralKind);
    }

    [Fact]
    public void Build_OnIsNullPredicate_DistinguishesPositiveAndNegated()
    {
        var qaPos = Build("qNull", "SELECT c.CaseID FROM tblCases AS c WHERE c.ClosedDate IS NULL");
        var posIsNull = Assert.IsType<IsNullPredicate>(qaPos.Where);
        Assert.False(posIsNull.Negated);

        var qaNeg = Build("qNotNull", "SELECT c.CaseID FROM tblCases AS c WHERE c.ClosedDate IS NOT NULL");
        var negIsNull = Assert.IsType<IsNullPredicate>(qaNeg.Where);
        Assert.True(negIsNull.Negated);
    }

    [Fact]
    public void Build_OnSelectListExpressionWithoutAlias_FlagsSyntheticNameOnFunctionCall()
    {
        var qa = Build("q", "SELECT Nz(c.Memo) FROM tblCases AS c");
        Assert.NotNull(qa.ReturnShape);
        var col = Assert.Single(qa.ReturnShape!);
        Assert.True(col.HasSyntheticName);
        Assert.Equal("Nz", col.Name);
    }

    [Fact]
    public void Build_OnQualifiedStarSelectItem_NamesItWithQualifierStar()
    {
        var qa = Build("q", "SELECT c.* FROM tblCases AS c");
        Assert.NotNull(qa.ReturnShape);
        var col = Assert.Single(qa.ReturnShape!);
        Assert.Equal("c.*", col.Name);
        Assert.Equal(ReturnColumnOrigin.Unknown, col.Origin);
    }

    // ── End-to-end serialization (fixture 04) ─────────────────────────

    [Fact]
    public void Build_Fixture04_SerializesToJsonMatchingPhase61Contract()
    {
        // End-to-end: parse fixture-04 SQL → build QueryAbstraction →
        // serialize to JSON → verify the JSON shape matches Phase 6.1
        // requirements (method name, parameters, return shape, WHERE,
        // ORDER BY, null interpretive slots).
        var result = SqlParserPipeline.Parse(
            Fixture04Sql,
            new ParseOptions { RequestedMode = ParseMode.Strict });
        var ast = result.BuildAst("qryActiveCasesByDistrict");
        var abstraction = QueryAbstractionBuilder.Build("qryActiveCasesByDistrict", ast);

        var json = JsonSerializer.Serialize(abstraction);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Core identity and shape — enums serialize as string names (JsonStringEnumConverter)
        Assert.Equal("qryActiveCasesByDistrict", root.GetProperty("SourceQueryName").GetString());
        Assert.Equal("Select", root.GetProperty("Shape").GetString());
        Assert.Equal("GetActiveCasesByDistrict", root.GetProperty("SuggestedMethodName").GetString());

        // Phase 6.1: parameters list — fixture 04 has two declared PARAMETERS
        Assert.Equal(JsonValueKind.Array, root.GetProperty("Parameters").ValueKind);
        Assert.Equal(2, root.GetProperty("Parameters").GetArrayLength());

        // Phase 6.1: return shape — fixture 04 has named SELECT columns
        Assert.Equal(JsonValueKind.Array, root.GetProperty("ReturnShape").ValueKind);
        Assert.True(root.GetProperty("ReturnShape").GetArrayLength() > 0);

        // Phase 6.1: FROM / JOIN structure
        Assert.Equal(JsonValueKind.Object, root.GetProperty("PrimaryFrom").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("Joins").ValueKind);

        // Phase 6.1: WHERE predicate (structured tree)
        Assert.Equal(JsonValueKind.Object, root.GetProperty("Where").ValueKind);

        // Phase 6.1: ORDER BY
        Assert.Equal(JsonValueKind.Array, root.GetProperty("OrderBy").ValueKind);

        // Interpretive slots remain null at the ir/ tier
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Purpose").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Intent").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Notes").ValueKind);

        // QueryAbstractionRef deferred — not present on QueryAbstraction itself
        // (that field lives on FormDataSourceBinding / ControlBinding, wired in Session 3+)
    }
}
