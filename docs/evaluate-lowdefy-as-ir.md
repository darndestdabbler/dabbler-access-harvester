# Brief: Evaluate Lowdefy's Schema as IR for the Access → Blazor Codegen

## Background

I am building a tool that migrates Microsoft Access databases (`.accdb`) to .NET/Blazor applications. The pipeline today is roughly:

```
Access (.accdb)
  ↓ extraction tool (already built)
Access-shaped YAML  (one file per Tables/Queries/Forms/Reports/Macros/Modules object)
  ↓ AI-assisted translation (in progress)
Custom IR YAML       ← the schema I'm designing right now
  ↓ deterministic code generator
Blazor C# project
  ↓ dotnet build
running app
```

I have been working on the custom IR layer (and its code generator) for about two months. The IR is intended to be the schema that AI writes to and humans review — a typed, validated intermediate representation that sits between extracted Access YAML and generated Blazor.

A recent conversation suggested that **Lowdefy's schema** (https://lowdefy.com, https://docs.lowdefy.com) — independently of Lowdefy as a runtime — is worth studying as a reference for how to shape this IR. Lowdefy decomposes UI declaratively into five orthogonal primitives:

1. **Blocks** — visual UI components (forms, tables, charts, containers, modals, etc.). Composable trees.
2. **Requests** — named, parameterized data operations against a connection (read or write). Decoupled from blocks; one block may render many requests, one request may feed many blocks.
3. **Operators** — pure-ish functions used in expressions (`_if`, `_get`, `_state`, `_js`, `_eq`, etc.). Used wherever a value is needed.
4. **Actions** — typed event handlers (`Validate`, `Reset`, `Request`, `Link`, `SetState`, `CallMethod`, etc.). Triggered by Events.
5. **Events** — named triggers on blocks/pages (`onClick`, `onInit`, `onSubmit`, etc.) that run an ordered list of Actions, with `try`/`catch` and debounce semantics.

Plus two build-time concerns worth noting:

- **`_ref` / `_var`** — file-level inclusion with parameter substitution, used to share configuration across pages and apps.
- **Schema validation** — every property of every block/action/operator is JSON-schema-validated; the runtime refuses to start on an invalid config.

The question on the table: **how much of this decomposition should the IR borrow, and where should it deliberately diverge?**

## Your Task

1. Read enough of the current codebase to understand what the IR looks like today (schema definitions, code generator, any sample IR files), the inputs (Access-shaped YAML — see the `HomeHealthCare.accdb` reference database and its extraction), and the outputs (generated Blazor).
2. Compare the current IR's shape against Lowdefy's five-primitive decomposition.
3. Identify where adopting Lowdefy's structure would *improve* the IR — for AI authoring, for human review, for code generation, or for maintenance over time.
4. Identify where it would be *wrong* to adopt — places where Access semantics or the Blazor target make a different decomposition more honest.
5. Surface concrete, prioritized recommendations.

## What to Examine in the Codebase

Please orient yourself before diving in. Look for:

- The IR schema definition itself (likely JSON Schema, C# records/POCOs with attributes, or YAML schema files). Note its top-level types and how they relate.
- The code generator (templates, source generators, or hand-rolled emitters). Note how IR types map to Blazor artifacts.
- Sample IR files, fixture files, or golden-master outputs that show real intended usage.
- Any documentation, README, ADRs, or design notes explaining the current schema's rationale.
- Test cases — what behaviors are pinned down, and what's intentionally loose?
- Any `TODO`, `FIXME`, or `// HACK` comments — these often mark places where the current abstraction is straining.

If a `HomeHealthCare`-derived sample IR exists, walk through the IR representation of `frmBudgetServicePlan` and `rptClientProfile` specifically. Those two objects exercise most of the hard patterns:

- `frmBudgetServicePlan` — cascading list selection (needs → plans), modal popup detail forms, parent–child state coordination, computed totals, conditional formatting.
- `rptClientProfile` — parent report with three subreports linked on `ClientID`, parameter-driven filtering via a separate parameter form.

## Dimensions to Evaluate

Score the current IR and the Lowdefy-aligned alternative on each of these:

1. **Compactness for AI authoring** — how many lines of IR does it take to express a typical detail form? A cascading list page? A parameter-driven report? Lowdefy's pitch is "50 lines vs 500." How does the current IR compare?
2. **Reviewability** — can a human scan a page of IR and predict what the UI does without running it? Lowdefy schemas are linear and dense; some custom DSLs trade that for "shaped like the domain" verbosity.
3. **Composability** — can the same sub-tree of IR be reused across pages without duplication? Is there a `_ref`-equivalent? If not, what's the duplication cost in real samples?
4. **Schema validation surface** — is every property statically validated? Are unknown properties rejected? Can the generator give a useful error pointing at the offending file and line?
5. **Separation of concerns** — are data fetching (Requests), display (Blocks), logic (Operators), and behavior (Actions/Events) cleanly separated, or do they bleed into each other? In Access, they're all tangled inside a Form object — easy to mirror, harder to maintain.
6. **Escape hatches** — when the IR can't express something, what's the path out? "Drop to hand-written Blazor for this page" must be cheap; if it's not, the IR will accrete junk types over time. Lowdefy's escape hatch is `_js` operators and custom Block/Action/Request plugins. What's ours?
7. **Code-gen ergonomics** — is the IR shaped so that the generator stays simple, or does the generator carry a lot of glue logic to bridge IR concepts to Blazor concepts? A good IR pushes complexity into the schema, not the emitter.
8. **Mapping fidelity from Access** — for the patterns in the reference DB (master/detail listboxes, modal dialog detail forms with `OpenArgs`, continuous forms, subreports, parameter forms, switchboard, data macros, transactional copy operations), how naturally does each shape sit in the IR?

## Initial Considerations (Seed Thoughts)

Use these as starting points; disagree freely with any of them after reading the code.

**Where Lowdefy's decomposition probably wins:**

- The Block/Request split is genuinely valuable. Access conflates them (a Form's `RecordSource` is a property of the form). Separating them lets the same query feed a list, an export, and a chart without duplication, and lets the same form bind to different queries depending on context.
- The Operator catalog is a forcing function for keeping expressions composable. Without it, expressions tend to grow into bespoke string templates that the codegen has to parse. With it, expressions are tree-shaped from the start and trivial to emit.
- The Event-as-ordered-list-of-Actions pattern matches Access's `[Event Procedure]` model directly, but adds `try`/`catch` and debounce without the user having to think about them. Worth stealing.
- `_ref` for cross-file reuse is the thing Access doesn't have, and Access apps suffer for it. A sample IR for a 20-form app will collapse to half the size with proper references.

**Where the Lowdefy shape may be wrong for this IR:**

- Lowdefy is general-purpose. We're not. Our IR can have first-class types for the patterns that actually show up: `MasterDetailListPage`, `ModalDetailForm`, `ContinuousForm`, `ReportWithSubreports`, `Switchboard`, `ReportParameterForm`. Lowdefy would express these as compositions of generic Blocks; we can name them and shrink the AI's surface area dramatically. The cost is less flexibility for cases outside the migration scope — that's the right tradeoff.
- Lowdefy's runtime is JavaScript; its operators are JS-shaped. We're generating C#. Operators in our IR should map cleanly to expression trees or C# methods, which means a smaller and slightly different set: `Nz`, `IIf`, `DLookup`, `DCount`, `DSum`, `Format`, plus the standard arithmetic/comparison/logical operators. `_js` becomes "inline C# expression" — or, more likely, doesn't exist at all, and the escape hatch is "name a method on a partial class."
- Lowdefy's connectors abstract over MongoDB/SQL/REST. We have a known target: EF Core against SQL Server or PostgreSQL. The Connection/Request layer can be much thinner — likely just "named query" and "named command," with EF Core handling everything else.
- Reports are a domain Lowdefy genuinely doesn't model well. If our IR has first-class `Report` and `Subreport` types with `LinkMasterFields` / `LinkChildFields` semantics, we're already past where Lowdefy's vocabulary helps us. Don't try to express subreports as nested Blocks — they aren't UI; they're a print-layout construct.

**Things worth checking against current code:**

- How is state propagated between a master and detail in the current IR? Lowdefy uses page-level `_state`; Access uses `Me.OpenArgs` and form references. Pick one explicitly and document it.
- Where does validation live? Field-level, form-level, table-level (data macros), and cross-form (the `BeforeUpdate` budget check) all exist in the source. The IR needs a story for each; check whether the current schema treats them uniformly or has four different mechanisms.
- How does the IR represent the "copy from prior fiscal year" transactional operation in `basDataOperations`? This is the canonical stress test. If it's not modeled at all yet (i.e., assumed to be hand-written), that's fine, but the IR should at least have a designated extension point — a named ServerAction or Command — so it's clear where such things live.
- Does the IR distinguish "page" from "modal" from "drawer"? Lowdefy does (via `type` on the block and via the `OpenLink`/`Modal` actions). Access does (`acDialog`, `PopUp`, `Modal`). Blazor needs to.

## Deliverable

Please write your evaluation as a markdown document with these sections:

1. **Codebase summary** — what the IR is today, in your own words. One or two paragraphs plus a sketch of the top-level types.
2. **Lowdefy-alignment scorecard** — the eight dimensions above, each with a short verdict (current IR strong / aligned / weak / divergent-but-justified) and one or two sentences of reasoning.
3. **Recommendations to adopt** — Lowdefy patterns worth importing, in priority order, each with a concrete sketch of what the change would look like in the current schema.
4. **Recommendations to reject** — Lowdefy patterns that would be wrong here, with reasoning.
5. **Open questions** — places where the right answer depends on a decision I haven't made yet, with the decision framed explicitly.
6. **Suggested next steps** — a short ordered list of small, reversible changes I could try first, before committing to a larger refactor.

Avoid speculation about parts of the codebase you didn't read. If something is unclear, ask before recommending.

## Reference Material

- Lowdefy site and docs: https://lowdefy.com, https://docs.lowdefy.com
- Lowdefy source: https://github.com/lowdefy/lowdefy (the `packages/docs/concepts/` directory has the cleanest descriptions of events, actions, operators, and the schema model)
- The reference Access database extracted YAML lives in the project; the `Summary.yaml` and `Relationships.yaml` files are the entry points.
