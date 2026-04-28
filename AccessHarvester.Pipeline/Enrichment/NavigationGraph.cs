using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Application-wide navigation graph reconstructed per Phase 6.7 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>: top-level
/// navigation structure (ribbon tabs, switchboard menus), the
/// form-to-form / form-to-report graph derived from
/// <c>DoCmd.OpenForm</c> / <c>DoCmd.OpenReport</c> / <c>DoCmd.Close</c>
/// dispatch sites in VBA bodies, and the explicit ordered entry-point
/// list (AutoExec macro chain → default form → switchboard root) the
/// downstream Blazor-migration AI consumer reads to seed routing.
/// </summary>
/// <remarks>
/// <para>
/// The graph is **structured** — nodes + edges + trigger metadata —
/// per the session-set Standards, not a flat call list. Every edge
/// carries the source procedure (so the consumer can pin the trigger
/// to a specific event handler) and the target form/report name (when
/// statically resolvable) plus the optional <c>WhereCondition</c> /
/// <c>OpenArgs</c> raw text the Access dispatch carried.
/// </para>
/// <para>
/// VBA-side discovery is fully mechanical: the
/// <see cref="NavigationGraphBuilder"/> walks every
/// <see cref="AccessHarvester.Vba.Parser.Ast.CallStatement"/> looking
/// for <c>DoCmd.&lt;NavigationVerb&gt;</c> dispatches and reads the
/// first positional argument as the target. Targets supplied as a
/// non-literal expression (variable, function call, concatenation,
/// dynamic name) surface as
/// <see cref="UnresolvedNavigationTarget"/> entries with the source
/// expression text preserved — they are the Phase 6.8 manual-review
/// candidates the next session aggregates.
/// </para>
/// <para>
/// Top-level surfaces (ribbon tabs, switchboard menus) and the
/// AutoExec macro chain do not live in the VBA AST — they come from
/// the metadata-extractor side via <see cref="NavigationGraphInputs"/>.
/// When inputs are omitted (the typical case before #10's metadata
/// extractor lands here), the graph still contains the VBA-side edges
/// but the corresponding top-level facets remain null and the entry-
/// point list is empty. Per Lean By Default the builder does not
/// invent ribbon/switchboard inputs from heuristic clues; it requires
/// the upstream extractor to supply them.
/// </para>
/// <para>
/// All collections are sorted at construction time so identical input
/// produces an identical graph across runs — the determinism standard
/// the project guidance pins for parser-and-enrichment outputs.
/// </para>
/// </remarks>
public sealed record NavigationGraph
{
    /// <summary>
    /// Ordered entry points the downstream consumer should treat as
    /// roots when building application routes. Order is deterministic:
    /// AutoExec macro chain (when present) first, then the explicitly
    /// supplied default form (when present), then switchboard roots in
    /// the order they were supplied. Empty when no inputs were
    /// provided to <see cref="NavigationGraphBuilder.Build"/>.
    /// </summary>
    public required IReadOnlyList<NavigationEntryPoint> EntryPoints { get; init; }

    /// <summary>
    /// Form-to-form / form-to-report navigation edges discovered in
    /// VBA bodies. Sorted case-insensitively by
    /// <see cref="NavigationEdge.SourceModule"/>, then
    /// <see cref="NavigationEdge.SourceProcedure"/>, then
    /// <see cref="NavigationEdge.SourceLine"/>, then
    /// <see cref="NavigationEdge.SourceColumn"/> so identical input
    /// always yields the same edge sequence.
    /// </summary>
    public required IReadOnlyList<NavigationEdge> Edges { get; init; }

    /// <summary>
    /// Dispatch sites the builder recognised as
    /// <c>DoCmd.&lt;NavigationVerb&gt;</c> calls but whose target form
    /// or report name was not a string literal. Surfaced explicitly
    /// rather than swallowed so Phase 6.8's manual-review aggregator
    /// can absorb them with full provenance.
    /// </summary>
    public required IReadOnlyList<UnresolvedNavigationTarget> UnresolvedTargets { get; init; }

    /// <summary>
    /// Top-level ribbon structure (custom XML from <c>USysRibbons</c>),
    /// when supplied via <see cref="NavigationGraphInputs.Ribbon"/>.
    /// Null when no input was provided. The builder does not parse the
    /// XML itself — it consumes the structured form the metadata
    /// extractor produces.
    /// </summary>
    public RibbonStructure? Ribbon { get; init; }

    /// <summary>
    /// Top-level switchboard structure (entries from the
    /// <c>Switchboard Items</c> table), when supplied via
    /// <see cref="NavigationGraphInputs.Switchboard"/>. Null when no
    /// input was provided.
    /// </summary>
    public SwitchboardStructure? Switchboard { get; init; }

    /// <summary>
    /// Macro chains supplied by the metadata extractor — typically the
    /// AutoExec macro plus any other named macros the application
    /// defines. Empty when no input was provided. AutoExec, when
    /// present, also appears as the first <see cref="EntryPoints"/>
    /// entry; it is duplicated here so consumers that read macros
    /// independently of entry points see the full action sequence.
    /// </summary>
    public required IReadOnlyList<MacroChain> Macros { get; init; }
}

/// <summary>
/// One root the application starts from. Order in
/// <see cref="NavigationGraph.EntryPoints"/> reflects priority:
/// AutoExec runs at database open, the default form opens after the
/// macro chain completes, and switchboard roots are reachable
/// thereafter from the user's first interaction.
/// </summary>
public sealed record NavigationEntryPoint
{
    /// <summary>
    /// Discriminates how the entry point becomes active.
    /// </summary>
    public required EntryPointKind Kind { get; init; }

    /// <summary>
    /// Display label for the entry point. AutoExec entries use the
    /// macro's name (typically <c>"AutoExec"</c>); default-form
    /// entries use the form name; switchboard entries use the
    /// switchboard's display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Form name the entry point hands control to. Null when the
    /// entry point is purely procedural (e.g., an AutoExec macro that
    /// calls a VBA initializer with no terminal form open).
    /// </summary>
    public string? OpensFormName { get; init; }

    /// <summary>
    /// VBA procedure the entry point hands control to via
    /// <c>RunCode</c> (AutoExec) or equivalent. Null when the entry
    /// point opens a form directly without an intermediate VBA call.
    /// </summary>
    public ProcedureReference? CallsProcedure { get; init; }
}

/// <summary>
/// Discriminates entry-point flavour for
/// <see cref="NavigationEntryPoint.Kind"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntryPointKind
{
    /// <summary>
    /// Special-name macro Access auto-runs at database open
    /// (typically <c>AutoExec</c>).
    /// </summary>
    AutoExec,

    /// <summary>
    /// Form designated as the default startup form via Access's
    /// startup options.
    /// </summary>
    DefaultForm,

    /// <summary>
    /// Switchboard root (a switchboard entry whose ItemNumber = 0 or
    /// equivalent root marker, depending on Access version).
    /// </summary>
    SwitchboardRoot,
}

/// <summary>
/// One navigation dispatch in VBA — typically a
/// <c>DoCmd.OpenForm</c> / <c>DoCmd.OpenReport</c> / <c>DoCmd.Close</c>
/// / <c>DoCmd.BrowseTo</c> call observed in a procedure body. The
/// edge identifies who triggers the navigation
/// (<see cref="SourceModule"/> + <see cref="SourceProcedure"/>) and
/// where it leads (<see cref="TargetKind"/> +
/// <see cref="TargetName"/>) plus the dispatch's optional Where /
/// OpenArgs payload preserved as raw source text.
/// </summary>
public sealed record NavigationEdge
{
    /// <summary>
    /// VBA module the dispatch lives in.
    /// </summary>
    public required string SourceModule { get; init; }

    /// <summary>
    /// Procedure inside <see cref="SourceModule"/> that contains the
    /// dispatch.
    /// </summary>
    public required string SourceProcedure { get; init; }

    /// <summary>
    /// Procedure flavour — Sub / Function / PropertyGet / PropertyLet
    /// / PropertySet — so the consumer can render the trigger with
    /// the correct shape.
    /// </summary>
    public required ProcedureKind SourceProcedureKind { get; init; }

    /// <summary>
    /// 1-based source line of the dispatch call.
    /// </summary>
    public required int SourceLine { get; init; }

    /// <summary>
    /// 1-based source column of the dispatch call.
    /// </summary>
    public required int SourceColumn { get; init; }

    /// <summary>
    /// Discriminates the navigation verb (OpenForm vs. OpenReport vs.
    /// Close vs. BrowseTo). Drives both the consumer's emission shape
    /// and the <see cref="TargetKind"/> resolution rule.
    /// </summary>
    public required NavigationAction Action { get; init; }

    /// <summary>
    /// Form vs. Report vs. Unknown. <see cref="NavigationAction.Close"/>
    /// can target either depending on its first argument
    /// (<c>acForm</c> / <c>acReport</c>); when that argument is
    /// missing or non-literal the kind reports
    /// <see cref="NavigationTargetKind.Unknown"/>.
    /// </summary>
    public required NavigationTargetKind TargetKind { get; init; }

    /// <summary>
    /// Target form or report name extracted from the first positional
    /// argument's string literal. Null when the argument was non-
    /// literal — in that case the dispatch site appears in
    /// <see cref="NavigationGraph.UnresolvedTargets"/> rather than
    /// here, and this edge is omitted.
    /// </summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// Raw source text of the <c>WhereCondition</c> argument when
    /// supplied (the fourth positional argument of
    /// <c>DoCmd.OpenForm</c>; the third of
    /// <c>DoCmd.OpenReport</c>). Preserved as-is so the consumer can
    /// pass it through to a parameterised query translator without
    /// re-stringifying. Null when the argument was omitted.
    /// </summary>
    public string? WhereCondition { get; init; }

    /// <summary>
    /// Raw source text of the <c>OpenArgs</c> argument when supplied
    /// (the seventh positional argument of <c>DoCmd.OpenForm</c>).
    /// Preserved as-is. Null when omitted.
    /// </summary>
    public string? OpenArgs { get; init; }
}

/// <summary>
/// Discriminates the navigation verb on
/// <see cref="NavigationEdge.Action"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NavigationAction
{
    /// <summary><c>DoCmd.OpenForm</c>.</summary>
    OpenForm,

    /// <summary><c>DoCmd.OpenReport</c>.</summary>
    OpenReport,

    /// <summary><c>DoCmd.Close</c> with an explicit form/report target.</summary>
    Close,

    /// <summary><c>DoCmd.BrowseTo</c> (subform / pane navigation).</summary>
    BrowseTo,
}

/// <summary>
/// Discriminates target flavour on
/// <see cref="NavigationEdge.TargetKind"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NavigationTargetKind
{
    /// <summary>Form (typical for <c>OpenForm</c> / <c>BrowseTo</c>).</summary>
    Form,

    /// <summary>Report (typical for <c>OpenReport</c>).</summary>
    Report,

    /// <summary>
    /// Object kind could not be determined — either because the
    /// dispatch was a <c>DoCmd.Close</c> whose first argument was
    /// non-literal, or the <c>BrowseTo</c> object-type argument was
    /// omitted.
    /// </summary>
    Unknown,
}

/// <summary>
/// Dispatch site whose target name was a non-literal expression
/// (variable, function call, concatenation). Carries enough provenance
/// for Phase 6.8's manual-review aggregator to emit an
/// <c>UnresolvedItem</c> without re-walking the body.
/// </summary>
public sealed record UnresolvedNavigationTarget
{
    /// <summary>
    /// VBA module the dispatch lives in.
    /// </summary>
    public required string SourceModule { get; init; }

    /// <summary>
    /// Procedure inside <see cref="SourceModule"/> that contains the
    /// dispatch.
    /// </summary>
    public required string SourceProcedure { get; init; }

    /// <summary>
    /// Procedure flavour, mirroring
    /// <see cref="NavigationEdge.SourceProcedureKind"/> on resolved
    /// edges so consumers can treat resolved and unresolved dispatches
    /// uniformly.
    /// </summary>
    public required ProcedureKind SourceProcedureKind { get; init; }

    /// <summary>
    /// 1-based source line of the dispatch call.
    /// </summary>
    public required int SourceLine { get; init; }

    /// <summary>
    /// 1-based source column of the dispatch call.
    /// </summary>
    public required int SourceColumn { get; init; }

    /// <summary>
    /// The navigation verb (OpenForm / OpenReport / Close / BrowseTo)
    /// the dispatch invoked.
    /// </summary>
    public required NavigationAction Action { get; init; }

    /// <summary>
    /// Reason the target could not be resolved. Stable strings —
    /// future builder revisions append to the closed set rather than
    /// renaming existing values. Currently:
    /// <list type="bullet">
    ///   <item><description><c>"non-literal-target"</c> — first argument was an expression other than a string literal.</description></item>
    ///   <item><description><c>"missing-target"</c> — call had no positional arguments.</description></item>
    /// </list>
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Source-text representation of the unresolved target expression
    /// (e.g., <c>"sFormName"</c>, <c>"GetTargetForm()"</c>,
    /// <c>"prefix &amp; suffix"</c>) so the consumer has the original
    /// VBA in front of it without re-reading the source file. Built by
    /// <see cref="NavigationGraphBuilder"/>'s small expression-text
    /// formatter; not a faithful pretty-printer.
    /// </summary>
    public string? TargetExpressionText { get; init; }
}

/// <summary>
/// Top-level ribbon structure parsed from <c>USysRibbons</c>. Supplied
/// by the metadata extractor; the navigation builder does not parse
/// the XML itself.
/// </summary>
public sealed record RibbonStructure
{
    /// <summary>
    /// Tabs in source order (the ribbon XML's display order). Each
    /// tab carries its own ordered group/control children.
    /// </summary>
    public required IReadOnlyList<RibbonTab> Tabs { get; init; }
}

/// <summary>One ribbon tab.</summary>
public sealed record RibbonTab
{
    /// <summary>Tab id as written in the ribbon XML's <c>id</c> attribute.</summary>
    public required string Id { get; init; }

    /// <summary>Display label, sourced from the XML's <c>label</c> attribute.</summary>
    public required string Label { get; init; }

    /// <summary>Groups inside the tab in source order.</summary>
    public required IReadOnlyList<RibbonGroup> Groups { get; init; }
}

/// <summary>One ribbon group (visual cluster of controls inside a tab).</summary>
public sealed record RibbonGroup
{
    /// <summary>Group id as written in the ribbon XML.</summary>
    public required string Id { get; init; }

    /// <summary>Display label.</summary>
    public required string Label { get; init; }

    /// <summary>Controls inside the group in source order.</summary>
    public required IReadOnlyList<RibbonControl> Controls { get; init; }
}

/// <summary>
/// One ribbon control (button, toggle, etc.). The
/// <see cref="OnAction"/> attribute names the VBA callback, which is a
/// navigation entry point in its own right.
/// </summary>
public sealed record RibbonControl
{
    /// <summary>Control id as written in the ribbon XML.</summary>
    public required string Id { get; init; }

    /// <summary>Display label.</summary>
    public required string Label { get; init; }

    /// <summary>
    /// VBA procedure name the control's <c>onAction</c> attribute
    /// points to. Null when the control is purely declarative
    /// (separators, labels). The matching procedure may itself appear
    /// as <see cref="NavigationEdge.SourceProcedure"/> on one or more
    /// edges.
    /// </summary>
    public string? OnAction { get; init; }
}

/// <summary>
/// Top-level switchboard parsed from the <c>Switchboard Items</c>
/// table. Supplied by the metadata extractor; the navigation builder
/// does not query the table itself.
/// </summary>
public sealed record SwitchboardStructure
{
    /// <summary>
    /// Switchboard pages in declaration order. Each page carries the
    /// items the user can pick from; one page is typically the root
    /// (the first one or the one with <c>ItemNumber = 0</c>).
    /// </summary>
    public required IReadOnlyList<SwitchboardPage> Pages { get; init; }
}

/// <summary>One switchboard page.</summary>
public sealed record SwitchboardPage
{
    /// <summary>Switchboard ID as stored in <c>Switchboard Items</c>.</summary>
    public required int SwitchboardId { get; init; }

    /// <summary>Page label (the switchboard heading shown to the user).</summary>
    public required string Name { get; init; }

    /// <summary>Items inside the page in display order.</summary>
    public required IReadOnlyList<SwitchboardItem> Items { get; init; }
}

/// <summary>
/// One row inside a switchboard page. Each row pairs a label with an
/// action — most often opening a form/report or jumping to another
/// switchboard page.
/// </summary>
public sealed record SwitchboardItem
{
    /// <summary>Display text shown to the user.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// Switchboard command code (per the Access switchboard convention:
    /// 1 = go to switchboard, 2 = open form add, 3 = open form browse,
    /// 4 = open report, 5 = design app, 6 = exit application,
    /// 7 = run macro, 8 = run code). Preserved as the raw integer so
    /// older Access versions with extended codes round-trip.
    /// </summary>
    public required int Command { get; init; }

    /// <summary>
    /// Argument the command consumes. Typically a form name (commands
    /// 2 / 3), report name (4), macro name (7), or function name (8).
    /// Null when the command takes no argument (5 / 6).
    /// </summary>
    public string? Argument { get; init; }
}

/// <summary>
/// One named Access macro the metadata extractor surfaces — typically
/// AutoExec, possibly other named macros (Forms!frmFoo.AfterUpdate, a
/// shortcut macro, a menu macro). Macros are not VBA: they are stored
/// in <c>MSysObjects</c> with a structured action sequence.
/// </summary>
public sealed record MacroChain
{
    /// <summary>
    /// Macro name as Access stores it — <c>"AutoExec"</c> for the
    /// special-name auto-run macro, otherwise the user-given name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Ordered macro actions. Each action carries its action name and
    /// the verb's argument map; deeper structure (argument types,
    /// sub-conditions) belongs to the macro extractor and is not
    /// re-modelled here.
    /// </summary>
    public required IReadOnlyList<MacroAction> Actions { get; init; }
}

/// <summary>One action inside a <see cref="MacroChain"/>.</summary>
public sealed record MacroAction
{
    /// <summary>
    /// Access macro action name (e.g., <c>"OpenForm"</c>,
    /// <c>"RunCode"</c>, <c>"SetWarnings"</c>).
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// Action arguments keyed by Access's argument name. Values are
    /// the raw payload the macro carried — string for textual args,
    /// boolean for True/False args, integer for enum args. The
    /// builder treats this as opaque payload it forwards intact.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Arguments { get; init; }

    /// <summary>
    /// VBA procedure the action calls when
    /// <see cref="ActionName"/> is <c>"RunCode"</c>. Extracted from
    /// the <c>FunctionName</c> argument with any trailing
    /// parentheses stripped, so a macro arg of
    /// <c>"Initialize()"</c> surfaces as <c>"Initialize"</c>. Null
    /// for non-RunCode actions and for RunCode entries whose
    /// FunctionName argument was missing.
    /// </summary>
    public string? CallsProcedureName { get; init; }
}

/// <summary>
/// Procedure identity used by
/// <see cref="NavigationEntryPoint.CallsProcedure"/> and reused on
/// other entry-point shapes that name a callback. Identifies the
/// procedure by module name only when it can be resolved — bare names
/// from macro arguments (where module is implicit) leave
/// <see cref="ModuleName"/> null.
/// </summary>
public sealed record ProcedureReference
{
    /// <summary>
    /// VBA module the procedure lives in. Null when the source the
    /// reference came from did not name a module (typical for macro
    /// <c>RunCode</c> arguments).
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>Procedure name as written.</summary>
    public required string ProcedureName { get; init; }
}

/// <summary>
/// Opt-in inputs to <see cref="NavigationGraphBuilder.Build"/> for the
/// non-VBA navigation surfaces. All properties are optional; omitting
/// any one means the corresponding facet on
/// <see cref="NavigationGraph"/> is null / empty rather than synthesised.
/// </summary>
public sealed record NavigationGraphInputs
{
    /// <summary>
    /// Custom ribbon parsed from <c>USysRibbons</c>. Null when the
    /// metadata extractor did not surface a ribbon (Access default
    /// ribbon in use).
    /// </summary>
    public RibbonStructure? Ribbon { get; init; }

    /// <summary>
    /// Switchboard parsed from <c>Switchboard Items</c>. Null when
    /// the application uses no switchboard.
    /// </summary>
    public SwitchboardStructure? Switchboard { get; init; }

    /// <summary>
    /// Macros from <c>MSysObjects</c>. Empty / null when the
    /// application defines none (AutoExec absence is the common case).
    /// </summary>
    public IReadOnlyList<MacroChain>? Macros { get; init; }

    /// <summary>
    /// Default startup form supplied by Access's startup options
    /// (often <c>"frmMainSwitchboard"</c>). Null when no startup form
    /// is configured.
    /// </summary>
    public string? DefaultFormName { get; init; }
}
