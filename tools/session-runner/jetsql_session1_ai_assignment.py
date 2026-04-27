"""Session 1 of jet-sql-grammar-and-parser: produce ai-assignment.md via route(analysis)."""
import sys, importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "ai_router", "ai-router/__init__.py", submodule_search_locations=["ai-router"])
mod = importlib.util.module_from_spec(spec); sys.modules["ai_router"] = mod
spec.loader.exec_module(mod)
route = mod.route
SessionLog = mod.SessionLog

SESSION_SET = Path("docs/session-sets/jet-sql-grammar-and-parser")
log = SessionLog(SESSION_SET)

prompt = """You are authoring the AI-Assignment Ledger for session set
`jet-sql-grammar-and-parser` (Phase 5.1-5.2 of the harvester plan).
This is a 4-session set. Below is the spec; produce the ledger in the
exact markdown schema specified in `docs/ai-led-session-workflow.md`
Step 3.5 for ALL FOUR sessions.

Available orchestrator engines: claude-code (Opus 4.7 / Sonnet 4.6),
codex (gpt-5.4 / gpt-5.4-mini), gemini-cli (gemini-2.5-pro). Effort
levels for Anthropic: low|medium|high|max. For OpenAI:
low|medium|high|xhigh. For Gemini: thinking_budget int or thinking_level.

Available route() task types and typical costs (per `router-config.yaml`):
- formatting / summarization (Gemini Flash, ~$0.001-0.01)
- documentation / test-generation (Gemini Pro / Sonnet, ~$0.05)
- code-review / analysis / refactoring (Sonnet or Gemini Pro, ~$0.10)
- session-verification (gpt-5-4, mandatory cross-provider, ~$0.30-1.00)
- architecture / planning / security-review (Opus, ~$0.50-2.00)
- schema-validation pinned to Sonnet for mechanical-correspondence

Calibration philosophy:
- Synthesis (architecture, parser-grammar, novel test authoring) -> Opus
  (or Opus-as-orchestrator in-conversation per the
  "Opus At effort:max + Adaptive Thinking Timeouts" Convention; for
  synthesis-document above ~3K input tokens, prefer in-conversation).
- Mechanical (schema validation, AST-vs-source diffing) -> Sonnet.
- Mechanical orchestration (file copy, build, csproj wiring) ->
  Sonnet 4.6 at low/medium effort orchestrator is often sufficient.

SPEC:
========
""" + (SESSION_SET / "spec.md").read_text(encoding="utf-8") + """
========

Output ONLY the markdown ledger; no surrounding commentary. Follow the
schema in workflow doc Step 3.5: a Session N of M block per session
including Recommended orchestrator, Rationale, Estimated routed cost,
a per-step table (Step | Type | route() task_type | Model | Effort/thinking | Rationale),
and an empty Actuals stub.
"""

result = route(
    content=prompt, task_type="analysis", complexity_hint=55,
    session_set=str(SESSION_SET), session_number=1,
)
out_path = SESSION_SET / "ai-assignment.md"
with open(out_path, "w", encoding="utf-8") as f:
    f.write(result.content)
print(f"WROTE {out_path}  model={result.model_name}  cost=${result.cost_usd:.4f}  in={result.input_tokens} out={result.output_tokens}")

log.log_step(
    1, 1, "session-1/author-ai-assignment",
    "Authored ai-assignment.md via route(analysis)", "complete",
    [{"model": result.model_name, "taskType": "analysis",
      "inputTokens": result.input_tokens, "outputTokens": result.output_tokens,
      "costUsd": result.cost_usd}],
)
print("LOGGED")
