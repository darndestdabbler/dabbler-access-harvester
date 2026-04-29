# Change Log — vba-symbol-db-path-b-probe

> **Status:** Complete (3 of 3 sessions, all VERIFIED).
> **Span:** 2026-04-29 (single-day set).
> **Verdict:** **Hybrid** — see [`decision.md`](decision.md) for the
> argument and the per-procedure decision boundary.

## Set Purpose (Recap)

Settle the Path A vs. Path B direction left open by the
`vba-symbol-db` Session 7 PIVOT. Path A bets on AI-interpretation of
the symbol DB substrate; Path B bets on translated-and-testable C#
with provider-independent test authoring as the falsification
mechanism. The set probed Path B with one procedure
(`Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`), preceded by
a substrate fix (cross-module reference resolution) that the probe
required.

## Sessions

### Session 1 of 3 — Cross-Module Reference Resolver Fix

- **Orchestrator:** Claude Opus 4.7 @ effort=high (Claude Code).
- **Routed cost:** $0.156.
- **Verdict:** VERIFIED, 0 issues.
- **What changed:** `AccessHarvester.Vba.Resolver.References.ReferenceResolver.ResolveIdentifier`
  gained a Step 1.5 sibling-module-public-symbol lookup so call sites
  in `Form_frmCompanyDetail` referencing `modStrings.GetString`,
  `modStrings.StringFormat`, and the
  `modStrings.enumStrings.sCompanyDelete_CompanyTypeChange` enum value
  bind cleanly. Regression tests added in
  `tests/AccessHarvester.SymbolDb.Tests/`.
- **Notable deviation:** The committed
  `tools/symbol-db/symbol-db.sqlite` was deliberately NOT overwritten
  to preserve the ~$24 of `vba-symbol-db` Sessions 4-7 interpretation
  columns. End-to-end persistence verified through a temporary
  rebuilt DB instead. The "should we commit a rebuilt structural DB?"
  question is flagged as deferred; under the Hybrid recommendation,
  most of those interpretation columns become Path A scope and stay
  meaningful for that subset.

### Session 2 of 3 — Translation Pass

- **Orchestrator:** Codex / GPT-5.5 @ effort=medium.
- **Routed cost:** $0.0267 (Gemini Pro session-verification only —
  per the special-session direction to bypass the router for
  non-verification work).
- **Verdict:** VERIFIED, 0 issues.
- **What changed:** New test-only project
  `tests/AccessHarvester.TranslationProbe.Tests/` containing:
  - `FrmCompanyDetailTranslationProbe.cs` — the C# translation of
    `cboCompanyTypeID_BeforeUpdate` plus `CompanyIsActive`, with
    faked interfaces (`IFormContext`, `IComboBoxControl`,
    `IDaoCounts`, `IStringRepository`, `IDialogService`,
    `IErrorHandler`).
  - `FrmCompanyDetailTranslationProbeTests.cs` — five xUnit tests
    covering blocking path, no-op path, vendor-products-only path,
    error delegation, and DAO-criteria formatting.
- **Translation packet:** Generated against a temporary rebuilt
  symbol DB (Session-1-fixed) so the `GetString` / `StringFormat` /
  enum cross-module references showed as `resolved`. Saved to
  [`translation-packet.md`](translation-packet.md).
- **Translator's choices:** Documented in
  [`translation/translation-rationale.md`](translation/translation-rationale.md).
  Notable: Access UI primitives (`DoCmd.OpenForm` / `DoCmd.Close` /
  `IsFormOpen`) collapsed into one `IDialogService` seam;
  `clsErrorHandler` became `IErrorHandler`; `StringFormat` /
  `StringFormatSql` became local helpers.
- **Symbol-DB gaps surfaced:** `IsFormOpen`, the Access UI constants,
  and the `Strings` table contents — all unresolved, all flagged as
  Hybrid follow-ups in the decision document. Captured in
  [`translation/symbol-db-gaps.md`](translation/symbol-db-gaps.md).

### Session 3 of 3 — Peer Review + Decision

- **Orchestrator:** Claude Opus 4.7 @ effort=high (Claude Code).
- **Routed cost:** $0.0516 (Gemini Pro session-verification only —
  same special-session direction as Session 2).
- **Verdict:** VERIFIED, confidence high, 0 issues.
- **What changed:** New peer-review folder, additional tests, and the
  set's decision + change-log + next-set recommendation:
  - [`peer-review/translation-review.md`](peer-review/translation-review.md)
    — independent semantic review of the Session 2 translation
    against the original VBA. Three acceptable divergences (D1-D3)
    and four under-tested invariants (U1-U4) catalogued.
  - `tests/AccessHarvester.TranslationProbe.Tests/Session3RegressionTests.cs`
    — four new xUnit tests pinning the U1-U4 invariants. All pass
    against the Session 2 translation; one (U3) demonstrably catches
    a hypothetical translation defect that all five Session 2 tests
    would miss.
  - [`peer-review/test-run-session-3.md`](peer-review/test-run-session-3.md)
    — combined test-run output (9 probe tests + 1600 other tests, all
    green).
  - [`decision.md`](decision.md) — Path A vs. Path B verdict
    (Hybrid), grounded in the three concrete probe instances above.
  - This file.
- **Cross-orchestrator independence:** Session 2 (Codex) translated;
  Session 3 (Claude) reviewed. The peer-review session never read
  Session 2's reasoning beyond what `translation-rationale.md`
  documented.

## Outcomes That Mattered

1. **The cross-module resolver gap is closed.** All four identifiers
   from the verdict table now bind correctly:
   `GetString` → `modStrings.GetString`,
   `StringFormat` → `modStrings.StringFormat`,
   `sCompanyDelete_CompanyTypeChange` → `modStrings.enumStrings....`,
   and within-module references continue to bind unchanged.
   `IsFormOpen` legitimately remains unresolved (lives outside the two
   probe modules; full-DB extraction is a separate set).
2. **The Path B premise was validated.** Provider-independent tests
   (Session 3's authored without seeing Session 2's reasoning) added
   four falsifiable assertions about the translated procedure that
   the AI-interpretation substrate does not contain. Specifically,
   the U3 inner-vs-outer error-handler distinction is the kind of
   parameter-level invariant prose interpretation does not capture.
3. **The Path B economics held up.** ~$0.21 routed across three
   sessions, roughly ~1/100th the per-procedure cost ratio of the
   `vba-symbol-db` interpretation pass. Subscription orchestrator
   windows absorbed the in-conversation work as the PIVOT verdict
   anticipated.
4. **The probe surfaced its own follow-up program.** Six concrete
   open questions in `decision.md` — most importantly the
   `path-b-translation-pipeline` next-set candidate.

## Costs

| Session | Routed cost | In-conversation orchestrator | Verdict |
|---|---|---|---|
| 1 | $0.156 | Claude Opus 4.7 @ high | VERIFIED |
| 2 | $0.0267 | Codex / GPT-5.5 @ medium | VERIFIED |
| 3 | $0.0516 | Claude Opus 4.7 @ high | VERIFIED |
| **Total** | **$0.234** | (subscription-paid) | — |

Compare against the spec's pre-session estimate of ~$1.10 for the
set: the actual came in ~5x cheaper, because the special-session
direction bypassed routed analysis for `ai-assignment.md` authoring
and next-orchestrator recommendations on Sessions 2 and 3 (the only
routed work was the mandatory Gemini Pro session-verification).

## Lessons Worth Remembering

- **Subscription-paid cross-provider rigor is a real substitute for
  routed cross-provider rigor when the providers run in different
  IDE windows.** Codex (Session 2) and Claude (Session 3) produced
  cross-provider falsification at marginal-zero per-call cost.
  Second context for the `vba-symbol-db` PIVOT lesson.
- **A peer-review session that finds no real bug is still
  valuable** when it produces *prospective* test pins. Three of
  the four Session 3 tests pin invariants the translator got
  right; one (U3) would catch a likely future regression. None
  failed retroactively, but all four narrow the future-defect
  surface.
- **De Morgan rewrites of `Or`/`And` gates need an explicit
  rationale or a test pinning the side-effect invariant** — see
  Session 3 review D1 / U1. A drive-by "simplify back to ||" is
  exactly the kind of refactor that breaks side-effect-laden
  VBA boolean operators.
- **Single-procedure probes can answer big-direction questions.**
  This set spent 3 sessions and ~$0.21 to answer "Path A or
  Path B?" — a question that would have cost dozens more sessions
  to settle by either committing wholesale to Path A and
  re-discovering the calibration issue or wholesale to Path B and
  spending months on the pipeline first. The
  `lessons-learned.md` "probe output value earlier" lesson hits
  for the second time.

## Next Session Set

See the `## Next-session-set recommendation` section in
[`ai-assignment.md`](ai-assignment.md). Headline: a Hybrid pipeline
session set, slug suggestion `path-b-translation-pipeline`,
prerequisites list captured in `decision.md` Open Follow-Ups. Should
be authored before any further `vba-symbol-db`-style interpretation
sets so the Hybrid scope decision propagates to whichever AI
columns the symbol-DB substrate continues to populate.
