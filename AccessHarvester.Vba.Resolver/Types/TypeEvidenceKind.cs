namespace AccessHarvester.Vba.Resolver.Types;

// Source category for one TypeEvidence row. The pass produces all
// kinds listed here; future sessions may add kinds (e.g.,
// `BinaryOperator` for typed-arithmetic inference) without reshaping
// the existing record. See session-2-type-inference.md §3.4.
public enum TypeEvidenceKind
{
    // Declaration site carried a typed-identifier suffix
    // ($/%/&/!/#/@). Definitive at the language level.
    TypeSuffix,
    // RHS of an assignment was a primitive literal
    // (string / numeric / date / Boolean).
    Literal,
    // RHS was a `New T` expression.
    NewExpression,
    // RHS resolved to a typed declaration (variable, parameter,
    // module-level field) whose declared type is non-null.
    BoundDeclarationType,
    // RHS resolved to a function or property-get whose ReturnType is
    // non-null.
    ProcedureReturnType,
    // RHS resolved to a curated VBA built-in whose ReturnType is
    // non-null.
    BuiltInReturnType,
    // RHS was a parameterized default-member call on a value of a
    // type registered in DefaultMemberRegistry.
    DefaultMemberReturnType,
}
