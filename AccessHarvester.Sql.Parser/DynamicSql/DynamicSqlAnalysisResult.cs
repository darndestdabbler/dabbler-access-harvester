using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Sql.Parser.DynamicSql;

// One SQL execution call site found in a procedure body. Templates
// is the (possibly representative) list of CandidateTemplates the
// analyzer enumerated for this site. When IsAnalyzable is false,
// UnanalyzableReason carries the stable code from
// DynamicSql.UnanalyzableReason and Templates may still hold a
// representative sample (so downstream tooling can show *something*
// rather than only a marker).
//
// FullCardinality is the cross-product cardinality the analyzer
// computed before applying MaxFullEnumeration. When IsAnalyzable is
// true, FullCardinality == Templates.Count.
public sealed record DynamicSqlExecutionSite(
    Statement HostStatement,
    Expression CallExpression,
    Expression SqlArgumentExpression,
    SymbolicSqlValue SymbolicValue,
    IReadOnlyList<CandidateTemplate> Templates,
    int FullCardinality,
    bool IsAnalyzable,
    string? UnanalyzableReason);

// Aggregate result for one ProcedureDeclaration. ExecutionSites are
// emitted in source order. The result is empty when the procedure
// contains no recognized SQL execution call.
public sealed record DynamicSqlAnalysisResult(
    IReadOnlyList<DynamicSqlExecutionSite> ExecutionSites);

// Analyzer tunables. Defaults are calibrated against fixture 05's
// 32-form ceiling: the full enumeration cap is set high enough that
// fixture 05 produces all 32 templates if the consumer wants them,
// while the representative-sample cap matches the fixture's hand-
// curated 5-template authoring choice. Session 3 of
// jet-sql-resolution-and-dynamic ratifies these defaults against the
// expected.json contract.
public sealed record DynamicSqlAnalyzerOptions(
    int MaxFullEnumeration = 32,
    int MaxRepresentativeSamples = 5)
{
    public static readonly DynamicSqlAnalyzerOptions Default = new();
}
