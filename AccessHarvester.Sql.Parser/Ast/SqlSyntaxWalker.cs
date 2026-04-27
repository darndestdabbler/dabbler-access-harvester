using System;
using System.Collections.Generic;

namespace AccessHarvester.Sql.Parser.Ast;

// Void-returning visitor with default child-recursion. Subclasses
// override only the VisitX methods they care about. Call base.VisitX(n)
// to continue recursion into children.
public abstract class SqlSyntaxWalker
{
    public virtual void Visit(SqlSyntaxNode? node)
    {
        if (node is null) return;

        switch (node)
        {
            case SqlStatement n:                        VisitSqlStatement(n); break;
            case ParametersDeclaration n:               VisitParametersDeclaration(n); break;
            case ParameterDeclaration n:                VisitParameterDeclaration(n); break;
            case FormReferenceParameterName n:          VisitFormReferenceParameterName(n); break;
            case BracketedParameterName n:              VisitBracketedParameterName(n); break;
            case SimpleParameterName n:                 VisitSimpleParameterName(n); break;
            case SqlTypeName n:                         VisitSqlTypeName(n); break;
            case UnionStatement n:                      VisitUnionStatement(n); break;
            case ParenthesizedSelect n:                 VisitParenthesizedSelect(n); break;
            case SelectCore n:                          VisitSelectCore(n); break;
            case TopClause n:                           VisitTopClause(n); break;
            case StarSelectList n:                      VisitStarSelectList(n); break;
            case ColumnSelectList n:                    VisitColumnSelectList(n); break;
            case QualifiedStarSelectItem n:             VisitQualifiedStarSelectItem(n); break;
            case ExpressionSelectItem n:                VisitExpressionSelectItem(n); break;
            case FromClause n:                          VisitFromClause(n); break;
            case SimpleTableSource n:                   VisitSimpleTableSource(n); break;
            case SubqueryTableSource n:                 VisitSubqueryTableSource(n); break;
            case ParenthesizedTableSource n:            VisitParenthesizedTableSource(n); break;
            case JoinClause n:                          VisitJoinClause(n); break;
            case OrderItem n:                           VisitOrderItem(n); break;
            case TransformStatement n:                  VisitTransformStatement(n); break;
            case UpdateStatement n:                     VisitUpdateStatement(n); break;
            case Assignment n:                          VisitAssignment(n); break;
            case InsertStatement n:                     VisitInsertStatement(n); break;
            case ValuesSource n:                        VisitValuesSource(n); break;
            case SelectSource n:                        VisitSelectSource(n); break;
            case DeleteStatement n:                     VisitDeleteStatement(n); break;
            case BinaryExpression n:                    VisitBinaryExpression(n); break;
            case UnaryExpression n:                     VisitUnaryExpression(n); break;
            case BetweenExpression n:                   VisitBetweenExpression(n); break;
            case NotBetweenExpression n:                VisitNotBetweenExpression(n); break;
            case IsNullExpression n:                    VisitIsNullExpression(n); break;
            case IsNotNullExpression n:                 VisitIsNotNullExpression(n); break;
            case InListExpression n:                    VisitInListExpression(n); break;
            case InSubqueryExpression n:                VisitInSubqueryExpression(n); break;
            case ExistsExpression n:                    VisitExistsExpression(n); break;
            case SubqueryExpression n:                  VisitSubqueryExpression(n); break;
            case ParenthesizedExpression n:             VisitParenthesizedExpression(n); break;
            case IntegerLiteralExpression n:            VisitIntegerLiteralExpression(n); break;
            case DecimalLiteralExpression n:            VisitDecimalLiteralExpression(n); break;
            case StringLiteralExpression n:             VisitStringLiteralExpression(n); break;
            case DateLiteralExpression n:               VisitDateLiteralExpression(n); break;
            case TrueLiteralExpression n:               VisitTrueLiteralExpression(n); break;
            case FalseLiteralExpression n:              VisitFalseLiteralExpression(n); break;
            case NullLiteralExpression n:               VisitNullLiteralExpression(n); break;
            case FormReference n:                       VisitFormReference(n); break;
            case FormsRoot n:                           VisitFormsRoot(n); break;
            case ReportsRoot n:                         VisitReportsRoot(n); break;
            case RefSegment n:                          VisitRefSegment(n); break;
            case ColumnReference n:                     VisitColumnReference(n); break;
            case ParameterPlaceholderExpression n:      VisitParameterPlaceholderExpression(n); break;
            case IIfExpression n:                       VisitIIfExpression(n); break;
            case SwitchExpression n:                    VisitSwitchExpression(n); break;
            case SwitchPair n:                          VisitSwitchPair(n); break;
            case ChooseExpression n:                    VisitChooseExpression(n); break;
            case DomainFunctionExpression n:            VisitDomainFunctionExpression(n); break;
            case CountStarExpression n:                 VisitCountStarExpression(n); break;
            case FunctionCallExpression n:              VisitFunctionCallExpression(n); break;

            default:
                throw new InvalidOperationException(
                    $"SqlSyntaxWalker.Visit has no dispatch arm for node type {node.GetType().FullName}. " +
                    "Add a case to the switch in SqlSyntaxWalker.Visit and a corresponding Visit method.");
        }
    }

    protected void VisitAll(IEnumerable<SqlSyntaxNode?>? nodes)
    {
        if (nodes is null) return;
        foreach (var n in nodes) Visit(n);
    }

    // Statements
    public virtual void VisitSqlStatement(SqlStatement n)
    {
        Visit(n.Parameters);
        Visit(n.Body);
    }
    public virtual void VisitParametersDeclaration(ParametersDeclaration n)
    {
        VisitAll(n.Parameters);
    }
    public virtual void VisitParameterDeclaration(ParameterDeclaration n)
    {
        Visit(n.Name);
        Visit(n.TypeName);
    }
    public virtual void VisitFormReferenceParameterName(FormReferenceParameterName n) => Visit(n.Reference);
    public virtual void VisitBracketedParameterName(BracketedParameterName n) { }
    public virtual void VisitSimpleParameterName(SimpleParameterName n) { }
    public virtual void VisitSqlTypeName(SqlTypeName n) { }
    public virtual void VisitUnionStatement(UnionStatement n) => VisitAll(n.Selects);
    public virtual void VisitParenthesizedSelect(ParenthesizedSelect n) => Visit(n.Inner);
    public virtual void VisitSelectCore(SelectCore n)
    {
        Visit(n.Top);
        Visit(n.SelectList);
        Visit(n.From);
        Visit(n.Where);
        if (n.GroupBy is not null) VisitAll(n.GroupBy);
        Visit(n.Having);
        if (n.OrderBy is not null) VisitAll(n.OrderBy);
    }
    public virtual void VisitTopClause(TopClause n) { }
    public virtual void VisitStarSelectList(StarSelectList n) { }
    public virtual void VisitColumnSelectList(ColumnSelectList n) => VisitAll(n.Items);
    public virtual void VisitQualifiedStarSelectItem(QualifiedStarSelectItem n) { }
    public virtual void VisitExpressionSelectItem(ExpressionSelectItem n) => Visit(n.Value);
    public virtual void VisitFromClause(FromClause n) => VisitAll(n.Sources);
    public virtual void VisitSimpleTableSource(SimpleTableSource n) => VisitAll(n.Joins);
    public virtual void VisitSubqueryTableSource(SubqueryTableSource n)
    {
        Visit(n.Subquery);
        VisitAll(n.Joins);
    }
    public virtual void VisitParenthesizedTableSource(ParenthesizedTableSource n)
    {
        Visit(n.Inner);
        VisitAll(n.Joins);
    }
    public virtual void VisitJoinClause(JoinClause n)
    {
        Visit(n.Right);
        Visit(n.On);
    }
    public virtual void VisitOrderItem(OrderItem n) => Visit(n.Expression);
    public virtual void VisitTransformStatement(TransformStatement n)
    {
        Visit(n.AggregateExpression);
        Visit(n.SelectBody);
        Visit(n.PivotExpression);
        if (n.PivotInValues is not null) VisitAll(n.PivotInValues);
    }
    public virtual void VisitUpdateStatement(UpdateStatement n)
    {
        Visit(n.Target);
        VisitAll(n.Assignments);
        Visit(n.Where);
    }
    public virtual void VisitAssignment(Assignment n)
    {
        Visit(n.Column);
        Visit(n.Value);
    }
    public virtual void VisitInsertStatement(InsertStatement n)
    {
        if (n.Columns is not null) VisitAll(n.Columns);
        Visit(n.Source);
    }
    public virtual void VisitValuesSource(ValuesSource n) => VisitAll(n.Values);
    public virtual void VisitSelectSource(SelectSource n) => Visit(n.Select);
    public virtual void VisitDeleteStatement(DeleteStatement n)
    {
        Visit(n.SelectList);
        Visit(n.From);
        Visit(n.Where);
    }

    // Expressions
    public virtual void VisitBinaryExpression(BinaryExpression n)
    {
        Visit(n.Left);
        Visit(n.Right);
    }
    public virtual void VisitUnaryExpression(UnaryExpression n) => Visit(n.Operand);
    public virtual void VisitBetweenExpression(BetweenExpression n)
    {
        Visit(n.Operand);
        Visit(n.Low);
        Visit(n.High);
    }
    public virtual void VisitNotBetweenExpression(NotBetweenExpression n)
    {
        Visit(n.Operand);
        Visit(n.Low);
        Visit(n.High);
    }
    public virtual void VisitIsNullExpression(IsNullExpression n) => Visit(n.Operand);
    public virtual void VisitIsNotNullExpression(IsNotNullExpression n) => Visit(n.Operand);
    public virtual void VisitInListExpression(InListExpression n)
    {
        Visit(n.Operand);
        VisitAll(n.Values);
    }
    public virtual void VisitInSubqueryExpression(InSubqueryExpression n)
    {
        Visit(n.Operand);
        Visit(n.Subquery);
    }
    public virtual void VisitExistsExpression(ExistsExpression n) => Visit(n.Subquery);
    public virtual void VisitSubqueryExpression(SubqueryExpression n) => Visit(n.Subquery);
    public virtual void VisitParenthesizedExpression(ParenthesizedExpression n) => Visit(n.Inner);
    public virtual void VisitIntegerLiteralExpression(IntegerLiteralExpression n) { }
    public virtual void VisitDecimalLiteralExpression(DecimalLiteralExpression n) { }
    public virtual void VisitStringLiteralExpression(StringLiteralExpression n) { }
    public virtual void VisitDateLiteralExpression(DateLiteralExpression n) { }
    public virtual void VisitTrueLiteralExpression(TrueLiteralExpression n) { }
    public virtual void VisitFalseLiteralExpression(FalseLiteralExpression n) { }
    public virtual void VisitNullLiteralExpression(NullLiteralExpression n) { }
    public virtual void VisitFormReference(FormReference n)
    {
        Visit(n.Root);
        VisitAll(n.Segments);
    }
    public virtual void VisitFormsRoot(FormsRoot n) { }
    public virtual void VisitReportsRoot(ReportsRoot n) { }
    public virtual void VisitRefSegment(RefSegment n) { }
    public virtual void VisitColumnReference(ColumnReference n) { }
    public virtual void VisitParameterPlaceholderExpression(ParameterPlaceholderExpression n) { }
    public virtual void VisitIIfExpression(IIfExpression n)
    {
        Visit(n.Condition);
        Visit(n.ThenExpr);
        Visit(n.ElseExpr);
    }
    public virtual void VisitSwitchExpression(SwitchExpression n) => VisitAll(n.Pairs);
    public virtual void VisitSwitchPair(SwitchPair n)
    {
        Visit(n.Condition);
        Visit(n.Value);
    }
    public virtual void VisitChooseExpression(ChooseExpression n)
    {
        Visit(n.Index);
        VisitAll(n.Choices);
    }
    public virtual void VisitDomainFunctionExpression(DomainFunctionExpression n)
    {
        Visit(n.FieldExpr);
        Visit(n.DomainExpr);
        Visit(n.CriteriaExpr);
    }
    public virtual void VisitCountStarExpression(CountStarExpression n) { }
    public virtual void VisitFunctionCallExpression(FunctionCallExpression n) => VisitAll(n.Arguments);
}
