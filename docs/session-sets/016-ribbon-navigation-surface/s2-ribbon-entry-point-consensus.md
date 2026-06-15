# S2 — Ribbon entry-point model: cross-provider design consensus

> Decision-time consensus (per `docs/ai-led-session-workflow.md` →
> Decision-time consensus, and the `feedback_seek_ai_consensus_before_asking_human`
> memory). Routed to **gpt-5-4** and **gemini-pro** before implementation.
> Mirrors S1's `s1-ribbon-model-consensus.md` precedent. Raw outputs:
> `C:\temp\ribbon_ep_gpt_5_4.md`, `C:\temp\ribbon_ep_gemini_pro.md` (local,
> not committed — proprietary-free design prose, reproduced in substance below).

## The question

How to model **ribbon entry points** (`onAction` / root `onLoad` →
resolved VBA procedure), including the explicit-unresolved case, given the
spec wording "emit a navigation entry point (kind: ribbon) … mirroring the
switchboard EntryPoints precedent."

Three options were put to both providers:

- **A** — reuse `NavigationGraph.EntryPoints`: add `EntryPointKind.Ribbon` +
  ribbon-only fields onto the shared `NavigationEntryPoint`.
- **B** — a dedicated `NavigationGraph.RibbonEntryPoints` collection with a
  purpose-built `RibbonEntryPoint` record carrying a `Resolution` enum.
- **C** — separate collections for resolved vs. unresolved ribbon callbacks
  (mirror the `EntryPoints` / `UnresolvedTargets` split).

## Verdict — **Option B (unanimous)**

Both providers independently chose **B**, with the same reasoning:

1. **`EntryPoints` is documented as "one root the application STARTS FROM"**
   (AutoExec / DefaultForm / SwitchboardRoot). Ribbon button callbacks are
   per-control click handlers, not startup roots — a real ribbon (Northwind)
   has dozens. Reusing `EntryPoints` corrupts that contract and floods the
   list. (Rejects A.)
2. **The repo already keeps distinct navigation surfaces in separate
   collections** (`UnresolvedNavigationTarget`, `ChainEdges`,
   `UnresolvedChainTargets`). A dedicated `RibbonEntryPoints` companion to the
   existing top-level `Ribbon` model is the consistent shape.
3. **Resolved / Unresolved / Ambiguous is the *resolution state of one
   callback occurrence*, not a different entity** — one record with a
   `Resolution` enum is cleaner than splitting arrays (rejects C: duplicated
   context, awkward `Ambiguous`, forced merges).
4. **On `Ambiguous`, never pick a module.** Faithful-mechanical means
   surfacing the ambiguity, not guessing: `Resolution=Ambiguous`,
   `ResolvedProcedure=null`.
5. **Root `OnLoad` fits naturally** as an entry in the same collection,
   discriminated from control `onAction` callbacks.

## Adopted shape

```
NavigationGraph.RibbonEntryPoints : IReadOnlyList<RibbonEntryPoint>?
    // nullable; null (omitted) when NavigationGraph.Ribbon is null
    // (HHC has no custom ribbon -> golden byte-neutral, like Ribbon itself).
    // Populated (possibly empty) only when a ribbon is present.

RibbonEntryPoint {
    RibbonCallbackKind   Kind;             // OnAction | OnLoad
    string               CallbackName;     // verbatim onAction / onLoad procedure name
    string?              ControlType;       // control element name; null for the OnLoad root
    string?              ControlId;         // custom id; null for OnLoad / idMso-only controls
    string?              ControlIdMso;      // built-in idMso; null for custom controls
    string?              ControlLabel;      // label; null when absent
    RibbonCallbackResolution Resolution;    // Resolved | Unresolved | Ambiguous
    ProcedureReference?  ResolvedProcedure; // {ModuleName, ProcedureName} on a unique match; null otherwise
}
```

- **Resolution** = case-insensitive (VBA semantics) name match of `CallbackName`
  against every harvested module's procedure declarations:
  0 matches → `Unresolved`; exactly 1 → `Resolved` (`ResolvedProcedure` =
  matched module + declared procedure name, actual casing); ≥2 → `Ambiguous`
  (`ResolvedProcedure` null — never guess). No signature/visibility filtering
  (that would be interpretation); pure name match per the faithful-mechanical
  principle.
- **Order** = deterministic source order: root `OnLoad` first, then a
  depth-first walk of `Tabs → Groups → Controls → Children`, then
  `ContextualTabSets`. The ribbon tree is already built in source order
  (S1's `RibbonStructureBuilder`), so this is byte-deterministic across runs
  without an additional sort, and preserves the authored order (more faithful
  than re-sorting by name).

## Spec reconciliation (recorded)

The spec's Step-2 phrase "emit a navigation entry point (kind: ribbon)" is
satisfied by **a ribbon entry point in `NavigationGraph.RibbonEntryPoints`**,
not a new `EntryPointKind` value on the startup-roots list. Both providers
flagged this as a trivial wording reconciliation; the intent (a surfaced,
resolution-explicit ribbon-callback navigation surface) is preserved. Noted in
the spec change-log; `EntryPointKind` is left as `AutoExec | DefaultForm |
SwitchboardRoot`.
