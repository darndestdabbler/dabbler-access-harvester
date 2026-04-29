# Verdict — vba-symbol-db

> **Decision:** **PIVOT.** The substrate is workable on a probe; the
> routed AI-interpretation methodology is not cost-effective at scale.
> The next set will repeat the interpretation pass under
> subscription-paid in-IDE orchestrators (Claude Code + Codex/GPT) on
> the full Northwind DB, dropping confidence and justification columns
> and using Claude↔Codex as the cross-provider signal.

## What the probe showed

Three providers (gemini-pro routed, gpt-5-4 routed, opus
in-conversation) read
`Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` from the symbol
DB packet at 0.91, 1.00, and 0.92 confidence respectively. They agreed
on what the procedure does, when it fires, the side effects, and the
user-visible behavior. Probe responses are saved verbatim under
`probe-results/`.

The agreement is real but easy. The procedure is shallow (~30 lines,
linear control flow), the comment block states the business rule, and
the gating signals (`CompanyIsActive` and `m_lngVendorProductCount`)
were already populated by Session 4's `Interpretation1`. A
nested-callee or dynamic-SQL-heavy procedure would have been a more
honest probe.

## Substrate finding (Sessions 1–2 resolver gap)

The packet's "Direct References" table shows `GetString`,
`StringFormat`, `IsFormOpen`, and the enum value
`sCompanyDelete_CompanyTypeChange` as `unresolved / not-in-scope`.
But `modStrings.GetString`, `modStrings.StringFormat`, and
`modStrings.enumStrings.sCompanyDelete_CompanyTypeChange` are present
in the same symbol DB. The Sessions 1–2 reference writer did not bind
cross-module references for these call sites.

All three providers worked around it with name-and-shape inference.
At scale across more procedures than this two-module probe, a
downstream consumer would land in the same blind spot every time a
procedure called into another module. **This is a real fix to make
before the symbol DB is used as the substrate for translation work.**

## Calibration finding (already established in Session 6)

Session 6's correlation report is the headline that survives this
session: `Confidence` is essentially uncorrelated with cross-provider
interpretation agreement (Pearson +0.004) and mildly *negative* against
justification agreement (-0.18). The Session 4 escalation thresholds
(0.75 / 0.70) were not validated by data; they were operating on a
self-rating system that does not correlate with the thing it is trying
to gate.

Combined with the cost finding below, this means the AI-self-rating
columns have not earned their token cost. The next set should drop
both `Confidence*` and `Justification*` from the writeback path.

## Cost finding

The set spent roughly **$23–24** in routed AI cost across all seven
sessions (Session 4 interpretation pipeline ≈ $1.49, Session 5
block-randomized re-interpretation ≈ $7.55, Session 6 grading pass
≈ $11.96, plus orchestrator-side verification rounds totalling
~$2.50). Session 7 added ~$0.13 for the two routed probe calls.

The headline calibration finding (Session 6) was worth that spend; the
per-symbol routed interpretation pattern itself was not. Routed
interpretation cost is per-call billed against API budget; in-IDE
orchestrator interpretation runs against the existing Anthropic Max +
GitHub Copilot subscriptions at marginal-zero cost. The next set
exploits that.

## What is NOT in this verdict

- **No migration-readiness round.** Skipped per the human's call. The
  comprehension probe's outcome was already enough to inform the
  pivot, and a second routed three-provider round would not have
  changed the direction.
- **No cross-provider session verification.** Skipped per the human's
  call ("we don't need to cross-verify this one"). The deliverables
  for this session (verdict, change-log) are conversational synthesis
  by the orchestrator; no code changed.
- **No rubric-graded adjudication.** The probe responses are saved
  verbatim. The scaffold for line-by-line human adjudication was
  prepared but not filled — the bottom line was clear without it.

## Open follow-ups

1. **Cross-module resolver gap** — `modStrings`-internal symbols are
   not bound from `Form_frmCompanyDetail` references. Targeted fix in
   the reference writer (Sessions 1–2 code in
   `AccessHarvester.SymbolDb/`). Likely small.
2. **Drop Confidence + Justification from the interpretation
   writeback** — the data Session 6 produced is conclusive enough to
   stop paying for these columns going forward.
3. **Full-Northwind extraction.** Sessions 1–6 were built around two
   probe modules (`Form_frmCompanyDetail`, `modStrings`). Scaling to
   all of NorthwindDevEdition is the input the next set needs.
4. **Path B (VBA→C# translator with AI fallback) remains the right
   eventual destination** but is a multi-set program; the next set
   stays focused on Path A.

## Next session set

Direction agreed in this conversation: **full-Northwind extraction +
in-IDE-orchestrator interpretation pass + Claude↔Codex cross-judge**.
Spec to be authored at the start of that set; suggested slug
`vba-symbol-db-full-northwind` or `vba-symbol-db-in-ide-interpretation`
(human's call).
