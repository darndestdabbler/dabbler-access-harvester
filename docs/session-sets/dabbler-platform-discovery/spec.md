# Dabbler Platform Discovery Spec

> **Purpose:** Produce a structured, machine-readable inventory of the
> sibling `../dabbler-platform` repo (and its dependency `../dabbler-pdf`)
> so that AI agents working in this repo — and, eventually, in the
> planned third (Access-to-Blazor migration) repo — have an authoritative
> reference for what migration *targets* the platform offers.
> **Created:** 2026-04-27
> **Session Set:** `docs/session-sets/dabbler-platform-discovery/`
> **Prerequisite:** None — read-only on sibling repos, no dependency on
> the harvester DAG. Safe to run in parallel with any in-progress
> harvester session set.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec was authored ad-hoc on 2026-04-27 at human request.** It
> is intentionally NOT part of `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`
> — that DAG covers the harvester's parser/resolver/extractor work only.
> This is a cross-repo support workstream.

---

## Project Overview

The harvester's downstream consumers (interpretation agents, the future
Access-to-Blazor migration repo) need to know what `dabbler-platform`
provides as migration *targets*: which Blazor components correspond to
which Access constructs, what the `report.rdf.json` schema looks like,
which platform primitives are stable contracts vs. in-flight scaffolding.
Today that knowledge lives only in the user's head and in
`dabbler-platform`'s own source.

This set produces a **read-only structured snapshot** of dabbler-platform
(plus its `dabbler-pdf` dependency) and lands it in this repo at
`docs/cross-repo/`. The snapshot is explicitly dated and tied to a
commit SHA so future agents know how stale it is and when to regenerate.

**Boundaries:**

- **Read-only on `../dabbler-platform` and `../dabbler-pdf`.** This set
  must not edit files in those repos. If an obvious fix or note would
  benefit the platform repo, surface it to the human at session end —
  the human applies it there manually.
- **No copying of platform source code into this repo.** The output is
  a *map* of the platform, not a mirror of it. Cite paths and commit
  SHAs; do not fork content.
- **Documentation, not implementation.** No C# / Blazor code lands here
  from this set.

---

## Features

### Feature 1: Component Inventory

Walk `../dabbler-platform/`'s component library and produce a structured
inventory: each public component's name, file path, purpose (one
sentence), required parameters, optional parameters, common usage
contexts. Group by functional category (form controls, layout,
data-display, navigation, modal/dialog, etc.). Note which components are
stable contracts vs. in-flight (look for `// TODO`, recent commits,
incomplete docstrings).

**Output:** `docs/cross-repo/dabbler-platform-components.md` —
single-screen-per-category index with deeper per-component sections.

### Feature 2: `report.rdf.json` Schema Documentation

The platform's `report.rdf.json` schema is purposely higher-level than a
syntax tree (per the human's clarification on 2026-04-27) and is the
most likely seam between harvested Access reports and platform-rendered
Blazor reports. Document the schema's shape, every field's purpose, and
example documents — at sufficient fidelity that an interpretation agent
in this repo could *generate* a valid `report.rdf.json` from a harvested
Access report's `ir/` representation.

**Output:** `docs/cross-repo/dabbler-platform-report-rdf-schema.md`.

### Feature 3: Migration-Relevance Mapping

Cross-reference: for each major Access construct kind (Form / Subform /
Report / Subreport / Query / Macro / VBA event handler), name the
dabbler-platform component(s) or pattern(s) that are the natural
migration target. Flag gaps: Access constructs with no clear platform
counterpart go on a "platform-extension candidates" list for the user.

**Output:** `docs/cross-repo/access-to-platform-migration-map.md`.

### Feature 4: `dabbler-pdf` Boundary

Brief documentation of what `dabbler-pdf` does for `dabbler-platform`,
its public surface (the API the platform calls), and any setup the
consuming repo needs (NuGet refs, license keys, etc.). Out of scope:
deep internals — we only document the boundary.

**Output:** `docs/cross-repo/dabbler-pdf-overview.md`.

### Feature 5: Currency Header (Every Output File)

Every output file under `docs/cross-repo/` carries a header block:

```markdown
> **Snapshot of:** `../dabbler-platform` @ commit `<sha>` on `<date>`
> **Regenerate when:** the platform's component library changes
> meaningfully, or when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.
```

This makes staleness visible without forcing every agent to remember
the convention.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
```

## Sessions
### Session 1 of 3: Inventory + Schema

- Walk `../dabbler-platform/` (and `../dabbler-pdf/`) read-only.
- Route an `analysis` call producing the component inventory table
  (Feature 1).
- Route a separate `analysis` call producing the `report.rdf.json`
  schema documentation (Feature 2).
- Land both output docs with currency headers.

### Session 2 of 3: Migration Map + PDF Boundary

- Read Sessions 1's outputs.
- Route an `analysis` call cross-referencing Access construct kinds
  against the platform component inventory (Feature 3). Flag platform
  gaps.
- Document the `dabbler-pdf` boundary (Feature 4).
- Land remaining output docs with currency headers.

### Session 3 of 3: Smoke-Test + Buffer

- Take a single concrete sample (e.g., one form from
  `samples/NorthwindDevEdition.accdb`) and walk through the
  migration-relevance map by hand. Note any gaps the map missed.
- Address verifier-surfaced findings.
- Generate change-log.

> Step lists above are bullet hints. The orchestrator running each
> session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Inventory + schema (Session 1):** `route(task_type="analysis",
  complexity_hint=50)` — two separate calls, one per output doc, so
  prompts stay under the size that triggers Gemini 400s.
- **Migration map + PDF boundary (Session 2):** `route(task_type="analysis",
  complexity_hint=55)` for the cross-reference work; `route(task_type="documentation",
  complexity_hint=30)` for the PDF boundary doc (it's code-doc style,
  not narrative prose).
- **Session-verification:** standard `route(task_type="session-verification",
  complexity_hint=70)`. Verification prompt should disclose which
  platform paths were inspected so the verifier does not hallucinate
  missing components.
- **Change-log (Session 3):** `route(task_type="summarization",
  max_tier=1)` per the standard pattern.

**Recommended orchestrator:** Sonnet 4.6 at effort=medium across all
three sessions. This set is mechanical inventory + structured
documentation, not synthesis — Opus is over-spend.

---

## Open questions for the human

1. **Snapshot scope.** Should `dabbler-pdf` get a full Feature-1-style
   component inventory of its own, or is the boundary doc (Feature 4)
   sufficient? Default: boundary doc only — `dabbler-pdf` is a
   dependency the platform abstracts away, not something the migration
   repo will call directly.

2. **Future regeneration.** When the platform changes, who triggers
   re-running this set? Default: the human, manually, when starting
   migration-planning work in the third repo. No automated trigger —
   adding one would push us toward cross-repo CI complexity for low
   payoff at this stage.

3. **Path layout under `docs/cross-repo/`.** Acceptable as proposed?
   The alternative is a flat single file (`dabbler-platform-overview.md`)
   that combines Features 1-4. The split is recommended because the
   per-feature docs are individually useful and Feature 1 will be the
   largest file by far.
