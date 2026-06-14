"""Set 011 Session 2 — cross-provider decision consensus on the R2-1/R2-2 scope conflict.

Re-grounding (Step 2-3) disproved the spec's premise that switchboard rows are
in the harvested table data. This routes the resulting scope decision to two
cross-provider models (gpt-5-4 OpenAI, gemini-pro Google) for independent
opinions BEFORE surfacing to the human. Persist-then-display per lessons-learned.
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import query  # type: ignore

SET = Path("docs/session-sets/011-resolver-and-navigation-wiring")

PROMPT = r"""
You are advising an AI orchestrator running an automated, spec-driven session set
on a C# .NET tool ("Dabbler Access Harvester") that extracts Microsoft Access
metadata into JSON. Give an independent recommendation on ONE scope decision.
Answer with a clear prose analysis, then a final one-line verdict
"VERDICT: <A|B|C> — <=15 words".

## Context

Session set 011 ("resolver-and-navigation-wiring") is the "P3" set from a prior
review (set 006). Session 1 (done, verified) wired the VBA resolver into
production. Session 2's spec says to do TWO things:

(F-2-2 core wiring) Wire the already-built-but-unwired "Phase 6 enrichment
substrate" into the production manifest pipeline:
  - NavigationGraphBuilder -> populate manifest.NavigationGraph (today absent)
  - UnresolvedItemAggregator -> populate unresolved-items.json (today always [])
  - EventHandlerClassifier / ValidationRuleExtractor / RowSourceSpecBuilder ->
    populate the manifest's (currently empty) top-level classification arrays.
These wire cleanly off an existing seam (resolvedModules + crossRefs already
built in Pipeline.cs). NO new COM/data-extraction surface is needed. I have
verified the HomeHealthCare corpus yields a real, non-trivial NavigationGraph
purely from harvested code-behind (~6 DoCmd.OpenForm, ~8 report->dialog OpenForm,
1 OpenReport, many DoCmd.Close edges). This is the genuine heart of F-2-2 ("the
substrate has zero production call sites").

(R2-1 / R2-2 consumer deliverables) The spec ALSO says, in the same session, to:
  - R2-1: "Add the Switchboard Items table reader to populate
    NavigationGraphInputs.Switchboard ... rows are in the harvested table data;
    no new COM surface."
  - R2-2: derive dialog->query->report chain edges from existing surfaces.

## The conflict I found by re-grounding against the current tree (the spec
demanded this re-grounding explicitly)

1. The spec's premise for R2-1 is FALSE. tblSwitchboardItems ships with
   "(no data file)" in the migration output; its 52 rows (ItemText/Command/
   Argument — the actual menu tree) are NOT harvested into ANY committed
   artifact. The set-006 review's OWN probe says verbatim: "no artifact in the
   fresh output carries row content" and "the 52 rows ... are therefore invisible
   to the downstream consumer." So a "switchboard items table reader" CANNOT read
   rows that are not harvested — it would require a NEW row-read (OleDb/DAO),
   which is new COM/data surface. The set's Hard Constraints forbid new COM
   surface and its Stop Rule says "Scope is P3 only; if a fix requires P4 edge
   derivation ... stop at the boundary, record a DW-<n> entry, leave it to the
   owning set."
2. The set-006 review ITSELF ranked R2-1/R2-2 as a SEPARATE downstream set "P4"
   ("switchboard-and-report-chain-edges", sized 3 sessions), whose scope
   explicitly lists "narrow OleDb row-read" + "embedded-macro parsing,
   record-source edges, dialog/parameter edges, TempVars flows". I.e. set 006
   already says R2-1/R2-2 is a 3-session set, not a sub-task of this one.
3. There IS a partial alternative for R2-1 without new COM: the switchboard is
   ALSO driven by a harvested macro (mcrSwitchboardActions: named sub-macros each
   doing OpenForm to a literal form). But turning that macro IR into
   NavigationGraphInputs is itself NEW production code (a macro-IR reader), and
   the menu CAPTIONS still live only in the unharvested table rows. R2-2 chain
   derivation is likewise substantial new logic (006 scoped it as P4).

## The decision

Option A — Deliver ONLY the F-2-2 core substrate wiring in Session 2 (real value,
no new COM, clearly P3); defer R2-1 (switchboard rows) and R2-2 (chain edges) to
the 006-ranked P4 set with DW-<n> deferred-work entries citing the disproven
premise. (This drops 2 of the session's 7 progress keys and 2 End-Of-Set
deliverables, so it needs human sign-off, but honors the Stop Rule + Evidence-
Over-Inference + no-new-COM constraints.)

Option B — Do A PLUS a best-effort R2-1 from the harvested macro IR + R2-2 chain
edges from existing surfaces, accepting it as partial (no menu captions; partial
chains) and NEW production code, to satisfy the spec's letter within this session.

Option C — Something else (state it precisely).

Weigh: spec letter vs. disproven premise; scope-discipline stop rule; the cost of
new partial code that the P4 set would rebuild; consumer value of partial vs.
deferred; risk of an unexplained golden delta. Which option, and why?
"""

results = {}
for model in ("gpt-5-4", "gemini-pro"):
    out_path = SET / f"s2-scope-consensus-{model}.md"
    try:
        r = query(model, content=PROMPT, task_type="analysis",
                  session_set=str(SET), session_number=2)
        # persist BEFORE any display side effect
        out_path.write_text(r.content, encoding="utf-8")
        results[model] = (r, out_path)
        print(f"[ok] {model}: {r.input_tokens} in / {r.output_tokens} out / ${r.cost_usd:.4f} -> {out_path}")
    except Exception as e:  # noqa: BLE001
        print(f"[ERR] {model}: {type(e).__name__}: {e}")

print("\n================ SAVED CONSENSUS OUTPUTS ================")
for model, (r, p) in results.items():
    print(f"\n----- {model} ({p}) -----")
    print(r.content)
