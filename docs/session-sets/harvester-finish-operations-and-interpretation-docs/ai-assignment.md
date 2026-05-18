# AI Assignment — harvester-finish-operations-and-interpretation-docs

## Session 1: Outline + Audience-Check Probe

### Recommended orchestrator
Claude Opus 4.7 @ effort=high

### Rationale
The session's primary work is synthesizing a coherent documentation outline from multiple complex source documents. Opus is the highest-capability model for this type of narrative structure generation and comprehension task, justifying its use as the orchestrator.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 3    | Author `ai-assignment.md` | `route(task_type="analysis")` → `gemini-pro` |
| 4    | Author interpretation guide outline | in-conversation (Opus) |
| 5.1  | Execute audience-check probe (Reviewer 1) | `route(task_type="probe")` → `gemini-pro` |
| 5.2  | Execute audience-check probe (Reviewer 2) | `route(task_type="probe")` → `gpt-5.4` |
| 6.1  | Author session summary | `route(task_type="summarization", max_tier=1)` → `gemini-flash` |
| 6.2  | Session verification | `route(task_type="verification")` → `gpt-5.4` |

### Actuals (filled at session close, 2026-05-18)
- Orchestrator used: Claude Opus 4.7 @ effort=high (matches recommendation).
- Total routed cost: ~$0.0898 per the canonical cost report
  ($0.0108 ai-assignment authoring [gemini-pro]
  + $0.0093 audience-check probe #1 first attempt [gemini-pro;
  succeeded, but the script crashed before persisting on the
  gpt-5-4-mini 429 — output lost, paid for]
  + $0.0091 audience-check probe #1 retry [gemini-pro, persisted]
  + $0.0372 audience-check probe #2 [sonnet fallback after OpenAI 429]
  + $0.0204 session-verification [gemini-pro fallback after OpenAI 429]
  + $0.0031 next-orchestrator recommendation [gemini-pro]).
  The activity-log's logged total is lower ($0.0805) because the
  crashed-and-replayed probe call was not logged the first time;
  the router-metrics.jsonl canonical log is the authority.
- Deviations from recommendation: probe #2 fell back to `sonnet`
  rather than `gpt-5-4` after sustained OpenAI 429s across both
  `gpt-5-4` and `gpt-5-4-mini` (3 retries each with backoff). The
  same 429 condition forced the session-verification step to escalate
  per the verifier-failure ladder (rung 2: fall back to `gemini-pro`).
  The audience-check probe artifact discloses the cross-provider
  degradation transparently; the verification verdict was VERIFIED.
- Notes for next-session calibration: if OpenAI is still 429ing at
  Session 2 start, plan on `gemini-pro` for the cross-provider
  verification step from the outset and skip the wasted GPT-5.4
  attempt. The outline was amended in this session to fold both
  must-have and nice-to-have audience-probe gaps into the relevant
  sections; Session 2 should treat those amendments as authoritative
  inputs.

## Session 2: Body Authoring

### Recommended orchestrator
Claude Opus 4.7 @ effort=high

### Rationale
Synthesis-heavy authoring of a ~3,500-5,000 word interpretation
guide is the canonical case for the "Use Opus as the orchestrator
for synthesis-document work above ~3K input tokens" Convention.

### Estimated routed cost
low-to-moderate (verification + next-orc recommendation only;
authoring is in-conversation)

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1    | Author `docs/harvester/interpretation-guide.md` | in-conversation (Opus) |
| 2    | `dotnet build` / `dotnet test` | N/A (docs-only session) |
| 3    | Cross-provider session-verification | `route(task_type="session-verification")` → preferred-pairing `gpt-5-4`; fall back to `gemini-pro` per the verifier-failure escalation ladder if OpenAI 429s persist |
| 4    | Next-orchestrator recommendation for Session 3 | `route(task_type="analysis")` → `gemini-pro` |

### Actuals (filled at session close, 2026-05-18)
- Orchestrator used: Claude Opus 4.7 @ effort=high (matches recommendation).
- Total routed cost: ~$0.034 for Session 2
  ($0.028 cross-provider verification [gemini-pro fallback after the
  preferred `gpt-5-4` 429ed three times in route()]
  + $0.0057 next-orc recommendation [gemini-pro]).
  The activity-log's logged total is $0.034; the router-metrics.jsonl
  canonical log records one additional failed `gpt-5-4` attempt at
  $0.00 (429 before token spend).
- Deviations from recommendation: cross-provider verification fell
  back to `gemini-pro` rather than the configured `gpt-5-4` after the
  router's 3-retry budget was exhausted on sustained OpenAI 429s.
  Same condition Session 1 hit; planned-for in the Session 2 routing
  table above. Used `query("gemini-pro", task_type="session-verification")`
  to bypass `task_type_overrides` per the ladder.
- Notes for next-session calibration: OpenAI 429 condition is durable
  across at least two days now; Session 3 should plan on `gemini-pro`
  for the cross-provider verification step from the outset and skip
  the `gpt-5-4` attempt. The body verified terse but JSON-block-
  compliant on the first attempt — the required-JSON-block convention
  worked as designed and avoided a re-prompt round.

## Session 3: Pointer Page + Closeout
Per Session 3 spec: author `docs/README.md` (the docs-index pointer
page), add a link from top-level `README.md` to the new
interpretation guide, run a downstream-AI probe (full guide → fresh
cross-provider model), then closeout (`disposition.json`,
`change-log.md`, `next-session-set-recommendation.md`,
`mark_session_complete()`, notify, then Step 9 reorganization
review AFTER notify).

---
**Next-session orchestrator recommendation (Session 3)** *(routed via
`route(task_type="analysis")` to `gemini-pro`, $0.0057; full text in
[`next-orchestrator-recommendation-session-3.md`](./next-orchestrator-recommendation-session-3.md))*:

Gemini CLI (Gemini 2.5 Pro) @ effort=medium. The Session 3 work mix
is structured-data manipulation (file index, README link) plus
closeout templates — none of which require Opus-class large-context
synthesis. Gemini Pro at medium effort is the cost-effective fit; the
single cross-provider downstream-AI probe is the dominant routed
cost. (The router's recommendation said "Gemini 1.5 Pro" — model-name
imprecision only; the actual current model is `gemini-2.5-pro`.)

**Caveat for Session 3:** OpenAI 429 condition is still in effect at
Session 2 close. If the orchestrator is Gemini, the cross-provider
verification step must route to an Anthropic model (Sonnet or Opus)
rather than gpt-5-4 — the "different provider than the orchestrator"
constraint flips compared with Sessions 1 and 2. The Session 3 spec
also includes a downstream-AI probe that needs the same constraint
honoured (don't ask Gemini to probe Gemini's output).

### Actuals (Session 3 — PARTIAL; STOPPED at probe)

- **Orchestrator used:** Claude Code (Claude Opus 4.7) @ effort=high
  — deviation from the Gemini Pro recommendation. Human took over
  mid-session after a Gemini Pro attempt got confused (wrote two
  scratch Python files referencing a non-existent
  `register_session_start` helper signature and a non-existent
  `gemini-3.1-pro` model). The user explicitly asked Claude to finish
  the set; Gemini's two scratch files were deleted (uncommitted).
- **Total routed cost (this partial session):** ~$0.043
  (downstream-AI probe via `gemini-pro` after `gpt-5-4` returned
  sustained HTTP 429s — third consecutive session in this set hitting
  that condition, exactly as Session 2's caveat warned).
- **Deviations from recommendation:**
  - Orchestrator engine changed from Gemini CLI to Claude Code per
    human direction.
  - Cross-provider downstream-AI probe fell back from `gpt-5-4` to
    `gemini-pro` after 429s. This is acceptable cross-provider
    coverage for a Claude orchestrator (gemini-pro is a different
    provider than Anthropic) but means the probe was not, strictly,
    "fresh" relative to Sessions 1 and 2 (both used `gemini-pro` for
    earlier routed work).
  - **Session was STOPPED mid-way.** The probe surfaced a real
    blocker: `examples/*.json` predates the canonical-golden-output
    set and uses a legacy `{ Spec: { Sections, Controls, ... } }`
    shape, while the canonical golden corpus uses the
    `{ Type, Properties, Children, ... }` shape the guide
    correctly documents. This is exactly the spec's "Out-of-date
    examples" stop rule. Human adjudicated to honour the stop rule.
    `mark_session_complete()` was NOT called;
    `disposition.json` / `change-log.md` /
    `next-session-set-recommendation.md` were NOT produced;
    end-of-session verification was NOT run.
- **Notes for next-session calibration:**
  - `gpt-5-4` 429s are durable across at least three sessions now.
    Future sessions should plan on a non-OpenAI cross-provider verifier
    from the outset.
  - The next operational step is a tree-amendment to
    `schema-finalization-and-docs` to regenerate `examples/*.json`
    against the current canonical schema. Session 3 resumes from the
    probe step (re-run the probe against the regenerated example,
    then continue with the closeout artefacts) once that amendment
    lands. The pointer page, top-level README link, and probe artefact
    produced in this partial session are committed and do not need to
    be re-done.