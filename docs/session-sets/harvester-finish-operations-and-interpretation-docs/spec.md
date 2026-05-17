# Operations and Interpretation Docs Spec — **STUB**

> **Status:** STUB authored 2026-05-17 by `remaining-work-planner` Session 2.
> Acceptance criteria are pinned; document organization, section
> shape, and length are left for the set's own Session 1.
>
> **Purpose:** Author the downstream-AI-consumer-facing
> interpretation guide: "given a harvester `ir/` JSON, here is what
> each field means, what its provenance is, how `UnresolvedItem`
> records should be interpreted, and what known limitations exist."
> Closes Phase 4 Criterion #6 (the parts not already covered by the
> existing developer-facing `README.md` and `docs/schema/`).
> **Created:** 2026-05-17
> **Session Set:** `docs/session-sets/harvester-finish-operations-and-interpretation-docs/`
> **Prerequisite:** `harvester-finish-canonical-parse-success-report`,
> `harvester-finish-canonical-golden-output` (the docs reference
> ground-truth output and per-reason narratives those sets produce).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: documentation-only; no UI surface, no behavioral
> change. Cross-provider AI verification is the quality gate.

---

## Origin and authority

- **Authority for scope:** the gap audit at
  [`docs/session-sets/remaining-work-planner/gap-audit.md`](../remaining-work-planner/gap-audit.md)
  → GAP #6.
- **Authority for "Done":**
  [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4
  Criterion #6.
- **Authority for the audience:**
  [`docs/planning/project-guidance.md`](../../planning/project-guidance.md)
  Principle "the downstream consumer is an AI agent, not a human
  reviewer." Outputs are JSON; layouts and styling do not matter;
  precision, completeness, and machine-readability do — but the
  prose docs need to give the AI agent enough context to
  *interpret* the JSON, not just consume it.

---

## Project Overview

The harvester has a substantive `README.md` (developer onboarding,
build/test, CLI surface) and the schema docs at
`docs/schema/v0-vs-tiers.md` and `docs/schema/v1-adr.md`. What is
missing is a narrative layer on top: "given a harvester run output,
here is how to read it." The migration plan's Appendix D and Phase
7.4 call out an Operations Manual; this set delivers the
downstream-AI-consumer-facing portion of that.

### In scope

1. An **interpretation guide** (working name; Session 1 may
   rename) that explains:
   - The three-tier output model (`raw/` / `kb/` / `ir/`) and
     when a consumer reads from which tier.
   - The `DatabaseManifest` envelope (post
     `harvester-finish-canonical-golden-output`): what every
     top-level array contains.
   - Per-category interpretation: what a Form's IR JSON looks like
     (geometry conventions, label resolution, event-binding shape,
     embedded-macro correlation, VBA signature attachment); what a
     Report adds (group levels); what a Query carries (RowSource
     spec, dependency facts); what a Module carries (procedure
     classifications, navigation graph edges).
   - The `UnresolvedItems` array: shape, per-reason narrative
     (sourced from the `harvester-finish-canonical-parse-success-report`
     set's per-reason notes), recommended downstream-AI handling.
   - Provenance conventions: `{ source, ... }` shape, what
     `unresolved` means, why some fields are nullable rather than
     omitted.
   - Known limitations: linked from `Frozen Unless Reauthorized`
     in strategy.md; what the harvester does NOT do today and what
     a downstream AI should not assume.
2. **Reading-order recommendations.** Which file to open first;
   which tier to read; how to walk the dependency DAG.
3. **A short examples gallery** drawing on `examples/*.json` (no
   new examples needed — the existing minimal-form / complex-form
   / northwind-output trio is sufficient).
4. **A pointer page** that lists every existing doc and what it
   contains, so a downstream-AI consumer landing on the repo
   doesn't have to search blindly.

### Out of scope

- Re-writing the README or the schema docs. The interpretation
  guide complements them; it does not replace them.
- Auto-generating prose from the schema. `SchemaDocGenerator` is
  the right tool for schema-doc work; this set is the narrative
  layer schemas can't produce.
- Per-fixture deep-dive docs (e.g., a 50-page "here is everything
  about Northwind's output"). The golden corpus serves that role;
  the docs reference fixture facts as examples, not the docs'
  primary content.
- An end-user manual (the audience is an AI agent, not a human
  operator).
- **Strategy.md "Frozen Unless Reauthorized" items remain frozen.**
  Specifically: Blazor application slices / workflows / pilots,
  production IR codegen, `dabbler-platform-discovery` refresh,
  additional Northwind workflows beyond the existing four pilot
  packages, CRUD-scaffolder expansion, and any item the 2026-05-17
  strategic review froze. The interpretation guide describes
  harvester output only; it does NOT document Blazor consumers,
  IR-codegen consumers, or platform integration paths. References
  to those areas (if any are useful for context) are explicitly
  marked "deferred to the future migration repo" per the strategy
  reset.

---

## Doc-home decision (Session 1 picks)

Per gap-audit GAP #6's "Doc directory tidying" minor, Session 1
decides whether the new docs land:

- **Flat in `docs/`** (default if uncertain) — current convention.
- **Under `docs/harvester/`** — separates "how to use the harvester"
  from "how the harvester project is run."

Either path is acceptable; the decision is Session 1's call after
inspecting current `docs/` state. Document the rationale in the
ai-assignment.md or the first session-review.

---

## Sessions

### Session 1 of 3: Outline + Audience-Check Probe

**Steps:**

1. Read GAP #6 + the existing `README.md`, `docs/schema/v0-vs-tiers.md`,
   `docs/schema/v1-adr.md`, `docs/VBA-Parser-Migration-Harvester-Plan.md`
   (Appendix D), and the prereq sets' change-logs.
2. Elaborate this stub.
3. Author `ai-assignment.md` via routed analysis.
4. Author the outline (section list + reading order).
5. **Audience-check probe.** Give the outline to two cross-provider
   models with the prompt: *"You are a downstream AI consumer. Does
   this outline cover what you would need to interpret a harvester
   run output?"* This is the Probe Substrate Value Earlier
   convention applied to docs. If either reviewer says "no, I'd be
   stuck on X," fold the gap into Session 2's writing scope.
6. End-of-session verification + commit + push + notify.

**Creates:**
- The outline document (at the chosen doc-home path).
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/ai-assignment.md`
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/audience-check-probe.md`
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/session-reviews/session-001.md`

---

### Session 2 of 3: Body Authoring

**Steps:**

1. Author the body sections per the outline. Each section follows
   the project-guidance lean convention: enough prose for the
   downstream AI to interpret the JSON, no more.
2. Inline JSON snippets from `examples/*.json` where they
   illustrate; do not duplicate full files.
3. Cross-link to existing schema docs, README, and per-reason
   narratives from the parse-success-report set.
4. `dotnet build && dotnet test` — N/A unless code is touched.
5. End-of-session verification + commit + push + notify.

**Creates:**
- The interpretation-guide body sections.
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/session-reviews/session-002.md`

---

### Session 3 of 3: Pointer Page + Closeout

**Steps:**

1. Author the docs-index pointer page that lists every relevant
   doc and what it contains. Lands at the doc-home path or at
   `docs/README.md` (Session 1 decides; the latter is consumer-
   discoverable from the repo root).
2. Update the top-level `README.md` with a link to the new
   interpretation guide so a fresh consumer finds it.
3. **Downstream-AI probe.** Hand the full guide to a fresh
   cross-provider model and ask: *"Can you interpret this
   `northwind-output.json` based only on the guide?"* Record the
   result; if material gaps, address before closeout.
4. End-of-session verification.
5. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
6. Step 9 reorganization review AFTER notify.

**Creates:**
- The pointer page.
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/downstream-ai-probe.md`
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/change-log.md`
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/disposition.json`
- `docs/session-sets/harvester-finish-operations-and-interpretation-docs/session-reviews/session-003.md`

**Touches:**
- `README.md` (one link addition).

**Ends with:** Phase 4 Criterion #6 closed; downstream AI consumer
has a interpretation entry-point document; documentation index
exists.

---

## Cost Expectations

- **Routed cost target:** ~$0.10–$0.40 per session
  (cross-provider verification + the two probe calls).
  ~$0.30–$1.20 across the set.
- **In-conversation token spend:** Moderate-to-high. Documentation
  authoring is synthesis-heavy; the probe calls are the explicit
  cross-provider rigor for docs (analogous to the parser-set verifier
  rigor for code).

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Probe reveals fundamental schema-comprehension gaps** | Audience-check or downstream-AI probe says "I'd need the schema redesigned to interpret this" | Stop. Surface to the human. The fix lives in `schema-finalization-and-docs` as a tree-amendment, not in the docs set. Don't paper over a schema problem with prose. |
| **Docs grow past ~150 pages of prose** | Section count blows past plan; signals the audience is being treated as a human reader, not an AI consumer | Stop. Surface to the human. Tighten scope per the lean-by-default Principle. |
| **Out-of-date examples** | `examples/*.json` no longer matches what the harvester produces after `harvester-finish-canonical-golden-output` lands | Stop. Tree-amendment to `schema-finalization-and-docs` to regenerate examples; resume docs after examples are current. |

---

## Why this is a stub

The stub fixes the contract: interpretation guide for a downstream
AI consumer, audience-check + downstream-AI probes, pointer page,
README link. Section shapes, doc-home placement, and length budget
are Session 1's call.
