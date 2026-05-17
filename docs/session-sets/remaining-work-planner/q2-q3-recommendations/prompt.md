You are advising on two scope/decision questions for the
`remaining-work-planner` Session 1 gap audit in dabbler-access-harvester.
The human deferred these two questions to cross-provider recommendation
(you + the other major provider) before Session 2 starts.

Please answer BOTH questions below. Be opinionated — "it depends" without
a primary recommendation is not useful here. State your recommendation
first, then the reasoning, then any caveats or "do X first if Y" branches.

**Question 2 (boundary-of-scope):** For Phase 4's "documented constructs
in canonical fixtures" criterion (criterion #1 — Parser coverage finality),
should the canonical-parse-success-report (GAP #1) cover ALL of:

  (a) VBA constructs in .bas/.cls/.frm modules (definitely in)
  (b) Jet SQL in saved queries (definitely in)
  (c) Macros as defined in Access (the MacroActionsEnricher surface)?
  (d) Property-sheet entries specifically — validation-rule expressions,
       default-value expressions, format strings, input masks — which are
       syntactically Access-expression-language (a subset of VBA/Jet SQL),
       not pure VBA?

In other words: what is the scope boundary for "documented constructs"
in Phase 4? The harvester ships infrastructure for (c) and (d) already
(MacroActionsEnricher, PropertyDecoder), but those have NOT been
exhaustively coverage-tested against the canonical fixtures. The choice:

- Narrow boundary — Phase 4 covers only (a)+(b). Macros + property-
  sheet expressions get separate dedicated session sets later (outside
  Phase 4's gating).
- Wide boundary — Phase 4 covers (a)+(b)+(c)+(d). The
  canonical-parse-success-report's per-fixture audit must enumerate
  macro coverage and property-sheet-expression coverage too, and any
  gap there must either parse correctly or emit an UnresolvedItem.
- Mid boundary — Phase 4 covers (a)+(b)+(c) but NOT (d), or similar
  fan-out.

The trade-offs are: narrow keeps Phase 4 small but the harvester ships
with known-unverified surface; wide pushes scope and cost but produces
a more complete finish-line artifact.

**Question 3 (packaging shape):** The harvester is a Windows-only
.NET 10 CLI today (the COM-based extractor requires Windows + Access
installed; the parser/enricher portions are hermetic). The downstream
consumer is an AI-tooling agent (per project-guidance.md). Which
distribution shape should GAP #7 (cli-packaging) target:

- dotnet tool (global or local install via "dotnet tool install") —
  lowest friction for consumers who already have .NET installed;
  requires a NuGet feed (private or public).
- Self-contained binary release ("dotnet publish --self-contained") —
  no .NET runtime prerequisite on the consumer side; ships as a zip
  with platform-specific binaries; larger artifact.
- Embedded-NuGet (consumer adds the harvester as a NuGet package
  reference) — for downstream code that hosts the harvester pipeline
  directly rather than invoking it as a separate process.
- Some combination — e.g., dotnet tool + embedded NuGet.

What's your primary recommendation for the AI-tooling-agent consumer
profile, and what's the rationale?

# Required output format

Structure your answer as:

## Q2 — Macros / property-sheet boundary

**Recommendation:** [narrow | wide | mid (specify which combos) | other]
**Rationale:** [2-4 sentences]
**Caveats / conditional branches:** [if any]

## Q3 — Packaging shape

**Recommendation:** [dotnet tool | self-contained binary | embedded NuGet | combination]
**Rationale:** [2-4 sentences]
**Caveats / conditional branches:** [if any]

# Context bundle

## Phase 4 Done criteria (from docs/planning/strategy.md)

The mechanical harvester (parser, extractor, enrichers, output schemas,
CLI) can produce stable, documented output for the canonical fixtures
with parser/extraction tests green and explicit UnresolvedItem records
for constructs it cannot process.

## Canonical fixtures (after Q1 resolved 2026-05-17)

- samples/NorthwindDevEdition.accdb (primary; rich VBA + macros +
  ribbon XML + reports + queries)
- samples/HomeHealthCare.accdb (secondary; smaller real-world Access app)
- MiniComplex is now OUT (human decision: not thoroughly tested enough
  to rely on)

## Current parser pipeline state (relevant to Q2)

- VBA parser (ANTLR Vba.g4, ~25 source files)
- VBA resolver (43 source files: declarations, scope, default member,
  type inference, x-ref)
- Jet SQL parser (ANTLR JetSql.g4, ~30 source files)
- Jet SQL resolution (dynamic SQL detection, form-reference linkage)
- Macros: MacroActionsEnricher.cs and Macros/MacroXmlParser.cs exist
  in AccessHarvester.Core/Enrichment/. Not exhaustively coverage-tested
  against Northwind / HHC macro shapes.
- Property-sheet entries: PropertyDecoder.cs exists in
  AccessHarvester.Core/Enrichment/. Access form/report property-bag
  JSON is captured at kb tier; property-sheet *values* are decoded
  mechanically; but Access validation-rule expressions and
  computed-default-value expressions are Access-expression syntax
  (a subset of VBA/Jet SQL) — those are NOT necessarily routed
  through the VBA parser today.
- UnresolvedItem.cs + UnresolvedItemAggregator.cs exist in
  AccessHarvester.Pipeline/Enrichment/.

## Open Validation #7 in strategy.md (partial resolution)

"Does the Hybrid procedure-level decision hold for non-event-handler
procedures (queries, macros, class-module methods)?"
Status: PARTIALLY RESOLVED — the four pilot workflows did NOT exercise
macros, queries-as-first-class workflows, or report-bound logic.

## Phase 4 closure plan summary (from gap-audit.md)

6 named gaps. The relevant ones for Q2 and Q3:

- GAP #1 harvester-finish/canonical-parse-success-report (small,
  1-2 sessions): the gate artifact closing Phase 4 criterion #1.
  Per-fixture report listing every construct parsed correctly +
  every UnresolvedItem emitted.
- GAP #3 harvester-finish/canonical-golden-output (medium, 3-4
  sessions): the central deliverable. Commit raw/, kb/, ir/
  output for the canonical fixtures + integration test that
  re-runs the harvester and diffs against golden.
- GAP #7 harvester-finish/cli-packaging (small, 1-2 sessions):
  final gate. Installable distribution shape; reproducible
  pack/release step; install steps documented.

## Downstream consumer profile (relevant to Q3)

The harvester's output is consumed by AI-tooling agents. The strategy
doc explicitly states: "The downstream consumer is an AI agent, not
a human reviewer." The COM-based extractor only runs on Windows with
Microsoft Access installed; the parser and enricher portions
(hermetic) run anywhere. The pilot host referenced in the migration
roadmap is a future repo, not yet created.
