"""015 S2 — routed next-session-set recommendation (last-session-authors-next-set).
DW-11 closes here; capture the consumer-driven-only handoff vs. DW-bundling call."""
import os
from ai_router import route
SS = "docs/session-sets/015-unresolved-items-completeness"
OUT = os.path.join(SS, "next-session-set-recommendation.md")
prompt = """You are recommending the NEXT session set for the dabbler-access-harvester repo
after set 015 (unresolved-items-completeness) closes. Decide between (A) authoring a new
session set now, or (B) a recorded CONSUMER-DRIVEN-ONLY handoff. Be decisive.

REPO POSTURE (authoritative): the harvester reached "consumer-driven-only" after its two
gating sets shipped — the repo does NOT self-initiate work; new sets are pulled in only by
a consumer report or a concrete DW trigger. Sets 006-014 were the 006-review arc (now
complete). Set 015 closed DW-11 (unresolved-items undercount, both layers).

STATE AFTER 015:
- DW-11 CLOSED by 015 (Layer A S1, Layer B S2).
- NEW DW-17 (created this session): module-level unresolved refs reach the run-wide INDEX
  but NOT per-module ParserOutput.Body.References (a completeness asymmetry). Deferred by
  design — adding a module-level slot to ParserOutput is a structural envelope change with
  broad golden churn, for no completeness the index doesn't already give. No consumer has
  asked for per-module module-level signal.
- Standing OPEN DW items, none with an active consumer trigger: DW-1..DW-8 (parser/grammar
  edge cases, BOM, crosstab, dynamic-SQL wiring), DW-12 (latent resolve-failure asymmetry,
  not currently triggerable), DW-13 (Ribbon surface — needs a real USysRibbons corpus),
  DW-14 (nav-target named-arg/indexer/With forms), DW-16 (sampler time-only zero-date root
  cause), DW-17 (above).
- 014's recommendation noted standing DW-bundling candidates remain (small parser/grammar
  DW items could be bundled into one set IF a trigger arrives).

QUESTION: Given the consumer-driven-only posture and that no DW item currently has an
active consumer trigger or a real-corpus dependency that has been satisfied, is the correct
call (A) author a specific next set now, or (B) a recorded consumer-driven-only handoff
listing the standing DW-bundling candidates for when a trigger arrives? If (A), name the
set and justify the trigger. If (B), state it plainly and list the candidates a future
trigger would most naturally pull in.

Return concise prose with a clear final recommendation line."""
r = route(content=prompt, task_type="planning", complexity_hint=55, session_set=SS, session_number=2)
hdr = f"# 015 — Next Session Set Recommendation (routed: {r.model_name}, ${r.cost_usd:.4f})\n\n> task_type=planning. Raw routed recommendation, unedited, plus the Hard Constraints carry-forward below.\n\n"
constraints = """
---

## Hard Constraints carry-forward (verbatim, for whatever set a trigger pulls in)

- NEVER regenerate/commit `examples/northwind-output.json` or any
  NorthwindDevEdition-derived artifact (reintroduces residue sets 003-005 removed).
- Golden regeneration is HomeHealthCare-only, via `GoldenCorpusRegenerator`
  (`AH_REGEN_GOLDEN=1`); re-run `CanonicalGoldenRegressionTests`; explain every delta;
  a "no regen" claim must be proven byte-level.
- COM-tier runs are deliberate, sequential, foreground, TRX logger on; never background a
  `dotnet` invocation; sweep spurious `samples/*.accdb` diffs before staging.
- The quarantined `SanitizeCommandTests` stays quarantined.
- Do not weaken assertions/tests to force green; pin new behaviour explicitly.
"""
with open(OUT,"w",encoding="utf-8") as f: f.write(hdr + r.content + constraints)
print(f"Wrote {OUT} | model={r.model_name} | cost=${r.cost_usd:.4f}")
