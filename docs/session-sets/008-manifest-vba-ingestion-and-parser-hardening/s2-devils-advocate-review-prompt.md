# 008 Session 2 — Devil's-Advocate Review Prompt (interim, non-routed)

> **How to use:** paste everything below the line into **GitHub Copilot with
> Gemini Pro**, and *separately* into **Copilot with GPT 5.4**. Give each the
> prompt cold — do **not** show one model the other's answer (independence is
> the point). Save each verdict raw into `s2-external-verification.md` and copy
> the per-item conclusions into the `Interim` rows of
> `docs/planning/fable-review-queue.md`.
>
> This is the interim cross-provider pass that precedes the COM/golden step and
> the eventual Fable review (per `docs/planning/fable-review-queue.md`). It is
> additive to — not a replacement for — the mandatory routed `session-verification`.

---

## Bias cautions (read first)

This prompt was authored by an AI agent (Claude Opus 4.8) that is carrying
forward another model's (Claude Fable 5's) work and therefore has an opinion on
the answer. Its framing may inadvertently constrain you to in-scope refinements
when the right answer is to question the scope. The work being reviewed may be
presented as further along or safer than it should be. Before answering as
posed, briefly check whether this is the right question; if a different question
would be more useful, answer that one too.

## Your stance: null hypothesis = the work is wrong

Adopt the devil's-advocate position as your **null hypothesis**: assume this
change set contains material errors, rests on unwarranted assumptions, and/or
that a better strategy was overlooked. Your job is to try to **uphold** that
null hypothesis with concrete evidence. Only reject it (i.e., conclude the work
is sound) where the evidence is **ample and specific** — not merely "it looks
reasonable." Treat "I can't find a concrete problem" as weaker than "here is the
input/scenario that breaks it."

Crucial discipline: for each item, argue a **specific** contrarian hypothesis
(named below) and try to make it concretely true — produce the VBA input, the
scenario, or the construct that demonstrates the failure. Do **not** produce
generic "could be risky" commentary; an unfalsifiable worry is not a finding.

## What changed and why

Set `008-manifest-vba-ingestion-and-parser-hardening`, Session 2. The VBA parser
crashed or lost whole modules on some legal legacy VBA. Three fixes were made
(source: `docs/session-sets/006-.../s3-review-findings.md` findings F-3-2, F-3-3,
F-3-10; scope + hard constraints in `docs/session-sets/008-.../spec.md`):

- **F-3-2 (grammar + AstBuilder):** accept type-suffixed *function*/*property-get*
  names (`Function SqlStr$()`); accept six keywords as ordinary identifiers
  (`STEP, COMPARE, EXPLICIT, PRESERVE, LIB, ALIAS`) where VBA does, via a new
  `contextualKeywordName` rule; stop the AstBuilder throwing a
  NullReferenceException into whole-module loss on recovery-mangled input.
- **F-3-3:** strip the type-declaration suffix (`$ % & ! # @`) into a separate
  `TypeSuffix` field at *every* declaration site, so a declaration's `Name`
  matches the suffix-stripped references that resolve against it.
- **F-3-10 ride-alongs:** replace silent enum-default guesses with recovered
  stubs; dedupe two byte-identical preprocessor builders; drop dead locals.

The work under review is exactly the diff from commit `1f5ee92`:

```
git diff 1f5ee92 -- AccessHarvester.Vba.Grammar/Vba.g4 AccessHarvester.Vba.Parser/Ast/
```

Read these for full context (you are path-aware — open them):
- `AccessHarvester.Vba.Grammar/Vba.g4` (the `contextualKeywordName` rule; its use
  in `primary`, `variableDecl`, `redimDecl`, `constDecl`, `moduleVariableDeclItem`,
  `eventParameter`, the parameter rules, `forNextStatement`/`forEachStatement`;
  and the `(IDENTIFIER | TYPED_IDENTIFIER)` name slots on `functionDeclaration`
  and `propertyGetDeclaration`)
- `AccessHarvester.Vba.Parser/Ast/AstBuilder.cs` (`BuildModuleItem` try/catch;
  the procedure builders; `SplitDeclaredName`; `ExtractTypeSuffix`; the
  `BinaryOperator?` / recovered-stub changes)
- `AccessHarvester.Vba.Parser/Ast/Module.cs` and `Procedure.cs` (the new
  `UnknownModuleItem` record; `ProcedureDeclaration.TypeSuffix`)
- `docs/session-sets/008-.../s2-resume-checkpoint.md` (the author's own rationale
  for each decision — challenge it)
- `docs/planning/fable-review-queue.md` (the flagged items S2-1..S2-4 and the
  empirical findings already gathered)

## Empirical findings already in hand (verify or refute these too)

- Solution builds clean (0 errors). Existing hermetic suites all pass with no
  regression: Grammar 294, Parser 69, Resolver 400, SymbolDb 69; Pipeline 788/789.
- The single Pipeline failure is a committed golden going stale in the expected
  direction: module `basResetFixture` flipped from parse-failed to parse-ok
  because `Dim step` now parses (this is F-3-2 working).
- The remaining parse failure is `rptClientProfile`'s `Me.Line (x1,y1)-(x2,y2)`
  graphics-method syntax — claimed to be a pre-existing, out-of-scope grammar gap
  unrelated to this change. **Challenge that claim if you can.**
- NOT YET DONE (so do not assume covered): trigger-input pins and anchored-use
  regression pins; the SymbolDb per-module try/catch; corpus case 14. The
  existing corpus does **not** exercise `Dim compare`, `ReDim Preserve a(10)`
  adjacency, or `Function F$()`.

## Hypotheses to steelman (one per item — make each concretely true if you can)

- **H1 (S2-1, ambiguity):** Admitting `contextualKeywordName` into `primary` and
  the variable-class slots makes the grammar mis-parse some **legal** VBA, or
  silently accept something it should reject, or changes how an existing
  anchored use parses. Produce the VBA line that demonstrates it. Prime suspects
  to actually test: `ReDim Preserve a(10)` (is `Preserve` still the keyword, or
  now an array var named `preserve`?); `Option Compare Database`;
  `Declare … Lib "x" Alias "y"`; `For step = 1 To 10` then `Next step`;
  a statement using one of the six words as a call target or on both sides of `=`.
- **H2 (S2-2, error-swallow):** `catch (Exception) when (HasErrorNode(item))` in
  `BuildModuleItem` hides a real builder bug. Describe a concrete input where a
  genuine logic error in a builder coincides with an error node elsewhere in the
  item and is silently downgraded to a recovered stub, masking the defect. Also
  argue whether the spec's literal pre-emptive `HasErrorNode` guard would have
  been the better design.
- **H3 (S2-1 scope):** The contextual-keyword scope is wrong — either too narrow
  (a legal construct still fails to parse because a keyword was NOT admitted to
  some slot, e.g. a `Type` member, an enum member, a label, a procedure name) or
  too broad. Name the construct.
- **H4 (S2-4):** Capturing `TypeSuffix` on the AST but not emitting it to
  `ParserOutput` this session is the wrong call and will cause a problem (e.g.
  for Session 3's diagnostics/schema work, or a determinism/round-trip issue).
- **H5 (better strategy):** A materially better or simpler approach to the whole
  F-3-2/F-3-3 problem was overlooked. Describe it and what would make it
  obviously better.

## Output format (per hypothesis)

For each of H1–H5:
- **Verdict:** `UPHELD` (the work has this problem) / `OVERTURNED` (evidence is
  ample that it does not) / `PARTIAL` / `CANT-DETERMINE`.
- **Evidence:** the concrete input, construct, or scenario — not a generalization.
  If UPHELD, give the exact VBA that breaks it and the expected-vs-actual behavior.
- **Severity:** Critical / Major / Minor / Observation.
- **Recommended action:** what to change, or what test to add to prove it either way.

End with a one-line overall: is this change set safe to build further work on
(pins, SymbolDb, corpus case, then a golden regeneration), or should something be
fixed first?
