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

---

## Interim review verdicts — 008 Session 2

### Gemini Pro (devil's-advocate), 2026-06-13 — raw at `s2-external-verification-gemini.md`

GPT 5.4 pass still pending; synthesis deferred until both are in. Opus
reconciled each verdict against the actual corpus (the discipline cuts both
ways — the review caught real issues; empirical checks corrected overstated
severities).

- **H1 / S2-1 (grammar ambiguity): Gemini OVERTURNED.** Agrees ALL(*) resolves
  `ReDim Preserve(10)` vs `… a(10)` and the `For … Step` forms; no ambiguity.
  Matches Opus's no-regression sweep. **Action:** still add the anchored-use pins
  to lock it, but no defect.
- **H2 / S2-2 (error-swallow guard): Gemini UPHELD, Major. Opus CONFIRMS — real.**
  `catch (Exception) when (HasErrorNode(item))` spans an entire `moduleItem`
  (a whole procedure body). A genuine builder bug (NRE/IndexOutOfRange) deep in a
  procedure that *also* has an unrelated syntax error elsewhere gets swallowed as
  a recovered stub, masking the real defect. **Action: fix** — narrow the catch to
  the specific recovery failure mode (e.g. `NullReferenceException`, or a
  shallow head-of-item error-node check) rather than blanket `Exception`. The
  explicit null-name guards already added to each procedure builder cover the
  known F-3-2 cascade, so the broad backstop can be tightened safely.
- **H3 / S2-1+S2-3 (keyword scope too narrow): Gemini UPHELD, Critical. Opus
  PARTIAL — real gap, but mischaracterized and NOT corpus-impacting / NOT a
  regression.** The listed constructs (`Sub Step()`, `Step` as type/enum member,
  `Compare:` label, `Step:=1` named arg, `Declare Function Compare`) do fail —
  but they failed *before* Fable's change too (STEP/COMPARE/etc. lex as keyword
  tokens, never as identifiers), so this is a pre-existing gap, not a regression
  Gemini's "now throw fatal errors because they no longer lex as identifiers" is
  factually wrong. **None of these constructs appear in the HomeHealthCare
  corpus** (only `Dim step` + `step` expressions, both covered). So not
  golden-impacting and not Critical. **Action: scope decision** — widen the
  contextual-keyword slots to procedure-names/type-member/enum-member/label/
  named-arg/declare (cheap, makes the fix complete) vs. defer as unproven-need
  (Fable's stated bounded-over-acceptance rationale). Lean toward widening the
  clearly-safe ones; confirm no new ambiguity via pins. Decide with GPT + at synthesis.
- **H4 / S2-4 (TypeSuffix dropped → wrong return type): Gemini UPHELD, Critical.
  Opus PARTIAL — real latent issue, but NO HomeHealthCare golden impact.** Gemini
  correctly traces that `Function SqlStr$()` would emit `ReturnType=null` →
  consumer reads Variant, not String (silent-wrong, the F-3-5 class). BUT **no
  suffixed function/property name exists in the HHC corpus**, so the committed
  golden is NOT corrupted and is safe to regenerate. The issue is real for (a)
  the planned corpus case 14 if it includes `Function SqlStr$()`, and (b) any
  real customer DB. Note this is also a PRE-EXISTING variable behavior (`Dim s$`
  already set Type=null + suffix) that Fable propagated to functions/params, not
  newly invented. **Action: fix or explicitly defer with disclosure** — see H5.
- **H5 / S2-4 (better strategy): Gemini UPHELD, Major. Opus AGREES it's the
  cleaner design.** Map the type-declaration char to a real `TypeReference`
  (`$`→String, `%`→Integer, `&`→Long, `!`→Single, `#`→Double, `@`→Currency) at
  AstBuilder time so the resolver/SymbolDb/projector see a normally-typed
  declaration — eliminating the downstream-blind `TypeSuffix` field and resolving
  H4 at the source. **Caveats to weigh at synthesis:** (a) it's a broader refactor
  touching pre-existing variable handling, possibly beyond Session 2's F-3-3
  scope; (b) consider keeping BOTH the mapped type AND a "was-a-suffix" signal if
  round-trip fidelity matters; (c) confirm against the repo's "only emit what
  Access wrote" convention (suffix→type is lossless inference, likely fine).
- **Overall (Gemini): "NOT safe to build further work upon; fix H3+H4 before
  locking any golden."** Opus reconciliation: the **HHC golden is safe to
  regenerate** (no corpus trigger for H3/H4). H2 is a real fix worth making
  regardless. H3/H4/H5 are genuine and worth resolving, but they are
  latent/scope/architecture calls, not golden-corrupting blockers — to be
  decided at synthesis once GPT's pass is in.

### GPT 5.4 (devil's-advocate), 2026-06-13 — raw at `s2-external-verification-gpt-5.4.md`

GPT ran actual `AccessHarvester parse` probes (stronger evidence than reasoning).
- **H1 (ambiguity): OVERTURNED.** Probed the exact collisions incl. `ReDim
  Preserve a(10)` vs `ReDim Preserve(10)` — both parse, the discriminator proving
  ALL(*) disambiguates as intended. Add anchored-use pins.
- **H2 (error-swallow): PARTIAL, Minor.** Catch is broader than the spec sketch,
  but GPT "could not make that concrete against the current codebase" — the
  current throw sites are exactly the recovery-mangled head-token cases this
  converts to stubs. Keep the failure-scoped guard; add a regression pin (malformed
  proc preserves neighbors) and consider surfacing the caught exception in
  debug/test builds. (Gemini rated this Major; GPT Minor — GPT is closer.)
- **H3 (scope): UPHELD, Major — SHARPENED.** The real, change-induced must-fix is
  the **named-argument symmetry hole**: Fable added `contextualKeywordName` to the
  parameter rules, so `Sub F(step As Long)` now parses — but `namedArgument` still
  requires `IDENTIFIER`, so the matching call `F Step:=1` fails. That is internally
  inconsistent with a declaration shape the grammar now allows (not pre-existing —
  the change created it). The other slots (proc names, type/enum members) GPT
  confirms fail too but says "do not widen every slot blindly; fix the
  declaration/use symmetry hole first."
- **H4 (TypeSuffix → null ReturnType): UPHELD, Major.** Confirmed mechanism:
  `Function SqlStr$()` parses but projects with no `Signature.ReturnType` and
  SymbolDb hashes it `Function(...):Void` — real semantic loss on newly-accepted
  input. Fix via the EXISTING `ReturnType` channel (derive it from the suffix when
  no `As` clause); not a schema change.
- **H5 (drop TypeSuffix → synthesize TypeReference): OVERTURNED.** Directly
  contradicts Gemini's H5. GPT argues KEEP the separate `TypeSuffix` — it is the
  repo's established representation for variables/params/loop-vars/identifiers;
  synthesizing a `TypeReference` and erasing the suffix throws away raw spelling
  and complicates recovery-shaped inputs. The real problem is H4's missing bridge,
  not the representation.

### Northwind representativeness benchmark, 2026-06-13 (local-only, genericized)

Ran the H3/H4 construct greps over the **local NorthwindDevEdition mirror (66
Microsoft-authored modules** vs HHC's 32; gitignored, no COM). Genericized counts
(no Northwind structural identifiers committed, per the hard constraint):
- Suffixed `Function`/`Property Get` NAMES: **0** (H4 trigger absent).
- Suffixed `Dim`/`Const`/`Static`/`ByVal`/param DECLARATIONS: **0**.
- Procedures named with a contextual keyword (`Sub Step()` etc.): **0** (H3 absent).
- `.Line (x,y)-(x,y)` / `.Circle` graphics methods: **0** (the HHC `rptClientProfile`
  gap is HHC-only).
- Only `$`-suffix usage: ~7 string-function CALL sites (`Format$(…)` class), which
  already parse — not declarations.
- **Conclusion:** the H3/H4 constructs are absent from BOTH databases — genuinely
  rare even in Microsoft's reference DB. Confirms "latent, not golden-impacting"
  for the committed HHC golden AND the local Northwind golden. (Caveat: greps are
  heuristic, not a full parse; the named-argument hole below is corpus-independent.)

### Synthesis (Gemini + GPT + both corpora), 2026-06-13 — Opus

- **H1:** no defect; **add anchored-use pins** (both reviewers).
- **H2:** defensible; **narrow the catch** (specific exception / shallow head check)
  + **regression pin**. In-scope (this session's own code).
- **H3 named-argument symmetry:** **FIX** — add `contextualKeywordName` to
  `namedArgument`. Change-induced inconsistency, corpus-independent, cheap. In-scope.
- **H3 other slots (proc names / type & enum members / labels / declare):**
  pre-existing, absent from both corpora → **scope decision** (defer vs widen the
  safe ones); not a blocker.
- **H4 return-type-from-suffix:** real but **latent (0 in both corpora)**. It is the
  type-emission class the spec's Non-Goals defer (F-3-5-adjacent). **Decision for
  the human:** fix now via the existing `ReturnType` channel (small; matters for
  corpus case 14 + real customer DBs) vs defer per spec stop-rule discipline +
  document. Affects corpus-case-14 design.
- **H5:** reviewers split; **Opus adjudicates toward GPT** — keep `TypeSuffix`
  (established convention, preserves raw spelling), resolve via the H4 bridge, not a
  representation swap. Gemini's broader refactor is not adopted.
- **Overall:** the committed HHC golden is **safe to regenerate**. No
  golden-corrupting blocker. Worth fixing before further build-out: H2 (narrow),
  H3-namedarg, the H1 pins; H4/H3-other-slots are human scope calls.
- **Human decision (2026-06-13):** fix H4 now (return-type from suffix, function/
  property-get); fix H3 named-arg + H2 narrow + H1 pins; **defer** H3 other-slots.
  Deferrals memorialized in `docs/planning/deferred-work.md` — **DW-1** (keyword
  slots: proc names / type & enum members / labels / declare), **DW-2** (`.Line`/
  `.Circle` graphics syntax, the `rptClientProfile` gap), **DW-3** (variable
  suffix→type, the H4 variable parallel, F-3-5 class).

---

## 009-output-encoding-correctness — Session 1 (F-1-1 cp1252 decode + F-1-8 pins)

Work authored by **Opus 4.8** (orchestrator), routed cross-provider verification
gpt-5-4 (R1 ISSUES_FOUND 3 Minor → fixed → R2 VERIFIED). Small diff, but **broad
consumer blast radius** — the decode path runs over *every* non-ASCII caption,
label, validation string, and VBA literal in *every* real customer DB — so it is
flagged for the higher-tier devil's-advocate pass even though the routed verifier
passed it. Interim review prompt: `s1-devils-advocate-prompt.md` (run in-IDE via
Gemini Pro AND GPT 5.4). Each item carries Opus's safer/riskier call; the
reviewers' null hypothesis is that the call is wrong.

### S1-1 — cp1252 read preserves BOM + agrees across output surfaces
- **What:** both `BuildPayload` read sites now pass `Encoding.GetEncoding(1252)`;
  the claim is that `File.ReadAllText(path, enc)` / `ReadAllLines(path, enc)`
  still honour a BOM (StreamReader `detectEncodingFromByteOrderMarks: true`) and
  only the **no-BOM** case falls back to cp1252 — making the translator agree
  with the manifest path (`Pipeline` also reads via `GetEncoding(1252)`) and not
  re-introduce the cross-surface mismatch F-1-1 set out to kill.
- **Risk: medium.** The common cases are pinned (cp1252 round-trip both read
  sites + a UTF-8-BOM precedence test). The subtle, *unproven* edge is that the
  translator's StreamReader BOM detection is **broader** than `RunParse`'s manual
  `EF BB BF` / `FF FE` byte check (StreamReader also auto-detects UTF-16 **BE**
  and UTF-32). The intended invariant (translator ≡ manifest) holds; a *secondary*
  divergence (translator vs RunParse on a UTF-16-BE-BOM'd file) is possible but
  likely inert — to be confirmed, not assumed.
- **Why it benefits from Fable:** anticipating a cross-surface encoding
  divergence by inspection of three read strategies is the higher-tier strength;
  Opus pinned the common path and reasons the rest is inert.
- **Interim:** pending (Gemini Pro / GPT 5.4 in-IDE).
- **Fable:** pending-fable.

### S1-2 — "no golden regen needed" without running the COM golden test
- **What:** the set's stop-rule says no golden regeneration is expected because
  the committed HomeHealthCare fixtures are all-ASCII; `CanonicalGoldenRegressionTests`
  (COM tier) was deliberately **not run**. The fix is argued ASCII-stable by
  construction (0x00–0x7F decode identically under cp1252 and UTF-8; BOM
  preserved).
- **Risk: medium → riskier if unverified.** The by-construction argument is
  sound *given* the premise that the raw SaveAsText `.txt` inputs (not just the
  kb output) are all-ASCII. The 006 probe found no U+FFFD/non-ASCII byte in the
  committed kb golden, which *implies* the raw inputs are ASCII-only (else the
  old UTF-8 read would have left U+FFFD in the golden) — but that inference was
  **not re-confirmed against the raw `.txt` fixtures this session**, and the COM
  regression test that would catch a real delta was skipped.
- **Why it benefits from Fable:** "the test I didn't run would have passed" is
  exactly the inference an adversarial reviewer should try to falsify with a
  direct byte-grep of the raw fixtures and/or a COM regen.
- **Interim:** pending.
- **Fable:** pending-fable.

### S1-3 — static-ctor `CodePagesEncodingProvider` registration (lean?)
- **What:** a static constructor on `SaveAsTextTranslator` registers the
  code-pages provider so the cp1252 read works from hosts that never ran the
  CLI's `Main` (the new hermetic tests, an embedding caller).
- **Risk: safer.** Idempotent, runs before any member access, correct. The only
  question is lean-by-default: the CLI `Main` and the manifest path already
  register it, so a reviewer could argue the translator-local registration is
  redundant for the production path. Recorded for completeness.
- **Why it benefits from Fable:** the redundant-vs-necessary call is a judgment
  about where defensive registration earns its keep.
- **Interim:** pending.
- **Fable:** pending-fable.

### S1-4 — hermetic tests lock the fix (not passing for the wrong reason)
- **What:** 9 hermetic facts; cp1252 inputs built from literal bytes (no
  test-side provider pre-registration) so they exercise the translator
  self-bootstrapping cp1252; both repaired read sites pinned independently.
- **Risk: safer.** R1 routed verification already drove out the three posture
  gaps (masked self-registration, untested `ReadAllText` path, no BOM pin) and
  R2 VERIFIED the fixes. Residual: provider registration is process-global, so
  the self-bootstrap property is not a hard per-test guarantee (acknowledged).
- **Why it benefits from Fable:** confirm no remaining "green for the wrong
  reason" path — i.e. a production reversion that leaves all 9 tests green.
- **Interim:** pending.
- **Fable:** pending-fable.
