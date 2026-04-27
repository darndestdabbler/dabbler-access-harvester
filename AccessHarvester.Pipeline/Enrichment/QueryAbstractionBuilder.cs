using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AccessHarvester.Sql.Parser.Ast;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Builds a <see cref="QueryAbstraction"/> from a parsed JET SQL
/// <see cref="SqlStatement"/>. Walks the AST, populates as many facets
/// as the input unambiguously determines, and leaves slots that
/// require resolved symbol-table information null for downstream
/// pipeline stages to fill.
/// </summary>
/// <remarks>
/// The builder is intentionally pure (no I/O, no symbol-table
/// resolution) so it stays unit-testable per the
/// <c>project-guidance.md</c> Testing Hierarchy.
/// <para>
/// <see cref="QueryParameterAbstraction.Type"/> is populated for
/// parameters with an explicit type keyword in the
/// <c>PARAMETERS</c> clause (e.g., <c>Long</c>, <c>DateTime</c>).
/// Type information for implicit form-reference parameters and
/// the return-shape fields (<see cref="ReturnColumn.Type"/>,
/// <see cref="ReturnColumn.Nullable"/>) require a resolved symbol
/// table and are left null for downstream pipeline stages.
/// </para>
/// </remarks>
public static class QueryAbstractionBuilder
{
    /// <summary>
    /// Build a <see cref="QueryAbstraction"/> from a parsed
    /// <see cref="SqlStatement"/>. The <paramref name="sourceQueryName"/>
    /// is the saved-query name (or a synthetic id for inline SQL); it
    /// drives <see cref="QueryAbstraction.SuggestedMethodName"/>.
    /// </summary>
    public static QueryAbstraction Build(string sourceQueryName, SqlStatement statement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceQueryName);
        ArgumentNullException.ThrowIfNull(statement);

        var declaredParameters = ExtractDeclaredParameters(statement.Parameters);
        var (shape, primaryFrom, joins, where, groupBy, having, orderBy, returnShape, aggregation) =
            DescribeBody(statement.Body);

        return new QueryAbstraction
        {
            SourceQueryName = sourceQueryName,
            Shape = shape,
            SuggestedMethodName = SuggestMethodName(sourceQueryName, shape),
            Parameters = declaredParameters,
            ReturnShape = returnShape,
            PrimaryFrom = primaryFrom,
            Joins = joins,
            Where = where,
            GroupBy = groupBy,
            Having = having,
            OrderBy = orderBy,
            Aggregation = aggregation,
        };
    }

    // ── Method-name suggestion ─────────────────────────────────────────

    /// <summary>
    /// Derive a PascalCase method-name suggestion from a saved-query name.
    /// Strips Hungarian-style prefixes (<c>qry</c>, <c>qsel</c>, etc.) and
    /// non-identifier characters, then prepends a verb based on
    /// <paramref name="shape"/>. Returns null when the source yields no
    /// usable letters or digits.
    /// </summary>
    public static string? SuggestMethodName(string sourceName, QueryShape shape)
    {
        var alnum = new StringBuilder(sourceName.Length);
        foreach (var ch in sourceName)
        {
            if (char.IsLetterOrDigit(ch))
                alnum.Append(ch);
            else if (ch is '_' or '-' or ' ')
                alnum.Append(' ');
            // drop everything else (brackets, punctuation, etc.)
        }
        var stripped = alnum.ToString().Trim();
        if (stripped.Length == 0) return null;

        // Strip leading qry / qrySomething Hungarian prefix, plus any
        // common "tbl"/"q"/"sp" prefixes the source might carry.
        stripped = StripHungarianPrefix(stripped);

        var pascal = ToPascalCase(stripped);
        if (pascal.Length == 0) return null;

        // Prepend a verb based on shape so the method reads naturally.
        return shape switch
        {
            QueryShape.Select => $"Get{pascal}",
            QueryShape.Union => $"Get{pascal}",
            QueryShape.Transform => $"Get{pascal}",
            QueryShape.Insert => $"Insert{pascal}",
            QueryShape.Update => $"Update{pascal}",
            QueryShape.Delete => $"Delete{pascal}",
            _ => pascal,
        };
    }

    private static string StripHungarianPrefix(string s)
    {
        // Common Access-author conventions: qry, qsel, qupd, qdel, qmkt, qapp.
        string[] prefixes = ["qsel", "qupd", "qdel", "qmkt", "qapp", "qry", "qu", "q"];
        foreach (var p in prefixes)
        {
            if (s.Length > p.Length &&
                s.StartsWith(p, StringComparison.OrdinalIgnoreCase) &&
                char.IsUpper(s[p.Length]))
            {
                return s[p.Length..];
            }
        }
        return s;
    }

    private static string ToPascalCase(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool capitaliseNext = true;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                capitaliseNext = true;
                continue;
            }
            sb.Append(capitaliseNext ? char.ToUpperInvariant(ch) : ch);
            capitaliseNext = false;
        }
        return sb.ToString();
    }

    // ── Parameter extraction ───────────────────────────────────────────

    private static IReadOnlyList<QueryParameterAbstraction>? ExtractDeclaredParameters(
        ParametersDeclaration? declaration)
    {
        if (declaration is null || declaration.Parameters.Count == 0) return null;

        var result = new List<QueryParameterAbstraction>(declaration.Parameters.Count);
        foreach (var p in declaration.Parameters)
        {
            var (sourceName, origin, derivedFrom) = DescribeParameterName(p.Name);
            var name = ToCamelCase(LeafSegment(sourceName));
            result.Add(new QueryParameterAbstraction
            {
                Name = name,
                SourceName = sourceName,
                Origin = origin,
                DerivedFrom = derivedFrom,
                Type = NormaliseDataTypeKeyword(p.TypeName.TypeKeyword),
            });
        }
        return result;
    }

    private static (string SourceName, ParameterOrigin Origin, string? DerivedFrom)
        DescribeParameterName(ParameterName name) => name switch
        {
            FormReferenceParameterName fr => (
                ReconstructFormReference(fr.Reference),
                fr.Reference.Root is ReportsRoot ? ParameterOrigin.ReportReference : ParameterOrigin.FormReference,
                $"{ReconstructFormReference(fr.Reference)} (declared parameter)"),
            BracketedParameterName b => (b.Name, ParameterOrigin.Declared, $"[{b.Name}] (declared parameter)"),
            SimpleParameterName s => (s.Name, ParameterOrigin.Declared, $"{s.Name} (declared parameter)"),
            _ => ("?", ParameterOrigin.Unknown, null),
        };

    private static string LeafSegment(string sourceName)
    {
        // Forms!frmX!ctlY -> ctlY; [Enter Customer ID] -> Enter Customer ID; foo -> foo.
        var bang = sourceName.LastIndexOf('!');
        var seg = bang >= 0 ? sourceName[(bang + 1)..] : sourceName;
        return seg.Trim('[', ']', ' ');
    }

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // If the source has spaces, PascalCase then lower the first char.
        var pascal = ToPascalCase(s);
        if (pascal.Length == 0) return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static string? NormaliseDataTypeKeyword(string? kw)
    {
        if (string.IsNullOrEmpty(kw)) return null;
        return kw.ToUpperInvariant() switch
        {
            "LONG" or "INTEGER" or "INT" => "long",
            "SHORT" => "short",
            "BYTE" => "byte",
            "DOUBLE" or "SINGLE" or "CURRENCY" => "double",
            "DATETIME" or "DATE" or "TIME" => "DateTime",
            "TEXT" or "MEMO" or "VARCHAR" or "CHAR" => "string",
            "YESNO" or "BIT" or "BOOLEAN" => "bool",
            "GUID" => "Guid",
            "BINARY" or "OLEOBJECT" => "byte[]",
            _ => kw,
        };
    }

    private static string ReconstructFormReference(FormReference fr)
    {
        var sb = new StringBuilder();
        sb.Append(fr.Root is ReportsRoot ? "[Reports]" : "[Forms]");
        for (var i = 0; i < fr.Segments.Count; i++)
        {
            var op = fr.Operators.Count > i ? fr.Operators[i] : RefAccessOperator.Bang;
            sb.Append(op == RefAccessOperator.Dot ? '.' : '!');
            sb.Append('[').Append(fr.Segments[i].Name).Append(']');
        }
        return sb.ToString();
    }

    // ── Body decomposition ─────────────────────────────────────────────

    private static (
        QueryShape Shape,
        TableSourceDescriptor? PrimaryFrom,
        IReadOnlyList<JoinDescriptor>? Joins,
        PredicateNode? Where,
        IReadOnlyList<ExpressionDescriptor>? GroupBy,
        PredicateNode? Having,
        IReadOnlyList<OrderByDescriptor>? OrderBy,
        IReadOnlyList<ReturnColumn>? ReturnShape,
        AggregationMarkers? Aggregation)
        DescribeBody(DmlStatement body) => body switch
        {
            SelectCore sc => DescribeSelect(sc),
            UnionStatement u => DescribeUnion(u),
            ParenthesizedSelect ps => DescribeBody(ps.Inner),
            TransformStatement => (QueryShape.Transform, null, null, null, null, null, null, null, null),
            UpdateStatement up => DescribeUpdate(up),
            InsertStatement => (QueryShape.Insert, null, null, null, null, null, null, null, null),
            DeleteStatement del => DescribeDelete(del),
            _ => (QueryShape.Unknown, null, null, null, null, null, null, null, null),
        };

    // The AST builder wraps every SELECT in a UnionStatement, even when
    // only one SelectStatement is present. Treat a single-Select union as
    // a plain SELECT so callers see the friendlier shape; reserve
    // QueryShape.Union for the genuine multi-select case.
    private static (
        QueryShape, TableSourceDescriptor?, IReadOnlyList<JoinDescriptor>?,
        PredicateNode?, IReadOnlyList<ExpressionDescriptor>?, PredicateNode?,
        IReadOnlyList<OrderByDescriptor>?, IReadOnlyList<ReturnColumn>?, AggregationMarkers?)
        DescribeUnion(UnionStatement u)
    {
        if (u.Selects.Count == 1)
        {
            return u.Selects[0] switch
            {
                SelectCore sc => DescribeSelect(sc),
                ParenthesizedSelect ps => DescribeBody(ps.Inner),
                _ => (QueryShape.Unknown, null, null, null, null, null, null, null, null),
            };
        }
        return (QueryShape.Union, null, null, null, null, null, null, null, null);
    }

    private static (
        QueryShape, TableSourceDescriptor?, IReadOnlyList<JoinDescriptor>?,
        PredicateNode?, IReadOnlyList<ExpressionDescriptor>?, PredicateNode?,
        IReadOnlyList<OrderByDescriptor>?, IReadOnlyList<ReturnColumn>?, AggregationMarkers?)
        DescribeSelect(SelectCore sc)
    {
        var (primary, joins) = DescribeFrom(sc.From);
        var where = sc.Where is null ? null : ConvertPredicate(sc.Where);
        var having = sc.Having is null ? null : ConvertPredicate(sc.Having);
        var groupBy = sc.GroupBy is null ? null : sc.GroupBy.Select(ConvertExpression).ToArray();
        var orderBy = sc.OrderBy is null ? null : sc.OrderBy.Select(ConvertOrderItem).ToArray();
        var (returnShape, hasAggregates) = DescribeReturnShape(sc.SelectList);
        var aggregation = BuildAggregationMarkers(sc, hasAggregates, hasGrouping: groupBy is not null);
        return (QueryShape.Select, primary, joins, where, groupBy, having, orderBy, returnShape, aggregation);
    }

    private static (
        QueryShape, TableSourceDescriptor?, IReadOnlyList<JoinDescriptor>?,
        PredicateNode?, IReadOnlyList<ExpressionDescriptor>?, PredicateNode?,
        IReadOnlyList<OrderByDescriptor>?, IReadOnlyList<ReturnColumn>?, AggregationMarkers?)
        DescribeUpdate(UpdateStatement up)
    {
        var (primary, joins) = DescribeFrom(up.Target);
        var where = up.Where is null ? null : ConvertPredicate(up.Where);
        return (QueryShape.Update, primary, joins, where, null, null, null, null, null);
    }

    private static (
        QueryShape, TableSourceDescriptor?, IReadOnlyList<JoinDescriptor>?,
        PredicateNode?, IReadOnlyList<ExpressionDescriptor>?, PredicateNode?,
        IReadOnlyList<OrderByDescriptor>?, IReadOnlyList<ReturnColumn>?, AggregationMarkers?)
        DescribeDelete(DeleteStatement del)
    {
        var (primary, joins) = DescribeFrom(del.From);
        var where = del.Where is null ? null : ConvertPredicate(del.Where);
        return (QueryShape.Delete, primary, joins, where, null, null, null, null, null);
    }

    // ── FROM / JOINs ───────────────────────────────────────────────────

    private static (TableSourceDescriptor? Primary, IReadOnlyList<JoinDescriptor>? Joins)
        DescribeFrom(FromClause? from)
    {
        if (from is null || from.Sources.Count == 0) return (null, null);
        return DescribeFrom(from.Sources[0]);
    }

    private static (TableSourceDescriptor? Primary, IReadOnlyList<JoinDescriptor>? Joins)
        DescribeFrom(TableSource source)
    {
        var primary = DescribeTableSource(source);
        var joins = CollectJoins(source);
        return (primary, joins.Count == 0 ? null : joins);
    }

    private static TableSourceDescriptor DescribeTableSource(TableSource source) => source switch
    {
        SimpleTableSource s => new TableSourceDescriptor { TableName = s.TableName, Alias = s.Alias },
        SubqueryTableSource sq => new TableSourceDescriptor { Alias = sq.Alias, IsSubquery = true },
        ParenthesizedTableSource p => DescribeTableSource(p.Inner),
        _ => new TableSourceDescriptor(),
    };

    private static List<JoinDescriptor> CollectJoins(TableSource source)
    {
        var joins = new List<JoinDescriptor>();
        IReadOnlyList<JoinClause> rawJoins = source switch
        {
            SimpleTableSource s => s.Joins,
            SubqueryTableSource sq => sq.Joins,
            ParenthesizedTableSource p => CollectJoinsFromParenthesised(p),
            _ => Array.Empty<JoinClause>(),
        };
        foreach (var jc in rawJoins)
        {
            joins.Add(new JoinDescriptor
            {
                Kind = MapJoinKind(jc.Kind),
                RightSource = DescribeTableSource(jc.Right),
                On = ConvertPredicate(jc.On),
            });
        }
        return joins;
    }

    private static IReadOnlyList<JoinClause> CollectJoinsFromParenthesised(ParenthesizedTableSource p)
    {
        // The grammar's parenthesised-table source carries both its own
        // joins (outside the parens) and any joins nested inside the
        // inner source. Concatenate so the consumer sees them all in
        // source order.
        var inner = p.Inner switch
        {
            SimpleTableSource s => s.Joins,
            SubqueryTableSource sq => sq.Joins,
            ParenthesizedTableSource pp => CollectJoinsFromParenthesised(pp),
            _ => (IReadOnlyList<JoinClause>)Array.Empty<JoinClause>(),
        };
        if (p.Joins.Count == 0) return inner;
        if (inner.Count == 0) return p.Joins;
        var merged = new List<JoinClause>(inner.Count + p.Joins.Count);
        merged.AddRange(inner);
        merged.AddRange(p.Joins);
        return merged;
    }

    private static JoinDescriptorKind MapJoinKind(JoinKind kind) => kind switch
    {
        JoinKind.Inner => JoinDescriptorKind.Inner,
        JoinKind.Left => JoinDescriptorKind.Left,
        JoinKind.Right => JoinDescriptorKind.Right,
        _ => JoinDescriptorKind.Inner,
    };

    // ── ORDER BY ───────────────────────────────────────────────────────

    private static OrderByDescriptor ConvertOrderItem(OrderItem item) => new()
    {
        Expression = ConvertExpression(item.Expression),
        Direction = item.Direction == AccessHarvester.Sql.Parser.Ast.OrderDirection.Descending
            ? OrderDirection.Descending
            : OrderDirection.Ascending,
    };

    // ── SELECT list -> ReturnShape ─────────────────────────────────────

    private static (IReadOnlyList<ReturnColumn>? Columns, bool HasAggregates)
        DescribeReturnShape(SelectList list)
    {
        if (list is StarSelectList)
        {
            // SELECT * — column list unknown without symbol table. Leave
            // ReturnShape null and signal no aggregates.
            return (null, false);
        }

        if (list is not ColumnSelectList cl) return (null, false);

        var cols = new List<ReturnColumn>(cl.Items.Count);
        bool hasAggregates = false;
        int synthetic = 0;
        foreach (var item in cl.Items)
        {
            switch (item)
            {
                case ExpressionSelectItem expr:
                    var (origin, isAggregate) = ClassifySelectExpression(expr.Value);
                    if (isAggregate) hasAggregates = true;
                    var (name, isSynthetic) = NameSelectColumn(expr, ref synthetic);
                    cols.Add(new ReturnColumn
                    {
                        Name = name,
                        Origin = origin,
                        SourceExpression = ReconstructText(expr.Value),
                        HasSyntheticName = isSynthetic,
                    });
                    break;
                case QualifiedStarSelectItem qs:
                    cols.Add(new ReturnColumn
                    {
                        Name = $"{qs.TableQualifier}.*",
                        Origin = ReturnColumnOrigin.Unknown,
                        SourceExpression = $"{qs.TableQualifier}.*",
                    });
                    break;
            }
        }
        return (cols, hasAggregates);
    }

    private static (ReturnColumnOrigin Origin, bool IsAggregate) ClassifySelectExpression(Expression e) =>
        e switch
        {
            ColumnReference => (ReturnColumnOrigin.Direct, false),
            CountStarExpression => (ReturnColumnOrigin.Aggregate, true),
            FunctionCallExpression fc when IsAggregateName(fc.FunctionName) => (ReturnColumnOrigin.Aggregate, true),
            FunctionCallExpression => (ReturnColumnOrigin.Computed, false),
            IIfExpression or SwitchExpression or ChooseExpression or DomainFunctionExpression
                or BinaryExpression or UnaryExpression or BetweenExpression or NotBetweenExpression
                or IsNullExpression or IsNotNullExpression or InListExpression or InSubqueryExpression
                or ParenthesizedExpression
                => (ReturnColumnOrigin.Computed, false),
            IntegerLiteralExpression or DecimalLiteralExpression or StringLiteralExpression
                or DateLiteralExpression or TrueLiteralExpression or FalseLiteralExpression
                or NullLiteralExpression
                => (ReturnColumnOrigin.Constant, false),
            _ => (ReturnColumnOrigin.Unknown, false),
        };

    private static (string Name, bool Synthetic) NameSelectColumn(ExpressionSelectItem item, ref int syntheticCounter)
    {
        if (!string.IsNullOrEmpty(item.Alias)) return (item.Alias, false);
        // No alias — try to derive from a column reference.
        if (item.Value is ColumnReference cr) return (cr.ColumnName, false);
        if (item.Value is FunctionCallExpression fc)
        {
            // Function name as default — e.g., SELECT Nz(...) -> "Nz".
            return (fc.FunctionName, true);
        }
        syntheticCounter++;
        return ($"Expr{syntheticCounter}", true);
    }

    private static bool IsAggregateName(string name) =>
        AggregateFunctionNames.Contains(name);

    private static readonly HashSet<string> AggregateFunctionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "COUNT", "SUM", "AVG", "MIN", "MAX", "FIRST", "LAST", "STDEV", "STDEVP", "VAR", "VARP",
    };

    private static AggregationMarkers? BuildAggregationMarkers(SelectCore sc, bool hasAggregates, bool hasGrouping)
    {
        var isDistinct = sc.Quantifier == SelectQuantifier.Distinct;
        var isDistinctRow = sc.Quantifier == SelectQuantifier.DistinctRow;
        if (!hasAggregates && !hasGrouping && !isDistinct && !isDistinctRow && sc.Top is null)
            return null;
        return new AggregationMarkers
        {
            IsDistinct = isDistinct,
            IsDistinctRow = isDistinctRow,
            TopN = sc.Top?.N,
            TopIsPercent = sc.Top?.Percent ?? false,
            HasAggregates = hasAggregates,
            HasGrouping = hasGrouping,
        };
    }

    // ── Predicate conversion ───────────────────────────────────────────

    internal static PredicateNode ConvertPredicate(Expression e) => e switch
    {
        ParenthesizedExpression pe => ConvertPredicate(pe.Inner),
        UnaryExpression { Operator: AccessHarvester.Sql.Parser.Ast.UnaryOperator.Not } u
            => new NotPredicate(ConvertPredicate(u.Operand)),
        BinaryExpression be when be.Operator == BinaryOperator.And
            => FlattenAnd(be),
        BinaryExpression be when be.Operator == BinaryOperator.Or
            => FlattenOr(be),
        BinaryExpression be when ComparisonOperatorText(be.Operator) is { } op
            => new ComparisonPredicate(op, ConvertExpression(be.Left), ConvertExpression(be.Right)),
        BinaryExpression be when be.Operator is BinaryOperator.Like or BinaryOperator.NotLike
            => new LikePredicate(
                ConvertExpression(be.Left),
                ConvertExpression(be.Right),
                Negated: be.Operator == BinaryOperator.NotLike),
        BetweenExpression bx
            => new BetweenPredicate(ConvertExpression(bx.Operand), ConvertExpression(bx.Low), ConvertExpression(bx.High), Negated: false),
        NotBetweenExpression bxn
            => new BetweenPredicate(ConvertExpression(bxn.Operand), ConvertExpression(bxn.Low), ConvertExpression(bxn.High), Negated: true),
        IsNullExpression isn
            => new IsNullPredicate(ConvertExpression(isn.Operand), Negated: false),
        IsNotNullExpression isnn
            => new IsNullPredicate(ConvertExpression(isnn.Operand), Negated: true),
        InListExpression il
            => new InPredicate(ConvertExpression(il.Operand), il.Values.Select(ConvertExpression).ToArray(), il.Negated),
        _ => new OpaquePredicate(ConvertExpression(e)),
    };

    private static AndPredicate FlattenAnd(BinaryExpression be)
    {
        var ops = new List<PredicateNode>();
        FlattenInto(be, BinaryOperator.And, ops);
        return new AndPredicate(ops);
    }

    private static OrPredicate FlattenOr(BinaryExpression be)
    {
        var ops = new List<PredicateNode>();
        FlattenInto(be, BinaryOperator.Or, ops);
        return new OrPredicate(ops);
    }

    private static void FlattenInto(Expression e, BinaryOperator op, List<PredicateNode> sink)
    {
        if (e is BinaryExpression be && be.Operator == op)
        {
            FlattenInto(be.Left, op, sink);
            FlattenInto(be.Right, op, sink);
            return;
        }
        if (e is ParenthesizedExpression pe)
        {
            FlattenInto(pe.Inner, op, sink);
            return;
        }
        sink.Add(ConvertPredicate(e));
    }

    private static string? ComparisonOperatorText(BinaryOperator op) => op switch
    {
        BinaryOperator.Equal => "=",
        BinaryOperator.NotEqual => "<>",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterOrEqual => ">=",
        _ => null,
    };

    // ── Expression conversion ──────────────────────────────────────────

    internal static ExpressionDescriptor ConvertExpression(Expression e) => e switch
    {
        ColumnReference cr => new ExpressionDescriptor
        {
            Display = ReconstructText(cr),
            Kind = ExpressionKind.Column,
            ColumnName = cr.ColumnName,
            TableQualifier = cr.TableQualifier,
        },
        FormReference fr => new ExpressionDescriptor
        {
            Display = ReconstructFormReference(fr),
            Kind = fr.Root is ReportsRoot ? ExpressionKind.ReportReference : ExpressionKind.FormReference,
            FormReferencePath = ReconstructFormReference(fr),
        },
        IntegerLiteralExpression i => new ExpressionDescriptor
        {
            Display = i.RawText,
            Kind = ExpressionKind.Literal,
            LiteralKind = LiteralKind.Number,
        },
        DecimalLiteralExpression d => new ExpressionDescriptor
        {
            Display = d.RawText,
            Kind = ExpressionKind.Literal,
            LiteralKind = LiteralKind.Number,
        },
        StringLiteralExpression s => new ExpressionDescriptor
        {
            Display = s.RawText,
            Kind = ExpressionKind.Literal,
            LiteralKind = LiteralKind.String,
        },
        DateLiteralExpression dt => new ExpressionDescriptor
        {
            Display = dt.RawText,
            Kind = ExpressionKind.Literal,
            LiteralKind = LiteralKind.Date,
        },
        TrueLiteralExpression => new ExpressionDescriptor
        {
            Display = "TRUE",
            Kind = ExpressionKind.Literal,
            LiteralKind = LiteralKind.Boolean,
        },
        FalseLiteralExpression => new ExpressionDescriptor
        {
            Display = "FALSE",
            Kind = ExpressionKind.Literal,
            LiteralKind = LiteralKind.Boolean,
        },
        NullLiteralExpression => new ExpressionDescriptor
        {
            Display = "NULL",
            Kind = ExpressionKind.Literal,
            LiteralKind = LiteralKind.Null,
        },
        ParameterPlaceholderExpression => new ExpressionDescriptor
        {
            Display = "?",
            Kind = ExpressionKind.Parameter,
        },
        FunctionCallExpression fc => new ExpressionDescriptor
        {
            Display = ReconstructText(fc),
            Kind = ExpressionKind.FunctionCall,
            FunctionName = fc.FunctionName,
        },
        IIfExpression ii => new ExpressionDescriptor
        {
            Display = ReconstructText(ii),
            Kind = ExpressionKind.FunctionCall,
            FunctionName = "IIf",
        },
        SwitchExpression sw => new ExpressionDescriptor
        {
            Display = ReconstructText(sw),
            Kind = ExpressionKind.FunctionCall,
            FunctionName = "Switch",
        },
        ChooseExpression ch => new ExpressionDescriptor
        {
            Display = ReconstructText(ch),
            Kind = ExpressionKind.FunctionCall,
            FunctionName = "Choose",
        },
        DomainFunctionExpression dfn => new ExpressionDescriptor
        {
            Display = ReconstructText(dfn),
            Kind = ExpressionKind.FunctionCall,
            FunctionName = dfn.FunctionName,
        },
        CountStarExpression => new ExpressionDescriptor
        {
            Display = "COUNT(*)",
            Kind = ExpressionKind.FunctionCall,
            FunctionName = "COUNT",
        },
        SubqueryExpression or InSubqueryExpression or ExistsExpression => new ExpressionDescriptor
        {
            Display = ReconstructText(e),
            Kind = ExpressionKind.Subquery,
        },
        BinaryExpression or UnaryExpression or ParenthesizedExpression
            or BetweenExpression or NotBetweenExpression or IsNullExpression or IsNotNullExpression
            or InListExpression
            => new ExpressionDescriptor
            {
                Display = ReconstructText(e),
                Kind = ExpressionKind.Compound,
            },
        _ => new ExpressionDescriptor
        {
            Display = ReconstructText(e),
            Kind = ExpressionKind.Unknown,
        },
    };

    // ── Verbatim text reconstruction ───────────────────────────────────
    //
    // The SQL parser AST does not carry source text on every node. For
    // facets that need a Display string we reconstruct a canonical text
    // form. The reconstruction is intentionally conservative — operators
    // are rendered with single spaces, and parenthesised groups are
    // preserved — so the output can be surfaced as a generated comment
    // alongside the original SQL without claiming to be byte-for-byte
    // identical.

    internal static string ReconstructText(Expression e) => e switch
    {
        ColumnReference cr => cr.TableQualifier is null
            ? RenderIdentifier(cr.ColumnName, cr.ColumnIsBracketed)
            : $"{cr.TableQualifier}.{RenderIdentifier(cr.ColumnName, cr.ColumnIsBracketed)}",
        FormReference fr => ReconstructFormReference(fr),
        IntegerLiteralExpression i => i.RawText,
        DecimalLiteralExpression d => d.RawText,
        StringLiteralExpression s => s.RawText,
        DateLiteralExpression dt => dt.RawText,
        TrueLiteralExpression => "TRUE",
        FalseLiteralExpression => "FALSE",
        NullLiteralExpression => "NULL",
        ParameterPlaceholderExpression => "?",
        ParenthesizedExpression p => $"({ReconstructText(p.Inner)})",
        UnaryExpression u => $"{UnaryOpText(u.Operator)}{ReconstructText(u.Operand)}",
        BinaryExpression be => $"{ReconstructText(be.Left)} {BinaryOpText(be.Operator)} {ReconstructText(be.Right)}",
        BetweenExpression bx => $"{ReconstructText(bx.Operand)} BETWEEN {ReconstructText(bx.Low)} AND {ReconstructText(bx.High)}",
        NotBetweenExpression bxn => $"{ReconstructText(bxn.Operand)} NOT BETWEEN {ReconstructText(bxn.Low)} AND {ReconstructText(bxn.High)}",
        IsNullExpression isn => $"{ReconstructText(isn.Operand)} IS NULL",
        IsNotNullExpression isnn => $"{ReconstructText(isnn.Operand)} IS NOT NULL",
        InListExpression il => $"{ReconstructText(il.Operand)} {(il.Negated ? "NOT IN" : "IN")} ({string.Join(", ", il.Values.Select(ReconstructText))})",
        FunctionCallExpression fc => $"{fc.FunctionName}({string.Join(", ", fc.Arguments.Select(ReconstructText))})",
        IIfExpression ii => $"IIf({ReconstructText(ii.Condition)}, {ReconstructText(ii.ThenExpr)}, {ReconstructText(ii.ElseExpr)})",
        SwitchExpression sw => $"Switch({string.Join(", ", sw.Pairs.Select(p => $"{ReconstructText(p.Condition)}, {ReconstructText(p.Value)}"))})",
        ChooseExpression ch => $"Choose({ReconstructText(ch.Index)}, {string.Join(", ", ch.Choices.Select(ReconstructText))})",
        DomainFunctionExpression dfn => dfn.CriteriaExpr is null
            ? $"{dfn.FunctionName}({ReconstructText(dfn.FieldExpr)}, {ReconstructText(dfn.DomainExpr)})"
            : $"{dfn.FunctionName}({ReconstructText(dfn.FieldExpr)}, {ReconstructText(dfn.DomainExpr)}, {ReconstructText(dfn.CriteriaExpr)})",
        CountStarExpression => "COUNT(*)",
        InSubqueryExpression isq => $"{ReconstructText(isq.Operand)} {(isq.Negated ? "NOT IN" : "IN")} (...)",
        ExistsExpression => "EXISTS (...)",
        SubqueryExpression => "(...)",
        _ => "<unrecognised expression>",
    };

    private static string RenderIdentifier(string name, bool bracketed) =>
        bracketed ? $"[{name}]" : name;

    private static string UnaryOpText(AccessHarvester.Sql.Parser.Ast.UnaryOperator op) => op switch
    {
        AccessHarvester.Sql.Parser.Ast.UnaryOperator.Not => "NOT ",
        AccessHarvester.Sql.Parser.Ast.UnaryOperator.Plus => "+",
        AccessHarvester.Sql.Parser.Ast.UnaryOperator.Minus => "-",
        _ => "",
    };

    private static string BinaryOpText(BinaryOperator op) => op switch
    {
        BinaryOperator.Or => "OR",
        BinaryOperator.Eqv => "EQV",
        BinaryOperator.Imp => "IMP",
        BinaryOperator.Xor => "XOR",
        BinaryOperator.And => "AND",
        BinaryOperator.Equal => "=",
        BinaryOperator.NotEqual => "<>",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.Like => "LIKE",
        BinaryOperator.NotLike => "NOT LIKE",
        BinaryOperator.In => "IN",
        BinaryOperator.NotIn => "NOT IN",
        BinaryOperator.Concat => "&",
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.IntDivide => "\\",
        BinaryOperator.Modulo => "MOD",
        BinaryOperator.Exponent => "^",
        _ => "?",
    };
}
