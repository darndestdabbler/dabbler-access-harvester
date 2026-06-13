"""008 Session 3 closeout cross-provider verification. gpt-5-4 (cross-provider
for a Claude orchestrator). Persist before print (cp1252 guard)."""
from ai_router import query

SETDIR = "docs/session-sets/008-manifest-vba-ingestion-and-parser-hardening"
with open(f"{SETDIR}/s3-verification-prompt.md", encoding="utf-8") as f:
    prompt = f.read()

r = query("gpt-5-4", prompt, task_type="session-verification",
          session_set="008-manifest-vba-ingestion-and-parser-hardening",
          session_number=3)
out = f"{SETDIR}/s3-verification.md"
with open(out, "w", encoding="utf-8") as f:
    f.write(r.content or "(empty)")
print(f"OK gpt-5-4 -> {out} (cost=${getattr(r,'cost_usd',0):.4f} "
      f"truncated={getattr(r,'truncated','?')})")
