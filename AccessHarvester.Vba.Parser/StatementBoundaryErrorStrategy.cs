using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace AccessHarvester.Vba.Parser;

// Augments ANTLR's DefaultErrorStrategy by widening the recovery set
// with VBA's natural sync points: statement separators (NEWLINE, COLON)
// and module-item leading-keyword tokens (procedure headers and the
// other module-level declaration starts). This anchors recovery on
// boundaries the human-author intended, instead of letting ANTLR skip
// past a procedure header when the broken construct is mid-procedure.
//
// Rationale: see docs/session-sets/parser-infrastructure/error-recovery-strategy.md §4.
internal sealed class StatementBoundaryErrorStrategy : DefaultErrorStrategy
{
    private static readonly IntervalSet SyncTokens = BuildSyncTokens();

    private static IntervalSet BuildSyncTokens()
    {
        var set = new IntervalSet(
            VbaLexer.NEWLINE,
            VbaLexer.COLON,
            VbaLexer.SUB,
            VbaLexer.FUNCTION,
            VbaLexer.PROPERTY,
            VbaLexer.END,
            VbaLexer.OPTION,
            VbaLexer.DECLARE,
            VbaLexer.TYPE,
            VbaLexer.ENUM,
            VbaLexer.IMPLEMENTS,
            VbaLexer.DIM,
            VbaLexer.PUBLIC,
            VbaLexer.PRIVATE,
            VbaLexer.FRIEND,
            VbaLexer.STATIC,
            VbaLexer.GLOBAL,
            VbaLexer.CONST,
            VbaLexer.EVENT);
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
