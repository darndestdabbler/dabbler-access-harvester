"""Set 015 Session 1 closeout: route the Session-2 orchestrator recommendation
(do-not-self-opine) and assemble ai-assignment.md. Persist-before-display."""
from pathlib import Path
from ai_router import route

SS = Path("docs/session-sets/015-unresolved-items-completeness")


def main():
    prompt = """Recommend the orchestrator (engine + model + effort) for Session 2 of 2 of the
session set `015-unresolved-items-completeness` (a C#/.NET 10 toolchain that MECHANICALLY
harvests Microsoft Access metadata for a downstream AI consumer). Do NOT self-opine on cost;
give a routed, reasoned recommendation.

Session 1 (just completed) closed **DW-11 Layer A**: procedure-HEADER unresolved references
(an Optional parameter's default expression) now reach `unresolved-items.json` via a
dedup-additive feed (the aggregator additionally consumes the full procedure-scoped resolver
reference set and suppresses the body overlap on a member-name-aware key, non-mutating dedup).
Proven golden-neutral byte-level on HomeHealthCare (no regen). Routed gpt-5-4: R1 found a real
Major on the dedup seam (intra-feed over-suppression of chained header refs) -> fixed -> R2
VERIFIED.

Session 2 of 2 (per spec) is materially LARGER and HIGHER-RISK — **DW-11 Layer B**:
module-level unresolved references (`Const`/`Dim` initializers, `Implements <unresolved>`,
module `Private x As New <unresolved>`) are dropped at projection because the v1.0 ParserOutput
schema has no module-level item type. Session 2 must: (1) DECIDE THE SMALLEST CORRECT SCHEMA
CHANGE explicitly (a manifest home for module-level refs) — applying the "bump the sub-document
version, not the envelope, when the host $def is additionalProperties-tolerant" lesson; (2) stop
dropping module-level refs at projection and route them to the new home; (3) ensure the
aggregator surfaces them WITHOUT double-counting Layer A's procedure-scoped refs; (4) pin both;
(5) COORDINATED HHC GOLDEN REGEN (the schema/shape change is likely golden-affecting — explain
every delta or prove byte-level no-regen); (6) closeout-grade cross-provider verification
(prose-first + trailing JSON, EMBED the Session-1 Layer A code + test snippet per the
cross-session-evidence convention since this is the FINAL session); (7) AUTHOR THE NEXT SESSION
SET (or a recorded consumer-driven-only handoff — DW-11 closes here); (8) discharge DW-11 in
deferred-work.md.

Output 2-4 sentences: recommended orchestrator (engine + model + effort) + the single
highest-leverage reason."""
    r = route(content=prompt, task_type="analysis", complexity_hint=45,
              session_set=str(SS), session_number=1)
    (SS / "ai-assignment-analysis-s1.md").write_text(
        f"# Routed next-orchestrator analysis (S1 -> S2)\n\nModel: {r.model_name} | "
        f"cost ${r.cost_usd:.4f}\n\n{r.content}\n", encoding="utf-8")
    print(f"NEXT-ORCH ({r.model_name}, ${r.cost_usd:.4f}):\n{r.content}\n")


if __name__ == "__main__":
    main()
