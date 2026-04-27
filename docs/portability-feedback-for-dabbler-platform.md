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

---

## Summary

The enhancements are good. The portability gaps are small and
mechanical: confirm command-level gating in the extension, split the
authoring guide into core + addendum *files*, audit Rules #16-18 for
implicit UI assumptions, make Step 8's next-set recommendation
plan-aware, document the spec-config defaults, and gate the backfill
template on the flags. None of these changes hurt platform; all of them
mean the next sibling repo (and the one after that) can pull updates
without forking.
