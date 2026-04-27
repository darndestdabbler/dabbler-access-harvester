using AccessHarvester.Sql.Grammar.Generated;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace AccessHarvester.Sql.Parser;

// Augments ANTLR's DefaultErrorStrategy by widening the recovery set
// with JET SQL's natural sync points: statement-starting keywords
// (SELECT, UPDATE, INSERT, DELETE, TRANSFORM, PARAMETERS) and the
// statement terminator (SEMI). This anchors recovery on boundaries the
// SQL author intended rather than letting ANTLR skip past a keyword
// when the broken construct is mid-expression.
internal sealed class StatementBoundaryErrorStrategy : DefaultErrorStrategy
{
    private static readonly IntervalSet SyncTokens = BuildSyncTokens();

    private static IntervalSet BuildSyncTokens()
    {
        var set = new IntervalSet(
            JetSqlLexer.SELECT,
            JetSqlLexer.UPDATE,
            JetSqlLexer.INSERT,
            JetSqlLexer.DELETE,
            JetSqlLexer.TRANSFORM,
            JetSqlLexer.PARAMETERS,
            JetSqlLexer.UNION,
            JetSqlLexer.SEMI);
        return set;
    }

    protected override IntervalSet GetErrorRecoverySet(Antlr4.Runtime.Parser recognizer)
    {
        var baseSet = base.GetErrorRecoverySet(recognizer);
        var union = new IntervalSet();
        union.AddAll(baseSet);
        union.AddAll(SyncTokens);
        return union;
    }
}
