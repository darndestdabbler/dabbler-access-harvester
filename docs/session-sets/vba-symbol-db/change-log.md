# Change Log — vba-symbol-db

> **Status:** Complete. Verdict is **PIVOT** — substrate is workable on
> a probe; the routed AI-interpretation methodology is not cost-effective
> at scale, and the next set repeats the interpretation pass under
> subscription-paid in-IDE orchestrators. See `verdict.md`.

## What this set delivered

A SQLite symbol database (`tools/symbol-db/symbol-db.sqlite`) for two
modules of `samples/NorthwindDevEdition.accdb`
(`Form_frmCompanyDetail` and `modStrings`), populated end-to-end:

- 115 symbols across both modules with FQNs, kinds, source bodies,
  signatures, MD5 body hashes, dead-code flags, and equivalence
  pointers.
- 561 reference edges and 156 member-access expression rows.
- 45 form-control rows, 20 event-binding rows, 1 macro with 2
  actions, 65 query rows, 29 table rows.
- AI-written `Interpretation1` (gemini-pro, Session 4) plus
  `Interpretation2` (Opus, Session 5) and `Interpretation3` (GPT-5.4,
  Session 5) on the 42-item block-randomized stratified sample.
- 294 InterpretationHistory audit rows.
- A separate `tools/symbol-db/consistency/grades.sqlite` with 126
  grading rows from Session 6's three-judge cross-provider pass.
- `tools/symbol-db/probe/build_packet.py` (added Session 7) plus the
  three saved probe responses under
  `docs/session-sets/vba-symbol-db/probe-results/`.

## Headline finding (Session 6)

`Confidence` is essentially uncorrelated with cross-provider
interpretation agreement (Pearson +0.004) and mildly *negative*
against justification agreement (-0.18). The Session 4 escalation
thresholds (0.75 / 0.70) were not validated by data. The AI
self-rating columns have not earned their token cost; the next set
should drop both `Confidence*` and `Justification*` from the
writeback path. Full per-judge data and per-kind breakdowns live in
`tools/symbol-db/consistency/correlation-report.md`.

## Substrate finding (Session 7)

The Session 7 comprehension probe surfaced a Sessions 1–2 resolver
pipeline gap: cross-module references like `GetString`,
`StringFormat`, and the enum value
`sCompanyDelete_CompanyTypeChange` are not bound to their
`modStrings.*` declarations even though those declarations exist in
the same symbol DB. All three probe providers worked around it with
name-and-shape inference; at scale across more procedures this is a
real fidelity loss. Targeted fix in the reference writer is a small
follow-up, recommended before the substrate is used as input to
translation work.

## Cost summary

| Session | Routed AI cost | What drove it |
|---|---|---|
| 1 | ~$0.61 | 1 ai-assignment authoring + 3 verification rounds |
| 2 | ~$0.92 | 3 verification rounds + 1 Gemini tiebreaker |
| 3 | ~$0.34 | 2 verification rounds, no tiebreaker |
| 4 | ~$2.16 | Interpretation pipeline ($1.49) + 3 verification rounds ($0.67) |
| 5 | ~$7.98 | 84 pinned `query()` calls (Opus + GPT-5.4) + verification |
| 6 | ~$12.46 | 126 routed grading calls + verification |
| 7 | ~$0.14 | 2 routed probe calls (gemini-pro + gpt-5-4); Opus in-conversation; no migration-readiness; no cross-provider verification |
| **Total** | **~$24.6** | |

## Sessions in execution order

| # | Title | Orchestrator | Verdict |
|---|---|---|---|
| 1 | Symbols Table + Dead-Code / Dead-Object / MD5 Detection + Measurement | claude-code (Opus 4.7 high) | VERIFIED |
| 2 | References + Member Accesses + Caller-Kind Annotation | claude-code (Sonnet 4.6 medium) | VERIFIED |
| 3 | Column Mappings + Macro Normalization | claude-code (Sonnet 4.6 medium) | VERIFIED |
| 4 | AI Interpretation Pipeline | claude-code (Opus 4.7 high) | VERIFIED |
| 5 | Block-Randomized Cross-Provider Re-Interpretation | claude-code (Sonnet 4.6 medium) | VERIFIED |
| 6 | Cross-Provider Consistency + Confidence Calibration | claude-code (Opus 4.7 high) | VERIFIED |
| 7 | Comprehension Probe + Migration-Readiness Review | claude-code (Opus 4.7 high) | n/a (verification skipped per human) |

Sessions 5–6 were inserted on 2026-04-29 after Session 4's "98% at
confidence ≥ 0.90" result was strong enough to be suspicious without
independent verification; the original capstone renumbered from
Session 5 → Session 7.

## Cross-set handoff recommendation

Direction agreed with the human in the Session 7 closing
conversation: **the next set repeats the interpretation pass under
subscription-paid in-IDE orchestrators on the full Northwind DB.**
Specifically:

- Full-Northwind symbol DB (extend Sessions 1–3 from two probe
  modules to every module in `NorthwindDevEdition.accdb`).
- Drop `Confidence*` and `Justification*` from the writeback path
  per the Session 6 calibration finding.
- Interpretation pass orchestrated by Claude Code and (in a parallel
  pass) Codex/GPT-5 from inside VS Code, exploiting the Anthropic Max
  + GitHub Copilot subscriptions for marginal-zero per-call cost.
- Cross-provider check is Claude↔Codex peer review of the other's
  interpretations in batches; skip the Session 6 rubric.
- Leave Gemini out of the interpretation and review path unless a
  Gemini coding assistant becomes available in VS Code.

A specific spec is to be authored at the start of that set; suggested
slugs include `vba-symbol-db-full-northwind` or
`vba-symbol-db-in-ide-interpretation` (human's call). The
human-decided next-set direction supersedes the routed analysis the
workflow normally runs at this step — no orchestrator-self-opinion
displacement was needed because the human set the direction in
conversation.

The Sessions 1–2 cross-module resolver gap (see `verdict.md` and the
"Substrate finding" section above) is the small follow-up that
should land *before or as part of* the next set's first session,
since the next set is going to multiply the gap by every module that
uses cross-module helpers.

**Path B** (a VBA→C# translator with AI fallback for unsupported
constructs) remains the right eventual destination but is a
multi-set program. Hold it until Path A produces full-DB substrate
and a working cross-judge process.
