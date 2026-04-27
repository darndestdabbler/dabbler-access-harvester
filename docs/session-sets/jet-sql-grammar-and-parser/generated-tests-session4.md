```csharp
[Fact]
public void GroupBy_WithCountStarAggregate_BuildsCorrectAst()
{
  var sql = "SELECT COUNT(*) FROM tbl GROUP BY dept";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  var select = stmt.Select.Selects.Single();
  
  Assert.NotNull(select.GroupBy);
  Assert.Single(select.GroupBy);
  Assert.IsType<CountStarExpression>(select.SelectList.Items.Single().Value);
}

[Fact]
public void GroupBy_WithHavingClause_BuildsCorrectAst()
{
  var sql = "SELECT dept, COUNT(*) FROM tbl GROUP BY dept HAVING COUNT(*) > 5";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  var select = stmt.Select.Selects.Single();

  Assert.NotNull(select.Having);
  var having = Assert.IsType<BinaryExpression>(select.Having);
  Assert.Equal(BinaryOperator.GreaterThan, having.Operator);
  Assert.IsType<CountStarExpression>(having.Left);
}

[Fact]
public void Select_WithDistinctQuantifier_BuildsCorrectAst()
{
  var sql = "SELECT DISTINCT name FROM tbl";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  var select = stmt.Select.Selects.Single();

  Assert.Equal(SelectQuantifier.Distinct, select.Quantifier);
}

[Fact]
public void Select_WithTopNClause_BuildsCorrectAst()
{
  var sql = "SELECT TOP 10 * FROM tbl";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  var select = stmt.Select.Selects.Single();
  
  Assert.NotNull(select.Top);
  Assert.Equal(10, select.Top.N);
  Assert.False(select.Top.Percent);
}

[Fact]
public void Select_WithTopNPercentClause_BuildsCorrectAst()
{
  var sql = "SELECT TOP 25 PERCENT name FROM tbl";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  var select = stmt.Select.Selects.Single();
  
  Assert.NotNull(select.Top);
  Assert.Equal(25, select.Top.N);
  Assert.True(select.Top.Percent);
}

[Fact]
public void FunctionCall_SwitchExpression_BuildsCorrectAst()
{
  var sql = "SELECT Switch(x=1, 'one', x=2, 'two', True, 'other') FROM t";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  var select = stmt.Select.Selects.Single();

  var switchExpr = Assert.IsType<SwitchExpression>(select.SelectList.Items.Single().Value);
  Assert.Equal(3, switchExpr.Pairs.Count);
}

[Fact]
public void FunctionCall_ChooseExpression_BuildsCorrectAst()
{
  var sql = "SELECT Choose(n, 'first', 'second', 'third') FROM t";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  var select = stmt.Select.Selects.Single();

  var chooseExpr = Assert.IsType<ChooseExpression>(select.SelectList.Items.Single().Value);
  Assert.NotNull(chooseExpr.Index);
  Assert.Equal(3, chooseExpr.Choices.Count);
}

[Fact]
public void WhereClause_WithExistsSubquery_BuildsCorrectAst()
{
  var sql = "SELECT 1 FROM t WHERE EXISTS (SELECT 1 FROM other WHERE other.id = t.id)";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));
  
  Assert.IsType<ExistsExpression>(stmt.Where);
}

[Fact]
public void WhereClause_WithNotInList_BuildsCorrectAst()
{
  var sql = "SELECT 1 FROM t WHERE status NOT IN ('Closed', 'Archived')";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));

  var notInExpr = Assert.IsType<InListExpression>(stmt.Where);
  Assert.True(notInExpr.Negated);
  Assert.Equal(2, notInExpr.Values.Count);
}

[Fact]
public void WhereClause_WithNotBetween_BuildsCorrectAst()
{
  var sql = "SELECT 1 FROM t WHERE age NOT BETWEEN 18 AND 65";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));

  Assert.IsType<NotBetweenExpression>(stmt.Where);
}

[Fact]
public void WhereClause_WithNotLike_BuildsCorrectAst()
{
  var sql = "SELECT 1 FROM t WHERE name NOT LIKE '*Smith*'";
  var stmt = Assert.IsType<SelectStatement>(BuildAst(sql));

  var notLikeExpr = Assert.IsType<BinaryExpression>(stmt.Where);
  Assert.Equal(BinaryOperator.NotLike, notLikeExpr.Operator);
}

[Fact]
public void Expression_ReportsFormReference_BuildsCorrectAst()
{
  var sql = "SELECT Reports