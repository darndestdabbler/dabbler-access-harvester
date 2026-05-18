"""Verifier fallback (step 2 of escalation ladder): GPT-5.4 returned 429
three times. Re-route to gemini-pro to keep cross-provider rigor without
the OpenAI rate-limit blocker.
"""
from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import query
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
SESSION_NUMBER = 1

# Reuse the same prompt as the original verifier driver
import importlib.util
spec = importlib.util.spec_from_file_location(
    "hfclip_session1_verify",
    Path("tools/hfclip_session1_verify.py").resolve(),
)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
PROMPT = mod.PROMPT


def main() -> int:
    out_path = SESSION_SET / "session-reviews" / "session-001.md"
    out_path.parent.mkdir(parents=True, exist_ok=True)

    result = query(
        model="gemini-pro",
        content=PROMPT,
        task_type="session-verification",
        context="harvester-finish-cli-packaging Session 1 — GPT-5.4 fallback to Gemini Pro after 429",
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )

    out_path.write_text(result.content, encoding="utf-8")

    print(f"[wrote {out_path}] model={result.model_name} "
          f"tokens={result.input_tokens}+{result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")

    log = SessionLog(str(SESSION_SET), total_sessions=2)
    log.log_step(
        session_number=SESSION_NUMBER,
        step_number=8,
        step_key="session-1/cross-provider-verification",
        description=(
            f"Cross-provider verification via query(model='gemini-pro', "
            f"task_type='session-verification') — fallback after GPT-5.4 "
            f"returned 429 Too Many Requests three times (verifier-failure "
            f"escalation ladder step 2)."
        ),
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    log.save_session_review(
        session_number=SESSION_NUMBER,
        review_text=result.content,
        round_number=1,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
