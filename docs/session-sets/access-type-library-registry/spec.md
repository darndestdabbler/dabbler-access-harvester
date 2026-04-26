# Access Type Library Registry Spec

> **Purpose:** Land a hand-curated registry covering the most common DAO, ADO, Office, and core Access object-model members, plus the consuming-side `ITypeLibraryRegistry` contract that the future VBA resolver will call into for type-aware reference resolution.
> **Created:** 2026-04-26
> **Session Set:** `docs/session-sets/access-type-library-registry/`
> **Prerequisite:** docs/session-sets/access-metadata-extraction/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #11a. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug** | `access-type-library-registry` |
| **Plan-phase coverage** | Phase 4.5 (type library registry, pragmatic curated approach) |
| **Prereqs** | #10 |
| **Sessions (rough)** | 2 |
| **Synthesis count** | 1 (type-library registry curation + consuming contract design) |
| **Outputs** | `AccessHarvester.Access/TypeLibraryRegistry.cs` (curated registry + lookup API), `AccessHarvester.Access/ITypeLibraryRegistry.cs` (contract the future resolver-side integration consumes) |

This set is the **upstream half** of the original combined `access-symbol-table-and-types` plan. Its companion downstream set is `access-resolver-symbol-table-integration` (tree row #11b), which is blocked on #9 and will additionally consume this set's contract.

---

## Project Overview

Implements **Phase 4.5** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. This set is independent of the resolver chain (#7 → #8 → #9): the registry is a static lookup of type members, and its consumers are downstream — they do not need to exist when the data is curated. The contract designed here is the seam through which `access-resolver-symbol-table-integration` (row #11b) will plug in once #9 lands.

Per Phase 4.5's "pragmatic approach": hand-curated, not introspected. Full type-library introspection is explicitly deferred per the parser plan.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #11a.

---

## Feature 1: Type Library Registry (Synthesis)

### Scope

Per Phase 4.5: hand-curated registry of the most common members for:

- **DAO** — `Recordset`, `Field`, `Database`, `QueryDef`, `TableDef`, `Fields` (collection), `Recordsets` (collection)
- **ADO** — `Recordset`, `Connection`, `Command`, `Field` (where divergent from DAO)
- **Office** — `FileDialog`, common `Application` members where they appear in Access VBA
- **Access core** — `Form`, `Report`, `Control` (base + common subtypes), `Module`, `DoCmd`, `Application` (Access-specific surface)

For each registered type: known members keyed by name, with return-type identifiers, member kind (property / method / field / event), and a flag distinguishing default members from named ones (default members are required for resolving constructs like `rs!FieldName` per the plan's Phase 3.5 default-member resolution).

For types not in the registry, the contract returns a "not found" sentinel that the caller (the future resolver integration) translates into `{ source: "external-unknown", typeLibrary: "<name>" }` provenance — graceful degradation, never a hard failure.

Output:

- `AccessHarvester.Access/ITypeLibraryRegistry.cs` — the lookup contract.
- `AccessHarvester.Access/TypeLibraryRegistry.cs` — the curated implementation.
- Hermetic unit tests under `tests/AccessHarvester.Access.Tests/TypeLibraryRegistry*.cs` covering every registered type's surface and the unknown-type fallback path.

### Standards

- **Curated, not introspected.** Per the plan: "Defer full type-library introspection to a later iteration if needed." This set ships a seed registry; expansion is post-v1. Adding a new type later is a single-file edit, not an architectural change.
- **Graceful degradation is the contract.** Unknown-type lookups return a sentinel (`bool TryGetMember(...)` returning false, or an explicit `MemberLookupResult.NotFound`). The caller emits the `external-unknown` provenance; the registry itself never throws and never logs.
- **Source-of-truth comment per registered type.** Each type entry carries a comment citing where its member list came from (MSDN page, Access COM type library introspection notes, etc.) so a future contributor adding members has a source to verify against.
- **No `[SupportedOSPlatform("windows")]` gating.** The registry is pure data — no COM calls — so it runs anywhere. Hermetic unit tests run on every platform.
- **No coupling to the resolver project.** This set must not introduce a `ProjectReference` to `AccessHarvester.Vba.Resolver` (which doesn't exist yet anyway). The contract is consumed via interface only; the resolver-side integration in #11b will adopt it.

---

## Session Plan

### Session 1 of 2: Registry Curation + Contract Design (Synthesis)

- Read Phase 4.5 + the parser plan's Appendix A "Recordset patterns" + the existing `AccessHarvester.Access/` project layout (the registry lives alongside the existing metadata types).
- Author the synthesis in-conversation per project-guidance Convention "use Opus as the orchestrator… for synthesis-document work" (this set's recommended orchestrator is Opus; in-conversation handling avoids Anthropic→Anthropic round-trip cost).
- Land:
  - `ITypeLibraryRegistry.cs` (contract).
  - `TypeLibraryRegistry.cs` (curated implementation, all four type-library surfaces).
  - Unit tests covering every registered type's known-members and the unknown-type fallback.
- Wire the project file if any new dependencies are needed (none expected).

### Session 2 of 2: Tightening + Cross-Provider Verification

- Build, run tests, schema/contract gates.
- Route session-verification with the registry source files, the contract, and the unit tests included in the prompt.
- Address any verifier-surfaced findings (max 2 retries per workflow).
- Last-session sub-steps per workflow Step 8: change-log generation, next-session-set recommendation (the natural follow-on is #11b once #9 lands; document this clearly), session-state flip to complete, branch merge into main.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Registry curation + contract design synthesis (Session 1):** Opus orchestrator, handle in-conversation per the in-conversation Convention. If the orchestrator is *not* Opus, route via `route(task_type="architecture", complexity_hint=65)`. Complexity is below the original combined set's `75` because this is curated-data work plus a thin interface, not an integration design that has to thread through a resolver pipeline.
- **Session-verification (Session 2):** `route(task_type="session-verification", complexity_hint=70)`. Include `ITypeLibraryRegistry.cs`, `TypeLibraryRegistry.cs`, the unit tests, and a one-paragraph note that the resolver-side integration is out-of-scope (lives in #11b) so the verifier doesn't hallucinate missing consumer code — see `lessons-learned.md` → "Send Related Files To The Cross-Provider Verifier Or Disclaim What Is Out Of Scope".
- **Change-log (Session 2):** `route(task_type="summarization")` with `max_tier=1` fallback per the documentation-vs-summarization Convention.

## Open questions for the human

None at split time. The "full type-library introspection" question is explicitly deferred per Phase 4.5.
