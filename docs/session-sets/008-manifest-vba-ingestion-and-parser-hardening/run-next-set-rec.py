"""Route the next-session-set recommendation analysis (Step 8). Persist before
print (cp1252 guard). Run from repo root with the workspace venv."""
from ai_router import route

PROMPT = r"""
You are recommending the NEXT session set to run after
`008-manifest-vba-ingestion-and-parser-hardening` (P1a) completes in the
Dabbler Access Harvester repo. Give a concise, decisive recommendation — not a
survey. Output economics matter: pick the cheapest capable next step that
maximizes consumer value.

## Completion state (just landed)
- 006 review arc ranking: P0 (done) -> P1a (008, JUST COMPLETED) ∥ P1b ->
  P2 -> P3 -> P4, with P5/P6 trailing.
- P1a (008) delivered: form/report code-behind now parses into manifest
  ParserOutput (F-3-1); legal legacy VBA no longer kills modules
  (F-3-2/F-3-3); grammar/AstBuilder hardened; per-module SymbolDb resilience;
  unresolved references now carry ResolutionReason and parse/query
  degradations emit diagnostics (F-3-6); the conformance-contract question
  (F-3-9) was decided (Option B — docs amended, no runner). Deferred items:
  DW-1..DW-4 in docs/planning/deferred-work.md (contextual-keyword slots,
  .Line/.Circle graphics syntax, variable suffix->type, !-suffix function
  names) — all parser-grammar-completeness candidates, none consumer-blocking.

## Candidate next sets (006 ranking)
- **P1b — output-encoding-correctness** (1 session). F-1-1 (Major) + F-1-8.
  SaveAsTextTranslator.BuildPayload reads raw SaveAsText .txt as UTF-8, so any
  real DB with non-ASCII captions/labels/validation is silently, irreversibly
  corrupted to U+FFFD in the kb/ and ir/ tiers the consumer reads — while the
  manifest path decodes correctly, so the same caption differs across output
  surfaces. Fix = two-line translator change (BOM-detect-then-cp1252, the
  strategy `parse` already uses) + hermetic cp1252 translator pins. Independent
  of P1a; NO shared golden churn (kb/ir goldens are ASCII-stable under the fix).
- **P2 — output-contract-fidelity-batch** (2 sessions). F-2-1 (crosstabs
  mislabeled MakeTable; parser-side cross-check via F-3-7), F-3-5 (ByVal vs
  default ByRef indistinguishable), BOM on 4 consumer JSON files, label-casing
  contradiction, others. One coordinated set, one golden regen at the end.
  "Sequence after P1a to share regen discipline, but not blocked by it."
- **P3 — resolver-and-navigation-wiring** (3 sessions). Prereq P1a (hard),
  P2 recommended. Wires the resolver registries so the post-F-3-1 Unresolved
  wall resolves.

## Question
Recommend the single next set to author and run, with orchestrator/effort
suggestion and a one-paragraph rationale. Address: is P1b-next (smallest,
independent, data-correctness) correct, or should P2 go first? Note that P1b
can also run in parallel with P2/P3 if the human wants two windows burning.

## HARD CONSTRAINTS (carry verbatim into the next set's spec)
- NEVER regenerate, recreate, or commit examples/northwind-output.json or any
  NorthwindDevEdition-derived artifact. All fixtures/goldens derive from
  committed HomeHealthCare artifacts only. NorthwindDevEdition is proprietary,
  local-only.
- Golden regeneration is HomeHealthCare-only, once per session, after that
  session's behavioral changes, then re-run CanonicalGoldenRegressionTests.
- COM-tier runs are deliberate, sequential, foreground; TRX <Counters> is the
  completion signal; never background a dotnet invocation; sweep spurious
  samples/*.accdb diffs (git checkout -- samples/*.accdb) before staging.
- The quarantined sanitize test stays quarantined.
- Do not weaken assertions or tests to force green; intent changes require
  explicit disclosure in the verification prompt.

End with a JSON block: {"next_set_slug": "...", "size_sessions": N,
"orchestrator": "...", "effort": "...", "can_parallel_with": ["..."],
"one_line": "..."}
"""

r = route(PROMPT, task_type="analysis", max_tier=2,
          session_set="008-manifest-vba-ingestion-and-parser-hardening",
          session_number=3)
out = "docs/session-sets/008-manifest-vba-ingestion-and-parser-hardening/s3-next-set-analysis.md"
with open(out, "w", encoding="utf-8") as f:
    f.write(r.content or "(empty)")
print(f"OK -> {out} (model={getattr(r,'model','?')} cost=${getattr(r,'cost_usd',0):.4f})")
