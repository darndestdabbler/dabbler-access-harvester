# Session 3 Stop-Rule Finding — embedded business-logic residue in `docs/lowdefy-evaluation/`

> **Raised:** Session 3 (2026-06-05), during the C#-source/doc redaction step.
> **Rule triggered:** spec `Stop Rules` →
> *"If proprietary content denser than structural names (full source bodies,
> interpretations) is discovered in a tracked file, surface it to the maintainer."*
> **Status:** RESOLVED 2026-06-05 — maintainer chose **Option (A)**: delete the
> whole `docs/lowdefy-evaluation/` arc (5 files). Executed via `git rm` this
> session. `prompt-for-codex.md` is therefore removed from the redact list and
> the 4 previously-undispositioned siblings are deleted with it.

## What was found

`docs/lowdefy-evaluation/prompt-for-codex.md` (a cross-provider second-opinion
prompt about Lowdefy-as-IR architecture) **embeds, verbatim, the full
`packages/add-an-order.md` workflow package** at lines ~536–712 — its complete
seven-section body: business rules with evidence, behavioral invariants, the
`AllocateInventory` oldest-first cross-order allocation policy, the
`(OrderID, ProductID)` uniqueness rule, the deletion/reallocation semantics, and
the open-questions log. It also names `samples/NorthwindDevEdition.accdb` as the
source DB.

This is the **same proprietary NorthwindDevEdition order/inventory business
logic** that Session 1 classified as *"denser than structural names → triggers
the spec Stop Rule"* and that the maintainer authorized **deleting** as the
order-entry pilot arc (the standalone `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
was `git rm`'d in Session 1).

## Why the manifest's `redact` classification is insufficient here

The disposition manifest marked `prompt-for-codex.md` as **redact** (rename
distinctive structural identifiers to generic/HHC). Renaming the `frm*/sfrm*/mod*`
names alone would leave the **business-logic prose** intact — the allocation
policy, the invariants, the tie-break rule, the entire add-an-order workflow.
That is exactly the denser-than-structural content the maintainer chose to
**delete** in its standalone form. Name-only redaction would leave the repo
carrying, in one file, the proprietary logic it removed everywhere else.

## Scope gap

`docs/lowdefy-evaluation/` has **5 tracked files**; only `prompt-for-codex.md`
is in the disposition manifest:

| File | In manifest? | Residue level |
|---|---|---|
| `prompt-for-codex.md` | yes (`redact`) | **HIGH** — embeds the deleted add-an-order package verbatim |
| `evaluation-codex.md` | no | LOW — discusses IR architecture; references "workflow package"/"add-an-order" conceptually (6 hits), no proprietary object names |
| `evaluation-gemini-pro.md` | no | LOW — same (4 hits) |
| `synthesis.md` | no | LOW — same (3 hits) |
| `evaluation-gemini-pro.cost.txt` | no | none (cost telemetry) |

The four siblings were not individually dispositioned. They are the routed
second-opinion outputs + synthesis of a **concluded one-shot evaluation**
exercise (Lowdefy-as-IR), structurally analogous to the other *"spent
experimental/process arc"* deletions Session 1 executed.

## Options presented to the maintainer

- **(A) Delete the whole `docs/lowdefy-evaluation/` arc (5 files).** Cleanest;
  consistent with the Session-1 spent-arc deletions and the add-an-order arc
  delete. Removes the dense residue outright. The Lowdefy-as-IR decision is
  already concluded; the evaluation is a spent process record.
- **(B) Gut-redact `prompt-for-codex.md`** (remove the embedded add-an-order
  package body + the `samples/NorthwindDevEdition.accdb` reference, keep the
  Lowdefy-evaluation framing) **and redact the 4 siblings** for any incidental
  names. Keeps the architecture-decision record; removes the business logic.
- **(C) Name-only redact** (manifest as written). **Not recommended** — leaves
  the proprietary business logic in the tree; inconsistent with the arc delete
  and likely flagged by closeout verification.
- **(D) Escalate to a separate gated history-rewrite set.** Not warranted for a
  HEAD-only action — the content is removable at HEAD; pre-rewrite history is
  already accepted residue per this set's policy.

**Orchestrator recommendation: (A)** — delete the spent arc.
