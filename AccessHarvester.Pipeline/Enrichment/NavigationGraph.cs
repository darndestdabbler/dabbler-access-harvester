using System;
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
    /// Ribbon navigation entry points (016 S2 / DW-13): one record per
    /// custom-ribbon callback — every control <c>onAction</c> plus the root
    /// <c>onLoad</c> — resolved against the harvested VBA modules. The
    /// "user clicks a ribbon button → runs this callback" surface, the ribbon
    /// analogue of <see cref="EntryPoints"/>'s startup roots. Kept in its own
    /// collection (not folded into <see cref="EntryPoints"/>, which models
    /// application <i>startup</i> roots) per the S2 cross-provider design
    /// consensus (<c>docs/session-sets/016-ribbon-navigation-surface/s2-ribbon-entry-point-consensus.md</c>).
    /// <para>
    /// Null (omitted from JSON) when <see cref="Ribbon"/> is null — a database
    /// with no custom ribbon contributes nothing, exactly like
    /// <see cref="Ribbon"/> itself, so a non-ribbon corpus stays byte-neutral.
    /// Populated (possibly empty, when a ribbon defines no callbacks) only when
    /// a ribbon is present. Emitted in deterministic source order: the root
    /// <c>onLoad</c> first, then a depth-first walk of tabs → groups →
    /// controls → children, then contextual tab sets.
    /// </para>
    /// <para>
    /// An <c>onAction</c> whose name matches no <i>bindable</i> procedure
    /// (Public/default in a standard module) is surfaced as
    /// <see cref="RibbonCallbackResolution.Unresolved"/> (never dropped); a name
    /// matching bindable procedures in more than one module is
    /// <see cref="RibbonCallbackResolution.Ambiguous"/> with no guessed module
    /// — the unresolvable-constructs-are-explicit principle.
    /// </para>
    /// </summary>
    public IReadOnlyList<RibbonEntryPoint>? RibbonEntryPoints { get; init; }

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

    /// <summary>
    /// Cross-object navigation **chain** edges derived from the metadata
    /// surfaces (embedded macros, record sources, query parameters) by
    /// <see cref="NavigationChainBuilder"/> — the R2-2 / DW-10 launcher →
    /// report → dialog → query → parameter chain the consumer requested.
    /// Distinct from the VBA-side <see cref="Edges"/> (individual
    /// <c>DoCmd</c> dispatches): a <see cref="ChainEdge"/> joins two
    /// application objects across a structural relationship. Empty when no
    /// chain relationships were found (e.g., when the builder was not run).
    /// Sorted deterministically.
    /// </summary>
    public IReadOnlyList<ChainEdge> ChainEdges { get; init; } = System.Array.Empty<ChainEdge>();

    /// <summary>
    /// Chain relationships whose far endpoint could not be resolved
    /// mechanically — most commonly a prompt-style declared query parameter
    /// whose supplier is the runtime user, or an inline record source that
    /// did not parse. Surfaced explicitly (never silently dropped) so the
    /// consumer sees "a chain exists here but its supplier is dynamic"
    /// rather than silence. Sorted deterministically.
    /// </summary>
    public IReadOnlyList<UnresolvedChainTarget> UnresolvedChainTargets { get; init; }
        = System.Array.Empty<UnresolvedChainTarget>();
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
    /// 0-based source column of the dispatch call, carried verbatim from the
    /// AST provenance (ANTLR convention; no normalisation). Surfaced 0-based
    /// in unresolved-items.json — contrast the per-module manifest's 1-based
    /// <c>SourceLocation</c> column (DW-26 / DW-27).
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
    /// 0-based source column of the dispatch call, carried verbatim from the
    /// AST provenance (ANTLR convention; no normalisation). Surfaced 0-based
    /// in unresolved-items.json — contrast the per-module manifest's 1-based
    /// <c>SourceLocation</c> column (DW-26 / DW-27).
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
/// Top-level ribbon structure parsed from <c>USysRibbons</c> Office Fluent
/// customUI XML by <see cref="RibbonXmlParser"/>. A <b>faithful recursive
/// tree</b>: every authored tab/group/control is surfaced (custom
/// <c>id</c>-bearing controls AND built-in <c>idMso</c> controls), nesting
/// is preserved via <see cref="RibbonControl.Children"/>, and any attribute
/// without a typed slot is preserved verbatim in source order on the node's
/// <c>Attributes</c> bag. The harvester does not interpret the ribbon
/// (no <c>imageMso</c> decode, no dynamic <c>get*</c>/<c>getVisible</c>
/// evaluation) beyond surfacing the static <see cref="RibbonControl.OnAction"/>
/// callbacks, which are navigation entry points. Design rationale +
/// cross-provider consensus: <c>docs/session-sets/016-ribbon-navigation-surface/s1-ribbon-model-consensus.md</c>.
/// </summary>
public sealed record RibbonStructure
{
    /// <summary>
    /// The <c>USysRibbons.RibbonName</c> of the row this structure was parsed
    /// from (e.g. <c>"Main"</c>), or null when set directly (e.g. a builder
    /// test). When a database defines more than one ribbon row, this names the
    /// one surfaced here; all rows are preserved losslessly in the raw
    /// <c>raw/Navigation/ribbon-xml.json</c> dump regardless.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Attributes of the <c>&lt;ribbon&gt;</c> element with no typed slot
    /// (e.g. <c>startFromScratch</c>), in source order. Verbatim, not interpreted.
    /// </summary>
    public IReadOnlyList<RibbonAttribute> Attributes { get; init; } = Array.Empty<RibbonAttribute>();

    /// <summary>
    /// The <c>customUI</c> root's <c>onLoad</c> callback (a ribbon-load VBA
    /// procedure), or null when absent. Surfaced as the one typed root
    /// callback; other dynamic callbacks ride verbatim in node
    /// <see cref="Attributes"/> and are not resolved this set.
    /// </summary>
    public string? OnLoad { get; init; }

    /// <summary>
    /// Tabs in source order (the ribbon XML's display order). Each
    /// tab carries its own ordered group/control children.
    /// </summary>
    public IReadOnlyList<RibbonTab> Tabs { get; init; } = Array.Empty<RibbonTab>();

    /// <summary>
    /// Contextual tab sets (the <c>&lt;contextualTabs&gt;</c> section), in
    /// source order. Empty when the ribbon defines none.
    /// </summary>
    public IReadOnlyList<RibbonContextualTabSet> ContextualTabSets { get; init; }
        = Array.Empty<RibbonContextualTabSet>();
}

/// <summary>One verbatim ribbon-XML attribute (name + value) with no typed slot.</summary>
public sealed record RibbonAttribute
{
    /// <summary>Attribute local name, exactly as written.</summary>
    public required string Name { get; init; }

    /// <summary>Attribute value, exactly as written.</summary>
    public required string Value { get; init; }
}

/// <summary>
/// One contextual tab set (<c>&lt;tabSet&gt;</c>), which scopes a built-in or
/// custom tool tab to a selection context. Carries its own tabs.
/// </summary>
public sealed record RibbonContextualTabSet
{
    /// <summary>Custom <c>id</c>, or null for a built-in <c>idMso</c> tab set.</summary>
    public string? Id { get; init; }

    /// <summary>Built-in <c>idMso</c>, or null for a custom tab set.</summary>
    public string? IdMso { get; init; }

    /// <summary>Unslotted attributes in source order. Verbatim.</summary>
    public IReadOnlyList<RibbonAttribute> Attributes { get; init; } = Array.Empty<RibbonAttribute>();

    /// <summary>Tabs inside the set in source order.</summary>
    public IReadOnlyList<RibbonTab> Tabs { get; init; } = Array.Empty<RibbonTab>();
}

/// <summary>One ribbon tab. Custom (<c>id</c>) or built-in (<c>idMso</c>).</summary>
public sealed record RibbonTab
{
    /// <summary>Custom <c>id</c> attribute, or null for a built-in <c>idMso</c> tab.</summary>
    public string? Id { get; init; }

    /// <summary>Built-in <c>idMso</c> attribute, or null for a custom tab.</summary>
    public string? IdMso { get; init; }

    /// <summary>Display label from the <c>label</c> attribute, or null when absent.</summary>
    public string? Label { get; init; }

    /// <summary>Unslotted attributes (e.g. <c>visible</c>) in source order. Verbatim.</summary>
    public IReadOnlyList<RibbonAttribute> Attributes { get; init; } = Array.Empty<RibbonAttribute>();

    /// <summary>Groups inside the tab in source order.</summary>
    public IReadOnlyList<RibbonGroup> Groups { get; init; } = Array.Empty<RibbonGroup>();
}

/// <summary>One ribbon group (visual cluster of controls inside a tab).</summary>
public sealed record RibbonGroup
{
    /// <summary>Custom <c>id</c>, or null for a built-in <c>idMso</c> group.</summary>
    public string? Id { get; init; }

    /// <summary>Built-in <c>idMso</c>, or null for a custom group.</summary>
    public string? IdMso { get; init; }

    /// <summary>Display label, or null when absent.</summary>
    public string? Label { get; init; }

    /// <summary>Unslotted attributes (e.g. <c>getVisible</c>) in source order. Verbatim.</summary>
    public IReadOnlyList<RibbonAttribute> Attributes { get; init; } = Array.Empty<RibbonAttribute>();

    /// <summary>Controls inside the group in source order.</summary>
    public IReadOnlyList<RibbonControl> Controls { get; init; } = Array.Empty<RibbonControl>();
}

/// <summary>
/// One ribbon control. Covers every customUI control element
/// (<c>button</c>, <c>toggleButton</c>, <c>splitButton</c>, <c>menu</c>,
/// <c>dropDown</c>, <c>gallery</c>, <c>comboBox</c>, <c>box</c>,
/// <c>control</c>, <c>menuSeparator</c>, …) via the open-string
/// <see cref="ControlType"/>; nested children (a <c>splitButton</c>'s
/// <c>button</c>/<c>menu</c>, a <c>box</c>'s contents, a <c>menu</c>'s items)
/// hang off <see cref="Children"/>. The <see cref="OnAction"/> attribute
/// names the static VBA callback, which is a navigation entry point in its
/// own right.
/// </summary>
public sealed record RibbonControl
{
    /// <summary>
    /// The XML element's local name (e.g. <c>"button"</c>, <c>"dropDown"</c>,
    /// <c>"menuSeparator"</c>). Open string — a new Fluent element never
    /// breaks the schema.
    /// </summary>
    public required string ControlType { get; init; }

    /// <summary>Custom <c>id</c>, or null for a built-in <c>idMso</c> control.</summary>
    public string? Id { get; init; }

    /// <summary>Built-in <c>idMso</c>, or null for a custom control.</summary>
    public string? IdMso { get; init; }

    /// <summary>Display label, or null when absent (e.g. an <c>idMso</c> control).</summary>
    public string? Label { get; init; }

    /// <summary>
    /// VBA procedure name the control's <c>onAction</c> attribute points to.
    /// Null when the control has no static <c>onAction</c> (declarative
    /// separators/boxes, or built-in <c>idMso</c> commands). The matching
    /// procedure may itself appear as
    /// <see cref="NavigationEdge.SourceProcedure"/> on one or more edges.
    /// 016 S2 resolves each <c>onAction</c> to a
    /// <see cref="RibbonEntryPoint"/> on
    /// <see cref="NavigationGraph.RibbonEntryPoints"/>.
    /// </summary>
    public string? OnAction { get; init; }

    /// <summary>
    /// Unslotted attributes (e.g. <c>imageMso</c>, <c>size</c>, <c>boxStyle</c>,
    /// <c>screentip</c>, <c>title</c>, the dynamic <c>get*</c> callbacks) in
    /// source order. Verbatim, not interpreted.
    /// </summary>
    public IReadOnlyList<RibbonAttribute> Attributes { get; init; } = Array.Empty<RibbonAttribute>();

    /// <summary>
    /// Nested child controls in source order (a <c>splitButton</c>/<c>menu</c>/
    /// <c>box</c>/<c>gallery</c>'s contents). Empty for leaf controls.
    /// </summary>
    public IReadOnlyList<RibbonControl> Children { get; init; } = Array.Empty<RibbonControl>();
}

/// <summary>
/// One ribbon navigation entry point (016 S2 / DW-13): a single custom-ribbon
/// callback — a control's <c>onAction</c> or the ribbon root's <c>onLoad</c> —
/// together with the result of resolving its name against the harvested VBA
/// modules. The ribbon analogue of <see cref="NavigationEntryPoint"/>: a user
/// clicking the control runs <see cref="CallbackName"/>, which (when it
/// resolves uniquely) is the procedure named by <see cref="ResolvedProcedure"/>.
/// </summary>
/// <remarks>
/// Faithful-mechanical: the callback is matched by name (case-insensitive, per
/// VBA semantics) against the procedures a ribbon callback can actually bind to
/// — Public (or modifier-less, which is Public by default) procedures in
/// <b>standard</b> modules. Private/Friend procedures and class/form/report
/// code-behind are excluded because Access cannot dispatch a ribbon callback to
/// them; this applies Access's static binding rule, not a dynamic-signature
/// check. A name matching exactly one bindable procedure
/// <see cref="RibbonCallbackResolution.Resolved"/>s; a name matching none is
/// <see cref="RibbonCallbackResolution.Unresolved"/> (its <c>CallbackName</c>
/// is still retained verbatim — Access could not bind it either); a name
/// matching more than one bindable procedure is
/// <see cref="RibbonCallbackResolution.Ambiguous"/> with
/// <see cref="ResolvedProcedure"/> left null (the producer never guesses which
/// module). All cases are surfaced — none is silently dropped.
/// </remarks>
public sealed record RibbonEntryPoint
{
    /// <summary>Whether this entry came from a control <c>onAction</c> or the root <c>onLoad</c>.</summary>
    public required RibbonCallbackKind Kind { get; init; }

    /// <summary>
    /// The callback procedure name exactly as authored in the ribbon XML
    /// (the <c>onAction</c> / <c>onLoad</c> attribute value). This is the
    /// identity the resolution keys off; preserved verbatim even when the
    /// name does not resolve.
    /// </summary>
    public required string CallbackName { get; init; }

    /// <summary>
    /// The owning control's element type (e.g. <c>"button"</c>,
    /// <c>"dropDown"</c>). Null for the ribbon-root <c>onLoad</c> entry, which
    /// belongs to no control.
    /// </summary>
    public string? ControlType { get; init; }

    /// <summary>The owning control's custom <c>id</c>, or null (root <c>onLoad</c>, or a built-in <c>idMso</c> control).</summary>
    public string? ControlId { get; init; }

    /// <summary>The owning control's built-in <c>idMso</c>, or null (root <c>onLoad</c>, or a custom <c>id</c> control).</summary>
    public string? ControlIdMso { get; init; }

    /// <summary>The owning control's display <c>label</c>, or null when absent / not applicable.</summary>
    public string? ControlLabel { get; init; }

    /// <summary>Whether the callback name resolved to a unique procedure, none, or several.</summary>
    public required RibbonCallbackResolution Resolution { get; init; }

    /// <summary>
    /// The resolved procedure (module + declared procedure name) when
    /// <see cref="Resolution"/> is <see cref="RibbonCallbackResolution.Resolved"/>;
    /// null for <see cref="RibbonCallbackResolution.Unresolved"/> and
    /// <see cref="RibbonCallbackResolution.Ambiguous"/>. The
    /// <see cref="ProcedureReference.ModuleName"/> is always populated here
    /// (a resolved ribbon callback always names its module), unlike the macro
    /// <c>RunCode</c> entries on <see cref="NavigationEntryPoint.CallsProcedure"/>.
    /// </summary>
    public ProcedureReference? ResolvedProcedure { get; init; }
}

/// <summary>Discriminates the ribbon callback source on <see cref="RibbonEntryPoint.Kind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RibbonCallbackKind
{
    /// <summary>A control's <c>onAction</c> click callback.</summary>
    OnAction,

    /// <summary>The ribbon root's <c>onLoad</c> lifecycle callback (<c>customUI@onLoad</c>).</summary>
    OnLoad,
}

/// <summary>Outcome of resolving a ribbon callback name on <see cref="RibbonEntryPoint.Resolution"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RibbonCallbackResolution
{
    /// <summary>The name matched exactly one harvested procedure; <see cref="RibbonEntryPoint.ResolvedProcedure"/> names it.</summary>
    Resolved,

    /// <summary>The name matched no harvested procedure (an explicit unresolved entry point — never dropped).</summary>
    Unresolved,

    /// <summary>The name matched procedures in more than one module; no module is guessed.</summary>
    Ambiguous,
}

/// <summary>
/// Top-level switchboard parsed from a switchboard-shaped table (one
/// whose columns carry the canonical <c>SwitchboardID, ItemNumber,
/// ItemText, Command, Argument</c> signature). Supplied by the metadata
/// extractor; the navigation builder does not query the table itself.
/// </summary>
/// <remarks>
/// The harvester does not assume the canonical Access Switchboard
/// Manager layout. Both the wizard-generated table (rows grouped by a
/// non-unique <c>SwitchboardID</c> page id; <c>ItemNumber = 0</c> the
/// page header) and hand-rolled custom variants (e.g. an autonumber
/// <c>SwitchboardID</c> plus a <c>ParentID</c> hierarchy column) are
/// materialised. The page-grouping rule is mechanical and generic — see
/// <see cref="SwitchboardStructureBuilder"/>. Command codes are
/// preserved as raw integers (see <see cref="SwitchboardItem.Command"/>)
/// and are NOT decoded against any assumed command set; the downstream
/// AI consumer interprets them.
/// </remarks>
public sealed record SwitchboardStructure
{
    /// <summary>
    /// Switchboard pages ordered by page key. Each page carries the
    /// items grouped under it.
    /// </summary>
    public required IReadOnlyList<SwitchboardPage> Pages { get; init; }

    /// <summary>
    /// Count of materialised switchboard rows that carry no page
    /// linkage (for a <c>ParentID</c>-hierarchy table, rows whose
    /// <c>ParentID</c> is null) and therefore belong to no page. They
    /// are preserved losslessly in the raw
    /// <c>raw/Navigation/switchboard-items.json</c> dump but are not
    /// reflected in <see cref="Pages"/> — recorded here explicitly so
    /// the absence is never silent. 0 for canonical tables (where the
    /// page key is the never-null <c>SwitchboardID</c>).
    /// </summary>
    public int UnlinkedItemCount { get; init; }
}

/// <summary>One switchboard page.</summary>
public sealed record SwitchboardPage
{
    /// <summary>
    /// Page key: the table's <c>ParentID</c> value when it has a
    /// <c>ParentID</c> column (hand-rolled hierarchy tables), else the
    /// (non-unique) <c>SwitchboardID</c> (canonical wizard tables). Not
    /// globally unique when more than one switchboard table is present —
    /// pair with <see cref="Table"/> for page identity in that case.
    /// </summary>
    public required int SwitchboardId { get; init; }

    /// <summary>
    /// The switchboard-shaped table this page's rows came from. Page-level
    /// source identity so multi-table corpora stay unambiguous.
    /// </summary>
    public required string Table { get; init; }

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
    /// The row's <c>Command</c> value, preserved as the <b>raw integer
    /// exactly as stored in the table</b>. The harvester does NOT decode
    /// it against any assumed command set: the canonical Access
    /// Switchboard Manager scheme (1 = go to switchboard, 2 = open form
    /// add, 3 = open form browse, 4 = open report, 5 = design app,
    /// 6 = exit, 7 = run macro, 8 = run code) is only <i>one</i> possible
    /// convention, and hand-rolled switchboard tables use their own codes
    /// (e.g. HomeHealthCare uses 0 = label/header, 1 = open form,
    /// 2 = go to page, 3 = open report). Interpreting the code is
    /// downstream-AI work; the harvester preserves the raw value so no
    /// meaning is lost or guessed. Nullable: a row with no stored
    /// <c>Command</c> surfaces as null (omitted), never coerced to a
    /// synthetic 0 that would collide with a real command-0 label.
    /// </summary>
    public required int? Command { get; init; }

    /// <summary>
    /// The row's <c>Argument</c> value (typically a form / report / macro
    /// / function name, or a target page). Null when the table stored no
    /// argument (Access Null or an empty/zero-length string — both
    /// surface here as null, since an empty argument is "no argument").
    /// </summary>
    public string? Argument { get; init; }

    /// <summary>
    /// Per-row provenance tying this structured item back to the
    /// materialised switchboard row it came from. Null only on items
    /// synthesised by tests; always populated for harvested switchboards.
    /// </summary>
    public SwitchboardItemProvenance? Provenance { get; init; }
}

/// <summary>
/// Provenance for one <see cref="SwitchboardItem"/>, per the output
/// <c>{ source, ... }</c> convention. Lets the consumer trace a
/// structured item back to its exact source row.
/// </summary>
public sealed record SwitchboardItemProvenance
{
    /// <summary>Always <c>"switchboard-row"</c>.</summary>
    public required string Source { get; init; }

    /// <summary>Name of the switchboard-shaped table the row came from.</summary>
    public required string Table { get; init; }

    /// <summary>
    /// The row's raw <c>SwitchboardID</c> value (the autonumber/PK for a
    /// custom table; the page id for a canonical one). Nullable so a row
    /// with no stored value is not coerced to a synthetic 0.
    /// </summary>
    public required int? SwitchboardId { get; init; }

    /// <summary>
    /// The row's <c>ItemNumber</c> (display order within its page).
    /// Nullable so a row with no stored value is not coerced to 0.
    /// </summary>
    public required int? ItemNumber { get; init; }

    /// <summary>
    /// The row's <c>ParentID</c> when the table carries that column
    /// (hand-rolled hierarchy tables); null for canonical tables that
    /// have no <c>ParentID</c> column.
    /// </summary>
    public int? ParentId { get; init; }
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

// ── Chain edges (R2-2 / DW-10) ─────────────────────────────────────────

/// <summary>
/// One cross-object navigation **chain** edge — a structural relationship
/// joining two application objects, derived mechanically from the metadata
/// surfaces (embedded macros, record sources, query parameters) rather than
/// from a single VBA dispatch. The launcher → report → dialog → query →
/// parameter chain a downstream AI consumer walks to understand "what opens
/// what, fed by which data, parameterised by which control."
/// </summary>
/// <remarks>
/// Direction is always supplier → consumer for the data/parameter kinds
/// (<c>FeedsRecordSource</c>: source → owner; <c>SuppliesParameter</c>:
/// supplier → consumer) and launcher → target for the open kinds
/// (<c>OpensReport</c>/<c>OpensForm</c>/<c>OpensDialog</c>:
/// trigger → opened object).
/// </remarks>
public sealed record ChainEdge
{
    /// <summary>The originating endpoint (launcher / data source / parameter supplier).</summary>
    public required ChainEndpoint From { get; init; }

    /// <summary>The destination endpoint (opened object / record-source owner / parameter consumer).</summary>
    public required ChainEndpoint To { get; init; }

    /// <summary>The chain relationship kind.</summary>
    public required ChainEdgeKind Kind { get; init; }

    /// <summary>
    /// How the edge was derived. A **closed kebab-case string set** (emitted
    /// verbatim; mirrors <see cref="UnresolvedNavigationTarget.Reason"/>'s
    /// raw-string discipline so on-disk values match the consumer contract
    /// exactly). Future producers append to the set; existing values are
    /// never renamed:
    /// <list type="bullet">
    ///   <item><description><c>"vba-call"</c> — a code-behind <c>DoCmd</c> dispatch (S3 producer).</description></item>
    ///   <item><description><c>"embedded-macro"</c> — a control's embedded <c>EventMacros</c> <c>OpenForm</c>/<c>OpenReport</c> action.</description></item>
    ///   <item><description><c>"macro-action"</c> — a standalone macro action (reserved; no producer in this set).</description></item>
    ///   <item><description><c>"record-source"</c> — a form/report record source (named object or inline-SQL FROM/JOIN target).</description></item>
    ///   <item><description><c>"query-form-reference"</c> — a query's implicit <c>[Forms]!</c>/<c>[Reports]!</c> control reference (in a query body or an inline record source).</description></item>
    ///   <item><description><c>"query-declared-parameter"</c> — a query's declared <c>PARAMETERS</c> entry.</description></item>
    ///   <item><description><c>"tempvar-flow"</c> — a <c>TempVars!</c> reference in a record source (its setter is paired in by the S3 code-behind producer).</description></item>
    /// </list>
    /// </summary>
    public required string Provenance { get; init; }
}

/// <summary>
/// One endpoint of a <see cref="ChainEdge"/> or
/// <see cref="UnresolvedChainTarget"/>. Identifies an application object by
/// kind + name, with an optional <see cref="Member"/> for the control,
/// parameter, or token the relationship hangs off of.
/// </summary>
public sealed record ChainEndpoint
{
    /// <summary>The object kind this endpoint names.</summary>
    public required ChainNodeKind Kind { get; init; }

    /// <summary>
    /// The object name (form / report / query / table name; the TempVar
    /// name for a <see cref="ChainNodeKind.TempVar"/>; a stable label for
    /// <see cref="ChainNodeKind.Prompt"/>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The control, parameter, or token within <see cref="Name"/> the
    /// relationship is anchored to (e.g., the launching command button on a
    /// form, or the referenced control on a dialog). Null when the
    /// relationship is at object granularity.
    /// </summary>
    public string? Member { get; init; }
}

/// <summary>Discriminates the endpoint object kind on <see cref="ChainEndpoint.Kind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChainNodeKind
{
    /// <summary>An Access form.</summary>
    Form,

    /// <summary>An Access report.</summary>
    Report,

    /// <summary>A saved query.</summary>
    Query,

    /// <summary>A table.</summary>
    Table,

    /// <summary>A <c>TempVars!</c> temporary variable (supplier resolved in the S3 code-behind pass).</summary>
    TempVar,

    /// <summary>A runtime user prompt (a prompt-style declared parameter's supplier).</summary>
    Prompt,

    /// <summary>
    /// A named target that could not be classified as a known query or
    /// table (e.g., an inline-SQL FROM target naming an object not present
    /// in the harvested set).
    /// </summary>
    Unknown,
}

/// <summary>Discriminates the chain relationship on <see cref="ChainEdge.Kind"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChainEdgeKind
{
    /// <summary>Opens a form modally as a parameter dialog (<c>acDialog</c>); distinguished only in the S3 VBA pass.</summary>
    OpensDialog,

    /// <summary>Opens a report.</summary>
    OpensReport,

    /// <summary>Opens a form (non-dialog).</summary>
    OpensForm,

    /// <summary>Supplies the record source of a form/report (the data the object binds to).</summary>
    FeedsRecordSource,

    /// <summary>Supplies a parameter value consumed by a query/report.</summary>
    SuppliesParameter,
}

/// <summary>
/// A chain relationship whose far endpoint is not mechanically resolvable —
/// the explicit-unresolved channel for chains, mirroring
/// <see cref="UnresolvedNavigationTarget"/> for the VBA edges. Most commonly
/// a prompt-style declared query parameter (the supplier is the runtime
/// user) or an inline record source that failed to parse.
/// </summary>
public sealed record UnresolvedChainTarget
{
    /// <summary>The chain kind that could not be completed.</summary>
    public required ChainEdgeKind Kind { get; init; }

    /// <summary>The known (consumer) endpoint of the would-be chain.</summary>
    public required ChainEndpoint Consumer { get; init; }

    /// <summary>The provenance closed-set value (see <see cref="ChainEdge.Provenance"/>).</summary>
    public required string Provenance { get; init; }

    /// <summary>
    /// Why the far endpoint is unresolved. A closed kebab-case string set:
    /// <list type="bullet">
    ///   <item><description><c>"prompt-parameter"</c> — a declared parameter whose value is entered at runtime.</description></item>
    ///   <item><description><c>"record-source-unparsed"</c> — an inline record source the SQL parser could not turn into a usable tree (no FROM/JOIN data source recovered).</description></item>
    ///   <item><description><c>"embedded-macro-unparsed"</c> — a control/object carries an <c>On…EmMacro</c> property the enricher did not structure into <c>EventMacros</c>, so no chain edge could be derived (the raw property is still preserved in <c>ir/</c>).</description></item>
    ///   <item><description><c>"unresolvable-reference"</c> — a query <c>[Forms]!</c>/<c>[Reports]!</c> parameter whose path named no resolvable object.</description></item>
    ///   <item><description><c>"non-literal-target"</c> — (S3 code-behind) a <c>DoCmd.OpenForm/OpenReport</c> whose target is a variable/expression, not a string literal — the chain exists but its target is dynamic.</description></item>
    ///   <item><description><c>"tempvar-no-supplier"</c> — (S3 code-behind) a record source references a <c>TempVars!</c> value that no harvested VBA sets, so the supply side of the chain is missing.</description></item>
    /// </list>
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The unresolved token's source text (e.g., the declared parameter name
    /// <c>"[Enter Provider ID:]"</c>) so the consumer sees the original
    /// without re-reading the object. Null when there is nothing to quote.
    /// </summary>
    public string? Detail { get; init; }
}
