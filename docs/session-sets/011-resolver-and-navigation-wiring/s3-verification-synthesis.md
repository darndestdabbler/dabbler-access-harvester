# Set 011 Session 3 — devil's-advocate verification synthesis

Closeout-grade cross-provider verification was run as a **two-provider
devil's-advocate pair via GitHub Copilot** (subscription in-IDE orchestrator
pair), in place of the routed `gpt-5-4 session-verification` used in S1/S2, per
operator direction. Two prompts × two providers = four raw reviews (saved
verbatim, never edited):

- Main S3 closeout prompt — [`s3-verification-gemini.md`](s3-verification-gemini.md),
  [`s3-verification-gpt-5.4.md`](s3-verification-gpt-5.4.md) (prompt:
  [`s3-verification-prompt.md`](s3-verification-prompt.md))
- Set-wide S1/S2 addendum — [`s3-verification-setwide-addendum-gemini.md`](s3-verification-setwide-addendum-gemini.md),
  [`s3-setwide-gpt-5.4.md`](s3-setwide-gpt-5.4.md) (prompt:
  [`s3-verification-prompt-setwide-addendum.md`](s3-verification-prompt-setwide-addendum.md))

The addendum was added mid-review at the operator's request so the critical pass
would cover the **whole set** (S1 + S2), not just S3. The orchestrator selected
its three targets by mining the prior sessions for claims closed on weak
evidence: S1's Major (shared `ProjectScope`) was retired in R2 by *reasoning*
not a fix; S1's Minor (owningClass alignment) by a *single* pin; S2 passed R1
*clean* and its 1414 reconciliation was *empirical on HHC*.

**Round verdict: ISSUES_FOUND.** Three providers returned ISSUES_FOUND, one
(Gemini main) VERIFIED. Net: two real defects (one fixed this session, one
deferred with disclosure) + one latent gap (fixed by doc) + two disproved
concerns.

## Per-concern adjudication

| # | Source | Verdict | Orchestrator adjudication |
|---|---|---|---|
| **C1** | GPT main (Major); Gemini main passed | **Upheld → fixed** | Confirmed against the golden: `Forms`=`{Spec}`, `Reports`=`{Spec,ParserOutput}`, name at `Spec.SourceFormName`; `Modules`=`ModuleName`; only `Queries`/`Tables`/`Relationships` carry top-level `Name`. §5.7 join sentence + §4 stub bullet corrected this session. |
| **C2** | both passed | VERIFIED | SchemaVersion 1.1 fix confirmed complete (`SchemaVersionLiteral="1.1"`, `SupportedSchemaVersions=["1.0","1.1"]`); no remaining stale envelope `1.0`. |
| **C3** | both passed | VERIFIED | R2-1/R2-2 deferral represented honestly (§5.7 "NOT populated today" + §8 + DW-9/DW-10). |
| **C4** | both passed | VERIFIED | 012 spec faithful to the 006 R2-1/R2-2 sketches; narrow shape-gated row-read constraint holds; 3-session split defensible; v4 stub valid. |
| **C5** | both passed | VERIFIED | "committed golden == fresh run" grounding sound: CanonicalGoldenRegression + ManifestSchemaConformance 6/6 on unchanged code. |
| **C6** | GPT main flagged (only via C1) | VERIFIED-after-C1-fix | S1/S2 deliverables confirmed present in the golden (NavigationGraph populated, 4103 ResolvedTargets / 0 `Kind:null`, arrays 76/19/233/81, UnresolvedItems 1414). GPT's "premature" tag was solely the C1 doc defect — now fixed. |
| **C7(a)** | both disproved | No action | Scope-build post-attach throw is unreachable from a parser-produced AST (statements are not `ModuleItem`s; `Scope.AddDeclaration` is append-only, no duplicate throw). No counterexample exists. |
| **C7(b)** | Gemini (Major); GPT could not reproduce | **Latent → doc fixed, code deferred (DW-12)** | Reachable by contract (resolve-failed module excluded from `resolvedModules` but its pass-1 scope stays visible → a `Resolved` ref can target a `ParserOutput=null` stub), but NOT triggerable today (no live `Resolve()` throw path — GPT's point). Consumer guard added to interpretation-guide §4; latent code asymmetry → DW-12. |
| **C8** | both disproved | No action | Name alignment is by-construction: `Path.GetFileNameWithoutExtension` feeds BOTH `FormMetadata.Name` and `owningClass.Name` from the same scalar; only `ModuleName != Name` diverges and the existing pin covers it. No silent-miss vector. |
| **C9** | GPT addendum (Major) + Gemini addendum (Critical) | **Upheld → deferred (DW-11) + doc fixed** | Real, demonstrated: production aggregator is procedure-body-scoped, so header/module-level unresolveds are dropped from `unresolved-items.json`. GPT proved the header case with a probe. Operator-adjudicated to **defer the code fix to DW-11** (design decision re: double-emit; golden-neutral so needs synthetic pins) and **document the scoping caveat now** in §6 (inline `ResolutionReason` is the authoritative complete signal). |

## Disposition summary

- **Fixed this session (docs):** C1 (§5.7 + §4 join-key/stub accuracy), C7(b)
  consumer guard (§4 `VBA_RESOLVE_FAILURE` null-check note).
- **Deferred with disclosure + DW + reproducer:** C9 → **DW-11**
  (`unresolved-items` undercount; the demonstrated defect), C7(b) latent code
  asymmetry → **DW-12**.
- **No action (disproved by both providers):** C7(a), C8.
- **No code or golden change** resulted from the fixes — all dispositions are
  documentation + deferred-work register + the next-set non-goals. The committed
  golden is unchanged; the COM gates remain 6/6.

The value of the set-wide adversarial pass: C9 (a Major/Critical correctness
defect) and C1 (a Major consumer-facing doc defect) were both **missed** by the
prior single-provider routed verifications (S2 R1 was clean; S1/S2 never
examined the join-key wording or the aggregator's structural completeness). The
two-provider devil's-advocate framing surfaced them and, on C7, demonstrated the
refutation discipline (GPT correctly bounded Gemini's overclaim to a latent,
non-triggerable case).

## Round 2 (re-verification of the closeout fixes)

A narrow R2 re-verify of the C1 / C7(b) fixes + the C9 disclosure
([`s3-verification-prompt-round-2.md`](s3-verification-prompt-round-2.md)) ran
through the same pair. **Gemini → VERIFIED.** **GPT-5.4 → ISSUES_FOUND**, with
two new Major findings — both about the *accuracy of the R1 doc fixes
themselves*, both proven by probe, and both confirmed by the orchestrator:

| # | Finding | Disposition |
|---|---|---|
| **R2-1** | The R1 §4 fix said `Forms` = `{ Spec }` / `Reports` = `{ Spec, ParserOutput }`, but `ParserOutput` is optional on **both** (present with code-behind; golden: 18/19 forms + 10/12 reports carry it). | **Fixed** — §4 rewritten: both always carry `Spec`, may also carry `ParserOutput`. |
| **R2-2** | The R1 §6 caveat overclaimed: it called the inline `Body.References[]` fields the **complete** signal for header *and* module-level refs. Module-level refs are dropped at projection (`ParserOutputProjector.cs:66`, "no module-level item type") → in **neither** `Body.References[]` nor the index. | **Fixed** — §6 split into Layer A (header: inline-complete) vs Layer B (module-level: **no** manifest signal); DW-11 broadened to both layers (Layer B needs a schema home) with both reproducers. |

R2 found **no code defect** and **no new issue outside the two doc imprecisions
it introduced**; the C1/C7b/C9 dispositions from R1 all held. The R2 fixes are
ground-truthed (entry-shape distribution confirmed against the golden;
module-level drop confirmed at `ParserOutputProjector.cs:65-67`). Structured R2
findings: [`s3-issues-round-2.json`](s3-issues-round-2.json); raw reviews:
[`s3-verification-round-2-gemini.md`](s3-verification-round-2-gemini.md),
[`s3-verification-round-2-gpt-5.4.md`](s3-verification-round-2-gpt-5.4.md).

This round is the clearest demonstration of the pair's value: GPT caught two
factual errors in the orchestrator's *own* corrections that a single-pass review
would have shipped.
