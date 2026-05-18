# Downstream-AI Probe — Session 3

> **Status:** Three probe rounds. Final verdict **SUFFICIENT WITH
> ADDITIONS — 0 blocking gaps**. The spec's "material gap" closeout
> gate is cleared. The three probe rounds chronicle a real failure
> path: the v1 probe surfaced the spec's "Out-of-date examples" stop
> rule (committed as WIP); the v2 probe (after regenerating
> `examples/*.json` from the canonical golden corpus) returned 2
> blocking + 3 nonblocking gaps; the v3 probe (after addressing the 2
> blocking gaps in the guide) returned 0 blocking + 5 nonblocking
> gaps. The 5 nonblocking gaps were then folded into the guide as
> polish and the session proceeded to end-of-session verification.

## Probe configuration (consistent across rounds)

- **Convention:** Session 3 spec → step 3 ("Hand the full guide to a
  fresh cross-provider model and ask: *Can you interpret this
  `northwind-output.json` based only on the guide?*").
- **Orchestrator:** Claude Code (Claude Opus 4.7) @ effort=high.
- **Cross-provider target (primary):** `gpt-5-4` (fresh provider).
  All three rounds returned sustained HTTP 429s, falling back to
  `gemini-pro` per the verifier-failure escalation ladder. The 429
  condition is durable across at least three days now.
- **Fallback:** `gemini-pro` via `query("gemini-pro",
  task_type="analysis", ...)`.
- **Prompt inputs (v2 and v3):** the full
  [`docs/harvester/interpretation-guide.md`](../../harvester/interpretation-guide.md)
  body plus the three example files
  ([`examples/minimal-form.json`](../../../examples/minimal-form.json),
  [`examples/complex-form.json`](../../../examples/complex-form.json),
  [`examples/northwind-output.json`](../../../examples/northwind-output.json)).
  The v1 probe sent only `examples/northwind-output.json` per the
  literal reading of the spec; subsequent rounds expanded to all three
  files since the guide's §9 names all three as the consumer-facing
  gallery.
- **Persisted:** raw responses go to disk *before* any display side
  effect, per the *Persist Routed Output To Disk* Convention. The v2
  pre-gap-fix response is preserved at
  [`probe-raw/downstream-ai-probe-gemini-pro-v2-pre-gapfix.txt`](probe-raw/downstream-ai-probe-gemini-pro-v2-pre-gapfix.txt);
  the v3 post-gap-fix response (the current verdict) is at
  [`probe-raw/downstream-ai-probe-gemini-pro.txt`](probe-raw/downstream-ai-probe-gemini-pro.txt).
  The v1 raw response was overwritten by v2 before this preservation
  pattern was adopted; the v1 verdict and findings are reconstructable
  from this document and from the WIP commit (see below).

## Round-by-round summary

### v1 — INSUFFICIENT (against stale `examples/*.json`)

- Sent the guide plus the legacy `examples/northwind-output.json`
  (which predated `harvester-finish-canonical-golden-output` and used
  a `{ Spec: { Sections, Controls, ... } }` shape).
- Verdict: **INSUFFICIENT** with 2 blocking gaps (file-structure
  mismatch, form-record shape mismatch) and 3 nonblocking gaps.
- Root-cause investigation confirmed the canonical golden corpus at
  `samples/GoldenOutput/HomeHealthCare/ir/Forms/*.ir.json` uses
  exactly the `{ Type, Properties, Children, ... }` shape the guide
  documents. The guide was correct; `examples/*.json` was stale.
- Cost: ~$0.043 (`gemini-pro` after `gpt-5-4` 429).
- Disposition: tripped the spec's "Out-of-date examples" stop rule.
  Human adjudicated to honour the stop rule. Session 3 committed as
  WIP (commit `8c85f07`); closeout deferred until `examples/*.json`
  was regenerated.

### Regen between v1 and v2

The user authorised regenerating the example files in this session
rather than opening a tree-amendment session to
`schema-finalization-and-docs`. This is a workflow deviation from the
spec's stop-rule prescription ("Tree-amendment to
`schema-finalization-and-docs`"); the trade is pragmatic — the
session-set worktree already had the canonical golden corpus available
and the regen was a four-command operation.

The three `examples/*.json` files were replaced with verbatim copies
from the canonical golden corpus:

- `examples/minimal-form.json` ← `samples/GoldenOutput/HomeHealthCare/ir/Forms/frmReportParam_ClientFY.ir.json`
  (116 lines — simplest form).
- `examples/complex-form.json` ← `samples/GoldenOutput/HomeHealthCare/ir/Forms/frmServiceDetail.ir.json`
  (1,045 lines — rich form with VBA, bindings, embedded macros).
- `examples/northwind-output.json` ← `samples/GoldenOutput/NorthwindDevEdition/manifest.json`
  (29 lines — the manifest envelope from a Northwind run; arrays are
  empty per the golden corpus normalisation).

The interpretation guide's §9 (Examples gallery) was rewritten to
describe each file accurately.

### v2 — SUFFICIENT WITH ADDITIONS (2 blocking, 3 nonblocking)

- Sent the guide plus all three regenerated example files.
- Verdict: **SUFFICIENT WITH ADDITIONS** with 2 blocking gaps and 3
  nonblocking gaps.
- Blocking gaps:
  1. `EventMacros` object — the enricher emits a structured
     per-event `Actions` array on the form record that the guide
     never mentioned; the guide pointed only at the legacy `_AXL:`
     comment reassembly recipe.
  2. `On<event>EmMacro` properties — Access also emits raw-text
     macro dumps as sibling properties on the host's `Properties`
     bag (e.g., `OnLoadEmMacro`); the guide's event-binding shape
     list omitted this pattern.
- Nonblocking gaps:
  3. Hex-encoded binary properties (`NameMap`, `PrtDevMode`, etc.)
     not contextualised.
  4. `VbaCode` for form/report class modules begins with a
     non-VBA `CodeBehindForm` header line not flagged in the guide.
  5. Canonical filename `manifest.json` not explicitly named in
     §2.
- Cost: ~$0.067 (`gemini-pro` after `gpt-5-4` 429).
- Disposition: addressed both blocking gaps in §5.1 and §5.6 of the
  guide (added `EventMacros` object description, added the
  `On<event>EmMacro` pattern to event-binding shape, added the
  `CodeBehindForm` note to `VbaCode`, added the hex-blob note).

### v3 — SUFFICIENT WITH ADDITIONS (0 blocking, 5 nonblocking)

- Sent the guide plus all three example files (same as v2).
- Verdict: **SUFFICIENT WITH ADDITIONS** with **0 blocking gaps** and
  5 nonblocking gaps:
  1. `TextCapacity` object on controls — not documented.
  2. Hex-encoded property context still wants more specifics.
  3. `EventMacros` omission semantics not explicit.
  4. `Label.source` casing inconsistency (guide showed PascalCase
     `Source`; JSON uses lowercase `source`).
  5. Canonical filename `manifest.json` still wants an explicit
     "always named X" sentence.
- Cost: ~$0.065 (`gemini-pro` after `gpt-5-4` 429).
- Disposition: spec gate cleared (0 blocking). Folded the 5
  nonblocking polish fixes into the guide:
  1. Added `TextCapacity` description to §5.1 (populated form
     with `charsPerLine` / `visibleLines` / `font`, plus the
     `unavailable: true` form).
  2. Extended the hex-blob note in §5.1.
  3. Added `EventMacros` omission semantics ("absence means no
     embedded macros, not data not captured").
  4. Fixed the `Label` casing in §7 (PascalCase → lowercase) plus
     a one-paragraph framing about which provenance objects use
     lowercase vs. which records use PascalCase.
  5. Added explicit "filenames are stable" sentence to §2.

## Final state

- **Verdict:** SUFFICIENT WITH ADDITIONS, 0 blocking gaps.
- **Guide changes** since the WIP commit:
  - §2: added stable-filename sentence.
  - §5.1: added `TextCapacity`, `EventMacros` (with omission note),
    `On<event>EmMacro`, `CodeBehindForm`, hex-blob bullets.
  - §5.6: cross-references the three-form embedded-macro story in
    §5.1.
  - §7: fixed `Label.source` casing and added a framing paragraph
    about provenance-object vs. record-shape casing conventions.
  - §9: rewritten to describe the three regenerated example files.
- **Examples regenerated:** the three `examples/*.json` files now
  carry verbatim canonical-schema content (HHC for the two form
  records, NW for the manifest envelope).

The session is unblocked. Proceeding to end-of-session cross-provider
verification and closeout.

## Cost summary across the three rounds

| Round | Verdict | Blocking | Nonblocking | Cost | Note |
|---|---|---|---|---|---|
| v1 | INSUFFICIENT | 2 | 3 | $0.043 | Triggered spec stop rule (WIP commit) |
| v2 | SUFFICIENT WITH ADDITIONS | 2 | 3 | $0.067 | After examples regen |
| v3 | SUFFICIENT WITH ADDITIONS | 0 | 5 | $0.065 | After blocking-gap fixes |
| **Total** | | | | **~$0.175** | All `gemini-pro`; `gpt-5-4` 429d each time |
