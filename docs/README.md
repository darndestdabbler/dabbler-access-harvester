# Dabbler Access Harvester — Documentation Index

> **Audience:** A downstream AI consumer or a fresh orchestrator landing on
> the repo. This page lists every document under `docs/` (and its
> subdirectories) with a one-line "what it is and who reads it" so the
> reader can pick the right starting point without crawling the tree.
>
> **First stop for a downstream AI consumer reading harvester output:**
> [`docs/harvester/interpretation-guide.md`](harvester/interpretation-guide.md).
>
> **First stop for an orchestrator starting a session:**
> [`docs/ai-led-session-workflow.md`](ai-led-session-workflow.md), then
> [`docs/planning/project-guidance.md`](planning/project-guidance.md) and
> [`docs/planning/lessons-learned.md`](planning/lessons-learned.md).

---

## Reading harvester output (downstream-AI consumer)

- [`harvester/interpretation-guide.md`](harvester/interpretation-guide.md) —
  Prose annotation layer over the `ir/` JSON contract. Reading order,
  per-category interpretation conventions, `UnresolvedItems` handling,
  provenance conventions, known limitations. Read this first.
- [`harvester/interpretation-guide-outline.md`](harvester/interpretation-guide-outline.md) —
  Decisions of record (doc-home, length budget, section ordering) plus
  the outline the body follows. Read when a body section's framing is in
  question or when revising the guide.
- [`harvester/reviewing-the-feedback-artifacts.md`](harvester/reviewing-the-feedback-artifacts.md) —
  Plain-language developer guide for the human-review step: per artifact
  (`object-use-checklist.json`, `object-cleanup-checklist.json`,
  `classification.json`), what it is, what the developer does, and the
  zero-install editor that opens it. The AI-agent-facing rule for
  delivering this guidance is interpretation-guide §3.3.
- [`schema/v0-vs-tiers.md`](schema/v0-vs-tiers.md) — Field-by-field
  reference for the v0 schema and the three-tier model (`raw/`, `kb/`,
  `ir/`). Read for "what does this field hold?" questions.
- [`schema/v1-adr.md`](schema/v1-adr.md) — Architecture decision record
  for the v1 schema. Read for the rationale behind the v0→v1 transition
  and the canonical envelope shape.
- [`ir-schema-forms-reports.md`](ir-schema-forms-reports.md) — Typed
  `FormMetadata` / `ReportMetadata` record reference (public contract).
  Read when the form/report `ir/` shape needs the typed-record view.
- [`homehealthcare-northwind-comparison.md`](homehealthcare-northwind-comparison.md) —
  Why HomeHealthCare and NorthwindDevEdition are the canonical fixtures
  and which exhibits which structural characteristic.

## Running the harvester (downstream-AI consumer / operator)

- [`harvester/install.md`](harvester/install.md) — Installing the
  self-contained Windows binary from the GitHub Releases channel,
  smoke-checking the install, and the per-subcommand surface.
- [`harvester/batch-operations.md`](harvester/batch-operations.md) —
  Driving `AccessHarvester run` across many `.accdb` files in one pass:
  multi-DB output layout, sequential-only operation, MSACCESS / RCW
  hygiene across runs, `--clean` / `--no-clean` semantics, the cross-DB
  roll-up index at `<out>/AccessMetaData/index.json`, and the
  `--json-status` provenance envelope.

## Project mechanics (orchestrator)

- [`ai-led-session-workflow.md`](ai-led-session-workflow.md) — Single
  source of truth for the per-session procedure: trigger phrases, router
  usage, cross-provider verification, delegation discipline, the Step 9
  reorganization review. Read before every session.
- [`planning/project-guidance.md`](planning/project-guidance.md) —
  Durable Principles and Conventions for the repo (output architecture,
  lean-by-default, testing hierarchy, code style, workflow expectations).
  Read before every session.
- [`planning/lessons-learned.md`](planning/lessons-learned.md) —
  Practical lessons from prior sessions: failure patterns, action recipes,
  promotion-candidate tracking. Read before every session.
- [`planning/session-set-authoring-guide.md`](planning/session-set-authoring-guide.md) —
  Single source of truth for *authoring* session-set specs (slug naming,
  sizing, configuration block). Read before writing or editing a spec.
- [`planning/strategy.md`](planning/strategy.md) — Approved strategic
  direction for the Access → Blazor migration program. Phase definitions,
  acceptance criteria, "Frozen Unless Reauthorized" list.
- [`planning/project-status.md`](planning/project-status.md) —
  Current-state snapshot a fresh orchestrator can read to act
  confidently without re-walking conversation history.
- [`bootstrap-new-orchestration-template-repo.md`](bootstrap-new-orchestration-template-repo.md) —
  Bootstrap notes for a fresh consumer of the AI-led workflow.
  Largely superseded by the `Dabbler: Install ai-router` command;
  retained for the historical context.

## VBA parser and Access expression grammar

- [`VBA-Parser-Migration-Harvester-Plan.md`](VBA-Parser-Migration-Harvester-Plan.md) —
  Full multi-session implementation plan for the VBA parser, symbol
  resolver, and Access metadata harvester. Read for the big-picture
  arc the per-set specs implement.
- [`spec-study/ms-vbal-summary.md`](spec-study/ms-vbal-summary.md) —
  Topic-organized prose summary of the MS-VBAL specification. Drives the
  coverage matrix and hazard list.
- [`spec-coverage/README.md`](spec-coverage/README.md) — Index for the
  spec-coverage artifacts.
- [`spec-coverage/ms-vbal-coverage.csv`](spec-coverage/ms-vbal-coverage.csv) —
  Coverage matrix mapping each MS-VBAL production to its scope and
  priority disposition.
- [`spec-coverage/hazard-list.md`](spec-coverage/hazard-list.md) —
  Concrete parsing hazards drawn from the fixture corpus with MS-VBAL
  citations and difficulty estimates.
- [`spec-coverage/parse-failure-log.md`](spec-coverage/parse-failure-log.md) —
  Running log of parse-failure data across harness runs and
  grammar-authoring milestones.
- [`test-cases/`](test-cases/) — Canonical parser test cases: `<n>.bas`
  / `<n>.cls` / `<n>.sql` inputs paired with `<n>.expected.json` and a
  `harvester-schema.v0.json` contract. The parser's source of truth.

## Cross-repo snapshots (read when migrating into dabbler-platform)

- [`cross-repo/dabbler-platform-components.md`](cross-repo/dabbler-platform-components.md) —
  Inventory of dabbler-platform's component library. Snapshot of
  `../dabbler-platform`; regenerate via `dabbler-platform-discovery`.
- [`cross-repo/dabbler-platform-report-rdf-schema.md`](cross-repo/dabbler-platform-report-rdf-schema.md) —
  Reference for the `report.rdf.json` schema dabbler-platform consumes.
- [`cross-repo/access-to-platform-migration-map.md`](cross-repo/access-to-platform-migration-map.md) —
  Maps Access constructs to their dabbler-platform equivalents.
- [`cross-repo/dabbler-pdf-overview.md`](cross-repo/dabbler-pdf-overview.md) —
  Boundary overview of the dabbler-pdf sibling repo (PDF reporting
  surface that consumes harvester output).
- [`portability-feedback-for-dabbler-platform.md`](portability-feedback-for-dabbler-platform.md) —
  Feedback authored for dabbler-platform maintainers on making the
  workflow/router enhancements adoptable out-of-box in sibling repos.

## Evaluations and proposals

- [`evaluate-lowdefy-as-ir.md`](evaluate-lowdefy-as-ir.md) — Briefing
  document for the Lowdefy-as-IR evaluation.
- [`lowdefy-evaluation/synthesis.md`](lowdefy-evaluation/synthesis.md) —
  Cross-provider synthesis (Opus + Gemini + GPT) of whether Lowdefy's
  schema fits as the migration IR. Read this first for the verdict.
- [`lowdefy-evaluation/evaluation-codex.md`](lowdefy-evaluation/evaluation-codex.md) —
  Codex/GPT raw evaluation of Lowdefy as IR.
- [`lowdefy-evaluation/evaluation-gemini-pro.md`](lowdefy-evaluation/evaluation-gemini-pro.md) —
  Gemini Pro raw evaluation of Lowdefy as IR.
- [`lowdefy-evaluation/prompt-for-codex.md`](lowdefy-evaluation/prompt-for-codex.md) —
  Prompt template used for the Codex evaluation pass.
- [`proposals/delegation-consensus-config.md`](proposals/delegation-consensus-config.md) —
  Design proposal for a `delegation.decision_consensus` configuration
  option in the router. Cross-repo (router + each consumer).
- [`strategic-reviews/2026-05-17-platform-tooling-fit/`](strategic-reviews/2026-05-17-platform-tooling-fit/) —
  Strategic review on whether dabbler-platform's CRUD tooling meets the
  harvester pilot's workflow needs. Read the directory's `README.md`
  for the question and `gemini-3-1-pro.md` / `gpt-5-5.md` for the
  cross-provider opinions.
- [`strategy-input/`](strategy-input/) — Raw cross-provider inputs that
  fed strategic decisions (attribute-strip spec, delegation config,
  property-sheet criteria). Reference only; the decisions live in
  `planning/strategy.md` and `planning/project-guidance.md`.

## Session-set archive

- [`session-sets/`](session-sets/) — One folder per session set, each
  with `spec.md`, `change-log.md` (when complete), session reviews, and
  closeout artifacts. The folder names match the slugs in
  [`session-sets/ROADMAP.md`](session-sets/ROADMAP.md).
- [`session-sets/ROADMAP.md`](session-sets/ROADMAP.md) — Roadmap of
  planned and completed session sets, in dependency order.
