using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Outcome of evaluating a footer control's <c>=Sum(...)</c> expression over a
/// result set: either a computed decimal value or an explicit "unsupported"
/// reason (never a silent failure).
/// </summary>
public abstract record SumAggregateResult;

/// <summary>The aggregate evaluated cleanly to <see cref="Value"/>.</summary>
public sealed record SumAggregateComputed(decimal Value) : SumAggregateResult;

/// <summary>
/// The expression is not a derivable whole-result-set <c>Sum</c> aggregate.
/// <see cref="Kind"/> is one of the <c>DisplayTotal*</c> constants on
/// <see cref="SumAggregateEvaluator"/> and <see cref="Detail"/> explains why,
/// so the caller can record a grounded
/// <see cref="ReportAggregates.Skipped"/> item.
/// </summary>
public sealed record SumAggregateUnsupported(string Kind, string Detail) : SumAggregateResult;

/// <summary>
/// Evaluates the closed set of Access footer-display-total expressions the
/// canonical report corpus actually uses: a single <c>=Sum(&lt;arith&gt;)</c>
/// where <c>&lt;arith&gt;</c> is an arithmetic expression over numeric
/// result-set columns. The grammar is intentionally minimal (the survey in
/// <c>harvester-report-displayed-total-parity</c> Session 1 found <c>Sum</c>
/// is the only aggregate function used in any canonical footer):
///
/// <code>
/// controlSource := '=' 'Sum' '(' expr ')'
/// expr          := term (('+' | '-') term)*
/// term          := factor (('*' | '/') factor)*
/// factor        := '-' factor | '(' expr ')' | NUMBER | '[' COLUMN ']'
/// </code>
///
/// <para>
/// This is a real tokenizer + recursive-descent parser, not a regex matcher,
/// per the repo's "prefer a real grammar" principle — but scoped exactly to
/// the closed set per "lean by default". Anything outside the grammar (an
/// unsupported aggregate function such as <c>IIf</c>/<c>Avg</c>, a reference
/// to another report control, a bare scalar column at footer scope, an
/// unresolved or non-numeric column, a parse error) returns a
/// <see cref="SumAggregateUnsupported"/> outcome so the caller records it as
/// Skipped rather than guessing.
/// </para>
///
/// <para>
/// All arithmetic is in <see cref="decimal"/> for financial-grade precision.
/// A row in which any referenced column is null contributes nothing to the
/// sum (Access <c>Sum</c> ignores rows whose argument is Null); a division by
/// zero makes that row's argument Null the same way.
/// </para>
/// </summary>
public static class SumAggregateEvaluator
{
    public const string KindUnsupportedFunction = "DisplayTotalUnsupportedFunction";
    public const string KindNonAggregate = "DisplayTotalNonAggregate";
    public const string KindParseError = "DisplayTotalParseError";
    public const string KindUnresolvedColumn = "DisplayTotalUnresolvedColumn";
    public const string KindNonNumericColumn = "DisplayTotalNonNumericColumn";
    public const string KindNumericOverflow = "DisplayTotalNumericOverflow";

    /// <summary>The only aggregate function the closed set supports.</summary>
    public const string SupportedFunction = "Sum";

    /// <summary>
    /// Evaluate <paramref name="controlSource"/> (the raw footer
    /// <c>ControlSource</c>, including its leading <c>=</c>) against
    /// <paramref name="resultSet"/>.
    /// </summary>
    public static SumAggregateResult Evaluate(string controlSource, DataTable resultSet)
    {
        if (controlSource is null) throw new ArgumentNullException(nameof(controlSource));
        if (resultSet is null) throw new ArgumentNullException(nameof(resultSet));

        string text = controlSource.TrimStart();
        if (text.StartsWith('=')) text = text[1..];
        text = text.TrimStart();

        // Classify the leading shape BEFORE tokenizing the whole expression.
        // An unsupported aggregate (e.g. IIf, whose body legally contains
        // '=' and ',') must be reported as UnsupportedFunction, not as a
        // tokenizer ParseError on its body — so we identify the leading
        // function name with a cheap scan and only tokenize/parse the body
        // once we know it is a Sum.
        int n = 0;
        if (n < text.Length && (char.IsLetter(text[n]) || text[n] == '_'))
        {
            n++;
            while (n < text.Length && (char.IsLetterOrDigit(text[n]) || text[n] == '_')) n++;
        }
        string leadingName = text[..n];
        int afterName = n;
        while (afterName < text.Length && char.IsWhiteSpace(text[afterName])) afterName++;
        bool isFunctionCall = leadingName.Length > 0
            && afterName < text.Length && text[afterName] == '(';

        if (!isFunctionCall)
        {
            return new SumAggregateUnsupported(KindNonAggregate,
                $"Footer expression '{controlSource}' is not a single aggregate-function " +
                "call over result-set columns (e.g. a reference to other report controls, " +
                "a bare column at footer scope, or a compound expression).");
        }

        if (!string.Equals(leadingName, SupportedFunction, StringComparison.OrdinalIgnoreCase))
        {
            return new SumAggregateUnsupported(KindUnsupportedFunction,
                $"Footer expression '{controlSource}' uses unsupported function " +
                $"'{leadingName}'; only the '{SupportedFunction}' aggregate is supported " +
                "by the DisplayTotal contract.");
        }

        // It is Sum( ... ): tokenize and parse the whole expression as
        // Sum '(' expr ')' EOF. A tokenizer failure now is a genuine parse
        // error in the Sum body.
        List<Token> tokens;
        try
        {
            tokens = Tokenize(text);
        }
        catch (ParseException ex)
        {
            return new SumAggregateUnsupported(KindParseError,
                $"Footer expression '{controlSource}' could not be tokenized: {ex.Message}");
        }

        if (tokens.Count < 2 || tokens[0].Type != TokenType.Ident || tokens[1].Type != TokenType.LParen)
        {
            return new SumAggregateUnsupported(KindParseError,
                $"Footer expression '{controlSource}' is malformed after the Sum function name.");
        }

        // Parse the inner arithmetic between the matched Sum( ... ).
        Node ast;
        var parser = new Parser(tokens, startIndex: 2);
        try
        {
            ast = parser.ParseExpression();
            parser.Expect(TokenType.RParen);
            if (!parser.AtEnd)
            {
                return new SumAggregateUnsupported(KindNonAggregate,
                    $"Footer expression '{controlSource}' is a compound expression, not a " +
                    "single Sum(...) aggregate.");
            }
        }
        catch (ParseException ex)
        {
            return new SumAggregateUnsupported(KindParseError,
                $"Footer expression '{controlSource}' has an unsupported inner expression: {ex.Message}");
        }

        // Resolve every referenced column once (case-insensitive) and confirm
        // it is numeric before evaluating any row.
        var columnRefs = new List<string>();
        ast.CollectColumns(columnRefs);
        var resolved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in columnRefs)
        {
            if (resolved.ContainsKey(name)) continue;
            DataColumn? column = FindColumn(resultSet, name);
            if (column is null)
            {
                return new SumAggregateUnsupported(KindUnresolvedColumn,
                    $"Footer expression '{controlSource}' references column '[{name}]', " +
                    "which is not in the executed result set.");
            }
            if (!IsNumeric(column.DataType))
            {
                return new SumAggregateUnsupported(KindNonNumericColumn,
                    $"Footer expression '{controlSource}' references column '[{name}]' of " +
                    $"non-numeric type {column.DataType.Name}.");
            }
            resolved[column.ColumnName] = column.Ordinal;
        }

        try
        {
            decimal acc = 0m;
            foreach (DataRow row in resultSet.Rows)
            {
                decimal? value = ast.Evaluate(row, resolved);
                if (value.HasValue)
                    acc = checked(acc + value.Value);
            }
            return new SumAggregateComputed(acc);
        }
        catch (OverflowException)
        {
            return new SumAggregateUnsupported(KindNumericOverflow,
                $"Footer expression '{controlSource}' overflowed decimal precision while summing.");
        }
    }

    private static DataColumn? FindColumn(DataTable table, string name)
    {
        foreach (DataColumn c in table.Columns)
            if (string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                return c;
        return null;
    }

    private static bool IsNumeric(Type t) =>
        t == typeof(byte) || t == typeof(sbyte) ||
        t == typeof(short) || t == typeof(ushort) ||
        t == typeof(int) || t == typeof(uint) ||
        t == typeof(long) || t == typeof(ulong) ||
        t == typeof(float) || t == typeof(double) ||
        t == typeof(decimal);

    // ── Tokenizer ────────────────────────────────────────────────────────

    private enum TokenType { Number, Ident, Column, LParen, RParen, Plus, Minus, Star, Slash }

    private readonly record struct Token(TokenType Type, string Text);

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            switch (c)
            {
                case '(': tokens.Add(new Token(TokenType.LParen, "(")); i++; continue;
                case ')': tokens.Add(new Token(TokenType.RParen, ")")); i++; continue;
                case '+': tokens.Add(new Token(TokenType.Plus, "+")); i++; continue;
                case '-': tokens.Add(new Token(TokenType.Minus, "-")); i++; continue;
                case '*': tokens.Add(new Token(TokenType.Star, "*")); i++; continue;
                case '/': tokens.Add(new Token(TokenType.Slash, "/")); i++; continue;
            }

            if (c == '[')
            {
                int end = s.IndexOf(']', i + 1);
                if (end < 0) throw new ParseException("unterminated '[' column reference");
                tokens.Add(new Token(TokenType.Column, s.Substring(i + 1, end - i - 1)));
                i = end + 1;
                continue;
            }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                bool seenDot = false;
                while (i < s.Length && (char.IsDigit(s[i]) || (s[i] == '.' && !seenDot)))
                {
                    if (s[i] == '.') seenDot = true;
                    i++;
                }
                tokens.Add(new Token(TokenType.Number, s[start..i]));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                tokens.Add(new Token(TokenType.Ident, s[start..i]));
                continue;
            }

            // Anything else (string literals, '&' concatenation, ',' argument
            // separators, '#' date literals, comparison operators) is outside
            // the closed arithmetic grammar.
            throw new ParseException($"unexpected character '{c}'");
        }
        return tokens;
    }

    // ── Recursive-descent parser ─────────────────────────────────────────

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public Parser(List<Token> tokens, int startIndex)
        {
            _tokens = tokens;
            _pos = startIndex;
        }

        public bool AtEnd => _pos >= _tokens.Count;

        public void Expect(TokenType type)
        {
            if (AtEnd || _tokens[_pos].Type != type)
                throw new ParseException($"expected {type}");
            _pos++;
        }

        public Node ParseExpression()
        {
            Node left = ParseTerm();
            while (!AtEnd && (_tokens[_pos].Type is TokenType.Plus or TokenType.Minus))
            {
                TokenType op = _tokens[_pos].Type;
                _pos++;
                Node right = ParseTerm();
                left = new BinaryNode(op, left, right);
            }
            return left;
        }

        private Node ParseTerm()
        {
            Node left = ParseFactor();
            while (!AtEnd && (_tokens[_pos].Type is TokenType.Star or TokenType.Slash))
            {
                TokenType op = _tokens[_pos].Type;
                _pos++;
                Node right = ParseFactor();
                left = new BinaryNode(op, left, right);
            }
            return left;
        }

        private Node ParseFactor()
        {
            if (AtEnd) throw new ParseException("unexpected end of expression");
            Token t = _tokens[_pos];
            switch (t.Type)
            {
                case TokenType.Minus:
                    _pos++;
                    return new NegateNode(ParseFactor());
                case TokenType.LParen:
                    _pos++;
                    Node inner = ParseExpression();
                    Expect(TokenType.RParen);
                    return inner;
                case TokenType.Number:
                    _pos++;
                    // TryParse (not Parse) so a malformed token the tokenizer
                    // accepted (a lone '.') or an oversized literal becomes a
                    // ParseException → routed to Skipped, never an uncaught
                    // FormatException / OverflowException escaping the evaluator.
                    if (!decimal.TryParse(t.Text, NumberStyles.Number,
                            CultureInfo.InvariantCulture, out decimal num))
                        throw new ParseException($"invalid numeric literal '{t.Text}'");
                    return new NumberNode(num);
                case TokenType.Column:
                    _pos++;
                    return new ColumnNode(t.Text);
                default:
                    // A bare identifier here means a nested function call or an
                    // unbracketed name — outside the closed grammar.
                    throw new ParseException($"unexpected token '{t.Text}'");
            }
        }
    }

    private sealed class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }

    // ── AST ──────────────────────────────────────────────────────────────

    private abstract class Node
    {
        /// <summary>Returns null when any referenced column is null in this row
        /// (or a division by zero occurs), so the row is excluded from the sum.</summary>
        public abstract decimal? Evaluate(DataRow row, IReadOnlyDictionary<string, int> columns);
        public abstract void CollectColumns(List<string> into);
    }

    private sealed class NumberNode : Node
    {
        private readonly decimal _value;
        public NumberNode(decimal value) => _value = value;
        public override decimal? Evaluate(DataRow row, IReadOnlyDictionary<string, int> columns) => _value;
        public override void CollectColumns(List<string> into) { }
    }

    private sealed class ColumnNode : Node
    {
        private readonly string _name;
        public ColumnNode(string name) => _name = name;
        public override decimal? Evaluate(DataRow row, IReadOnlyDictionary<string, int> columns)
        {
            object value = row[columns[_name]];
            if (value is null or DBNull) return null;
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        public override void CollectColumns(List<string> into) => into.Add(_name);
    }

    private sealed class NegateNode : Node
    {
        private readonly Node _operand;
        public NegateNode(Node operand) => _operand = operand;
        public override decimal? Evaluate(DataRow row, IReadOnlyDictionary<string, int> columns)
        {
            decimal? v = _operand.Evaluate(row, columns);
            return v.HasValue ? -v.Value : null;
        }
        public override void CollectColumns(List<string> into) => _operand.CollectColumns(into);
    }

    private sealed class BinaryNode : Node
    {
        private readonly TokenType _op;
        private readonly Node _left;
        private readonly Node _right;
        public BinaryNode(TokenType op, Node left, Node right) { _op = op; _left = left; _right = right; }

        public override decimal? Evaluate(DataRow row, IReadOnlyDictionary<string, int> columns)
        {
            decimal? l = _left.Evaluate(row, columns);
            decimal? r = _right.Evaluate(row, columns);
            if (!l.HasValue || !r.HasValue) return null;
            return _op switch
            {
                TokenType.Plus => checked(l.Value + r.Value),
                TokenType.Minus => checked(l.Value - r.Value),
                TokenType.Star => checked(l.Value * r.Value),
                // Division by zero yields Null (the row is excluded), matching
                // Access's treatment rather than guessing a value.
                TokenType.Slash => r.Value == 0m ? null : l.Value / r.Value,
                _ => throw new InvalidOperationException($"Unexpected operator {_op}."),
            };
        }

        public override void CollectColumns(List<string> into)
        {
            _left.CollectColumns(into);
            _right.CollectColumns(into);
        }
    }
}
