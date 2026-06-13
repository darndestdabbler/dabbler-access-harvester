"""Route the ai-assignment.md analysis for set 011 (Session 1 actuals +
Session 2 recommendation). Persist-before-display."""
import os
from ai_router import route

SESSION_SET = "docs/session-sets/011-resolver-and-navigation-wiring"
OUT = os.path.join(SESSION_SET, "ai-assignment-analysis.md")

CONTENT = r"""
Recommend the cheapest CAPABLE orchestrator for Session 2 of session set
011-resolver-and-navigation-wiring, and a one-sentence rationale. Output just the
recommendation + rationale (engine, model, effort).

Set context: 3-session Full-tier set wiring the harvester's built-but-unwired
capabilities into production output. No UI (gate = build + tests + cross-provider
verification).

Session 1 (JUST COMPLETED, F-3-4 resolver wiring): orchestrated by Claude Code
Opus 4.8 @ effort=medium. Work: a two-pass shared-ProjectScope restructure of the
manifest parse loop; building the AccessObjectModelRegistry from ir metadata +
Form/Report owningClass; a new DefaultMemberRegistry consultation in the resolver
(new SyntheticDefaultMemberDeclaration emitted via the existing ResolvedToObjectModel
variant — design chosen by GPT-5.4+Gemini-Pro consensus); omitting SqlStrings;
coordinated COM golden regen (every delta mapped to F-3-4); cross-provider
verification reached VERIFIED at R2. It was a synthesis-heavy session (closed-type
design judgment, golden-delta attribution). Routed spend ~ $0.32 (item-3 design
consensus $0.084 + verification $0.240).

Session 2 (NEXT, F-2-2 navigation & enrichment wiring): wire the Phase 6 substrate
into the pipeline off the existing resolvedModules + crossRefs seam —
NavigationGraphBuilder + UnresolvedItemAggregator (populate manifest.NavigationGraph
and unresolved-items.json), EventHandlerClassifier / ValidationRuleExtractor /
RowSourceSpecBuilder (classification content), a new "Switchboard Items" table reader
(R2-1), and dialog->query->report chain derivation (R2-2). Also reconcile the
hermetic EndToEndFixtureTests (which exercised the unwired substrate) with the now-
wired path, and a coordinated golden regen. This is integration wiring + a new
table reader + test reconciliation — substantial, with moderate design surface
(graph-edge derivation) but mostly known-pattern wiring.

Consider: synthesis/judgment depth needed, the cross-provider verification is
mandatory regardless, and the human runs Claude/Codex/Gemini orchestrators
interchangeably.
"""


def main() -> None:
    r = route(content=CONTENT, task_type="analysis", session_set=SESSION_SET, session_number=1,
              context="Author the next-session orchestrator recommendation for ai-assignment.md.")
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
