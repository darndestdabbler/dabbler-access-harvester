using System;

namespace AccessHarvester.Sql.Parser.Ast;

// Generic-return visitor. Default implementations return default(T) and
// do NOT recurse into children. Per the VBA AST convention, a missing
// arm in the Visit dispatch throws InvalidOperationException rather
// than silently dropping the node.
public abstract class SqlSyntaxVisitor<TResult>
{
    public virtual TResult? Visit(SqlSyntaxNode? node)
    {
        if (node is null) return default;

        return node switch
        {
            // Statements
            SqlStatement n                          => VisitSqlStatement(n),
            ParametersDeclaration n                 => VisitParametersDeclaration(n),
            ParameterDeclaration n                  => VisitParameterDeclaration(n),
            FormReferenceParameterName n             => VisitFormReferenceParameterName(n),
            BracketedParameterName n                => VisitBracketedParameterName(n),
            SimpleParameterName n                   => VisitSimpleParameterName(n),
            SqlTypeName n                           => VisitSqlTypeName(n),
            UnionStatement n                        => VisitUnionStatement(n),
            ParenthesizedSelect n                   => VisitParenthesizedSelect(n),
            SelectCore n                            => VisitSelectCore(n),
            TopClause n                             => VisitTopClause(n),
            StarSelectList n                        => VisitStarSelectList(n),
            ColumnSelectList n                      => VisitColumnSelectList(n),
            QualifiedStarSelectItem n               => VisitQualifiedStarSelectItem(n),
            ExpressionSelectItem n                  => VisitExpressionSelectItem(n),
            FromClause n                            => VisitFromClause(n),
            SimpleTableSource n                     => VisitSimpleTableSource(n),
            SubqueryTableSource n                   => VisitSubqueryTableSource(n),
            ParenthesizedTableSource n              => VisitParenthesizedTableSource(n),
            JoinClause n                            => VisitJoinClause(n),
            OrderItem n                             => VisitOrderItem(n),
            TransformStatement n                    => VisitTransformStatement(n),
            UpdateStatement n                       => VisitUpdateStatement(n),
            Assignment n                            => VisitAssignment(n),
            InsertStatement n                       => VisitInsertStatement(n),
            ValuesSource n                          => VisitValuesSource(n),
            SelectSource n                          => VisitSelectSource(n),
            DeleteStatement n                       => VisitDeleteStatement(n),

            // Expressions
            BinaryExpression n                      => VisitBinaryExpression(n),
            UnaryExpression n                       => VisitUnaryExpression(n),
            BetweenExpression n                     => VisitBetweenExpression(n),
            NotBetweenExpression n                  => VisitNotBetweenExpression(n),
            IsNullExpression n                      => VisitIsNullExpression(n),
            IsNotNullExpression n                   => VisitIsNotNullExpression(n),
            InListExpression n                      => VisitInListExpression(n),
            InSubqueryExpression n                  => VisitInSubqueryExpression(n),
            ExistsExpression n                      => VisitExistsExpression(n),
            SubqueryExpression n                    => VisitSubqueryExpression(n),
            ParenthesizedExpression n               => VisitParenthesizedExpression(n),
            IntegerLiteralExpression n              => VisitIntegerLiteralExpression(n),
            DecimalLiteralExpression n              => VisitDecimalLiteralExpression(n),
            StringLiteralExpression n               => VisitStringLiteralExpression(n),
            DateLiteralExpression n                 => VisitDateLiteralExpression(n),
            TrueLiteralExpression n                 => VisitTrueLiteralExpression(n),
            FalseLiteralExpression n                => VisitFalseLiteralExpression(n),
            NullLiteralExpression n                 => VisitNullLiteralExpression(n),
            FormReference n                         => VisitFormReference(n),
            FormsRoot n                             => VisitFormsRoot(n),
            ReportsRoot n                           => VisitReportsRoot(n),
            RefSegment n                            => VisitRefSegment(n),
            ColumnReference n                       => VisitColumnReference(n),
            ParameterPlaceholderExpression n        => VisitParameterPlaceholderExpression(n),
            IIfExpression n                         => VisitIIfExpression(n),
            SwitchExpression n                      => VisitSwitchExpression(n),
            SwitchPair n                            => VisitSwitchPair(n),
            ChooseExpression n                      => VisitChooseExpression(n),
            DomainFunctionExpression n              => VisitDomainFunctionExpression(n),
            CountStarExpression n                   => VisitCountStarExpression(n),
            FunctionCallExpression n                => VisitFunctionCallExpression(n),

            _ => throw new InvalidOperationException(
                $"SqlSyntaxVisitor<TResult>.Visit has no dispatch arm for node type {node.GetType().FullName}. " +
                "Add a case to the switch in SqlSyntaxVisitor<TResult>.Visit and a corresponding Visit method."),
        };
    }

    public virtual TResult? DefaultVisit(SqlSyntaxNode node) => default;

    // Statements
    public virtual TResult? VisitSqlStatement(SqlStatement n) => DefaultVisit(n);
    public virtual TResult? VisitParametersDeclaration(ParametersDeclaration n) => DefaultVisit(n);
    public virtual TResult? VisitParameterDeclaration(ParameterDeclaration n) => DefaultVisit(n);
    public virtual TResult? VisitFormReferenceParameterName(FormReferenceParameterName n) => DefaultVisit(n);
    public virtual TResult? VisitBracketedParameterName(BracketedParameterName n) => DefaultVisit(n);
    public virtual TResult? VisitSimpleParameterName(SimpleParameterName n) => DefaultVisit(n);
    public virtual TResult? VisitSqlTypeName(SqlTypeName n) => DefaultVisit(n);
    public virtual TResult? VisitUnionStatement(UnionStatement n) => DefaultVisit(n);
    public virtual TResult? VisitParenthesizedSelect(ParenthesizedSelect n) => DefaultVisit(n);
    public virtual TResult? VisitSelectCore(SelectCore n) => DefaultVisit(n);
    public virtual TResult? VisitTopClause(TopClause n) => DefaultVisit(n);
    public virtual TResult? VisitStarSelectList(StarSelectList n) => DefaultVisit(n);
    public virtual TResult? VisitColumnSelectList(ColumnSelectList n) => DefaultVisit(n);
    public virtual TResult? VisitQualifiedStarSelectItem(QualifiedStarSelectItem n) => DefaultVisit(n);
    public virtual TResult? VisitExpressionSelectItem(ExpressionSelectItem n) => DefaultVisit(n);
    public virtual TResult? VisitFromClause(FromClause n) => DefaultVisit(n);
    public virtual TResult? VisitSimpleTableSource(SimpleTableSource n) => DefaultVisit(n);
    public virtual TResult? VisitSubqueryTableSource(SubqueryTableSource n) => DefaultVisit(n);
    public virtual TResult? VisitParenthesizedTableSource(ParenthesizedTableSource n) => DefaultVisit(n);
    public virtual TResult? VisitJoinClause(JoinClause n) => DefaultVisit(n);
    public virtual TResult? VisitOrderItem(OrderItem n) => DefaultVisit(n);
    public virtual TResult? VisitTransformStatement(TransformStatement n) => DefaultVisit(n);
    public virtual TResult? VisitUpdateStatement(UpdateStatement n) => DefaultVisit(n);
    public virtual TResult? VisitAssignment(Assignment n) => DefaultVisit(n);
    public virtual TResult? VisitInsertStatement(InsertStatement n) => DefaultVisit(n);
    public virtual TResult? VisitValuesSource(ValuesSource n) => DefaultVisit(n);
    public virtual TResult? VisitSelectSource(SelectSource n) => DefaultVisit(n);
    public virtual TResult? VisitDeleteStatement(DeleteStatement n) => DefaultVisit(n);

    // Expressions
    public virtual TResult? VisitBinaryExpression(BinaryExpression n) => DefaultVisit(n);
    public virtual TResult? VisitUnaryExpression(UnaryExpression n) => DefaultVisit(n);
    public virtual TResult? VisitBetweenExpression(BetweenExpression n) => DefaultVisit(n);
    public virtual TResult? VisitNotBetweenExpression(NotBetweenExpression n) => DefaultVisit(n);
    public virtual TResult? VisitIsNullExpression(IsNullExpression n) => DefaultVisit(n);
    public virtual TResult? VisitIsNotNullExpression(IsNotNullExpression n) => DefaultVisit(n);
    public virtual TResult? VisitInListExpression(InListExpression n) => DefaultVisit(n);
    public virtual TResult? VisitInSubqueryExpression(InSubqueryExpression n) => DefaultVisit(n);
    public virtual TResult? VisitExistsExpression(ExistsExpression n) => DefaultVisit(n);
    public virtual TResult? VisitSubqueryExpression(SubqueryExpression n) => DefaultVisit(n);
    public virtual TResult? VisitParenthesizedExpression(ParenthesizedExpression n) => DefaultVisit(n);
    public virtual TResult? VisitIntegerLiteralExpression(IntegerLiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitDecimalLiteralExpression(DecimalLiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitStringLiteralExpression(StringLiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitDateLiteralExpression(DateLiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitTrueLiteralExpression(TrueLiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitFalseLiteralExpression(FalseLiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitNullLiteralExpression(NullLiteralExpression n) => DefaultVisit(n);
    public virtual TResult? VisitFormReference(FormReference n) => DefaultVisit(n);
    public virtual TResult? VisitFormsRoot(FormsRoot n) => DefaultVisit(n);
    public virtual TResult? VisitReportsRoot(ReportsRoot n) => DefaultVisit(n);
    public virtual TResult? VisitRefSegment(RefSegment n) => DefaultVisit(n);
    public virtual TResult? VisitColumnReference(ColumnReference n) => DefaultVisit(n);
    public virtual TResult? VisitParameterPlaceholderExpression(ParameterPlaceholderExpression n) => DefaultVisit(n);
    public virtual TResult? VisitIIfExpression(IIfExpression n) => DefaultVisit(n);
    public virtual TResult? VisitSwitchExpression(SwitchExpression n) => DefaultVisit(n);
    public virtual TResult? VisitSwitchPair(SwitchPair n) => DefaultVisit(n);
    public virtual TResult? VisitChooseExpression(ChooseExpression n) => DefaultVisit(n);
    public virtual TResult? VisitDomainFunctionExpression(DomainFunctionExpression n) => DefaultVisit(n);
    public virtual TResult? VisitCountStarExpression(CountStarExpression n) => DefaultVisit(n);
    public virtual TResult? VisitFunctionCallExpression(FunctionCallExpression n) => DefaultVisit(n);
}
