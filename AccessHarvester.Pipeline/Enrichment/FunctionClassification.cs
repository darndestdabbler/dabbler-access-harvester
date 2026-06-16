using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Translation-strategy classification + complexity metrics for a single
/// user-defined VBA procedure. Implements Phase 6.6 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>: every Sub /
/// Function / Property is bucketed by the side-effect categories its
/// body exercises (pure vs. data-access vs. UI-bound vs. external I/O,
/// or any combination as <see cref="IsMixed"/>) so a downstream Blazor-
/// migration AI consumer can decide whether to lift the body into a
/// pure C# helper, a repository method, a Blazor component handler, or
/// an external-IO adapter without re-walking the resolved AST.
/// </summary>
/// <remarks>
/// <para>
/// Classification is deterministic and structurally driven per the
/// session-set Standards: it consults the resolver's
/// <c>ResolutionOutcome</c> for every reference site inside the
/// procedure body, plus a curated mapping from
/// <c>BuiltInCategory</c> / built-in names / Access-object-model
/// declaration kinds to a <see cref="ClassificationCategory"/>. The
/// builder never inspects argument values, control-flow direction, or
/// VBA <c>CreateObject</c> string literals — those are interpretive
/// concerns left to the AI consumer downstream.
/// </para>
/// <para>
/// Per the session-set Standards' "do not pre-decompose mixed
/// functions" rule, the builder emits <see cref="IsMixed"/> = true
/// whenever <see cref="Categories"/> contains more than one non-
/// <see cref="ClassificationCategory.Pure"/> signal, but does not
/// attempt to split the body. <see cref="PrimaryCategory"/> picks one
/// representative signal in a fixed precedence order so consumers that
/// must pick a single bucket have a deterministic answer.
/// </para>
/// <para>
/// Late-bound chains (Object-typed locals such as
/// <c>db.OpenRecordset</c>) cannot be statically resolved without type
/// inference, which is a downstream concern; they surface as
/// <see cref="ClassificationCategory.LateBound"/> via
/// <see cref="UnresolvedReferences"/> and force <see cref="IsMixed"/>
/// to true so the AI consumer knows the static signal is incomplete.
/// </para>
/// <para>
/// Interpretive slots remain absent at the <c>ir/</c> tier per
/// <c>docs/planning/project-guidance.md</c> -> Output Architecture: this
/// record exposes structure, not opinions about what the procedure
/// "is for".
/// </para>
/// <para>
/// Builders for this record live in
/// <see cref="FunctionClassificationBuilder"/>, which walks one
/// <see cref="AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration"/>
/// against the corresponding
/// <see cref="AccessHarvester.Vba.Resolver.References.ResolvedModule"/>
/// and the project-wide
/// <see cref="AccessHarvester.Vba.Resolver.CrossReference.CrossReferenceIndex"/>.
/// </para>
/// </remarks>
public sealed record FunctionClassification
{
    /// <summary>
    /// VBA module name as the resolver's
    /// <see cref="AccessHarvester.Vba.Resolver.Scopes.ModuleScope.ModuleName"/>
    /// reports it. Stable across runs for the same input.
    /// </summary>
    public required string ModuleName { get; init; }

    /// <summary>
    /// Procedure name as written in source. VBA names are case-
    /// insensitive but the source spelling is preserved here for
    /// round-tripping back to the input.
    /// </summary>
    public required string ProcedureName { get; init; }

    /// <summary>
    /// Discriminates Sub vs. Function vs. Property Get/Let/Set so the
    /// consumer can pick the right C# emission shape (void method,
    /// returning method, property accessor) without re-inspecting the
    /// AST.
    /// </summary>
    public required ProcedureKind Kind { get; init; }

    /// <summary>
    /// True when the source spelled <c>Public</c> (or omitted the
    /// access keyword in a non-class module, where the project-wide
    /// default is Public). False when the source spelled
    /// <c>Private</c> / <c>Friend</c> or omitted in a class module
    /// where the default is Private. Mirrors the resolver's
    /// canonicalised access level.
    /// </summary>
    public required bool IsPublic { get; init; }

    /// <summary>
    /// True when the source spelled <c>Static</c>. Static procedures
    /// retain local-variable state across calls and are flagged so the
    /// downstream consumer can model them as classes-with-fields rather
    /// than free functions.
    /// </summary>
    public required bool IsStatic { get; init; }

    /// <summary>
    /// Number of declared parameters, before any <c>ParamArray</c> /
    /// <c>Optional</c> distinction. The full
    /// <see cref="AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration.Parameters"/>
    /// list lives on the parser AST; this is an at-a-glance facet for
    /// consumers that scan classifications without re-reading the AST.
    /// </summary>
    public required int ParameterCount { get; init; }

    /// <summary>
    /// True for Function and PropertyGet (which have a return value);
    /// false for Sub, PropertyLet, and PropertySet.
    /// </summary>
    public required bool HasReturnValue { get; init; }

    /// <summary>
    /// All side-effect categories the body exercises, in the order they
    /// are first encountered during the body walk. <see cref="ClassificationCategory.Pure"/>
    /// appears alone when the body has no observable side effects;
    /// otherwise it is omitted (Pure is "absence of signals", not its
    /// own signal among others).
    /// </summary>
    public required IReadOnlyList<ClassificationCategory> Categories { get; init; }

    /// <summary>
    /// One representative category for consumers that must pick a
    /// single bucket. Precedence (highest first):
    /// <see cref="ClassificationCategory.UIBound"/>,
    /// <see cref="ClassificationCategory.DataAccess"/>,
    /// <see cref="ClassificationCategory.ExternalIO"/>,
    /// <see cref="ClassificationCategory.LateBound"/>,
    /// <see cref="ClassificationCategory.Pure"/>. UI is highest because
    /// a UI-bound procedure typically must be hosted inside a Blazor
    /// component regardless of any other signals it carries; data-access
    /// is next because the consumer decides repository vs. service
    /// shape based on it; ExternalIO and LateBound are closer to "leaf
    /// concerns" the consumer adapters around.
    /// </summary>
    public required ClassificationCategory PrimaryCategory { get; init; }

    /// <summary>
    /// True when the body exercises more than one non-Pure category, or
    /// when <see cref="Categories"/> contains
    /// <see cref="ClassificationCategory.LateBound"/> alongside any
    /// other signal. False when the body is purely one category (or
    /// purely <see cref="ClassificationCategory.Pure"/>). The builder
    /// does not attempt to decompose mixed bodies — that is left to the
    /// downstream consumer per Lean By Default.
    /// </summary>
    public required bool IsMixed { get; init; }

    /// <summary>
    /// Mechanical complexity facets (cyclomatic, lines of code,
    /// statement count). Computed from the parser AST without
    /// re-resolving names.
    /// </summary>
    public required ComplexityMetrics Metrics { get; init; }

    /// <summary>
    /// Procedures called directly from this body. Deduplicated by the
    /// callee declaration's identity (so a procedure called three times
    /// appears once). Sorted case-insensitively by
    /// <see cref="DependencyEdge.ModuleName"/>, then
    /// <see cref="DependencyEdge.ProcedureName"/>, then
    /// <see cref="DependencyEdge.Kind"/> — same input always yields the
    /// same edge sequence so consumers can diff classifications across
    /// runs without false churn. Empty when the body makes no procedure
    /// calls or every call target is a built-in / late-bound chain.
    /// Built-in callees (Format, IIf, Nz, ...) are deliberately
    /// excluded — they are not user-defined dependencies and recording
    /// them would bloat the graph without informing translation. Self-
    /// references (recursive calls; the VBA return-via-name idiom) are
    /// also excluded.
    /// </summary>
    public required IReadOnlyList<DependencyEdge> DirectDependencies { get; init; }

    /// <summary>
    /// All procedures reachable from this body via a finite chain of
    /// <see cref="DirectDependencies"/> — i.e., the BFS closure of
    /// direct dependencies across all modules. <see cref="DirectDependencies"/>
    /// is a subset; the closure additionally contains the transitive
    /// reach. Sorted case-insensitively by
    /// <see cref="DependencyEdge.ModuleName"/>, then
    /// <see cref="DependencyEdge.ProcedureName"/>, then
    /// <see cref="DependencyEdge.Kind"/> for stable cross-run
    /// comparison. Self-references (recursive procedures) do not
    /// appear (they are skipped at the BFS seed-marking step rather
    /// than recorded as a self-loop).
    /// </summary>
    public required IReadOnlyList<DependencyEdge> TransitiveDependencies { get; init; }

    /// <summary>
    /// Procedures that call this one, sourced from the project-wide
    /// cross-reference index. Deduplicated by the caller declaration's
    /// identity. Sorted case-insensitively by
    /// <see cref="DependencyEdge.ModuleName"/>, then
    /// <see cref="DependencyEdge.ProcedureName"/>, then
    /// <see cref="DependencyEdge.Kind"/> for stable cross-run
    /// comparison. Empty when nothing in the indexed module set
    /// references this procedure (note: external entry points such as
    /// AutoExec or form/report event handlers may legitimately have an
    /// empty caller list — see Phase 6.7's navigation graph for those).
    /// Self-references (recursive calls; the VBA return-via-name idiom)
    /// are excluded for the same reason they are excluded from
    /// <see cref="DirectDependencies"/>.
    /// </summary>
    public required IReadOnlyList<DependencyEdge> Callers { get; init; }

    /// <summary>
    /// Reference sites inside the body that the resolver could not bind
    /// to a declaration — typically late-bound member access on Object-
    /// typed locals (<c>db.OpenRecordset</c>) or unresolved bare
    /// identifiers. Populated only when the body has at least one
    /// unresolved reference; null otherwise. Each entry mirrors a
    /// <see cref="AccessHarvester.Vba.Resolver.References.UnresolvedReasons"/>
    /// reason code so Phase 6.8's manual-review aggregator can absorb
    /// the records into the run-wide <c>UnresolvedItems</c> array
    /// without re-walking the resolver output.
    /// </summary>
    public IReadOnlyList<UnresolvedReferenceFact>? UnresolvedReferences { get; init; }
}

/// <summary>
/// Discriminates the procedure flavour in a way the downstream consumer
/// can switch on without inspecting the parser AST. Mirrors
/// <see cref="AccessHarvester.Vba.Parser.Ast.PropertyKind"/> for the
/// three property variants and adds explicit Sub / Function / Declare
/// entries.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcedureKind
{
    /// <summary>VBA <c>Sub</c> — no return value.</summary>
    Sub,

    /// <summary>VBA <c>Function</c> — has a return value.</summary>
    Function,

    /// <summary>VBA <c>Property Get</c> — read accessor; has a return value.</summary>
    PropertyGet,

    /// <summary>VBA <c>Property Let</c> — value-assignment accessor; no return.</summary>
    PropertyLet,

    /// <summary>VBA <c>Property Set</c> — reference-assignment accessor; no return.</summary>
    PropertySet,

    /// <summary>
    /// VBA <c>Declare</c> statement (external library entry point). Has
    /// no body and so cyclomatic complexity / line count are reported as
    /// zero; only signature facets are meaningful.
    /// </summary>
    Declare,
}

/// <summary>
/// One of the side-effect classes a procedure body can exhibit. Per
/// Phase 6.6, this is the discriminator the downstream consumer reads
/// to pick a translation strategy.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClassificationCategory
{
    /// <summary>
    /// No observable side effects: only literal constants, locals,
    /// parameters, arithmetic / string built-ins, and intra-procedure
    /// control flow. Direct candidate for one-to-one translation as a
    /// pure C# helper method.
    /// </summary>
    Pure,

    /// <summary>
    /// Reads or writes Access tables / queries via the domain-data
    /// built-ins (<c>DLookup</c>, <c>DCount</c>, <c>DSum</c>, ...) or
    /// <c>CurrentDb</c> / DAO recordset operations recognised through
    /// the resolver's Access object-model registry. Translation target
    /// is a repository or service method.
    /// </summary>
    DataAccess,

    /// <summary>
    /// Touches Access UI surfaces: form / report instances exposed via
    /// the synthetic Forms/Reports collections, control members, the
    /// Screen object, MsgBox / InputBox prompts, or
    /// <c>DoCmd.OpenForm</c> / <c>OpenReport</c> dispatches. Body
    /// belongs in a Blazor component handler.
    /// </summary>
    UIBound,

    /// <summary>
    /// Operates on the file system, console, or external COM servers
    /// via VBA file I/O built-ins (<c>Open</c>, <c>Print#</c>,
    /// <c>FreeFile</c>, <c>Dir</c>, ...) or the <c>Shell</c>
    /// interaction. Translation target is an adapter over the .NET
    /// I/O stack.
    /// </summary>
    ExternalIO,

    /// <summary>
    /// Body contains member-access chains on Object-typed values the
    /// resolver could not bind statically (e.g.,
    /// <c>db.OpenRecordset</c> where <c>db</c> is declared
    /// <c>As Object</c>). The static signal is therefore incomplete;
    /// the consumer must consult <see cref="FunctionClassification.UnresolvedReferences"/>
    /// or run dynamic analysis to recover the missing categories.
    /// Always co-occurs with <see cref="FunctionClassification.IsMixed"/>
    /// = true.
    /// </summary>
    LateBound,
}

/// <summary>
/// Mechanical complexity facets of one procedure body. All values are
/// derived from the parser AST alone; no resolver state is consulted.
/// </summary>
public sealed record ComplexityMetrics
{
    /// <summary>
    /// Standard cyclomatic complexity: 1 + the number of decision
    /// points in the body. Decision points are <c>If</c> /
    /// <c>ElseIf</c> branches, <c>For</c> / <c>For Each</c> /
    /// <c>While</c> / <c>Do While|Until</c> loops (post-test variants
    /// included), <c>Select Case</c> branches (one per
    /// <c>Case</c> clause; <c>Case Else</c> is not counted because it
    /// is the fall-through branch, not a separate decision), and
    /// short-circuit logical operators (<c>And</c>, <c>Or</c>,
    /// <c>Xor</c>, <c>Eqv</c>, <c>Imp</c>) inside conditions. Empty
    /// bodies and Declare-statement procedures report 1 (one path).
    /// </summary>
    public required int CyclomaticComplexity { get; init; }

    /// <summary>
    /// Distinct source-line count spanned by the procedure body. Uses
    /// each statement's <see cref="AccessHarvester.Vba.Parser.Ast.Provenance.Range"/>
    /// when present; falls back to the statement's
    /// <see cref="AccessHarvester.Vba.Parser.Ast.Provenance.Line"/> /
    /// Column scalars when Range is null (parser is mid-transition per
    /// Provenance.cs). Whitespace-only and comment-only lines that the
    /// parser absorbed as trivia are not counted.
    /// </summary>
    public required int LinesOfCode { get; init; }

    /// <summary>
    /// Number of statements directly in the procedure body, recursively
    /// including statements nested inside control-flow blocks
    /// (<c>If</c> branches, loop bodies, <c>With</c> blocks,
    /// <c>Select Case</c> arms). Excludes <c>ElseIfClause</c> /
    /// <c>CaseClause</c> wrapper nodes (their inner statements are
    /// counted) and the
    /// <see cref="AccessHarvester.Vba.Parser.Ast.UnknownStatement"/>
    /// recovery sentinel.
    /// </summary>
    public required int StatementCount { get; init; }
}

/// <summary>
/// One edge in the call graph. Identifies a procedure by module and
/// name with kind so callers / direct deps / transitive deps can be
/// rendered uniformly.
/// </summary>
public sealed record DependencyEdge
{
    /// <summary>
    /// VBA module the referenced procedure is declared in.
    /// </summary>
    public required string ModuleName { get; init; }

    /// <summary>
    /// Procedure name as declared in source.
    /// </summary>
    public required string ProcedureName { get; init; }

    /// <summary>
    /// Procedure flavour (Sub / Function / Property*) so the consumer
    /// can render the edge with the correct shape (void return vs.
    /// value return vs. property accessor).
    /// </summary>
    public required ProcedureKind Kind { get; init; }
}

/// <summary>
/// One reference site inside a procedure body that the resolver could
/// not bind to a declaration. The shape is intentionally narrow: enough
/// for Phase 6.8's manual-review aggregator to absorb without re-
/// walking the resolver output, no more.
/// </summary>
public sealed record UnresolvedReferenceFact
{
    /// <summary>
    /// One of the constants on
    /// <see cref="AccessHarvester.Vba.Resolver.References.UnresolvedReasons"/>
    /// (e.g., <c>"not-in-scope"</c>,
    /// <c>"member-access-on-unresolved-target"</c>). Stable strings;
    /// future resolver sessions append to the closed set.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// 1-based line of the reference site, sourced from the AST
    /// expression's <see cref="AccessHarvester.Vba.Parser.Ast.Provenance.Line"/>.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// 0-based column of the reference site, sourced verbatim from the AST
    /// expression's <see cref="AccessHarvester.Vba.Parser.Ast.Provenance.Column"/>
    /// (ANTLR convention; no normalisation). Surfaced 0-based in
    /// unresolved-items.json — contrast the per-module manifest's 1-based
    /// <c>SourceLocation</c> column (DW-26 / DW-27). (Line above is 1-based.)
    /// </summary>
    public required int Column { get; init; }

    /// <summary>
    /// The unresolved member name when the failure was a member-access
    /// resolution (e.g., <c>"OpenRecordset"</c> in
    /// <c>db.OpenRecordset</c>); the bare identifier name when the
    /// failure was a not-in-scope identifier (e.g., <c>"FooBar"</c>);
    /// null when the AST node carried no usable name (rare — only the
    /// recovery-sentinel
    /// <see cref="AccessHarvester.Vba.Parser.Ast.UnknownExpression"/>).
    /// </summary>
    public string? MemberName { get; init; }
}
