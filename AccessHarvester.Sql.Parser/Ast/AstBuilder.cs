using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Sql.Grammar.Generated;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace AccessHarvester.Sql.Parser.Ast;

// Walks an ANTLR JetSql parse tree and produces a SqlStatement AST.
// Same conventions as VBA AstBuilder:
//   - Deterministic: same parse tree → same AST.
//   - Recovery: any subtree containing an IErrorNode gets RecoveredProvenance.
internal sealed class AstBuilder
{
    private readonly IReadOnlyList<ParseDiagnostic> _diagnostics;
    private readonly string _sourceFile;

    public AstBuilder(IReadOnlyList<ParseDiagnostic> diagnostics, string sourceFile = "")
    {
        _diagnostics = diagnostics;
        _sourceFile = sourceFile;
    }

    public SqlStatement Build(JetSqlParser.SqlStatementContext ctx)
        => BuildSqlStatement(ctx);

    // ── Statement ────────────────────────────────────────────────────

    private SqlStatement BuildSqlStatement(JetSqlParser.SqlStatementContext ctx)
    {
        var prov = MakeProvenance(ctx);
        ParametersDeclaration? parameters = null;
        if (ctx.parametersDecl() is { } pd)
            parameters = BuildParametersDeclaration(pd);

        DmlStatement body;
        if (ctx.selectLikeStatement() is { } sel)
            body = BuildSelectLike(sel);
        else if (ctx.updateStatement() is { } upd)
            body = BuildUpdateStatement(upd);
        else if (ctx.insertStatement() is { } ins)
            body = BuildInsertStatement(ins);
        else
            body = BuildDeleteStatement(ctx.deleteStatement()!);

        return new SqlStatement(prov, parameters, body);
    }

    // ── PARAMETERS ───────────────────────────────────────────────────

    private ParametersDeclaration BuildParametersDeclaration(JetSqlParser.ParametersDeclContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var decls = ctx.parameterDecl().Select(BuildParameterDeclaration).ToList();
        return new ParametersDeclaration(prov, decls);
    }

    private ParameterDeclaration BuildParameterDeclaration(JetSqlParser.ParameterDeclContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var name = BuildParameterName(ctx.parameterName());
        var type = BuildSqlTypeName(ctx.sqlType());
        return new ParameterDeclaration(prov, name, type);
    }

    private ParameterName BuildParameterName(JetSqlParser.ParameterNameContext ctx)
    {
        var prov = MakeProvenance(ctx);
        if (ctx.formReference() is { } fr)
            return new FormReferenceParameterName(prov, BuildFormReference(fr));
        if (ctx.bracketedIdentifier() is { } bi)
            return new BracketedParameterName(prov, StripBrackets(bi.BRACKETED_IDENTIFIER().GetText()));
        return new SimpleParameterName(prov, ResolveIdentifier(ctx.identifier()!));
    }

    private SqlTypeName BuildSqlTypeName(JetSqlParser.SqlTypeContext ctx)
    {
        var prov = MakeProvenance(ctx);
        // The first child that is a keyword gives us the type name; optional
        // (precision, scale) follow in parentheses.
        string typeKeyword = ctx.GetChild(0).GetText().ToUpperInvariant();
        int? precision = null;
        int? scale = null;
        var integers = ctx.INTEGER_LITERAL();
        if (integers.Length >= 1 && int.TryParse(integers[0].GetText(), out int p)) precision = p;
        if (integers.Length >= 2 && int.TryParse(integers[1].GetText(), out int s)) scale = s;
        return new SqlTypeName(prov, typeKeyword, precision, scale);
    }

    // ── SELECT-like ──────────────────────────────────────────────────

    private DmlStatement BuildSelectLike(JetSqlParser.SelectLikeStatementContext ctx)
    {
        if (ctx.transformStatement() is { } t)
            return BuildTransformStatement(t);
        return BuildUnionStatement(ctx.unionStatement()!);
    }

    private UnionStatement BuildUnionStatement(JetSqlParser.UnionStatementContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var selectCtxs = ctx.selectStatement();
        var selects = selectCtxs.Select(BuildSelectStatement).ToList();
        // ALL flags: one per gap between selects. The grammar emits UNION ALL?
        // before each subsequent select; we infer ALL presence by token count.
        var allFlags = new List<bool>();
        var children = ctx.children;
        bool nextAll = false;
        foreach (var child in children)
        {
            if (child is ITerminalNode t2)
            {
                if (t2.Symbol.Type == JetSqlLexer.UNION) nextAll = false;
                else if (t2.Symbol.Type == JetSqlLexer.ALL) nextAll = true;
            }
            else if (child is JetSqlParser.SelectStatementContext)
            {
                allFlags.Add(nextAll);
                nextAll = false;
            }
        }
        return new UnionStatement(prov, selects, allFlags);
    }

    private SelectStatement BuildSelectStatement(JetSqlParser.SelectStatementContext ctx)
    {
        if (ctx.unionStatement() is { } u)
            return new ParenthesizedSelect(MakeProvenance(ctx), BuildUnionStatement(u));
        return BuildSelectCore(ctx.selectCore()!);
    }

    private SelectCore BuildSelectCore(JetSqlParser.SelectCoreContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var quantifier = SelectQuantifier.None;
        if (ctx.selectQuantifier() is { } q)
        {
            quantifier = q.GetText().ToUpperInvariant() switch
            {
                "DISTINCT" => SelectQuantifier.Distinct,
                "DISTINCTROW" => SelectQuantifier.DistinctRow,
                "ALL" => SelectQuantifier.All,
                _ => SelectQuantifier.None
            };
        }
        TopClause? top = ctx.topClause() is { } tc ? BuildTopClause(tc) : null;
        var selectList = BuildSelectList(ctx.selectList());
        FromClause? from = ctx.fromClause() is { } fc ? BuildFromClause(fc) : null;
        Expression? where = ctx.whereClause() is { } wc ? BuildExpression(wc.expression()) : null;
        List<Expression>? groupBy = ctx.groupByClause() is { } gc
            ? gc.expression().Select(BuildExpression).ToList()
            : null;
        Expression? having = ctx.havingClause() is { } hc ? BuildExpression(hc.expression()) : null;
        List<OrderItem>? orderBy = ctx.orderByClause() is { } oc
            ? oc.orderItem().Select(BuildOrderItem).ToList()
            : null;

        return new SelectCore(prov, quantifier, top, selectList, from, where, groupBy, having, orderBy);
    }

    private TopClause BuildTopClause(JetSqlParser.TopClauseContext ctx)
    {
        var prov = MakeProvenance(ctx);
        int n = int.Parse(ctx.INTEGER_LITERAL().GetText());
        bool percent = ctx.PERCENT() is not null;
        return new TopClause(prov, n, percent);
    }

    private SelectList BuildSelectList(JetSqlParser.SelectListContext ctx)
    {
        var prov = MakeProvenance(ctx);
        if (ctx.STAR() is not null)
            return new StarSelectList(prov);
        var items = ctx.selectItem().Select(BuildSelectItem).ToList();
        return new ColumnSelectList(prov, items);
    }

    private SelectItem BuildSelectItem(JetSqlParser.SelectItemContext ctx)
    {
        var prov = MakeProvenance(ctx);
        if (ctx is JetSqlParser.SelectStarQualifiedContext sq)
        {
            string qualifier = ResolveTableQualifier(sq.tableQualifier());
            return new QualifiedStarSelectItem(prov, qualifier);
        }
        var ec = (JetSqlParser.SelectExpressionContext)ctx;
        var expr = BuildExpression(ec.expression());
        string? alias = ec.columnAlias() is { } ca ? ResolveColumnAlias(ca) : null;
        return new ExpressionSelectItem(prov, expr, alias);
    }

    // ── FROM / JOINs ────────────────────────────────────────────────

    private FromClause BuildFromClause(JetSqlParser.FromClauseContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var sources = ctx.tableSource().Select(BuildTableSource).ToList();
        return new FromClause(prov, sources);
    }

    private TableSource BuildTableSource(JetSqlParser.TableSourceContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var joins = ctx.joinChain() is { } jc
            ? jc.joinClause().Select(BuildJoinClause).ToList()
            : new List<JoinClause>();

        if (ctx.atomicTableSource() is { } atomic)
        {
            if (atomic is JetSqlParser.AtomicTableNameSourceContext tn)
            {
                string name = ResolveTableName(tn.tableName());
                string? alias = tn.tableAlias() is { } ta ? ResolveTableAlias(ta) : null;
                return new SimpleTableSource(prov, name, alias, joins);
            }
            else
            {
                var sub = (JetSqlParser.AtomicSubquerySourceContext)atomic;
                var subquery = BuildUnionStatement(sub.unionStatement());
                string? alias = sub.tableAlias() is { } ta ? ResolveTableAlias(ta) : null;
                return new SubqueryTableSource(prov, subquery, alias, joins);
            }
        }

        // Parenthesized table source
        var inner = BuildTableSource(ctx.tableSource()!);
        return new ParenthesizedTableSource(prov, inner, joins);
    }

    private JoinClause BuildJoinClause(JetSqlParser.JoinClauseContext ctx)
    {
        var prov = MakeProvenance(ctx);
        JoinKind kind;
        if (ctx.INNER() is not null)
            kind = JoinKind.Inner;
        else if (ctx.LEFT() is not null)
            kind = JoinKind.Left;
        else
            kind = JoinKind.Right;

        var right = BuildTableSource(ctx.tableSource());
        var on = BuildExpression(ctx.expression());
        return new JoinClause(prov, kind, right, on);
    }

    private OrderItem BuildOrderItem(JetSqlParser.OrderItemContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var expr = BuildExpression(ctx.expression());
        var dir = ctx.DESC() is not null ? OrderDirection.Descending : OrderDirection.Ascending;
        return new OrderItem(prov, expr, dir);
    }

    // ── TRANSFORM ────────────────────────────────────────────────────

    private TransformStatement BuildTransformStatement(JetSqlParser.TransformStatementContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var aggExpr = BuildExpression(ctx.expression(0));
        string? aggAlias = ctx.columnAlias() is { } ca ? ResolveColumnAlias(ca) : null;
        var selectBody = BuildUnionStatement(ctx.unionStatement());
        var pivotExpr = BuildExpression(ctx.expression(1));
        List<Expression>? pivotIn = ctx.expressionList() is { } el
            ? el.expression().Select(BuildExpression).ToList()
            : null;
        return new TransformStatement(prov, aggExpr, aggAlias, selectBody, pivotExpr, pivotIn);
    }

    // ── UPDATE ───────────────────────────────────────────────────────

    private UpdateStatement BuildUpdateStatement(JetSqlParser.UpdateStatementContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var target = BuildTableSource(ctx.tableSource());
        var assignments = ctx.assignmentList().assignment().Select(BuildAssignment).ToList();
        Expression? where = ctx.whereClause() is { } wc ? BuildExpression(wc.expression()) : null;
        return new UpdateStatement(prov, target, assignments, where);
    }

    private Assignment BuildAssignment(JetSqlParser.AssignmentContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var col = BuildColumnReferenceFromAssignment(ctx.columnReference());
        var val = BuildExpression(ctx.expression());
        return new Assignment(prov, col, val);
    }

    // ── INSERT ───────────────────────────────────────────────────────

    private InsertStatement BuildInsertStatement(JetSqlParser.InsertStatementContext ctx)
    {
        var prov = MakeProvenance(ctx);
        string tableName = ResolveTableName(ctx.tableName());
        List<ColumnReference>? columns = ctx.columnListClause() is { } clc
            ? clc.columnReference().Select(BuildColumnReferenceFromAssignment).ToList()
            : null;
        InsertSource source;
        if (ctx.valuesClause() is { } vc)
            source = new ValuesSource(MakeProvenance(vc), vc.expression().Select(BuildExpression).ToList());
        else
            source = new SelectSource(MakeProvenance(ctx.unionStatement()), BuildUnionStatement(ctx.unionStatement()));
        return new InsertStatement(prov, tableName, columns, source);
    }

    // ── DELETE ───────────────────────────────────────────────────────

    private DeleteStatement BuildDeleteStatement(JetSqlParser.DeleteStatementContext ctx)
    {
        var prov = MakeProvenance(ctx);
        SelectList? selectList = ctx.selectList() is { } sl ? BuildSelectList(sl) : null;
        FromClause? from = ctx.fromClause() is { } fc ? BuildFromClause(fc) : null;
        Expression? where = ctx.whereClause() is { } wc ? BuildExpression(wc.expression()) : null;
        return new DeleteStatement(prov, selectList, from, where);
    }

    // ── Expressions ──────────────────────────────────────────────────

    private Expression BuildExpression(JetSqlParser.ExpressionContext ctx)
        => BuildOrExpression(ctx.orExpression());

    private Expression BuildOrExpression(JetSqlParser.OrExpressionContext ctx)
    {
        var parts = ctx.andExpression();
        if (parts.Length == 1) return BuildAndExpression(parts[0]);

        // Operators sit between pairs: collect them from terminal nodes
        var ops = CollectBinaryOperators(ctx, parts.Length - 1,
            t => t.Symbol.Type switch
            {
                JetSqlLexer.OR  => BinaryOperator.Or,
                JetSqlLexer.EQV => BinaryOperator.Eqv,
                JetSqlLexer.IMP => BinaryOperator.Imp,
                JetSqlLexer.XOR => BinaryOperator.Xor,
                _ => (BinaryOperator?)null
            });

        Expression acc = BuildAndExpression(parts[0]);
        for (int i = 0; i < ops.Count; i++)
            acc = new BinaryExpression(MakeProvenance(ctx), acc, ops[i], BuildAndExpression(parts[i + 1]));
        return acc;
    }

    private Expression BuildAndExpression(JetSqlParser.AndExpressionContext ctx)
    {
        var parts = ctx.notExpression();
        if (parts.Length == 1) return BuildNotExpression(parts[0]);
        Expression acc = BuildNotExpression(parts[0]);
        for (int i = 1; i < parts.Length; i++)
            acc = new BinaryExpression(MakeProvenance(ctx), acc, BinaryOperator.And, BuildNotExpression(parts[i]));
        return acc;
    }

    private Expression BuildNotExpression(JetSqlParser.NotExpressionContext ctx)
    {
        if (ctx.NOT() is not null)
            return new UnaryExpression(MakeProvenance(ctx), UnaryOperator.Not, BuildNotExpression(ctx.notExpression()!));
        return BuildComparisonExpression(ctx.comparisonExpression()!);
    }

    private Expression BuildComparisonExpression(JetSqlParser.ComparisonExpressionContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var concats = ctx.concatExpression();
        var left = BuildConcatExpression(concats[0]);

        // IS [NOT] NULL — one operand, no second concat
        if (ctx.NULL() is not null)
            return ctx.NOT() is not null
                ? new IsNotNullExpression(prov, left)
                : new IsNullExpression(prov, left);

        // BETWEEN low AND high — three concatExpressions
        if (ctx.BETWEEN() is not null)
        {
            bool notBetween = ctx.NOT() is not null;
            var low = BuildConcatExpression(concats[1]);
            var high = BuildConcatExpression(concats[2]);
            return notBetween
                ? new NotBetweenExpression(prov, left, low, high)
                : new BetweenExpression(prov, left, low, high);
        }

        // IN (...) — one operand, subquery or expression list
        if (ctx.IN() is not null)
        {
            bool negated = ctx.NOT() is not null;
            if (ctx.unionStatement() is { } us)
                return new InSubqueryExpression(prov, left, negated, BuildUnionStatement(us));
            if (ctx.expressionList() is { } el)
                return new InListExpression(prov, left, negated, el.expression().Select(BuildExpression).ToList());
        }

        // No right-hand side — bare concat expression
        if (concats.Length == 1)
            return left;


        // Two-operand comparison
        var opToken = FindFirstTerminalOf(ctx, t =>
            t.Symbol.Type is JetSqlLexer.EQ or JetSqlLexer.NEQ or JetSqlLexer.LT
                          or JetSqlLexer.LTE or JetSqlLexer.GT or JetSqlLexer.GTE
                          or JetSqlLexer.LIKE);

        if (opToken is null) return left; // shouldn't happen

        BinaryOperator op;
        bool notLike = false;
        if (opToken.Symbol.Type == JetSqlLexer.LIKE)
        {
            notLike = ctx.NOT() is not null;
            op = notLike ? BinaryOperator.NotLike : BinaryOperator.Like;
        }
        else
        {
            op = opToken.Symbol.Type switch
            {
                JetSqlLexer.EQ  => BinaryOperator.Equal,
                JetSqlLexer.NEQ => BinaryOperator.NotEqual,
                JetSqlLexer.LT  => BinaryOperator.LessThan,
                JetSqlLexer.LTE => BinaryOperator.LessOrEqual,
                JetSqlLexer.GT  => BinaryOperator.GreaterThan,
                JetSqlLexer.GTE => BinaryOperator.GreaterOrEqual,
                _               => BinaryOperator.Equal
            };
        }
        var right = BuildConcatExpression(concats[1]);
        return new BinaryExpression(prov, left, op, right);
    }

    private Expression BuildConcatExpression(JetSqlParser.ConcatExpressionContext ctx)
    {
        var parts = ctx.additiveExpression();
        if (parts.Length == 1) return BuildAdditiveExpression(parts[0]);
        Expression acc = BuildAdditiveExpression(parts[0]);
        for (int i = 1; i < parts.Length; i++)
            acc = new BinaryExpression(MakeProvenance(ctx), acc, BinaryOperator.Concat, BuildAdditiveExpression(parts[i]));
        return acc;
    }

    private Expression BuildAdditiveExpression(JetSqlParser.AdditiveExpressionContext ctx)
    {
        var parts = ctx.multiplicativeExpression();
        if (parts.Length == 1) return BuildMultiplicativeExpression(parts[0]);
        var ops = CollectBinaryOperators(ctx, parts.Length - 1,
            t => t.Symbol.Type switch
            {
                JetSqlLexer.PLUS  => BinaryOperator.Add,
                JetSqlLexer.MINUS => BinaryOperator.Subtract,
                _ => (BinaryOperator?)null
            });
        Expression acc = BuildMultiplicativeExpression(parts[0]);
        for (int i = 0; i < ops.Count; i++)
            acc = new BinaryExpression(MakeProvenance(ctx), acc, ops[i], BuildMultiplicativeExpression(parts[i + 1]));
        return acc;
    }

    private Expression BuildMultiplicativeExpression(JetSqlParser.MultiplicativeExpressionContext ctx)
    {
        var parts = ctx.exponentExpression();
        if (parts.Length == 1) return BuildExponentExpression(parts[0]);
        var ops = CollectBinaryOperators(ctx, parts.Length - 1,
            t => t.Symbol.Type switch
            {
                JetSqlLexer.STAR      => BinaryOperator.Multiply,
                JetSqlLexer.SLASH     => BinaryOperator.Divide,
                JetSqlLexer.BACKSLASH => BinaryOperator.IntDivide,
                JetSqlLexer.MOD       => BinaryOperator.Modulo,
                _ => (BinaryOperator?)null
            });
        Expression acc = BuildExponentExpression(parts[0]);
        for (int i = 0; i < ops.Count; i++)
            acc = new BinaryExpression(MakeProvenance(ctx), acc, ops[i], BuildExponentExpression(parts[i + 1]));
        return acc;
    }

    private Expression BuildExponentExpression(JetSqlParser.ExponentExpressionContext ctx)
    {
        var parts = ctx.unaryExpression();
        if (parts.Length == 1) return BuildUnaryExpression(parts[0]);
        Expression acc = BuildUnaryExpression(parts[0]);
        for (int i = 1; i < parts.Length; i++)
            acc = new BinaryExpression(MakeProvenance(ctx), acc, BinaryOperator.Exponent, BuildUnaryExpression(parts[i]));
        return acc;
    }

    private Expression BuildUnaryExpression(JetSqlParser.UnaryExpressionContext ctx)
    {
        if (ctx.PLUS() is not null)
            return new UnaryExpression(MakeProvenance(ctx), UnaryOperator.Plus, BuildUnaryExpression(ctx.unaryExpression()!));
        if (ctx.MINUS() is not null)
            return new UnaryExpression(MakeProvenance(ctx), UnaryOperator.Minus, BuildUnaryExpression(ctx.unaryExpression()!));
        return BuildPrimaryExpression(ctx.primaryExpression()!);
    }

    private Expression BuildPrimaryExpression(JetSqlParser.PrimaryExpressionContext ctx)
    {
        var prov = MakeProvenance(ctx);
        return ctx switch
        {
            JetSqlParser.LitPrimaryContext n    => BuildLiteral(n.literal()),
            JetSqlParser.ExistsPrimaryContext n => new ExistsExpression(prov, BuildUnionStatement(n.unionStatement())),
            JetSqlParser.SubqueryPrimaryContext n => new SubqueryExpression(prov, BuildUnionStatement(n.unionStatement())),
            JetSqlParser.ParenPrimaryContext n  => new ParenthesizedExpression(prov, BuildExpression(n.expression())),
            JetSqlParser.IifPrimaryContext n    => BuildIIf(n.iifFunction()),
            JetSqlParser.SwitchPrimaryContext n => BuildSwitch(n.switchFunction()),
            JetSqlParser.ChoosePrimaryContext n => BuildChoose(n.chooseFunction()),
            JetSqlParser.DomainFunctionPrimaryContext n => BuildDomainFunction(n.domainFunction()),
            JetSqlParser.FunctionCallPrimaryContext n => BuildFunctionCall(n.functionCall()),
            JetSqlParser.FormRefPrimaryContext n => BuildFormReference(n.formReference()),
            JetSqlParser.ColumnRefPrimaryContext n => BuildColumnRefExpr(n.columnReference()),
            JetSqlParser.ParamPlaceholderPrimaryContext _ => new ParameterPlaceholderExpression(prov),
            _ => throw new InvalidOperationException($"Unknown primary expression context: {ctx.GetType().Name}")
        };
    }

    private Expression BuildLiteral(JetSqlParser.LiteralContext ctx)
    {
        var prov = MakeProvenance(ctx);
        return ctx switch
        {
            JetSqlParser.IntegerLitContext n  => new IntegerLiteralExpression(prov, n.INTEGER_LITERAL().GetText()),
            JetSqlParser.DecimalLitContext n  => new DecimalLiteralExpression(prov, n.DECIMAL_LITERAL().GetText()),
            JetSqlParser.StringLitContext n   => new StringLiteralExpression(prov, n.STRING_LITERAL().GetText()),
            JetSqlParser.DateLitContext n     => new DateLiteralExpression(prov, n.DATE_LITERAL().GetText()),
            JetSqlParser.TrueLitContext _     => new TrueLiteralExpression(prov),
            JetSqlParser.FalseLitContext _    => new FalseLiteralExpression(prov),
            JetSqlParser.NullLitContext _     => new NullLiteralExpression(prov),
            _ => throw new InvalidOperationException($"Unknown literal context: {ctx.GetType().Name}")
        };
    }

    private FormReference BuildFormReference(JetSqlParser.FormReferenceContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var root = BuildFormReferenceRoot(ctx.formRefRoot());
        var segments = new List<RefSegment>();
        var operators = new List<RefAccessOperator>();

        var children = ctx.children;
        bool first = true;
        RefAccessOperator pendingOp = RefAccessOperator.Bang;
        foreach (var child in children)
        {
            if (first) { first = false; continue; } // skip root
            if (child is ITerminalNode t)
            {
                if (t.Symbol.Type == JetSqlLexer.BANG) pendingOp = RefAccessOperator.Bang;
                else if (t.Symbol.Type == JetSqlLexer.DOT) pendingOp = RefAccessOperator.Dot;
            }
            else if (child is JetSqlParser.RefSegmentContext seg)
            {
                operators.Add(pendingOp);
                segments.Add(BuildRefSegment(seg));
            }
        }

        return new FormReference(prov, root, segments, operators);
    }

    private FormReferenceRoot BuildFormReferenceRoot(JetSqlParser.FormRefRootContext ctx)
    {
        var prov = MakeProvenance(ctx);
        if (ctx.FORMS() is not null) return new FormsRoot(prov);
        if (ctx.REPORTS() is not null) return new ReportsRoot(prov);
        // bracketed [Forms] / [Reports]
        var text = ctx.bracketedIdentifier().BRACKETED_IDENTIFIER().GetText();
        var inner = StripBrackets(text).ToUpperInvariant();
        return inner == "REPORTS" ? new ReportsRoot(prov) : (FormReferenceRoot)new FormsRoot(prov);
    }

    private RefSegment BuildRefSegment(JetSqlParser.RefSegmentContext ctx)
    {
        var prov = MakeProvenance(ctx);
        if (ctx.bracketedIdentifier() is { } bi)
            return new RefSegment(prov, StripBrackets(bi.BRACKETED_IDENTIFIER().GetText()), true);
        return new RefSegment(prov, ResolveIdentifier(ctx.identifier()!), false);
    }

    private ColumnReference BuildColumnRefExpr(JetSqlParser.ColumnReferenceContext ctx)
    {
        var prov = MakeProvenance(ctx);
        return BuildColumnReferenceCore(prov, ctx);
    }

    private ColumnReference BuildColumnReferenceFromAssignment(JetSqlParser.ColumnReferenceContext ctx)
        => BuildColumnReferenceCore(MakeProvenance(ctx), ctx);

    private ColumnReference BuildColumnReferenceCore(Provenance prov, JetSqlParser.ColumnReferenceContext ctx)
    {
        // table.col OR [bracketed] OR identifier
        if (ctx.tableQualifier() is { } tq)
        {
            string table = ResolveTableQualifier(tq);
            bool isBracketed;
            string col;
            if (ctx.bracketedIdentifier() is { } bi) { col = StripBrackets(bi.BRACKETED_IDENTIFIER().GetText()); isBracketed = true; }
            else { col = ResolveIdentifier(ctx.identifier()!); isBracketed = false; }
            return new ColumnReference(prov, table, col, isBracketed);
        }
        if (ctx.bracketedIdentifier() is { } bi2)
            return new ColumnReference(prov, null, StripBrackets(bi2.BRACKETED_IDENTIFIER().GetText()), true);
        return new ColumnReference(prov, null, ResolveIdentifier(ctx.identifier()!), false);
    }

    private IIfExpression BuildIIf(JetSqlParser.IifFunctionContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var exprs = ctx.expression();
        return new IIfExpression(prov, BuildExpression(exprs[0]), BuildExpression(exprs[1]), BuildExpression(exprs[2]));
    }

    private SwitchExpression BuildSwitch(JetSqlParser.SwitchFunctionContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var exprs = ctx.expression();
        var pairs = new List<SwitchPair>();
        for (int i = 0; i + 1 < exprs.Length; i += 2)
            pairs.Add(new SwitchPair(MakeProvenance(exprs[i]), BuildExpression(exprs[i]), BuildExpression(exprs[i + 1])));
        return new SwitchExpression(prov, pairs);
    }

    private ChooseExpression BuildChoose(JetSqlParser.ChooseFunctionContext ctx)
    {
        var prov = MakeProvenance(ctx);
        var exprs = ctx.expression();
        var index = BuildExpression(exprs[0]);
        var choices = exprs.Skip(1).Select(BuildExpression).ToList();
        return new ChooseExpression(prov, index, choices);
    }

    private DomainFunctionExpression BuildDomainFunction(JetSqlParser.DomainFunctionContext ctx)
    {
        var prov = MakeProvenance(ctx);
        string fnName = ctx.domainFunctionName().GetText().ToUpperInvariant();
        var exprs = ctx.expression();
        Expression? criteria = exprs.Length >= 3 ? BuildExpression(exprs[2]) : null;
        return new DomainFunctionExpression(prov, fnName, BuildExpression(exprs[0]), BuildExpression(exprs[1]), criteria);
    }

    private Expression BuildFunctionCall(JetSqlParser.FunctionCallContext ctx)
    {
        var prov = MakeProvenance(ctx);
        if (ctx is JetSqlParser.CountStarCallContext)
            return new CountStarExpression(prov);
        var gc = (JetSqlParser.GenericFunctionCallContext)ctx;
        string name = ResolveIdentifier(gc.functionName());
        var args = gc.expressionList() is { } el
            ? el.expression().Select(BuildExpression).ToList()
            : new List<Expression>();
        return new FunctionCallExpression(prov, name, args);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private string ResolveIdentifier(JetSqlParser.IdentifierContext ctx)
    {
        if (ctx.IDENTIFIER() is { } id) return id.GetText();
        return ctx.nonReservedKeyword()!.GetText();
    }

    private string ResolveIdentifier(JetSqlParser.FunctionNameContext ctx)
    {
        if (ctx.identifier() is { } id) return ResolveIdentifier(id);
        return ctx.GetText();
    }

    private string ResolveTableQualifier(JetSqlParser.TableQualifierContext ctx)
    {
        if (ctx.bracketedIdentifier() is { } bi) return StripBrackets(bi.BRACKETED_IDENTIFIER().GetText());
        return ResolveIdentifier(ctx.identifier()!);
    }

    private string ResolveTableName(JetSqlParser.TableNameContext ctx)
    {
        if (ctx.bracketedIdentifier() is { } bi) return StripBrackets(bi.BRACKETED_IDENTIFIER().GetText());
        return ResolveIdentifier(ctx.identifier()!);
    }

    private string ResolveTableAlias(JetSqlParser.TableAliasContext ctx)
    {
        if (ctx.bracketedIdentifier() is { } bi) return StripBrackets(bi.BRACKETED_IDENTIFIER().GetText());
        return ResolveIdentifier(ctx.identifier()!);
    }

    private string ResolveColumnAlias(JetSqlParser.ColumnAliasContext ctx)
    {
        if (ctx.bracketedIdentifier() is { } bi) return StripBrackets(bi.BRACKETED_IDENTIFIER().GetText());
        if (ctx.STRING_LITERAL() is { } sl) return sl.GetText();
        return ResolveIdentifier(ctx.identifier()!);
    }

    private static string StripBrackets(string text)
    {
        if (text.Length >= 2 && text[0] == '[' && text[^1] == ']')
            return text[1..^1];
        return text;
    }

    // Collect binary operators between n+1 operands from a rule context's
    // terminal children, using a selector function. Returns n operators.
    private static List<BinaryOperator> CollectBinaryOperators(
        ParserRuleContext ctx,
        int count,
        Func<ITerminalNode, BinaryOperator?> selector)
    {
        var ops = new List<BinaryOperator>(count);
        foreach (var child in ctx.children)
        {
            if (ops.Count >= count) break;
            if (child is ITerminalNode t)
            {
                var op = selector(t);
                if (op.HasValue) ops.Add(op.Value);
            }
        }
        return ops;
    }

    private static ITerminalNode? FindFirstTerminalOf(
        ParserRuleContext ctx,
        Func<ITerminalNode, bool> predicate)
    {
        foreach (var child in ctx.children)
        {
            if (child is ITerminalNode t && predicate(t)) return t;
        }
        return null;
    }

    // ── Provenance helpers ───────────────────────────────────────────

    private Provenance MakeProvenance(ParserRuleContext ctx)
    {
        var start = ctx.Start;
        int line = start?.Line ?? 0;
        int col = start?.Column ?? 0;
        var range = ctx.Start is not null && ctx.Stop is not null
            ? new SourceRange(ctx.Start.StartIndex, ctx.Stop.StopIndex)
            : null;

        if (ContainsErrorNode(ctx))
        {
            string reason = FindNearestDiagnosticReason(line, col);
            return new RecoveredProvenance(reason, line, col, range);
        }
        return new SourceProvenance(line, col, range);
    }

    private static bool ContainsErrorNode(IParseTree tree)
    {
        if (tree is IErrorNode) return true;
        for (int i = 0; i < tree.ChildCount; i++)
            if (ContainsErrorNode(tree.GetChild(i))) return true;
        return false;
    }

    private string FindNearestDiagnosticReason(int line, int col)
    {
        ParseDiagnostic? best = null;
        foreach (var d in _diagnostics)
        {
            if (d.Line <= line && (best is null || d.Line > best.Line
                || (d.Line == best.Line && d.Column > best.Column)))
                best = d;
        }
        return best?.Message ?? "unknown-recovery";
    }
}
