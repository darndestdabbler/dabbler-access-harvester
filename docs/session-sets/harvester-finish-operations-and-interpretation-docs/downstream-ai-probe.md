# Downstream-AI Probe — Session 3

> **Status:** Probe ran 2026-05-18. Verdict **INSUFFICIENT**. The cause
> is a stale `examples/*.json` artefact, not a defect in the
> interpretation guide; the spec's "Out-of-date examples" stop rule has
> fired. Session 3 is STOPPED mid-session per the human's adjudication;
> closeout is deferred until `examples/*.json` is regenerated against
> the current canonical schema. The pointer page (`docs/README.md`),
> the top-level README link, and this probe artefact are committed
> as work-in-progress.

## Probe configuration

- **Convention:** Session 3 spec → step 3 ("Hand the full guide to a
  fresh cross-provider model and ask: *Can you interpret this
  `northwind-output.json` based only on the guide?*").
- **Orchestrator:** Claude Code (Claude Opus 4.7) @ effort=high.
- **Cross-provider target (primary):** `gpt-5-4` (fresh provider —
  Session 1 used `gemini-pro` and `sonnet`, Session 2 verification used
  `gemini-pro`).
- **Fallback:** `gemini-pro`, used after `gpt-5-4` returned sustained
  HTTP 429s (third consecutive session in this set hitting that
  condition; documented across Sessions 1 and 2's actuals).
- **Routed via:** `query("gpt-5-4", task_type="analysis", ...)` →
  fallback to `query("gemini-pro", task_type="analysis", ...)`.
- **Cost:** `gemini-pro` $0.0430 (16,361 input tokens / 2,254 output).
  The 429s on `gpt-5-4` were rejected before token spend.
- **Prompt inputs:** the full
  [`docs/harvester/interpretation-guide.md`](../../harvester/interpretation-guide.md)
  body plus
  [`examples/northwind-output.json`](../../../examples/northwind-output.json)
  verbatim.
- **Raw response:** persisted to
  [`probe-raw/downstream-ai-probe-gemini-pro.txt`](probe-raw/downstream-ai-probe-gemini-pro.txt)
  before any display side effect, per the
  *Persist Routed Output To Disk* Convention.

## Verdict

**INSUFFICIENT** — but with an important qualifier: the *content* of
the guide is correct for the current canonical harvester output. The
gap the probe surfaces is between the guide and the *legacy* example
files at `examples/*.json`, not between the guide and the actual
`samples/GoldenOutput/<fixture>/` corpus the guide was written against.

The probe model could not have known this — it has no way to
cross-check the legacy examples against the golden corpus from the
prompt alone. The verdict is a true positive for the question asked
("can you interpret this JSON using only the guide?"); the underlying
root cause is that the consumer was handed the wrong example file.

## Gaps reported by the probe

### Blocking (2)

1. **File-structure mismatch.** The guide describes a multi-file
   output layout (`manifest.json` plus per-category `ir/` directories
   plus `unresolved-items.json`). The example file is a single
   consolidated JSON object with every category as a top-level array.
   The guide does not mention this consolidated format.
2. **Form/Report record shape mismatch.** The guide's §5.1 documents
   the form record as `{ Type: "Form", Properties: {...}, Children:
   [...], VbaCode, Procedures, ... }`. The example's forms are wrapped
   in a `Spec` object with `DataSource`, `Sections` (string arrays of
   control names), `Controls` (flat array referencing back to sections
   by name), and a `ParserOutput: null` slot. The guide makes no
   mention of `Spec`, `Sections`-as-string-arrays, or `ParserOutput`.

### Non-blocking (3)

3. **`NavigationGraph` top-level object.** The example's manifest has
   a top-level `NavigationGraph` (`EntryPoints`, `Edges`,
   `UnresolvedTargets`) that the guide's §4 envelope description
   omits.
4. **`UnresolvedItem` field set.** The example's `UnresolvedItems`
   entries have a `ConstructDescription` field and lack the `Severity`
   field the guide documents in §6.
5. **`ParserOutput: null` on form records.** Each form in the example
   carries this slot; the guide does not mention it.

## Root-cause investigation

I confirmed the canonical golden corpus shape directly:

```bash
$ head samples/GoldenOutput/HomeHealthCare/ir/Forms/frmAbout.ir.json
{
  "Type": "Form",
  "Properties": {
    "PopUp": "True",
    ...
  },
  ...
}
```

The canonical golden corpus emits exactly the
`{ Type, Properties, Children, ... }` shape the guide describes. The
`{ Spec: { Sections, Controls, ... } }` shape in
`examples/northwind-output.json` is the legacy structure used by the
pre-import `AccessExporterTemp1` codebase, before the
`harvester-finish-canonical-golden-output` set normalized the output
to the current per-fixture multi-file corpus shape.

In other words:

- The interpretation guide's contract matches
  `samples/GoldenOutput/<fixture>/`.
- The three `examples/*.json` files
  (`minimal-form.json`, `complex-form.json`,
  `northwind-output.json`) are pre-canonical and follow a different
  schema.
- The guide's §9 ("Examples gallery") points at the legacy example
  files — that is the breakage path the probe walked into.

## Disposition

The spec's stop-rule table contains this exact case:

> | **Out-of-date examples** | `examples/*.json` no longer matches what
> | the harvester produces after `harvester-finish-canonical-golden-output`
> | lands | Stop. Tree-amendment to `schema-finalization-and-docs` to
> | regenerate examples; resume docs after examples are current. |

The human adjudicated to honour the stop rule: do not close out
Session 3 until `examples/*.json` is regenerated against the current
canonical schema. The three deliverables produced before the probe
ran — the docs index at
[`docs/README.md`](../../README.md), the link added to the top-level
[`README.md`](../../../README.md), and this probe artefact plus its
raw output — are committed as work-in-progress on the
`session-set/harvester-finish-operations-and-interpretation-docs`
branch. `session-state.json` remains at `status=in-progress`,
`lifecycleState=work_in_progress`; `mark_session_complete()` is NOT
called.

## What the next orchestrator picks up

When the resume window opens:

1. **Prerequisite (handled before resuming):** a tree-amendment to
   `schema-finalization-and-docs` regenerates the three
   `examples/*.json` files from a real harvester run against the
   current schema (`{ Type, Properties, Children, ... }` shape,
   per-fixture multi-file corpus or a documented consolidated
   "snapshot" format — the regeneration set decides which).
2. **Resume Session 3:** re-run the downstream-AI probe against the
   regenerated example, expect a SUFFICIENT or
   SUFFICIENT-WITH-ADDITIONS verdict, then continue with end-of-session
   verification and closeout (`disposition.json`,
   `change-log.md`, `next-session-set-recommendation.md`, commit +
   push + `mark_session_complete()` + notify + Step 9 reorganization
   review).

## Files committed in this partial Session 3

- [`docs/README.md`](../../README.md) — new docs-index pointer page.
- [`README.md`](../../../README.md) — top-level README updated with a
  link to the interpretation guide and a pointer to `docs/README.md`.
- [`downstream-ai-probe.md`](downstream-ai-probe.md) (this file).
- [`probe-raw/downstream-ai-probe-gemini-pro.txt`](probe-raw/downstream-ai-probe-gemini-pro.txt)
  — raw verifier output (never edited).
- [`probe-raw/downstream-ai-probe-gemini-pro.meta.json`](probe-raw/downstream-ai-probe-gemini-pro.meta.json)
  — token / cost metadata sidecar.
- [`activity-log.json`](activity-log.json) — Session 3 entries logged
  through the probe step.
- [`session-state.json`](session-state.json) — Session 3 registered
  as in-progress.

## Why this is not a guide defect

The guide accurately documents the current canonical harvester output.
Adding sections for the legacy `examples/*.json` shape would create the
exact failure mode the
[`docs/planning/project-guidance.md`](../../planning/project-guidance.md)
"Lean By Default" Principle warns against — documenting two schemas
when the harvester only produces one. Regenerating the example files
is the lower-cost, higher-truth fix.
