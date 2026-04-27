using System.Collections.Generic;

namespace AccessHarvester.Vba.Parser.Ast;

// Statement is abstract — see ast-shape.md §4 for the full variant list
// and the rationale for which grammar alternatives collapse vs. stay
// separate.
public abstract record Statement(Provenance Provenance) : VbaSyntaxNode(Provenance);

// ── Control flow ───────────────────────────────────────────────────

public sealed record IfStatement(
    Provenance Provenance,
    Expression Condition,
    IReadOnlyList<Statement> ThenBody,
    IReadOnlyList<ElseIfClause> ElseIfClauses,
    IReadOnlyList<Statement>? ElseBody,                 // null = no Else; empty = Else with empty body
    bool IsSingleLine)                                  // single-line If (`If x Then a Else b`) vs block If
    : Statement(Provenance);

public sealed record ElseIfClause(
    Provenance Provenance,
    Expression Condition,
    IReadOnlyList<Statement> Body)
    : VbaSyntaxNode(Provenance);

public sealed record ForStatement(
    Provenance Provenance,
    string LoopVariable,
    string? LoopVariableTypeSuffix,                     // suffix from TYPED_IDENTIFIER, else null
    Expression From,
    Expression To,
    Expression? Step,
    IReadOnlyList<Statement> Body)
    : Statement(Provenance);

public sealed record ForEachStatement(
    Provenance Provenance,
    string LoopVariable,
    string? LoopVariableTypeSuffix,
    Expression Collection,
    IReadOnlyList<Statement> Body)
    : Statement(Provenance);

public sealed record DoLoopStatement(
    Provenance Provenance,
    DoLoopKind Kind,
    Expression? Condition,                              // null only on Infinite
    IReadOnlyList<Statement> Body)
    : Statement(Provenance);

public enum DoLoopKind
{
    Infinite,
    PreTestWhile,
    PreTestUntil,
    PostTestWhile,
    PostTestUntil,
}

public sealed record WhileWendStatement(
    Provenance Provenance,
    Expression Condition,
    IReadOnlyList<Statement> Body)
    : Statement(Provenance);

public sealed record SelectCaseStatement(
    Provenance Provenance,
    Expression Selector,
    IReadOnlyList<CaseClause> Cases,
    IReadOnlyList<Statement>? CaseElseBody)             // null = no Case Else
    : Statement(Provenance);

public sealed record CaseClause(
    Provenance Provenance,
    IReadOnlyList<CaseExpression> Expressions,
    IReadOnlyList<Statement> Body)
    : VbaSyntaxNode(Provenance);

public abstract record CaseExpression(Provenance Provenance) : VbaSyntaxNode(Provenance);

public sealed record CaseValueExpression(
    Provenance Provenance,
    Expression Value)
    : CaseExpression(Provenance);

public sealed record CaseRangeExpression(
    Provenance Provenance,
    Expression Lo,
    Expression Hi)
    : CaseExpression(Provenance);

public sealed record CaseIsCompareExpression(
    Provenance Provenance,
    BinaryOperator Operator,                            // restricted to comparison ops at the grammar level
    Expression Value)
    : CaseExpression(Provenance);

public sealed record WithStatement(
    Provenance Provenance,
    Expression Target,
    IReadOnlyList<Statement> Body)
    : Statement(Provenance);

// ── Error handling ─────────────────────────────────────────────────

public sealed record OnErrorStatement(
    Provenance Provenance,
    OnErrorKind Kind,
    string? Label)                                      // only on Goto; "0" sentinel allowed
    : Statement(Provenance);

public enum OnErrorKind { Goto, ResumeNext }

public sealed record ResumeStatement(
    Provenance Provenance,
    ResumeKind Kind,
    string? Label)                                      // only on Label
    : Statement(Provenance);

public enum ResumeKind { Bare, Next, Label }

// ── Control transfer ───────────────────────────────────────────────

public sealed record GotoStatement(Provenance Provenance, string Label) : Statement(Provenance);
public sealed record GosubStatement(Provenance Provenance, string Label) : Statement(Provenance);
public sealed record ReturnStatement(Provenance Provenance) : Statement(Provenance);
public sealed record ExitStatement(Provenance Provenance, ExitKind Kind) : Statement(Provenance);
public sealed record StopStatement(Provenance Provenance) : Statement(Provenance);
public sealed record EndStatement(Provenance Provenance) : Statement(Provenance);

public enum ExitKind { Sub, Function, Property, For, Do }

// ── Events ────────────────────────────────────────────────────────

public sealed record RaiseEventStatement(
    Provenance Provenance,
    string EventName,
    IReadOnlyList<Argument> Arguments)
    : Statement(Provenance);

// ── Local declarations ────────────────────────────────────────────

public sealed record LocalDeclarationStatement(
    Provenance Provenance,
    LocalDeclarationKind Kind,                          // Dim | Static
    IReadOnlyList<VariableDeclarator> Declarators)
    : Statement(Provenance);

public enum LocalDeclarationKind { Dim, Static }

public sealed record RedimStatement(
    Provenance Provenance,
    bool Preserve,
    IReadOnlyList<RedimDeclarator> Declarators)
    : Statement(Provenance);

public sealed record LocalConstantStatement(
    Provenance Provenance,
    IReadOnlyList<ConstantDeclarator> Declarators)
    : Statement(Provenance);

// ── Assignment / call ─────────────────────────────────────────────

public sealed record SetStatement(
    Provenance Provenance,
    Expression Target,
    Expression Value)
    : Statement(Provenance);

public sealed record LetStatement(
    Provenance Provenance,
    Expression Target,
    Expression Value,
    bool ExplicitLet)                                   // true if "Let" keyword present
    : Statement(Provenance);

public sealed record CallStatement(
    Provenance Provenance,
    Expression Target,
    IReadOnlyList<Argument> BareArguments,              // empty if Target's IndexOrCall postfix carries the args
    bool ExplicitCall)                                  // true if "Call" keyword present
    : Statement(Provenance);

// ── Labels ────────────────────────────────────────────────────────

public sealed record LabelStatement(
    Provenance Provenance,
    string Name)                                        // alpha label or numeric line-number, source spelling preserved
    : Statement(Provenance);

// ── Preprocessor pass-through (statement position) ───────────────

// Mirrors PreprocessorDirective (ModuleItem) for #If/#ElseIf/#Else/
// #End If/#Const lines that appear inside procedure bodies. Body is
// the raw source text after the directive keyword (line-trimmed).
// Per ast-shape.md §4 and Phase 2.5 of the plan: parse but don't
// evaluate — preserve as opaque blocks.
public sealed record PreprocessorStatement(
    Provenance Provenance,
    PreprocessorKind Kind,
    string Body)
    : Statement(Provenance);

// ── Recovery sentinel ─────────────────────────────────────────────

// Emitted when a parse-tree subtree at statement position contains an
// IErrorNode the builder cannot meaningfully descend into. Always
// carries RecoveredProvenance.
public sealed record UnknownStatement(Provenance Provenance) : Statement(Provenance);
