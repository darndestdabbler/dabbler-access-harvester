# Reviewing the Feedback Artifacts

> A plain-language guide for a developer reviewing the JSON files the Access
> Harvester's feedback tools produce — what each file is, what you do with it,
> and the zero-install editor that opens it. No JSON literacy required.

After an AI agent runs the harvester's feedback tools — `identify-usage`,
`identify-cleanup`, and `classify` / `sanitize` — it produces a handful of JSON
artifacts. Only the files ending in `-checklist.json` and `classification.json`
need your attention; the rest are for the agent.

The agent that hands you these files should already have explained, in plain
language, what to do. This guide is the reference behind that explanation — and
your fallback if you ever receive a file with no explanation at all.

## The artifacts at a glance

| Artifact | What it is | What you (the developer) do | Editor |
| --- | --- | --- | --- |
| `object-use.json` | Machine-readable usage classification + confidence score, one entry per object. | Nothing — it is for the AI agent. | — |
| `object-use-checklist.json` | One review item per object; carries a question wherever the classifier was unsure which usage an object has. | Open it, answer the questions, tick the items you confirm, add `Feedback` where useful, and Save. | [uat-checklist-editor](#opening-the-two-checklists-uat-checklist-editor) (hosted) |
| `object-cleanup.json` | Machine-readable stale-object flags. | Nothing — it is for the AI agent. | — |
| `object-cleanup-checklist.json` | One keep/remove review item per object flagged as possibly unused. | Open it; for each flagged object confirm keep vs. remove (tick `Passes` / write `Feedback`), and Save. | [uat-checklist-editor](#opening-the-two-checklists-uat-checklist-editor) (hosted) |
| `classification.json` (from `classify` / `sanitize`) | Per-column data-sensitivity classification. | Set the sensitivity category for each column, and Save. | [classification-editor](#opening-the-classification-file-classification-editor) (local) |

## Opening the two checklists (uat-checklist-editor)

`object-use-checklist.json` and `object-cleanup-checklist.json` open in the same
hosted, zero-install web editor and share the same file format.

1. Open the editor at **<https://darndestdabbler.github.io/uat-checklist-editor/>**.
2. Click **Open JSON** and choose the checklist file
   (`object-use-checklist.json` or `object-cleanup-checklist.json`).
3. The review items appear, grouped by object type and object name. Any questions
   the tool raised appear as numbered steps under the item.
4. For each item: tick the **Pass** checkbox if you confirm it, and/or type notes
   in the **Feedback** box (for cleanup items, use Feedback to say keep vs. remove).
5. Click **Save**.

Your answers are written back **into the same JSON file** (the `Passes` and
`Feedback` fields), and the agent reads them as the authoritative statement of
intent. An item left unticked with empty Feedback is treated as "not yet
reviewed."

**Browser note:** in Chrome / Edge, Save overwrites the original file in place; in
Firefox, Save downloads an updated copy that you hand back to the agent.

## Opening the classification file (classification-editor)

`classification.json` sets the data-sensitivity category for each database column.
Its editor is **not yet hosted**, so today you run it locally — it is a single
self-contained HTML file that runs entirely in your browser, with nothing to
install:

1. Download [`tools/classification-editor.html`](../../tools/classification-editor.html)
   from this repo.
2. Open that local `.html` file in your browser.
3. Load your `classification.json` file into the editor.
4. For each column, choose the appropriate sensitivity category.
5. Click **Save** to write your choices back into `classification.json`.

**Note:** a hosted, zero-install version of this editor is a deferred follow-up;
for now the local-file path above is the supported route. Do **not** try to open
the editor via a raw GitHub `.html` URL — a raw HTML URL does not render as a
running application.

---

You never need to hand-edit these JSON files — the editors do that for you. If an
agent ever hands you a raw JSON file with no explanation, that is a bug: the agent
is supposed to walk you through it using this guide.
