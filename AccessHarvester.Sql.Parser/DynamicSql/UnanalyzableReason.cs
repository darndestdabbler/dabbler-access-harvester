namespace AccessHarvester.Sql.Parser.DynamicSql;

// Reason codes attached to an Unanalyzable execution site. Stable
// strings so downstream tools and `expected.json` fixtures can match
// on them without parsing prose. New reasons are additive — never
// repurpose an existing code.
public static class UnanalyzableReason
{
    // The cross-product of branch alternatives at the execution site
    // exceeded DynamicSqlAnalyzerOptions.MaxFullEnumeration. The site
    // still emits a representative sample
    // (DynamicSqlAnalyzerOptions.MaxRepresentativeSamples) so consumers
    // can show *something*, but the full enumeration is suppressed.
    public const string CombinatorialBlowup = "combinatorial-blowup";

    // The SQL argument was a reference to a string-typed local
    // variable that the analyzer never saw assigned. Distinct from
    // UnknownExpression because the downstream consumer treats
    // unbound-variable as a missed dataflow rather than a runtime
    // unknown.
    public const string UnboundVariable = "unbound-variable";

    // The SQL argument expression did not flatten to any literal
    // content — every part of the chain was an unknown hole. The site
    // is preserved for the manual-review queue but no template is
    // synthesized.
    public const string UnknownExpression = "unknown-expression";
}
