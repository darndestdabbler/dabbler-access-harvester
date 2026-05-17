# Portability Feedback for dabbler-platform's Workflow/Routing Enhancements

**Audience:** dabbler-platform maintainers
**From:** dabbler-access-harvester (a no-UI sibling repo that shares the
AI-router / session-set workflow lineage)
**Goal:** make the recent enhancements adoptable **out-of-box** in any
sibling repo — UI-focused or not — instead of forcing each consumer to
fork-and-prune. Where a UI-specific feature genuinely helps platform-style
repos, this doc proposes a gating pattern that keeps it dormant elsewhere
rather than removing it.

The cross-check pass already did most of the right things (UAT rules
conditional on `requiresUAT`, authoring-guide body free of platform terms,
addendum carrying the platform specifics). The recommendations below are
the residual gaps a no-UI consumer hits.

---

## Guiding principle

> **Universal core, gated extensions, addendum specifics.**
>
> Anything in the *core* of a shared artifact (workflow doc, authoring
> guide, router config, VS Code extension, Rules list) must work
> unmodified when `requiresUAT: false` and `requiresE2E: false` are the
> permanent defaults. Anything UI/UAT/E2E-specific must be either (a)
> gated on a spec-level flag, or (b) confined to an addendum/optional
> module that the consumer can omit.

If a consumer has to delete or edit a shared file to make it stop being
wrong, the artifact is not yet portable.

---

## 1. VS Code extension (`tools/vscode-session-sets/`)

### Issue

`Open UAT Checklist` and `Reveal Playwright Tests` commands are useful
for platform but meaningless in a no-UI repo. The summary says the
pending-UAT badge is gated on `requiresUAT: true`; please confirm the
**commands themselves** are also gated, not just the badges.

### Recommendation

- **Command-palette visibility:** in `package.json`, gate
  `command.dabblerSessionSets.openUatChecklist` and
  `…revealPlaywrightTests` behind a `when` clause that resolves to false
  when no spec in the workspace has `requiresUAT: true` (or
  `requiresE2E: true`). A workspace-scoped context key set by the
  extension on activation (e.g. `dabblerSessionSets.anyRequiresUat`) is
  the cleanest hook.
- **Tree-view actions:** same gating on inline action buttons / context
  menu entries — they should not appear on session-set nodes whose spec
  has the flag false (already implied) **and** should not appear at all
  in workspaces where no spec sets it true.
- **Settings:** add `dabblerSessionSets.uatSupport.enabled` (default
  `auto`, with `auto | always | never`) so a repo that never wants UAT
  surfaces can hard-disable rather than relying on per-spec inference.
- **README:** document explicitly that the extension is safe to install
  in a no-UI repo and that UAT/E2E commands self-hide. This removes the
  "should I install this?" friction for non-platform consumers.

### Why this helps platform too

Same gating means platform's own non-UI session sets (build tooling,
infra, docs-only sets) don't show stale UAT badges either.

---

## 2. `session-set-authoring-guide.md`

### Issue

The summary says the body is repo-portable and platform specifics live
in an addendum. Confirm the *file layout* matches the *content split* —
i.e., the addendum is a **separate file** consumers can omit, not a
section inside the same file that consumers have to redact.

### Recommendation

- Ship two files: `session-set-authoring-guide.md` (portable) +
  `session-set-authoring-guide.platform-addendum.md` (Blazor /
  MudBlazor / Playwright paths, UAT-checklist conventions).
- The portable file references the addendum via a single optional line:
  *"If your repo has UI/UAT surfaces, see the platform addendum for
  conventions."*
- Add a short **"Adapting this guide to your repo"** section at the top
  of the portable file describing the two flags (`requiresUAT`,
  `requiresE2E`) and stating that all rules in the guide assume both
  default to false unless the spec opts in.

### Why this helps platform too

Decouples evolution: the portable guide can be revved without bumping
the platform addendum and vice versa. Other future repos (mobile?
backend services?) can add their own addendums without polluting the
core.

---

## 3. Workflow doc (`docs/ai-led-session-workflow.md`)

### 3a. Rules #16 / #17 / #18

Without seeing the exact text: please audit each rule for an implicit
UI assumption. If any rule references UAT, Playwright, browser
verification, or a specific framework, gate it on the spec flag in the
rule's own text:

> **Rule #N (when `requiresUAT: true`):** …

Rules without that prefix must be runnable in a console-only,
no-browser, no-human-tester repo.

### 3b. Step 8 next-session-set recommendation

Currently framed as "route a recommendation for the next set."
A consumer with a **pre-planned session-set chain** (the harvester has
a 12-set VBA-parser plan in `docs/VBA-Parser-Migration-Harvester-Plan.md`)
needs the recommendation to *validate against the plan and propose
deviations*, not pick freely.

**Recommended Step 8 wording:**

> If the repo has a master plan (`docs/*-plan.md` or equivalent
> referenced in `CLAUDE.md`), the routed recommendation must:
> 1. Read the plan.
> 2. Identify the next set per the plan.
> 3. Either confirm it, or propose a deviation with explicit
>    justification.
>
> If the repo has no master plan, recommend freely from open
> goals/backlog.

This change costs platform nothing (it has no master plan today, so
branch 2 applies) and unblocks plan-driven repos.

### 3c. Trigger phrases

Universal already. One small ask: keep the canonical list in **one**
place (workflow doc) and have CLAUDE.md / AGENTS.md / GEMINI.md
*reference* it rather than re-list it. Drift between four copies is the
predictable failure mode.

---

## 4. `router-config.yaml` task types

### Issue

`uat-plan-generation` and `uat-coverage-review` are defined task types.
A no-UI repo will never invoke them, which is fine, but if the
mandatory-rules list or any workflow step references them
unconditionally, the consumer has to edit the shared file.

### Recommendation

- Keep the task types defined (cheap, zero-cost to a non-consumer).
- Audit every reference to those task types in the workflow doc and
  rules list; ensure each is wrapped in a `requiresUAT: true`
  conditional.
- Add a short comment in `router-config.yaml` next to those entries:
  `# Only invoked when a session-set spec sets requiresUAT: true.`

---

## 5. `backfill_ai_assignment.py`

### Issue

Useful tool. Risk: the forward-looking section template may bake in
UI-specific deliverables (UAT plan, Playwright fixtures).

### Recommendation

- Forward-looking section template should be assembled **conditionally**
  from the spec's config block:
  - Always include: scope, deliverables, verification, risks.
  - Include "UAT plan" only if `requiresUAT: true`.
  - Include "E2E coverage" only if `requiresE2E: true`.
- The routed prompt for the forward-looking analysis should receive the
  config block as input and be instructed to omit UI sections when both
  flags are false.

---

## 6. Bootstrap files (CLAUDE.md / AGENTS.md / GEMINI.md / template-seed)

### Issue

Required-reading lists are now consistent across the four (good). Risk:
template-seed copies may carry platform-flavored examples that mislead a
new repo bootstrapped from the seed.

### Recommendation

- Audit `template-seed/CLAUDE.md` (and AGENTS/GEMINI) for any inline
  example that assumes UI. Replace with neutral examples or two
  alternative examples ("UI repo" vs. "library/CLI repo").
- The trigger-phrases summary in bootstrap files should be a one-liner
  pointing at the workflow doc, not a re-listing.

---

## 7. Session-set spec configuration block

### Issue

Already conditional. One refinement: make the *defaults explicit* in
the authoring guide so that omitting the block is identical to
`requiresUAT: false, requiresE2E: false`.

### Recommendation

In the authoring guide's Session Set Configuration section:

> **Defaults:** if the configuration block is omitted entirely, the
> spec is treated as `requiresUAT: false, requiresE2E: false,
> requiresHumanVerification: false`. UI-focused repos should set the
> flags explicitly per set.

This lets non-UI consumers ignore the block entirely without tripping
the workflow's conditional rules.

---

## 8. CRUD scaffolding templates (added 2026-05-17)

> **Topical shift from sections 1–7.** Sections 1–7 are about the
> shared session-set / AI-router workflow. This section is about the
> `crud-scaffolding` set's `dotnet new` templates (`dabbler-crud-service`
> and `dabbler-crud-page`), which are CRUD-codegen tooling separate
> from the workflow itself. The feedback is grouped here because it
> still concerns dabbler-platform's downstream consumers; if it grows,
> consider splitting it into its own document.

### Context

On 2026-05-17 the harvester repo started Phase 3a
(`blazor-update-workflows`, hand-authored extension of the
`blazor-add-an-order` pilot host) and paused mid-implementation
when the orchestrator noticed `crud-scaffolding` had shipped two
production templates on 2026-05-16. The human convened a cross-
provider strategic review with two in-IDE orchestrators that
have file-system access to both repos: Gemini 3.1 Pro and GPT-5.5.

Both reviewers' raw reports are preserved at
`docs/strategic-reviews/2026-05-17-platform-tooling-fit/`. Both
reviewers independently reached identical conclusions on the
items below. The harvester pilot will not consume these templates
in this repo (Phase 3a is deferred to a future migration repo per
`docs/planning/strategy.md` → Revisions → 2026-05-17), but the
review surfaced concrete recommendations that improve the
templates for *any* downstream consumer.

### Issue 8a — Applicability note

### Issue

The `dotnet new dabbler-crud-service` + `dotnet new dabbler-crud-page`
templates ship without an explicit note about what they are and are
not for. A consumer encountering them might reasonably assume they
are the recommended pattern for any "build a list-and-edit page"
work, including workflow-driven update flows with state machines,
transition gates, and inventory reallocation.

The templates are actually scoped to **single-entity CRUD/admin
slices** — flat tables where "list, filter, edit, save" is the
dominant shape. They do not (and should not) model lifecycle
transitions, domain commands, multi-entity orchestration, or
report-preview side effects. Both reviewers framed it identically:
"misapplied, not useless."

The harvester's first attempted consumer (`Update an Order` from
`workflow-package-pilot/packages/update-an-order.md`) is exactly
the kind of workflow these templates should NOT be used for —
`New → Invoiced → Shipped → Paid → Closed` state machine, line-
item allocation rebalancing on edit, status-conditional field
locks, inventory reallocation before delete.

### Recommendation

Add an explicit applicability note near the top of
`docs/scaffolding-templates/README.md`:

> **What these templates are for:** single-entity CRUD/admin
> slices — entities where "list, filter, edit, save" is the
> dominant shape. Examples: suppliers, shippers, payment methods,
> lookup tables, simple maintenance screens.
>
> **What they are NOT for:** workflow-driven flows with status
> state machines, transition gates, multi-entity orchestration,
> or domain-specific commands. Author those by hand with a domain
> service + explicit page composition. The CRUD scaffolder can
> still support the entity-maintenance surface adjacent to a
> workflow (e.g., a `Shippers` admin page next to a workflow-
> driven `Update an Order` page), just not the workflow itself.

The note should reference the strategic-review reports at
`dabbler-access-harvester/docs/strategic-reviews/2026-05-17-platform-tooling-fit/`
as the source for the boundary.

### Why this helps platform too

Saves the next consumer the same pause-and-review cycle the
harvester pilot just spent. Also reduces the temptation to
reshape the templates around workflow-driven use cases — both
reviewers explicitly warned against that ("would turn a useful
platform utility into a half-migration framework"). Keeping the
boundary visible from the README is the cheapest defense.

---

### Issue 8b — Doc accuracy drift

### Issue

The platform's `FEEDBACK_REMEDIATION_SUMMARY.md` (referenced by
GPT-5.5 in its review) calls out documentation claims that still
need correction even after the critical-runtime gaps were fixed.
Three specific items GPT-5.5 flagged:

1. `AddSupplierScaffolding()` usage vs. direct DI wiring — both
   options appear in different places; one source-of-truth needed
   so consumers know which one is canonical.
2. Navigation menu entry status — the page-template docs imply
   a menu entry is generated; the actual deliverable defers menu
   wiring to Granularity 3.
3. Playwright / manual-verification wording — `CRUD_SCAFFOLDING_FINAL_SUMMARY.md`
   refers to Playwright tests in some places and manual route
   verification in others; the actual state is the latter.

### Recommendation

Reconcile the docs against the actual deliverables. The
remediation summary already names these as open items — closing
them takes a single doc-review pass, not new code.

### Why this helps platform too

Doc-vs-deliverable drift produces verifier false positives in
any future session set that touches the templates' docs, and
sets up an avoidable correction round for the next consumer.

---

### Issue 8c — Defer Granularity 3 until real usage pressure

### Issue

The CRUD scaffolding's `non-mvp-roadmap.md` lists Granularity 3
(DI automation, navigation menu registration, policy registration,
test scaffolding) as the next planned investment, plus reflection-
driven discovery (a `dotnet tool` variant introspecting `DbContext`),
schema-drift support, OData variants, and multi-entity (master-
detail) generation.

Both reviewers independently recommended **not** building any of
those until the existing single-entity templates have been used
successfully on at least a couple of real entities by an actual
consumer. The Access migration arc just demonstrated that the
templates' current scope was a non-fit for the workload that
came up next; building more automation around a tool whose first
real-world miss was an applicability mismatch is the wrong move.

### Recommendation

Keep the existing roadmap entries (don't delete; they remain
valid candidates), but add a guard at the top of
`non-mvp-roadmap.md`:

> **Reauthorization gate:** Items in this roadmap are deferred
> until the existing MVP templates have been used on at least
> 2-3 real entities by a downstream consumer. The first consumer
> to surface Granularity-3 / reflection / schema-drift /
> master-detail need cases will provide concrete evidence about
> which gap matters most. Building automation pre-evidence risks
> investing in the wrong gap.

### Why this helps platform too

Same principle as the harvester's own "Probe Output Value Earlier"
convention — substrate built ahead of a real consumer can be
substrate for the wrong shape. The CRUD scaffolder is at the same
inflection: it has shipped, but it has not been used at scale by
a real consumer yet.

---

### Issue 8d — Optional: recipes, not machinery

### Issue

The templates are end-of-the-line code generation. There is no
documented pattern for "extend a generated CRUD list/detail page
with extra domain actions" — which is exactly the bridge a
workflow consumer needs to use CRUD templates for the entity-
maintenance surfaces and hand-author the workflow surface on top.

Both reviewers flagged this as a *low-cost* platform improvement:
documentation showing how to extend the generated code with extra
domain actions and an extension hook pattern (e.g., partial
classes, an `IExtendedActions<T>` interface the page can render).
This is recipe work — describing the pattern — not new
machinery.

### Recommendation

Add a `docs/scaffolding-templates/recipes/` subdirectory with at
least one worked example: "Adding a workflow action to a CRUD
detail page." Show the boilerplate consumers can copy verbatim:
the partial class, the DI registration, the button group, the
controller endpoint, the API client method. Make it explicit that
the recipe is for adjacent domain actions, NOT for replacing the
hand-authored workflow page wholesale.

This is the kind of feedback that would have made the harvester's
2026-05-17 strategic review unnecessary — if the recipe had
existed, the orchestrator could have inventoried the platform's
docs and seen "CRUD scaffolder + recipe for adjacent actions" as
a hybrid path. Even if no consumer ever uses it, the recipe
clarifies what the scaffolder is and is not for in a way that
prose alone doesn't.

### Why this helps platform too

Recipes are cheap to write, hard to write wrong (they're concrete
code patterns, not architecture), and they communicate scope
intent better than any prose statement. Two reviewers
independently recommended this pattern; the cost is one
afternoon of documentation work.

---

### What the harvester repo will adopt from Issue 8 changes

Nothing. The harvester repo is not consuming the CRUD scaffolder
in any planned work (Phase 3 candidates 3a, 3b, 3e, 3f, 3g are
all deferred to a future migration repo per the 2026-05-17
strategy reset). The recommendations in section 8 are for the
benefit of dabbler-platform's other downstream consumers — most
notably the future migration repo when it scopes up.

If the future migration repo opens, expect feedback flow to
flip: that repo will be a *consumer* of these templates and will
generate fresh applicability evidence. This section can be
updated then with concrete usage actuals.

---

## What the harvester repo will adopt once the above lands

For reference, so platform knows which changes are load-bearing for at
least one downstream consumer:

| Enhancement | Adopt as-is | Adopt after gating fix |
|---|---|---|
| `session-set-authoring-guide.md` (portable file only) | ✅ |  |
| Verification JSON schema + 2-attempt fallback | ✅ |  |
| Trigger phrases section + `— maxout` suffix | ✅ |  |
| `backfill_ai_assignment.py` |  | ✅ (conditional template) |
| Step 3.5 `ai-assignment.md` formalization | ✅ |  |
| Step 8 `mark_session_complete` + next-set recommendation |  | ✅ (plan-aware wording) |
| Rules #16 / #17 / #18 |  | ✅ (audit for UI assumptions) |
| VS Code extension |  | ✅ (command gating + settings flag) |
| Per-spec config block | ✅ (with explicit defaults) |  |
| CRUD scaffolder (section 8) |  | ❌ (not consuming; recommendations are for other downstream consumers) |

---

## Summary

The enhancements (sections 1–7) are good. The portability gaps are
small and mechanical: confirm command-level gating in the extension,
split the authoring guide into core + addendum *files*, audit Rules
#16-18 for implicit UI assumptions, make Step 8's next-set
recommendation plan-aware, document the spec-config defaults, and
gate the backfill template on the flags. None of these changes hurt
platform; all of them mean the next sibling repo (and the one after
that) can pull updates without forking.

Section 8 (added 2026-05-17) is a different kind of feedback: a
post-hoc applicability audit of the `crud-scaffolding` set's
`dotnet new` templates, surfaced when the harvester's Phase 3a
implementation attempt paused mid-implementation to ask whether the
templates were the right tool for the workload. The applicability
note, doc-accuracy reconciliation, Granularity-3 reauthorization
gate, and the optional recipe-pattern documentation are
recommendations both reviewers in the strategic-review pair (Gemini
3.1 Pro and GPT-5.5) reached independently. The harvester is not a
consumer of those templates, so the section 8 row in the table
above is marked "not adopting" — but the next downstream consumer
will benefit from the recommendations being in place.
