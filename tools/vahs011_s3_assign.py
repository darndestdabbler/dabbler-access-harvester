import os
from pathlib import Path
os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")
from ai_router import route
SET = Path("docs/session-sets/011-resolver-and-navigation-wiring")
PROMPT = """Recommend the cheapest CAPABLE orchestrator (engine/model/effort) for Session 3
of set 011 (a C# .NET Access-metadata harvester, AI-led session workflow). Output a
short rationale (2-3 sentences) + a one-line 'RECOMMENDATION: <engine> <model> @ effort=<level>'.

Context: Session 3 is the final/closeout session. Its ORIGINAL spec scope was
"R2-1/R2-2 consumer validation + docs + coordinated closeout", but R2-1/R2-2 were
DEFERRED to a future P4 set in Session 2 (their data is not harvested; needs new COM
surface). So Session 3's ACTUAL scope is now: (1) validate the Session-2-delivered
navigation-graph + unresolved-items + enrichment surfaces against a FRESH tool run;
(2) document the navigation-graph consumer surface in the interpretation guide;
(3) author the next session set (the P4 switchboard-and-report-chain-edges set) as a
spec + not-started v4 state stub; (4) coordinated closeout (disposition, change-log,
next-set recommendation, close_session) with closeout-grade cross-provider verification.
This is documentation + validation + spec-authoring synthesis (moderate), not novel
algorithm design. Available models: claude-opus-4-8 (tier3), claude-sonnet-4-6 (tier2),
gpt-5-4 (tier3, OpenAI), gemini-pro (tier3, Google). The session-verification router
override is calibrated to gpt-5-4 for a Claude orchestrator; if a Google orchestrator
is picked, the verifier override must change. Anthropic effort levels: low|medium|high|max."""
r = route(content=PROMPT, task_type="analysis", session_set=str(SET), session_number=2)
out = SET / "s3-orchestrator-recommendation.md"
out.write_text(r.content, encoding="utf-8")
print(f"[ok] {r.model_name} ${r.cost_usd:.4f} -> {out}")
print(r.content)
