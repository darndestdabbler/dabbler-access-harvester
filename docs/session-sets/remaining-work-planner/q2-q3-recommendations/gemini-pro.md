## Q2 — Macros / property-sheet boundary

**Recommendation:** Wide

**Rationale:** Phase 4's goal is to establish a stable, well-understood baseline for the canonical fixtures. Adopting the wide boundary—(a) VBA, (b) Jet SQL, (c) Macros, and (d) property-sheet expressions—is the only way to create a truly complete baseline. The `UnresolvedItem` mechanism is designed specifically for this purpose: to ensure that any construct the harvester encounters is either successfully parsed or explicitly marked as a known gap, preventing the tool from shipping with silently unverified surface area. This approach forces a comprehensive audit and produces a more honest and reliable "finish-line" artifact.

**Caveats / conditional branches:** Before committing to implementation, conduct a 1-day spike to enumerate all unique macro actions and property-sheet expression types present in the Northwind and HomeHealthCare fixtures. If this audit reveals an unexpectedly large volume of complex, unhandled constructs, you can timebox the Phase 4 effort: implement support for the most common 80% and file `UnresolvedItem` records for the long tail, deferring full implementation but not a full audit.

## Q3 — Packaging shape

**Recommendation:** Self-contained binary release

**Rationale:** The primary consumer is an AI-tooling agent, which implies a need for robust, loosely coupled, and dependency-free integration. A self-contained binary (`dotnet publish --self-contained`) is the ideal shape, as it requires nothing of the host environment other than the correct OS. This eliminates friction and potential versioning conflicts by not assuming the consumer has the .NET runtime installed, making the harvester a simple, self-sufficient tool the agent can deploy and execute.

**Caveats / conditional branches:** If the AI-tooling agent's execution environment is *guaranteed* to be a standardized .NET environment, then a `dotnet tool` would be marginally better due to its superior installation and update management. Lacking that guarantee, the self-contained binary is the safer and more versatile primary recommendation. You can easily provide both with minimal extra CI/CD work if both consumption models are required.