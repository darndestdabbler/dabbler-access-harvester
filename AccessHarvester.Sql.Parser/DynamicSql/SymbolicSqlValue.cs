using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Sql.Parser.DynamicSql;

// Symbolic representation of a possibly-computed SQL string used by
// DynamicSqlAnalyzer. A SymbolicSqlValue is built incrementally as the
// analyzer walks a VBA procedure body and is later flattened by
// DynamicSqlAnalyzer.Flatten() into one or more CandidateTemplates.
//
// Why an ADT over a flat string list:
//   - Branches stay lazy: an If-statement that mutates a variable on
//     just one path produces SqlBranches at the variable level, not a
//     pre-multiplied Cartesian list. Cardinality is realized only at
//     the SQL execution site.
//   - Holes carry the original VBA expression so downstream rendering
//     can attach parameter bindings and the original surface text.
public abstract record SymbolicSqlValue;

// A literal piece of SQL contributed by a VBA String literal. Text is
// the *unescaped* string content — the analyzer is responsible for
// stripping the surrounding quotes and collapsing `""` -> `"` before
// constructing the SqlLiteral, matching the JET SQL string the runtime
// would observe.
public sealed record SqlLiteral(string Text) : SymbolicSqlValue;

// Concatenation chain (`a & b & c`). Sequencing is preserved — each
// `&` operator yields a Part. Order matters; this is not a set. Nested
// SqlConcat instances are flattened on construction so the analyzer
// never has to walk SqlConcat-of-SqlConcat trees.
public sealed record SqlConcat(IReadOnlyList<SymbolicSqlValue> Parts) : SymbolicSqlValue;

// Branch point produced by an `If` statement that mutated the variable
// on at least one path. Alternatives are emitted in source order
// (Then, ElseIf*, Else; missing-Else contributes the env snapshot at
// the moment the If began). Reason captures why the branch exists
// (informational; carried so downstream tooling can group templates by
// trigger).
public sealed record SqlBranches(
    IReadOnlyList<SymbolicSqlValue> Alternatives,
    string? Reason = null) : SymbolicSqlValue;

// `Forms!frm!ctl` (or `Reports!rpt!ctl`, plus `.Form` / `.Report`
// continuation segments) reference embedded inside a concatenation.
// ResolvedTarget is non-null when the chain resolved through the
// IAccessObjectModelRegistry; UnresolvedReason carries the stable code
// from FormReferences.UnresolvedReason when it did not. Both fields
// are not present at the same time: the resolver returns one or the
// other.
public sealed record SqlFormControlHole(
    Expression OriginalExpression,
    string OriginalText,
    Declaration? ResolvedTarget,
    string? UnresolvedReason) : SymbolicSqlValue;

// Any other expression that contributed to a SQL concatenation but
// could not be reduced (e.g., a `Replace(...)` call, an arithmetic
// expression, a late-bound member access). Surfaces in templates as
// `*` plus a wildcard parameter binding.
//
// OriginalExpression is null when the hole represents a synthesized
// placeholder rather than a real source expression (e.g., a runtime
// parameter seeded into the env at procedure entry, or a tracked
// variable invalidated after a loop body mutated it). OriginalText
// in those cases describes the synthesized origin (parameter name,
// `<loop-mutation:varName>`, etc.).
public sealed record SqlUnknownHole(
    Expression? OriginalExpression,
    string OriginalText) : SymbolicSqlValue;

// Reference to a string-typed local variable that was never assigned
// before the use site. Distinct from SqlUnknownHole because the
// downstream consumer (`enrichment-functions-navigation-review`, #16)
// treats unbound-variable as a missed dataflow rather than a runtime
// unknown.
public sealed record SqlUnboundVariable(string Name) : SymbolicSqlValue;
