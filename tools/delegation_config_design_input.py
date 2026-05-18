"""Cross-engine input on a proposed delegation config option.

User feedback (2026-05-17, saved as feedback_seek_ai_consensus_before_asking_human):
"Whenever you want input from the human on non-trivial design/architecture/
process-related things, please just get input from GPT and Gemini and then
use your best judgment rather than bothering me with a whole bunch of
questions that AI should be able to figure out."

User follow-up: "Perhaps we should generate a suggested configuration
option for the AI Orchestrator -- which would allow humans to delegate
that kind of question-handling to AI consensus/orchestrator best
judgment."

This script routes the design question to gpt-5-4 + gemini-pro and saves
both responses for synthesis into a proposal artifact.
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import query

OUT_DIR = Path("docs/strategy-input")
OUT_DIR.mkdir(parents=True, exist_ok=True)

framing = """\
You are designing a new configuration option for the Dabbler AI
orchestrator workflow. The orchestrator runs in a session-set model
described at `docs/ai-led-session-workflow.md`: an AI coding agent
(Claude Code / Codex / Gemini) executes a planned body of work
session-by-session, routing reasoning tasks to a cheaper external
model via the `dabbler-ai-router` Python package, and ending each
session with mandatory cross-provider verification.

The router's existing configuration lives at
`ai_router/router-config.yaml` (workspace-relative; the router itself
is a pip-installed package). The YAML already has a top-level
`delegation:` section with `always_route_task_types`,
`direct_work_max_lines`, and `direct_work_max_files` keys that
govern when the orchestrator may handle a task in-conversation vs.
must route through the AI router.

# The new behavior the human wants

When the orchestrator encounters a **non-trivial design /
architecture / process question** during a session (spec-shape
choices, refactor placement, API surface decisions, scoping calls,
testing-strategy picks, file-layout decisions, similar), the human
wants the orchestrator to:

1. Route the same framing to gpt-5-4 (OpenAI) AND gemini-pro
   (Google) via the router's `query()` function.
2. Synthesize both responses.
3. Apply best judgment (act on the synthesis, or surface the
   synthesized recommendation as a single concrete recommendation).

…rather than presenting the human with a multiple-choice
`AskUserQuestion` matrix of options the AI could have figured out.

The human still wants to be asked when human input is genuinely
required: explicit business priority, taste calls, things only
the human knows, high-blast-radius irreversible actions, or
ambiguity the consensus didn't resolve.

# The design question

Propose a configuration option (or small set of options) that
captures this behavior. Be concrete: name(s), YAML shape, default
value(s), placement in `router-config.yaml`, how the orchestrator
discovers and interprets the option, and the documentation /
workflow-doc updates needed to make the option real.

Specifically answer:

1. **Name + shape.** What should the config key(s) be called?
   Single boolean toggle (`delegate_design_questions_to_consensus:
   true`)? A list of categories (`auto_delegate_categories: [design,
   architecture, refactor-placement, ...]`)? A threshold / confidence
   number? A combination? Justify the chosen shape.

2. **Location.** Does this extend the existing `delegation:` section
   or warrant a new top-level section? Why?

3. **Default.** Opt-in (asking-the-human remains the default behavior)
   or opt-out (consensus-by-default once the human flips the switch
   on this repo)? Why?

4. **Consensus mechanism.** Should the engines be fixed (gpt-5-4 +
   gemini-pro), configurable (a separate `consensus_engines:` list),
   or inherited from the existing `verification.preferred_pairings`
   map? What happens when the engines disagree (tiebreaker model?
   surface to human? pick one canonically?)?

5. **Category boundary.** How does the orchestrator decide a question
   is "non-trivial design / architecture / process" vs "human-only"?
   Is this a runtime heuristic the orchestrator applies, or a
   declarative list in the config? Examples of each category would
   help.

6. **Interaction with `AskUserQuestion`.** Should `AskUserQuestion`
   still be available for genuine human-only calls? How does the
   orchestrator decide which tool to reach for? Should there be a
   logging affordance so the human can audit consensus-driven
   decisions after the fact?

7. **Cross-repo touchpoints.** The router is a pip-installed package
   at `dabbler-ai-orchestration`. Workflow behavior is documented in
   this repo's `docs/ai-led-session-workflow.md`. Which changes need
   to land in the router package (e.g., schema acceptance) vs. in
   the workflow doc (orchestrator-side behavior) vs. in the
   per-agent instruction files (CLAUDE.md / AGENTS.md / GEMINI.md)?

8. **Observability.** Should consensus-driven decisions be logged
   to a structured journal (e.g., `consensus-decisions.jsonl`)?
   What fields would such a journal need to be useful?

Concrete + opinionated beats exhaustive. Keep your response under
~900 words. End with a one-paragraph "minimum viable shape" summary
naming the smallest version of the option that delivers the value.
"""

print("=" * 72)
print("Routing to gpt-5-4 (OpenAI)...")
gpt_result = query(
    model="gpt-5-4",
    content=framing,
    task_type="analysis",
    session_set="post-close-strategy-input",
    session_number=0,
)
gpt_path = OUT_DIR / "delegation-config-gpt-5-4.md"
gpt_path.write_text(gpt_result.content, encoding="utf-8")
print(f"gpt-5-4: ${gpt_result.cost_usd:.4f}, "
      f"in/out tokens: {gpt_result.input_tokens}/{gpt_result.output_tokens}")
print(f"Saved to {gpt_path}")
print()

print("=" * 72)
print("Routing to gemini-pro (Google)...")
gemini_result = query(
    model="gemini-pro",
    content=framing,
    task_type="analysis",
    session_set="post-close-strategy-input",
    session_number=0,
)
gemini_path = OUT_DIR / "delegation-config-gemini-pro.md"
gemini_path.write_text(gemini_result.content, encoding="utf-8")
print(f"gemini-pro: ${gemini_result.cost_usd:.4f}, "
      f"in/out tokens: {gemini_result.input_tokens}/{gemini_result.output_tokens}")
print(f"Saved to {gemini_path}")
print()

print("=" * 72)
total = gpt_result.cost_usd + gemini_result.cost_usd
print(f"Total routed cost: ${total:.4f}")
