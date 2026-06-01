# Next Session-Set Recommendation

> Authored at the close of `harvester-consumer-workflow-instructions`
> (2026-06-01). Routed via `route(task_type="analysis")` → gemini-pro
> (do-not-self-opine, per Rule #6). Advisory: the authority for "what is
> startable now" is the DAG in `ROADMAP.md` cross-referenced against per-set
> `change-log.md` presence.

## Recommendation: `sanitize`-against-HomeHealthCare timeout investigative spike

**Why now.** Reliability before features — the ordering this arc just validated
(the COM graceful-shutdown set unblocked the test suite before any feature work).
The one remaining known reliability issue is the `sanitize`-against-HomeHealthCare
timeout documented in `docs/planning/lessons-learned.md` (a latent slow/hang in the
sanitize CLI path — `OleDbValueSampler` / `OleDbRowSource` / COM cleanup — distinct
from the zombie-`msaccess.exe` problem the COM set fixed).

**Re-measure FIRST (possibly a no-op).** The just-closed
`harvester-access-com-graceful-shutdown` set hardened `AccessComSession` and may
have resolved this timeout as a side effect. The first action is a cheap
re-measurement on a Windows+Access host; if the timeout no longer reproduces, the
spike closes with a documentation note and no further code work.

**Scope.** Single targeted session; **no spec required** (the goal is already
documented in `lessons-learned.md`). If the timeout persists: instrument the
sanitize CLI path, isolate the root cause, and either fix it or quarantine the
test with a `Skip.If` gate + issue link.

**After the spike — the capstone.** `harvester-sql-migration-generator` (the SQL
Server migration generator / data-classification-plan Phase 4). Its "defer until
the CI is reliable" gate is now satisfied. It needs its own spec **and** a
deliberate belongs-here-vs-future-migration-repo check (project guidance defers
migration-app work to the future migration repo; confirm the generator is
mechanical-harvester scope before specifying it here).

## Recommended orchestrator (first action: the re-measure)

- **Engine/provider:** claude-code / anthropic (or any capable engine on a
  Windows+Access host)
- **Model/effort:** Opus 4.8 @ **effort=low** for the cheap re-measure; escalate
  only if the timeout reproduces and genuine instrumentation/diagnosis is needed.

**Routing note.** The routed call recommended an ultra-cheap diagnostic model
(`Groq Llama-3-8B`) on pure cost grounds. That is not a configured router model and
is unrealistic for a `sanitize`/OLE-DB/COM diagnostic that must comprehend this
repo's extraction code on a live Access host — but the underlying economics are
sound for the *first* step: the re-measure is nearly free and may close the item,
so start cheap (effort=low) and escalate on reproduction rather than provisioning a
frontier model up front.
