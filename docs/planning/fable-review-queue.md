# Fable Review Queue

> **What this is:** a running ledger of work done while Claude Fable 5 is
> unavailable (Anthropic shut Fable down 2026-06-13; it may be restored in the
> weeks to come). Each entry records the orchestrator's (Opus 4.8) safer-vs-riskier
> judgment, plus interim Gemini Pro / GPT 5.4 verdicts, so that when Fable is
> restored it can quickly see what was regarded as solid vs. shaky in its absence
> and weigh in. **Fable may disagree — that is the point of the record.**
>
> **How entries flow:** the orchestrator flags any high-stakes activity that would
> especially benefit from Fable's (higher-tier) judgment, records its own
> safer/riskier call with reasoning, then seeks INTERIM review NOT via `route()`
> but via the subscription-paid in-IDE orchestrator pair (GitHub Copilot → Gemini
> Pro AND GPT 5.4) — separate from, and additional to, the mandatory routed
> `session-verification`. Status stays `pending-fable` until Fable reviews.
>
> Governing discipline: `docs/planning/project-guidance.md` (Evidence Over
> Inference) plus the user's 2026-06-13 direction. Claude-side reinforcement lives
> in the `.claude` memories `feedback_validation_first_on_inherited_inflight_work`,
> `feedback_fable_review_queue_for_high_value_work`, and
> `project_fable_shutdown_opus_continuity`.

## Legend

- **Risk** — orchestrator's call: `safer` / `medium` / `riskier`
- **Interim** — Gemini Pro / GPT 5.4 in-IDE verdicts (`pending` until run)
- **Fable** — `pending-fable` until Fable reviews; then `agree` / `disagree (note)`

---

## 008-manifest-vba-ingestion-and-parser-hardening — Session 2

Work originally authored by **Fable 5** (uncommitted at the 2026-06-12 storm
shutdown — see `docs/session-sets/008-…/s2-resume-checkpoint.md`), being validated
and carried forward by **Opus 4.8**. Entries below are the Opus read of Fable's
own design decisions; each will be re-derived empirically (build + corpus) before
commit, not accepted on the checkpoint's word.

### S2-1 — `contextualKeywordName` admitted into `primary` (grammar ambiguity)
- **What:** `Vba.g4` adds `STEP | COMPARE | EXPLICIT | PRESERVE | LIB | ALIAS` as a
  bare-expression alternative (`# ContextualKeywordExpr`) and into the
  variable-class name slots (declaration names, parameter names, loop vars).
- **Risk: riskier.** Whether this introduces a parse ambiguity that changes
  behavior on real input (e.g. `ReDim Preserve a(10)` vs a redim variable named
  `preserve`; `Option Compare Database`; `Declare … Lib "x" Alias "y"`) cannot be
  settled by reading — only by building and running the full corpus plus targeted
  anchored-use pins. The grammar comment asserts "anchored uses win via ALL(*)
  prediction"; that claim is **unproven** as of this entry.
- **Why it benefits from Fable:** anticipating subtle grammar ambiguity by
  inspection is exactly the higher-tier strength. Opus will instead PROVE it
  empirically and may miss a case that only surfaces on unseen input.
- **Opus empirical finding (2026-06-13):** build clean (0 errors); the existing
  hermetic suite is green with NO regression — Grammar 294/294, Parser 69/69,
  Resolver 400/400, SymbolDb 69/69, Pipeline 788/789. The lone Pipeline "failure"
  is the parse-success-report golden going stale in the *expected* direction:
  `basResetFixture` flipped `parseFailed → parseOk` (parseOk 30→31), i.e. **F-3-2
  works** — `Dim step` now parses. A probe confirmed the remaining `parseFailed: 1`
  is `rptClientProfile`'s `Me.Line (x1,y1)-(x2,y2)` graphics-method syntax (VBA0003
  `missing ')' at ','`) — a pre-existing grammar gap present BEFORE and AFTER
  Fable's edits, unrelated to F-3-2/F-3-3, correctly out of scope (the fixture
  comment tags it a "migration stress test"). **Net: no regression; the fix lands.
  Residual risk is NOT regression but whether the NEW capability is
  over-permissive** — the existing corpus never exercises `Dim compare` /
  `ReDim Preserve a(10)` adjacency / `Function F$()`, so the anchored-use + trigger
  pins (S2 remaining work) are still required to close it. Risk lowered
  `riskier → medium`.
- **Interim:** pending (Gemini Pro / GPT 5.4 in-IDE).
- **Fable:** pending-fable.

### S2-2 — `catch (Exception) when (HasErrorNode(item))` in `BuildModuleItem`
- **What:** failure-scoped recovered-stub guard (catch + error-node filter) instead
  of the spec's literal pre-emptive `HasErrorNode` scan.
- **Risk: medium.** Defensible — it avoids collapsing partially-recovered
  procedures that finer statement-level guards already preserve — but a genuine
  builder bug occurring on a tree that *also* carries an unrelated error node would
  be silently swallowed as a recovered stub. Also deviates from the spec wording,
  which the routed verifier may flag.
- **Why it benefits from Fable:** the silent-swallow boundary is a judgment call
  about where "lean, no defensive catch" yields to "real failure path."
- **Interim:** pending.
- **Fable:** pending-fable.

### S2-3 — contextual-keyword scope decision (which slots got it)
- **What:** keywords admitted only at variable-class positions; deliberately NOT at
  `typeMember` / `enumMember` / labels / event + procedure names /
  `procedureReference`.
- **Risk: safer.** Bounded over-acceptance; reasoned and documented. Recorded for
  completeness so Fable can confirm the scope matches its intent.
- **Interim:** pending.
- **Fable:** pending-fable.

### S2-4 — `ProcedureDeclaration.TypeSuffix` kept AST-only (no schema/golden field)
- **What:** the function/property-get return-type suffix is captured on the AST but
  NOT projected into `ParserOutput` this session (Session 3 owns schema additions).
- **Risk: safer.** Additive and scope-respecting. Recorded for completeness.
- **Interim:** pending.
- **Fable:** pending-fable.
