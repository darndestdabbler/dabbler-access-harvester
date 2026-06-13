"""Route the F-3-9 conformance-contract trade-off to GPT-5.4 and Gemini Pro for
decision-time consensus. Persist each response to disk BEFORE any print
(cp1252 crash guard). Run from repo root with the workspace venv."""
import sys
from ai_router import query

SETDIR = "docs/session-sets/008-manifest-vba-ingestion-and-parser-hardening"
with open(f"{SETDIR}/s3-f39-consensus-prompt.md", encoding="utf-8") as f:
    prompt = f.read()

targets = [
    ("gpt-5-4", f"{SETDIR}/s3-f39-consensus-gpt-5.4.md"),
    ("gemini-pro", f"{SETDIR}/s3-f39-consensus-gemini.md"),
]

for model, outpath in targets:
    try:
        r = query(model, prompt, task_type="analysis",
                  session_set="008-manifest-vba-ingestion-and-parser-hardening",
                  session_number=3)
        with open(outpath, "w", encoding="utf-8") as f:
            f.write(r.content or "(empty)")
        print(f"OK {model} -> {outpath}  (cost=${getattr(r, 'cost_usd', 0):.4f} "
              f"truncated={getattr(r, 'truncated', '?')})")
    except Exception as e:
        with open(outpath, "w", encoding="utf-8") as f:
            f.write(f"ERROR routing to {model}: {e}")
        print(f"ERR {model}: {e}", file=sys.stderr)
print("F39 consensus done.")
