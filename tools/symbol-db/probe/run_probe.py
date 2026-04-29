"""Route the comprehension probe prompt to two pinned providers.

The third opinion (Anthropic Opus) is authored in-conversation by the
session-7 orchestrator per the in-conversation convention; this script
covers gemini-pro and gpt-5-4. Each provider response is persisted to
disk verbatim in `docs/session-sets/vba-symbol-db/probe-results/`,
along with the routed metadata (model, tokens, cost, elapsed).
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
PACKET = REPO_ROOT / "docs" / "session-sets" / "vba-symbol-db" / "probe-results" / "comprehension-packet.md"
OUT_DIR = REPO_ROOT / "docs" / "session-sets" / "vba-symbol-db" / "probe-results"
SESSION_SET = "docs/session-sets/vba-symbol-db"
SESSION_NUMBER = 7


def _load_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        str(REPO_ROOT / "ai-router" / "__init__.py"),
        submodule_search_locations=[str(REPO_ROOT / "ai-router")],
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


PROBE_PROMPT_HEADER = """\
You are reading a per-procedure view assembled from a SQLite "symbol database"
that the harvester built for a Microsoft Access application
(NorthwindDevEdition.accdb). The symbol DB indexes every VBA symbol in two
modules from that DB (Form_frmCompanyDetail, modStrings), every reference
edge between symbols, every form/control/event binding, and every lookup
table — for one canonical fixture.

The procedure below is the comprehension-probe target. Its own
`Interpretation*` columns are intentionally blanked. Every directly-referenced
symbol it can see in the symbol DB carries the AI-written `Interpretation1`
that Session 4 produced, so you can lean on callee summaries instead of
re-reading callee source.

YOUR TASK
=========

Working ONLY from this packet (do not invent context that is not present
here), tell us:

1. **What does the procedure do?** Plain prose, 2-4 sentences.
2. **What are its preconditions?** What state must hold before it runs.
3. **What side effects does it have?** UI, DB, message-box, navigation,
   etc. Be specific about which controls / forms / records change.
4. **What does the user see?** A user-visible-behavior description.
5. **Where is the symbol DB MISSING information you wished you had?**
   List symbols, forms, tables, queries, or relationships you needed but
   could not find. Distinguish "would have been useful" from "blocking".
6. **Confidence** (0.0-1.0) in your answer to questions 1-4, and a
   one-sentence justification.

Do NOT pretend to know what an unresolved identifier means if the packet
gives you no evidence — flag it as "no evidence in packet" and explain
what you would have to assume to interpret it.

After your prose answer, output a JSON block in this exact format:
```json
{
  "what_it_does": "<2-4 sentences>",
  "preconditions": ["<bullet>", "..."],
  "side_effects": ["<bullet>", "..."],
  "user_visible_behavior": "<1-3 sentences>",
  "missing_from_symbol_db": [
    {"item": "<symbol/table/etc.>", "severity": "blocking | useful", "why": "<one sentence>"}
  ],
  "confidence": 0.0,
  "confidence_justification": "<one sentence>"
}
```

If you cannot answer a question from the packet, set the field to the
string "insufficient evidence in packet" rather than guessing.

================================================================
PACKET
================================================================

"""


def build_prompt() -> str:
    packet = PACKET.read_text(encoding="utf-8")
    return PROBE_PROMPT_HEADER + packet


def _save_response(provider_slug: str, prompt: str, result, elapsed: float) -> Path:
    md_path = OUT_DIR / f"probe-{provider_slug}.md"
    meta_path = OUT_DIR / f"probe-{provider_slug}.meta.json"
    md_path.write_text(result.content, encoding="utf-8")
    meta = {
        "provider_slug": provider_slug,
        "model_name": result.model_name,
        "task_type": "analysis",
        "input_tokens": result.input_tokens,
        "output_tokens": result.output_tokens,
        "cost_usd": result.cost_usd,
        "elapsed_seconds": elapsed,
        "prompt_chars": len(prompt),
    }
    meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")
    return md_path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--providers",
        nargs="+",
        default=["gemini-pro", "gpt-5-4"],
        help="model slugs to query() (default: gemini-pro gpt-5-4)",
    )
    args = parser.parse_args()

    if not PACKET.exists():
        raise SystemExit(f"comprehension packet not found: {PACKET}. Run build_packet.py first.")
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    ar = _load_router()
    prompt = build_prompt()
    print(f"prompt size: {len(prompt):,} chars")

    for model in args.providers:
        print(f"--- routing to {model} ---")
        t0 = time.time()
        # Persist content to disk BEFORE printing per the convention.
        try:
            result = ar.query(
                model=model,
                content=prompt,
                task_type="analysis",
                session_set=SESSION_SET,
                session_number=SESSION_NUMBER,
            )
        except Exception as exc:
            print(f"!! {model} failed: {exc}")
            continue
        elapsed = time.time() - t0
        path = _save_response(model.replace("/", "-"), prompt, result, elapsed)
        print(f"wrote {path} | model={result.model_name} | tokens={result.input_tokens}/{result.output_tokens} | cost=${result.cost_usd:.4f} | {elapsed:.1f}s")


if __name__ == "__main__":
    main()
