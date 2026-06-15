# S1 — Ribbon model design: cross-provider consensus

**Decision date:** 2026-06-15 (Set 016 Session 1)
**Category:** api-surface / spec-clarification (binds a long-lived model contract)
**Mechanism:** cross-provider consensus (gpt-5-4 + gemini-pro, `task_type=architecture`),
bias-cautions preamble on. Per `feedback_seek_ai_consensus_before_asking_human` and the
workflow's Decision-time consensus. Raw verdicts: `C:/temp/ribbon-consensus-{gpt-5-4,gemini-pro}.md`
(not committed). Journal: `ai_router/consensus-decisions.jsonl`.

## The question

The shipped `RibbonStructure` model is a flat `Tabs(Id,Label) > Groups(Id,Label) >
Controls(Id,Label,OnAction?)` with `required` Id/Label. The **real** NorthwindDevEdition
ribbon (the local validation corpus) is far richer than the spec assumed:

- Namespace is `…/2006/01/customui`, **not** the spec's `…/2009/07` → parser must be
  namespace-tolerant (match by local name).
- **Built-in `idMso`** tabs/groups/controls (`<button idMso="Cut"/>`,
  `<tab idMso="TabAddIns" .../>`) carry **no `id`, no `label`, no `onAction`** → breaks
  `required string Id/Label`.
- **Nesting below the group**: `<box>`, `<splitButton><button/><menu>…</menu></splitButton>`,
  `<menu>`, `<dropDown>`, `<gallery>`, `<comboBox>`, `<control>`, `<menuSeparator>` — 3–4 levels deep.
- A separate **`<contextualTabs>`** section (`<tabSet idMso><tab idMso .../></tabSet>`).
- Custom `onAction` callbacks live only on the one custom tab (~13 buttons + 1 dropDown).

## Consensus (both engines, independently)

Both chose **Option 2 — faithful recursive tree**. Flattening (Opt 1) is a destructive
transform that mis-parents `splitButton`/`menu` children; filtering to nav-only (Opt 3)
"interprets" by pre-judging what the downstream AI needs. Unanimous specific calls:

- **(a) Recurse via `Children`**, preserve XML source order — not flatten.
- **(b) Relax `Id`/`Label` to nullable; add `IdMso` + `ControlType`** (XML element local
  name) — yes/yes/yes; add `IdMso` to tabs/groups too; do not enforce "exactly one of id/idMso".
- **(c) Model `contextualTabs` fully** as a parallel `ContextualTabSets` collection on the
  root (an `IsContextual` flag would lose the `tabSet` boundary).
- **(e) Built-in `idMso` controls = full nodes**, never summarized/counted (counting is a
  derived interpretation).

**Disagreement — (d) RawAttributes bag:** GPT **yes** (else `visible`/`imageMso`/`boxStyle`/
`title`/`getItemCount`/`startFromScratch`/`onLoad` are silently lost); Gemini **no** (lean,
fixed typed slots, add as proven necessary).

## Synthesis (orchestrator judgment, grounded in the spec)

**Adopt Option 2 with a per-node verbatim `Attributes` bag, keeping the typed convenience
slots tiny.** The spec's own words resolve (d) in GPT's favour: *"preserve raw where the
schema has no slot (do not interpret imageMso, sizes, dynamic getVisible/getEnabled callbacks
beyond recording the static onAction). Unknown/unsupported control elements are surfaced, not
silently dropped."* A verbatim, source-ordered `(Name,Value)` attribute list is mechanical
(not a speculative abstraction), and it is the exact mechanism that lets us **not** model
`imageMso`/`visible`/`get*` as typed slots (honouring "don't interpret" + lean on the typed
surface) while **not dropping** them (honouring "surfaced not dropped"). The bag holds only
**unslotted** attributes (everything except the promoted `id`/`idMso`/`label`/`onAction`);
no double-recording. This converges with GPT's "dominant 4th option" (a generic
`RibbonNode{ElementName,Attributes,Children}` AST) — our typed tree IS that AST with the four
nav-relevant slots hoisted for readability (addresses Gemini's tree-walk concern), reusing the
shipped `RibbonStructure/RibbonTab/RibbonGroup/RibbonControl` names the `NavigationGraph`
already references.

### Final model (extends the shipped records, namespace `AccessHarvester.Pipeline.Enrichment`)

```
record RibbonAttribute        { string Name; string Value; }                 // source order
record RibbonStructure        { IReadOnlyList<RibbonAttribute> Attributes;   // <ribbon> attrs (startFromScratch)
                                string? OnLoad;                              // customUI@onLoad (a load callback)
                                IReadOnlyList<RibbonTab> Tabs;
                                IReadOnlyList<RibbonContextualTabSet> ContextualTabSets; }
record RibbonContextualTabSet { string? Id; string? IdMso;
                                IReadOnlyList<RibbonAttribute> Attributes;
                                IReadOnlyList<RibbonTab> Tabs; }
record RibbonTab              { string? Id; string? IdMso; string? Label;
                                IReadOnlyList<RibbonAttribute> Attributes;
                                IReadOnlyList<RibbonGroup> Groups; }
record RibbonGroup            { string? Id; string? IdMso; string? Label;
                                IReadOnlyList<RibbonAttribute> Attributes;
                                IReadOnlyList<RibbonControl> Controls; }
record RibbonControl          { string ControlType;                          // XML element local name
                                string? Id; string? IdMso; string? Label;
                                string? OnAction;
                                IReadOnlyList<RibbonAttribute> Attributes;
                                IReadOnlyList<RibbonControl> Children; }
```

`ControlType` is an **open string** (not an enum) so a new Fluent element never breaks the
schema (GPT's churn mitigation). `OnLoad` is the one typed root callback slot (a single
well-known customUI attribute, clearly a VBA callback); all other `get*`/`onLoad`-style
dynamic callbacks ride verbatim in node `Attributes` and are **not** resolved to entry points
this set (spec defers dynamic-callback evaluation).

## Follow-ups recorded (both filed in `deferred-work.md` during S1)

- **DW-20 (dynamic ribbon callbacks):** `onLoad` + the `get*` callback family (`getItemCount`,
  `getItemLabel`, `getSelectedItemIndex`, `getItemID`, `getVisible`, `getEnabled`) are real
  ribbon→VBA edges this set deliberately does **not** resolve to navigation entry points (spec:
  static `onAction` only). Surfaced verbatim in each node's `Attributes` bag; confirmed on real
  Northwind data (`customUI@onLoad="ribbonLoaded"`, `ddMRU_Get*`).
- **DW-21 (unmodeled customUI sections):** the structured view models `<tabs>`/`<contextualTabs>`
  only; `<qat>`/`<officeMenu>`/`<backstage>` and root attrs beyond `onLoad` are raw-only
  (lossless in `ribbon-xml.json`). Raised by S1 routed verification (gpt-5-4); documented boundary,
  not a code expansion (lean/scope).
- Parse **both** `2006/01` and `2009/07` customUI namespaces (match by local name).
