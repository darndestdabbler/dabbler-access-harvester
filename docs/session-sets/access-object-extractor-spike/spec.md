# Access Object Extractor Spike Spec

> **Purpose:** Determine how `samples/AccessObjectExtractor.accdb` (an
> Access-native VBA extraction tool) relates to the existing C# extraction
> pipeline in `AccessHarvester.Core/`, and produce a written integration
> strategy so a follow-on session set can act on it with confidence.
> **Created:** 2026-05-14
> **Session Set:** `docs/session-sets/access-object-extractor-spike/`
> **Prerequisite:** None — read-only investigation. Safe to run against
> `main` without a worktree.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This is a spike (read-only research, no code changes).** The
> deliverable is a written gap analysis and integration-strategy
> recommendation, not shipped code. Verification assesses analytical
> completeness, not code correctness.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

---

## Background

The user created `samples/AccessObjectExtractor.accdb` — a standalone
Access database containing VBA modules that extract metadata from a target
`.accdb` to a YAML tree. The extraction is driven by
`scripts/run_extraction.ps1` (PowerShell → COM → Access headless). The
extracted YAML for `HomeHealthCare.accdb` already lives in
`samples/DatabaseYaml/HomeHealthCare.accdb/` and is committed as the
canonical YAML mirror.

The existing C# pipeline in `AccessHarvester.Core/` uses four exporters
(`QueryExporter`, `TableExporter`, `RelationshipExporter`,
`SaveAsTextExporter`) that emit raw JSON, then pass through Translation and
Enrichment phases to produce `kb/` and `ir/` output.

It is currently unclear whether the YAML produced by
`AccessObjectExtractor.accdb` and the JSON produced by the C# extractors:

- Cover the same objects/fields (same data, different format)
- Are complementary (each captures something the other misses)
- Are in conflict (both should be canonical for the same thing)

Until that question is answered, the integration path is unknown.

---

## Spike Questions

1. **Coverage comparison** — For each object type (Tables, Queries, Forms,
   Reports, Macros, Modules, Relationships), what fields does the
   AccessObjectExtractor YAML emit? What fields does the corresponding C#
   exporter JSON emit? Where do they overlap? Where do they diverge?

2. **Gap analysis** — What does the Access-native VBA tool capture that the
   C# side currently misses? What does the C# side capture that the VBA
   tool does not?

3. **Integration strategy** — Which of these best describes the relationship?
   - **Replace:** The YAML becomes the canonical `raw/` source and the C#
     exporters are retired or reduced.
   - **Complement:** YAML and C# JSON capture disjoint concerns; both are
     needed in the pipeline.
   - **Inform:** The YAML was an exploratory prototype; its useful fields
     should be back-ported into the C# extractors, and then the VBA tool
     is retired.
   - **Diverge:** The YAML is for a different downstream consumer (e.g., AI
     interpretation) and the C# JSON is for the pipeline; they coexist
     without merging.

4. **`scripts/run_extraction.ps1` role** — Should this script become a
   first-class repo tool (documented, tested, part of the canonical
   workflow) or remain a local developer utility?

5. **Follow-on work** — What is the minimal next session set that acts on
   the chosen strategy?

---

## Session Plan

### Session 1 of 1: Gap Analysis + Strategy (Research)

- Read `scripts/run_extraction.ps1` and understand how it drives
  `AccessObjectExtractor.accdb`.
- Inspect the committed YAML under `samples/DatabaseYaml/HomeHealthCare.accdb/`
  — sample Forms, Tables, Queries, Reports, Macros, Modules YAML files.
- Read the C# exporter source files:
  `AccessHarvester.Core/Extraction/QueryExporter.cs`,
  `AccessHarvester.Core/Extraction/TableExporter.cs`,
  `AccessHarvester.Core/Extraction/RelationshipExporter.cs`,
  `AccessHarvester.Core/Extraction/SaveAsTextExporter.cs`.
- Route an `analysis` call that produces the coverage comparison table and
  gap analysis.
- Route a second `analysis` call for the integration-strategy recommendation,
  using the gap analysis as input.
- Land both outputs as `session-reviews/session-001.md`.
- Route a `session-verification` call against the written analysis.
- Generate `change-log.md` and `next-session-set-recommendation.md`.

> Step bullets above are hints. The orchestrator running this session must
> elaborate them per `docs/ai-led-session-workflow.md`. No code is written;
> `dotnet build` and `dotnet test` are not required.

---

## Deliverables

1. **`session-reviews/session-001.md`** — Contains:
   - Coverage comparison table (per object type, YAML fields vs. C# JSON fields)
   - Gap analysis prose (YAML-only fields, C# JSON-only fields, shared fields)
   - Integration strategy recommendation (Replace / Complement / Inform / Diverge)
   with rationale
   - `scripts/run_extraction.ps1` role recommendation
   - Follow-on session-set scope summary

2. **`change-log.md`** — Standard end-of-set change log (documents the
   research output, not code changes).

3. **`next-session-set-recommendation.md`** — Recommends the slug, session
   count, and orchestrator for the follow-on set.

---

## Routing notes

- **Gap analysis + strategy (Session 1):** Two `route(task_type="analysis")`
  calls — one for the comparison table (feeds raw YAML/JSON field lists;
  `complexity_hint=55`), one for strategy synthesis (`complexity_hint=60`).
- **Session-verification:** Standard `route(task_type="session-verification",
  complexity_hint=50)`. Verification assesses analytical completeness and
  internal consistency of the written analysis, not code correctness.
- **Change-log:** `route(task_type="summarization", max_tier=1)`.
- **Next-set recommendation:** `route(task_type="analysis", complexity_hint=30)`.

**Recommended orchestrator:** `anthropic sonnet` at `effort=high`. This is
a research session — complex analysis, not code synthesis — and Sonnet at
high effort matches that profile without Opus-level cost.
