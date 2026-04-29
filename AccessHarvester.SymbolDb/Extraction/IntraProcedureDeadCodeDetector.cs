using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.SymbolDb.Extraction;

// Detects unreachable statements inside a procedure body. A procedure is
// flagged as having intra-procedure dead code when statements appear after
// an unconditional control-flow exit at the top level of its body:
//
//   * Exit Sub / Exit Function / Exit Property
//   * Goto <label> not followed by the labelled section being targeted from
//     elsewhere -- this detector is conservative and only flags the simple
//     "any statement after unconditional terminator" case.
//   * If False Then ... End If -- whole branch unreachable.
//
// This first pass intentionally only looks at the procedure body's top-level
// statement list. Nested blocks (inside If / While / For) are out of scope:
// inferring "unconditional" inside arbitrary control flow is the dataflow
// analyzer's job, not the symbol-DB extractor's.
public static class IntraProcedureDeadCodeDetector
{
    public static bool HasDeadCode(ProcedureDeclaration procedure, out string? reason)
    {
        var body = procedure.Body;
        for (int i = 0; i < body.Count; i++)
        {
            if (IsUnconditionalTerminator(body[i]) && HasMeaningfulStatementAfter(body, i))
            {
                reason = body[i] switch
                {
                    ExitStatement => "after-unconditional-exit",
                    GotoStatement => "after-unconditional-goto",
                    EndStatement => "after-end-statement",
                    _ => "unreachable-statement",
                };
                return true;
            }

            if (IsAlwaysFalseIf(body[i]))
            {
                reason = "if-false-then-block";
                return true;
            }
        }

        reason = null;
        return false;
    }

    private static bool IsUnconditionalTerminator(Statement statement) => statement switch
    {
        ExitStatement => true,
        GotoStatement => true,
        EndStatement => true,
        _ => false,
    };

    // After a top-level unconditional terminator there might be:
    //   * a label (LabelStatement) that something earlier branched to: legit
    //   * additional executable statements: dead
    // A pragmatic approximation: any executable Statement after the terminator
    // is dead unless every following statement is itself a label or another
    // terminator. The conservative choice flags any non-label follower so the
    // measurement report includes the cases the AI interpreter should skip.
    private static bool HasMeaningfulStatementAfter(IReadOnlyList<Statement> body, int index)
    {
        if (index + 1 >= body.Count) return false;
        return body[index + 1] is not LabelStatement;
    }

    private static bool IsAlwaysFalseIf(Statement statement)
    {
        if (statement is not IfStatement ifStatement) return false;
        // We only flag the literal `If False Then` shape; anything else
        // (constants, expressions) is the dataflow analyzer's job.
        return ifStatement.Condition is FalseExpression;
    }
}
