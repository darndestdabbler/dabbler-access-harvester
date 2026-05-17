## Q2 — Macros / property-sheet boundary

**Recommendation:** mid — include **(a)+(b)+(c)** in Phase 4 gating; **exclude (d)** from the canonical-parse-success-report gate.

**Rationale:** Macros are a first-class executable logic surface in Access, and the repo already ships dedicated macro extraction/parsing/enrichment infrastructure, so leaving them out would make “parser coverage finality” feel materially incomplete. Property-sheet expressions are different: they are heterogeneous, partially cosmetic, and not currently a single coherent parser lane; pulling them into GAP #1 would likely turn a closeout audit into fresh parser-scope discovery work. Phase 4 should therefore require per-fixture enumeration and parse/UnresolvedItem accounting for VBA, Jet SQL, and macros, while property-sheet values remain harvested/decoded but not coverage-gating. That keeps the gate honest on real logic surfaces without letting a long-tail metadata grammar explode the phase.

**Caveats / conditional branches:** Do a quick inventory first of expression-bearing property-sheet fields in Northwind/HHC and record them, but treat that as non-gating unless you discover they contain major business logic. If that inventory reveals a small, clearly bounded set of high-value expressions already trivially routable through existing parsers, you can opportunistically absorb just those into a follow-on mini-gap — but do not block Session 2 on full (d) coverage.

## Q3 — Packaging shape

**Recommendation:** self-contained binary

**Rationale:** For an AI-tooling-agent consumer, the best contract is “download pinned artifact, unzip, execute” with no dependency on local .NET runtime state or NuGet feed configuration. That fits the current reality that the extractor is already Windows + Access constrained, so a Windows self-contained release is not introducing meaningful extra platform fragmentation. A dotnet tool is more convenient for .NET-native humans, but it adds avoidable install/runtime/feed friction for automation. Embedded NuGet is premature because the host repo does not exist yet, and process-boundary CLI invocation is the simpler integration shape today.

**Caveats / conditional branches:** If you already know the agent hosts will always have .NET 10 and authenticated feed access, you can add a dotnet tool later as a convenience secondary channel — but it should not be the Phase 4 gate artifact. If a real in-process host emerges later and needs parser/enricher reuse, publish library packages then; do not optimize GAP #7 around a hypothetical embedding scenario now.