using System;
using System.Collections.Generic;

namespace AccessHarvester.Sql.Parser.DynamicSql;

// Structural deep equality for SymbolicSqlValue.
//
// Why a separate comparer instead of relying on record `.Equals`:
// records auto-generate value-equality on declared properties, but the
// list-typed fields inside SqlConcat (Parts) and SqlBranches
// (Alternatives) — and the AST-node fields inside the hole records
// (OriginalExpression) — fall back to reference equality. Two
// SymbolicSqlValues produced by independent EvalExpr passes over the
// same source can be semantically identical yet `.Equals`-different
// because their inner Lists / AST nodes are distinct instances.
//
// The analyzer relies on equality in two places where reference-
// equality fabricates divergence:
//   - WalkLoopBody compares the pre-loop env to the post-body env to
//     decide which tracked variables were mutated. Reference equality
//     would over-invalidate vars whose value re-built to a structurally
//     identical SymbolicSqlValue but a fresh List instance.
//   - MergeEnv calls AllAlternativesEqual to collapse If/SelectCase
//     branches whose alternatives all produced the same value.
//     Reference equality would treat all-equal branches as divergent
//     and emit a redundant SqlBranches.
//
// DeepEquals compares semantic fields only:
//   - SqlLiteral by Text.
//   - SqlConcat by element-wise DeepEquals across Parts.
//   - SqlBranches by element-wise DeepEquals across Alternatives plus
//     Reason string.
//   - SqlFormControlHole by OriginalText, UnresolvedReason, and
//     reference-equality on ResolvedTarget (the registry returns
//     canonical Declaration singletons; reference equality is the
//     correct semantic).
//   - SqlUnknownHole by OriginalText (OriginalExpression is allowed
//     to be a synthesized null per the param-seed and loop-mutation
//     code paths).
//   - SqlUnboundVariable by Name (case-insensitive, mirroring the
//     env's StringComparer).
internal static class SymbolicSqlValueEquality
{
    public static bool DeepEquals(SymbolicSqlValue? a, SymbolicSqlValue? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.GetType() != b.GetType()) return false;

        return a switch
        {
            SqlLiteral la =>
                string.Equals(la.Text, ((SqlLiteral)b).Text, StringComparison.Ordinal),

            SqlConcat ca =>
                SeqEquals(ca.Parts, ((SqlConcat)b).Parts),

            SqlBranches ba =>
                string.Equals(ba.Reason, ((SqlBranches)b).Reason, StringComparison.Ordinal)
                && SeqEquals(ba.Alternatives, ((SqlBranches)b).Alternatives),

            SqlFormControlHole fa =>
                string.Equals(fa.OriginalText, ((SqlFormControlHole)b).OriginalText, StringComparison.Ordinal)
                && string.Equals(fa.UnresolvedReason, ((SqlFormControlHole)b).UnresolvedReason, StringComparison.Ordinal)
                && ReferenceEquals(fa.ResolvedTarget, ((SqlFormControlHole)b).ResolvedTarget),

            SqlUnknownHole ua =>
                string.Equals(ua.OriginalText, ((SqlUnknownHole)b).OriginalText, StringComparison.Ordinal),

            SqlUnboundVariable va =>
                string.Equals(va.Name, ((SqlUnboundVariable)b).Name, StringComparison.OrdinalIgnoreCase),

            _ => a.Equals(b),
        };
    }

    private static bool SeqEquals(IReadOnlyList<SymbolicSqlValue> x, IReadOnlyList<SymbolicSqlValue> y)
    {
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
            if (!DeepEquals(x[i], y[i])) return false;
        return true;
    }
}
