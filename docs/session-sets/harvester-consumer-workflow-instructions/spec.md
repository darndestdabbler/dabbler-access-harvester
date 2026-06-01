# Harvester Consumer-Workflow & Feedback-Tool Instructions Spec

> **Purpose:** Make the harvester output *self-instructing* so a consuming AI
> agent knows the full workflow (including the `identify-usage` /
> `identify-cleanup` feedback tools) without prior knowledge, AND so the agent
> can walk a non-expert **developer** through the human-review artifacts — what
> each JSON file means, how to act on it, and which zero-install editor opens
> it. Closes the "the capability exists but nothing told the agent/developer to
> use it" gap.
> **Created:** 2026-05-29
> **Session Set:** `docs/session-sets/harvester-consumer-workflow-instructions/`
> **Prerequisite:** `harvester-manifest-index-and-batch-readiness` (the
> self-describing manifest + `Documentation` pointer + auto-emitted
> `READ-ME-FIRST.md` this set extends); `harvester-object-use-identifier` +
> `harvester-object-cleanup-identifier` (the feedback tools being documented).
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.
> **Status:** **FINALIZED 2026-06-01** by the maintainer (claude-code). Authored
> as a DRAFT by a downstream consumer (`Dabbler.NorthwindToBlazor`, Set 001
> Session 3); the *Finalization checklist* below records the maintainer
> decisions. Ready to run.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites: [harvester-manifest-index-and-batch-readiness, harvester-object-use-identifier, harvester-object-cleanup-identifier]
totalSessions: 2   # provisional — see Finalization checklist
```

---

## Why This Set, Why Now

The first real downstream consumer (`Dabbler.NorthwindToBlazor`, 2026-05-29)
**re-derived the entire object inventory by hand** — classifying 197 objects by
eyeballing RecordSources and names — because nothing in the harvester output
told the consuming agent that `identify-usage` already does exactly that, with
**confidence scores** and **auto-generated clarifying questions**, and that
`identify-cleanup` flags stale objects. The tools existed (and worked: on that
DB, `identify-usage` classified 167 objects and generated 50 questions;
`identify-cleanup` flagged 37) — they were simply undiscoverable from the
agent's vantage point.

Same root cause as the manifest-completeness miss this consumer hit earlier:
**the output must instruct the agent**, because there will be an agent per
database across *hundreds* of migrations, and "the agent should just know" does
not scale.

A second, equally important gap: even once the agent runs the feedback tools,
the artifacts (`object-use.json`, `object-use-checklist.json`,
`object-cleanup.json`, `object-cleanup-checklist.json`, and `classification.json`
from classify/sanitize) are **opaque to a developer** who has never seen them.
The agent must be instructed to explain them and point the developer at the
right editor — and the harvester's consumer docs currently mention **no editor
at all**.

---

## Sessions

### Session 1 of 2: AI-consumer workflow in the self-describing entry point

**Goal:** A consuming agent, reading only the harvest output, learns the full
canonical workflow and the role of every tool.

**Steps:**
1. Add a **"Workflow for AI consumers"** section to
   `docs/harvester/interpretation-guide.md` and mirror a concise pointer into
   the auto-emitted `READ-ME-FIRST.md` template so it travels with the output.
   **Finalization decision (2026-06-01):** the manifest `Documentation` block is
   *not* given a new schema field — its existing `Documentation.Url` already
   resolves to `interpretation-guide.md`, which now carries the workflow
   section, and `READ-ME-FIRST.md` (which ships next to `manifest.json`) points
   a cold consumer straight at it. A dedicated manifest field would be redundant
   and would force a schema-version bump + golden-corpus regen for no added
   discoverability. The canonical sequence:
   - `run` → read `manifest.json` (use `FileMap` to locate per-object IR;
     verify counts).
   - **`identify-usage`** → read `object-use.json` for per-object
     classification + confidence; do **not** re-derive the inventory by hand.
   - **`identify-cleanup`** → read `object-cleanup.json` for stale-object flags.
   - Present the **`*-checklist.json`** files to the developer (Session 2),
     collect answers, and treat the annotated result as authoritative intent.
2. State the **correct `--in` target** explicitly (the per-database base dir
   `…/AccessMetaData/<dbName>/`, not the `--out` root) — the consumer wasted a
   run pointing `identify-usage --in` at the output root and got 0 objects.
3. Cross-provider verify; keep determinism + schema validation green.

**Ends with:** the workflow is discoverable from the output itself; an agent no
longer hand-rolls work the tools already do.

### Session 2 of 2: Developer-facing guide for the feedback artifacts + editor pointers

**Goal:** Make the human-review step **super easy** for a developer who has
never seen these JSON files, and instruct the AI agent to deliver that guidance
whenever the feedback tools are used.

**Steps:**
1. Author a **"For developers: reviewing the feedback artifacts"** doc (in
   `docs/harvester/`) — for each artifact, in plain language:
   | Artifact | What it is | What the developer does | Editor |
   |---|---|---|---|
   | `object-use.json` | machine-readable usage classification + confidence | (nothing — it's for the AI) | — |
   | `object-use-checklist.json` | one review item per object; questions where the tool is unsure | open it, answer the questions, mark items reviewed | **uat-checklist-editor** |
   | `object-cleanup.json` | machine-readable stale-object flags | (for the AI) | — |
   | `object-cleanup-checklist.json` | review items for objects flagged stale | confirm keep/remove | **uat-checklist-editor** |
   | `classification.json` (classify/sanitize) | per-column data sensitivity | set categories | **classification-editor** |
2. **Add an explicit AI-agent instruction** (in the interpretation-guide
   "Workflow for AI consumers" + READ-ME-FIRST): *when any feedback tool is
   run, the agent MUST tell the developer, in plain language, what the produced
   JSON means, exactly how to act on it, and the URL of the editor that opens
   it — never just hand over a raw JSON file.* Include a short copy-paste
   script the agent can adapt.
3. **Wire the editor URLs into the output** so they travel with the data: have
   each `*-checklist.json` (or the READ-ME-FIRST) carry the editor URL that
   opens it, so neither agent nor developer has to hunt.
4. Cross-provider verify; closeout (change-log, ROADMAP/state, Step 9).

**Ends with:** the agent always explains the artifacts + links the editor; a
developer can review with one click and no JSON literacy.

---

## Open decision: where do the editors live? — RESOLVED 2026-06-01 (Session 2)

The two editors are inconsistently hosted today, which undercuts "one-click for
the developer":

- **uat-checklist-editor** — its own repo, **hosted** zero-install on GitHub
  Pages (`https://darndestdabbler.github.io/uat-checklist-editor/`). Opens the
  `*-checklist.json` files.
- **classification-editor** — lives **only** as `tools/classification-editor.html`
  in this repo; **not hosted**. Opens `classification.json`.

**Resolution (Session 2, by cross-provider consensus — gpt-5-4 + gemini-pro —
recorded in `ai_router/consensus-decisions.jsonl`):**

- **(a) classification-editor hosting → DEFER.** Do **not** publish
  classification-editor to GitHub Pages now. This repo is at its Phase-4
  "mechanical finish line"; new hosting is a new maintenance surface and is not
  needed to complete it. The docs/output instruct the developer to download
  `tools/classification-editor.html` and open it locally (it runs fully
  client-side). A hosted version is logged as a deferred follow-up. (Note: a raw
  GitHub `.html` URL is **not** a substitute — it does not render as a running
  app, so "open locally" is the supported route, not a raw URL.)
- **(b) uat-checklist-editor for both checklists → CONFIRMED, with a bug fix.**
  The editor's committed schema requires **PascalCase** `{ "ProjectName",
  "Review": [...] }`. `object-cleanup-checklist.json` was already PascalCase and
  loaded correctly. But `object-use-checklist.json` was being serialized
  **camelCase** (`UsageChecklistWriter` reused one camelCase options object for
  both `object-use.json` and the checklist), so it loaded as an **empty**
  checklist in the editor — the opposite of the spec's original premise. Session
  2 fixed `UsageChecklistWriter` to emit the checklist in PascalCase (mirroring
  `CleanupChecklistWriter`), updated the unit test that had encoded the bug, and
  regenerated the two committed golden `object-use-checklist.json` samples
  (hermetic — `identify-usage` reads `ir/` with no COM dependency; the
  usage/cleanup golden files are not under the golden-corpus regression gate).
  After the fix, uat-checklist-editor is confirmed canonical for **both**
  checklists.

## Finalization checklist (maintainer) — completed 2026-06-01

- [x] Confirm slug, `totalSessions`, Session Set Configuration block. Slug and
  the two-session split are sound; `totalSessions: 2` confirmed (no longer
  provisional). Config block (full tier, no UAT/E2E, three prerequisites)
  accepted as-is.
- [x] Resolve the editor-hosting decision above → **deferred to Session 2's open**
  (the decision is squarely about S2's deliverables — which editor opens which
  checklist and whether `classification-editor` gets hosted). S2 resolves (a)
  and (b) before wiring URLs into the output.
- [x] Author `ai-assignment.md` (routed at Session 1 Step 3 per the workflow);
  `session-state.json` stub already present (v4).
- [x] ROADMAP row already present; updated from "remains DRAFT" to finalized.

---

> **Provenance:** `Dabbler.NorthwindToBlazor` Set 001 Session 3 (2026-05-29),
> where the agent hand-built an inventory the tools would have produced, and the
> feedback-artifact JSONs had no developer-facing instructions or editor links.
