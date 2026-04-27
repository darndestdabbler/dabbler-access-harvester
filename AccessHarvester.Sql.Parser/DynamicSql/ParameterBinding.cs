using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Sql.Parser.DynamicSql;

// One parameter slot in a CandidateTemplate. ParameterBindings appear
// in the order their `?` / `*` placeholders appear in the rendered
// template text, so consumers that need to align bindings with
// placeholders can iterate in parallel.
public sealed record ParameterBinding(
    ParameterBindingSource Source,
    string OriginalText,
    Expression? OriginalExpression,
    Declaration? ResolvedTarget,
    string? UnresolvedReason);

public enum ParameterBindingSource
{
    // The placeholder corresponds to a `Forms!frm!ctl` (or `Reports!`)
    // chain. ResolvedTarget is the synthetic control declaration when
    // the chain resolved through the IAccessObjectModelRegistry; null
    // when it did not (UnresolvedReason carries the code in that
    // case).
    FormControl,

    // The placeholder corresponds to an expression the analyzer could
    // not reduce — function call, arithmetic, late-bound member
    // access, etc. OriginalText carries the surface text so a manual
    // reviewer can see what was elided.
    Unknown,

    // The placeholder corresponds to a string-typed local variable
    // that the analyzer never saw assigned before the use site.
    // OriginalText carries the variable name.
    UnboundVariable,
}
