Here’s the shape I’d ship:

```yaml
delegation:
  always_route_task_types: [...]
  direct_work_max_lines: 120
  direct_work_max_files: 4

  decision_consensus:
    enabled: false
    categories:
      - design
      - architecture
      - process
      - api_surface
      - refactor_placement
      - scoping
      - testing_strategy
      - file_layout
    engines:
      - openai:gpt-5-4
      - google:gemini-pro
    unresolved_action: ask_user
```

## 1. Name + shape

Use `delegation.decision_consensus`, not a bare boolean.

Why:
- A boolean alone won’t age well; you already need at least:
  - whether it is on,
  - what kinds of questions it applies to,
  - what to do when consensus is weak.
- A numeric confidence threshold sounds attractive but is too squishy for V1. Model self-confidence is noisy; category + guardrails is more reliable.

Recommended fields:
- `enabled: false`
- `categories: [...]` — whitelist of eligible question types
- `engines: [...]` — ordered provider:model IDs
- `unresolved_action: ask_user` — explicit fallback policy

## 2. Location

Extend the existing top-level `delegation:` section.

Reason: this is still routing policy: “when may the orchestrator decide in-conversation vs. route reasoning elsewhere vs. ask the human.” It belongs beside `always_route_task_types` and the direct-work thresholds. A new top-level section would be justified only if you were introducing a broader decision-governance subsystem.

## 3. Default

**Opt-in** (`enabled: false`).

Reason:
- This changes user-facing behavior materially.
- Current repos should not silently stop asking humans.
- It preserves backward compatibility for existing orchestrator prompts and expectations.

Once enabled in a repo, it becomes consensus-by-default for the configured categories.

## 4. Consensus mechanism

Make `engines` configurable, with defaults of:
- `openai:gpt-5-4`
- `google:gemini-pro`

Do **not** inherit from `verification.preferred_pairings`.
- End-of-session verification and in-session decision support are different jobs.
- You may want different model pairs for each.

Behavior:
1. Orchestrator sends the same framing to both via `query()`.
2. It synthesizes the outputs.
3. If both answers are directionally aligned, act on the synthesis.
4. If they materially disagree, or the synthesis still depends on human-only information, follow `unresolved_action`.

For V1, `unresolved_action` should only support `ask_user`. No tiebreaker model yet; that adds latency, cost, and another policy axis.

## 5. Category boundary

Use a **runtime heuristic gated by a declarative whitelist**.

Rule:
- First, classify “human-only” questions.
- If not human-only, and the question falls into a configured `categories` bucket, run consensus.

### Consensus-eligible examples
- “Should this refactor live in the service layer or repository layer?”
- “Should this API expose one endpoint or two?”
- “Do we test this with unit tests, integration tests, or both?”
- “Should this new logic live in the existing module or a new file?”

### Human-only examples
- Business priority: “Which feature matters more this sprint?”
- Taste/preference: “Do you want terse or verbose UX copy?”
- Repo-external/private knowledge: “Is this partner integration still in use?”
- High-blast-radius/irreversible: “Should I delete the legacy migration history?”
- Unresolved ambiguity: consensus still depends on unstated product intent

So: categories are declarative; boundary enforcement is heuristic.

## 6. Interaction with `AskUserQuestion`

Yes, `AskUserQuestion` absolutely stays.

Decision order should be:
1. If clearly human-only → `AskUserQuestion`
2. Else if `decision_consensus.enabled` and category is eligible → dual-model consensus
3. Else → normal local/routed handling

The orchestrator should stop using `AskUserQuestion` as a lazy “options matrix” for design choices it can resolve itself.

Yes, add an audit trail. Humans should be able to inspect why a consensus-driven decision was made.

## 7. Cross-repo touchpoints

### In `dabbler-ai-orchestration` / router package
- Accept and validate `delegation.decision_consensus` in config schema
- Validate `engines` entries against supported provider:model syntax
- No `query()` API change required; orchestrator can call it twice
- Optional later: helper like `load_router_config()` or `query_many()`, but not required

### In this repo’s `docs/ai-led-session-workflow.md`
Add:
- the decision tree above,
- examples of eligible vs. human-only questions,
- requirement to synthesize into one recommendation,
- fallback behavior on disagreement,
- logging/journaling expectations.

### In `CLAUDE.md` / `AGENTS.md` / `GEMINI.md`
Add an instruction like:
> When `delegation.decision_consensus.enabled` is true, do not use `AskUserQuestion` for non-trivial design/architecture/process decisions unless the issue is human-only or the dual-model synthesis remains unresolved.

## 8. Observability

Yes: log to a structured journal, e.g. `.dabbler/consensus-decisions.jsonl`.

Fields:
- `timestamp`
- `session_id`
- `category`
- `question_summary`
- `question_hash` or prompt digest
- `engines`
- `engine_1_summary`
- `engine_2_summary`
- `agreement_level` (`aligned`, `partial`, `conflict`, `degraded`)
- `chosen_recommendation`
- `applied` (bool)
- `fallback_action` (`none`, `ask_user`)
- `fallback_reason`
- `files_impacted` (if known)

Don’t require full raw prompts/responses in the journal by default; summaries + hashes are usually enough and safer.

**Minimum viable shape:** add `delegation.decision_consensus.enabled: false` plus `categories`, defaulting to a fixed dual-provider pair of `openai:gpt-5-4` and `google:gemini-pro`, with `unresolved_action: ask_user`. That is the smallest config surface that lets the orchestrator replace most design/process `AskUserQuestion` matrices with two-model synthesis while still preserving human escalation for real human-only calls.