"""Author ai-assignment.md for vba-lexical-and-expression-grammar via route(analysis)."""
from __future__ import annotations
import importlib.util, sys, pathlib

REPO = pathlib.Path(__file__).resolve().parents[2]


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router",
        str(REPO / "ai-router" / "__init__.py"),
        submodule_search_locations=[str(REPO / "ai-router")],
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


SESSION_SET = REPO / "docs" / "session-sets" / "vba-lexical-and-expression-grammar"
SPEC = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
ASSIGNMENT_PATH = SESSION_SET / "ai-assignment.md"

PROMPT = f"""You are authoring an AI-assignment ledger for a 4-session set in
the dabbler-access-harvester repo. The goal is to recommend, per session,
the cheapest orchestrator + per-step model that can safely complete the
work. Output is a markdown file the human will read and use to choose
the orchestrator for each session.

## Calibration philosophy

- Mechanical work (file edits, namespace rewrites, csproj boilerplate,
  git, build/test, dispatch + logging) -> cheapest orchestrator that can
  drive file/shell tools reliably (Sonnet 4.6 at low/medium effort
  usually sufficient).
- Routed reasoning -> use existing router-config.yaml task_type_overrides
  and task_type_params. Override via complexity_hint only when the spec
  describes work materially larger or smaller than the task type's
  default.
- Synthesis (architecture, parser-grammar design, novel test-case
  authoring, plan generation) -> top tier (Opus 4.7).

## Repo-specific calibration constraints (must be honored)

- Synthesis-document work above ~3K input tokens via
  `route(task_type=architecture)` to Opus is documented as unreliable
  (timeouts at provider boundary). The repo's mitigation is to use Opus
  AS the orchestrator and author in-conversation when the synthesis
  matches the routing target. This applies to Sessions 1 and 2 of this
  set, both of which call for grammar synthesis.
- `session-verification` is non-negotiable cross-provider. The override
  in router-config.yaml routes session-verification to gpt-5-4 when the
  orchestrator is a Claude model. If the human swaps to a Codex/Gemini
  orchestrator, swap the override before starting.
- Mechanical-correspondence task types (e.g., schema-validation,
  AST-vs-source diffing, parse-tree shape comparison) pin to Sonnet at
  default effort. Session 3's `task_type=schema-validation` is
  mechanical correspondence.
- `route(task_type=summarization)` is for narrative prose. Session 4's
  change-log generation should use summarization with `max_tier=1`
  fallback if Gemini Pro 400s.

## Session set spec

```markdown
{SPEC}
```

## Output format

Produce a markdown file with this header:

```markdown
# AI Assignment Ledger - vba-lexical-and-expression-grammar

> **Authored:** 2026-04-25 by route(task_type='analysis').
> **Updated after each session:** the orchestrator appends an "Actuals"
> block under each session row once it completes, and updates the next
> session's row if the actuals reveal a miscalibration.
```

Then for each of the 4 sessions, produce a section with:

- `## Session N of 4: <title>`
- `**Recommended orchestrator:** <engine> <model> @ effort=<low|medium|high|max>`
- `**Rationale:** <2-sentence justification grounded in the spec's step list>`
- `**Estimated routed cost:** $<X.XX>` (rough; cite which calls drive it)
- A markdown table with columns: `Step | Type | route() task_type | Model | Effort/thinking | Rationale`
- An empty `**Actuals (filled after the session runs):**` block with bullet placeholders for orchestrator used, total routed cost, deviations, notes for next-session calibration.

Be concrete: name actual models (gemini-flash, gemini-pro, sonnet, opus,
gpt-5-4, gpt-5-4-mini) and effort/thinking levels per task_type_params
in router-config.yaml. For Sessions 1 and 2 specifically, recommend
Opus 4.7 as the orchestrator and note that the synthesis call is
in-conversation per the convention rather than a route(architecture).
"""


def main() -> None:
    ar = load_ai_router()
    result = ar.route(
        content=PROMPT,
        task_type="analysis",
        complexity_hint=55,
        session_set=str(SESSION_SET),
        session_number=1,
    )
    # Persist BEFORE any print/log side effects (per lessons-learned).
    ASSIGNMENT_PATH.write_text(result.content, encoding="utf-8")
    print(f"Wrote {ASSIGNMENT_PATH}")
    print(f"Model: {result.model_name}")
    print(f"Tokens in/out: {result.input_tokens} / {result.output_tokens}")
    print(f"Cost: ${result.cost_usd:.4f}")


if __name__ == "__main__":
    main()
