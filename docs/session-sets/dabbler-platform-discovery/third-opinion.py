"""Route the Issue 4 disagreement (sample .rdf.json snippet copying) to a
third-party tiebreaker model and record the adjudication.

Original generator: gemini-pro (Google)
Original verifier:  gpt-5-4    (OpenAI)
Tiebreaker:         claude-opus-4-7 via direct query() (Anthropic) — third
                    provider, not the generator or the original verifier.
"""
from __future__ import annotations
from pathlib import Path

from ai_router import query
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/dabbler-platform-discovery")
SPEC_TEXT = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
SCHEMA_DOC = (Path("docs/cross-repo/dabbler-platform-report-rdf-schema.md")).read_text(encoding="utf-8")

PROMPT = f"""You are an independent tiebreaker on a session-set verification
disagreement. Read the source spec carefully and judge a specific finding
on its merits.

# The spec

The session-set spec for `dabbler-platform-discovery` (read-only
discovery of the sibling `dabbler-platform` repo, output produced as
structured Markdown docs under `docs/cross-repo/` in the harvester
repo) sets these boundaries:

  > **Boundaries:**
  > - **Read-only on `../dabbler-platform` and `../dabbler-pdf`.** This set
  >   must not edit files in those repos. If an obvious fix or note would
  >   benefit the platform repo, surface it to the human at session end —
  >   the human applies it there manually.
  > - **No copying of platform source code into this repo.** The output is
  >   a *map* of the platform, not a mirror of it. Cite paths and commit
  >   SHAs; do not fork content.
  > - **Documentation, not implementation.** No C# / Blazor code lands here
  >   from this set.

And the Feature 2 deliverable scope:

  > The platform's `report.rdf.json` schema is purposely higher-level than a
  > syntax tree (per the human's clarification on 2026-04-27) and is the
  > most likely seam between harvested Access reports and platform-rendered
  > Blazor reports. Document the schema's shape, every field's purpose, and
  > example documents — at sufficient fidelity that an interpretation agent
  > in this repo could *generate* a valid `report.rdf.json` from a harvested
  > Access report's `ir/` representation.

# The finding under dispute

The Session 1 verifier (gpt-5-4) flagged this as Major:

  Location: docs/cross-repo/dabbler-platform-report-rdf-schema.md — example
            snippet blocks in `Metadata`, `Data Sources`, `Parameters`,
            `Fields`, `Grouping`, `Bands`, and `Elements`

  Summary:  The document copies sibling-repo sample `.rdf.json` content
            into this repo, which violates the spec boundary forbidding
            copied platform source artifacts.

  Fix:      Replace verbatim sample snippets from ../dabbler-platform with
            synthetic minimal examples authored in this repo or with prose
            descriptions that cite the source paths without reproducing
            their content.

The orchestrator (gemini-pro produced the deliverable; the orchestrator
is claude-opus-4-7) disagrees, reading:

  - "No copying of platform source code" = no copying of C#/Blazor
    implementation code; JSON-fixture data is documentation-class.
  - Feature 2 explicitly asks for "example documents" and "sufficient
    fidelity that an interpretation agent could generate a valid
    report.rdf.json." Synthetic examples authored fresh in this repo
    by the same model that struggles to interpret the schema may not
    constitute genuine grounding for downstream agents.
  - Each example snippet is cited with its sibling-repo path so a
    consumer can trace it back to the canonical source.

# The actual schema doc

There are 7 quoted JSON snippets in the doc, each ranging from 3 to
roughly 20 lines, and each tagged with a source path like:

  **Example Snippet** (`src/Apps/AcmeCoffeeOnline.Web/Reports/orders-by-customer.rdf.json`):
  ```json
  {{ ...JSON fragment... }}
  ```

Full doc follows for context (do NOT re-summarize the doc; focus on the
disputed practice):

---SCHEMA DOC START---
{SCHEMA_DOC}
---SCHEMA DOC END---

# What to decide

Apply the spec's plain reading. Pick ONE of these positions and
explain your reasoning in 2-4 paragraphs. End with a one-line verdict
that begins with `VERDICT:` followed by `A`, `B`, or `C`.

  A. The verifier is correct. Even small, cited JSON snippets from
     the sibling repo are "forking content" under the spec's
     boundary, and example documents must be synthetic.

  B. The orchestrator is correct. Small, cited JSON fixtures are
     documentation-class material that Feature 2's "example documents"
     deliverable plainly contemplates. The current document satisfies
     the spec.

  C. The right answer is a middle reading. Specify how (e.g., shorter
     snippets, prose descriptions plus a path pointer, synthetic
     examples plus a path pointer to the canonical sample).

Be specific. If you pick A, name the spec sentence that you read as
prohibiting the practice. If you pick B, name the spec sentence you
read as authorizing it. If you pick C, name your preferred rewrite
pattern in one sentence.
"""

result = query(
    model="opus",
    content=PROMPT,
    task_type="session-verification",
    session_set=str(SESSION_SET),
    session_number=1,
)

# Persist BEFORE printing per the cp1252 lesson
out_path = SESSION_SET / "third-opinion-issue-4.md"
out_path.write_text(
    f"# Third opinion on Issue 4 (sample .rdf.json copying)\n\n"
    f"Model: {result.model_name}\n"
    f"Cost: ${result.cost_usd:.4f}\n\n"
    f"---\n\n"
    f"{result.content}\n",
    encoding="utf-8",
)

log = SessionLog(SESSION_SET)
log.log_step(
    session_number=1,
    step_number=7,
    step_key="session-1/issue-4-third-opinion",
    description="Third-opinion adjudication on Issue 4 (sample .rdf.json copying) via query(opus)",
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "session-verification",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)

print(f"Wrote {out_path}  ({result.model_name}, ${result.cost_usd:.4f})")
print("--- last 1200 chars of response ---")
print(result.content[-1200:])
