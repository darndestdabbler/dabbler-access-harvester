Here is a design proposal for the new configuration option.

### 1. Name + Shape

The configuration will be a single dictionary key named `decision_consensus`. A dictionary is superior to a simple boolean because it groups all related configuration (the models, the categories, the kill-switch) into a single, self-describing block.

**Justification:**

*   **Clarity:** `decision_consensus` explicitly describes the feature's purpose.
*   **Scalability:** A dictionary shape allows for future extension (e.g., adding a `tiebreaker_engine` or `synthesis_prompt`) without breaking the schema.
*   **Declarative Control:** A list of `delegate_categories` is more reliable than a runtime heuristic. It makes the agent's behavior deterministic based on the configuration and provides a clear contract for what gets delegated.

**YAML Shape:**

```yaml
decision_consensus:
  enabled: true
  engines:
    - "openai/gpt-5-4"
    - "google/gemini-pro"
  delegate_categories:
    - "api_surface_design"
    - "refactor_strategy"
    - "testing_strategy"
    - "file_layout"
    - "dependency_choice"
    - "spec_clarification"
```

### 2. Location

The `decision_consensus` key should be placed inside the existing `delegation:` section.

**Justification:** This feature is a specialized form of delegation. The orchestrator is delegating a decision-making task to a consensus-driven subroutine via the router. Placing it within `delegation:` keeps all routing-related configuration logically grouped.

**Example `router-config.yaml`:**

```yaml
delegation:
  always_route_task_types:
    - "image_generation"
    - "data_analysis"
  direct_work_max_lines: 250
  direct_work_max_files: 5

  decision_consensus:
    # ... as defined above
```

### 3. Default

Opt-in. The `decision_consensus` block will not be present in the default configuration file, and its absence (or `enabled: false`) will preserve the existing behavior of using `AskUserQuestion`.

**Justification:** This is a power-user feature that increases token cost and delegates significant autonomy to the agent. The default behavior should be the safest and most predictable one, which is to ask the human.

### 4. Consensus Mechanism

The engines should be explicitly configurable via the `engines:` list.

The consensus mechanism is as follows:
1.  Query all engines in the `engines` list in parallel.
2.  The orchestrator's primary model (e.g., Claude Code) receives both responses and is tasked with synthesizing them into a single, actionable recommendation.
3.  If the synthesis results in a clear path forward, the orchestrator proceeds.
4.  If the responses are contradictory or the synthesis reveals persistent ambiguity, the orchestrator must use `AskUserQuestion`, presenting the synthesized conflict to the human for a final decision.

**Justification:** A configurable list is more future-proof than hardcoded models. Conflating this with `verification.preferred_pairings` is incorrect, as decision-making and verification are different tasks that may benefit from different models. The primary agent is the best choice for synthesis as it has the full context of the ongoing session.

### 5. Category Boundary

The boundary is defined declaratively by the `delegate_categories` list in the configuration.

*   **Orchestrator Logic:** The orchestrator will be prompted with a new tool, `GetConsensus`. Its instructions will state: "When you encounter a decision point that falls into one of the configured `delegate_categories`, you **must** use the `GetConsensus` tool. For all other questions requiring human input, use `AskUserQuestion`."

*   **Examples of "Consensus" Categories:**
    *   `api_surface_design`: "Should the new `BillingService` expose a single `POST /invoices` endpoint or separate `POST /invoices/draft` and `POST /invoices/finalize` endpoints?"
    *   `refactor_strategy`: "Should the logic in this 200-line function be extracted into a new class, or refactored into smaller private methods within the existing class?"

*   **Examples of "Human-Only" (`AskUserQuestion`) Scenarios:**
    *   **Business Priority:** "The deadline is tomorrow. Should I focus on completing the core feature with minimal testing, or ensure full test coverage and risk missing the deadline?"
    *   **Taste/Subjectivity:** "What should the error message copy be for a failed login attempt?"
    *   **Irreversible Actions:** "I am about to force-push to the `main` branch to correct a mistaken commit. Please confirm."

### 6. Interaction with `AskUserQuestion`

`AskUserQuestion` remains a critical tool. The orchestrator chooses between `GetConsensus` and `AskUserQuestion` based on the logic defined in #5. `AskUserQuestion` is the tool for human-only categories and the fallback when consensus fails.

Yes, there must be a logging affordance. Every consensus-driven decision should be clearly logged to stdout and, more formally, to a structured journal (see #8) so the human can audit the agent's autonomous decisions.

### 7. Cross-Repo Touchpoints

1.  **`dabbler-ai-router` (package):** The config parsing schema must be updated to accept the optional `decision_consensus` dictionary within the `delegation` section. No new router functions are required; the orchestrator can call the existing `router.query()` multiple times.
2.  **`docs/ai-led-session-workflow.md` (this repo):** A new section must be added to document the `decision_consensus` feature, explaining its purpose, YAML shape, and the behavioral change it enables.
3.  **Agent Instruction Files (`CLAUDE.md`, etc.) (this repo):** The system prompts must be updated to:
    *   Define a new tool, `GetConsensus(question: str, category: str)`.
    *   Provide strict instructions on when to use `GetConsensus` vs. `AskUserQuestion`, referencing the configured `delegate_categories`.

### 8. Observability

Yes, consensus-driven decisions must be logged to a structured journal for auditability. A file named `workspace/.dabbler/consensus-decisions.jsonl` would be appropriate.

**Required Fields for `consensus-decisions.jsonl`:**

```json
{
  "timestamp": "2023-10-27T10:00:00Z",
  "session_id": "session_abc123",
  "question": "Should the user profile data be stored in a single JSONB column or as separate columns in the 'users' table?",
  "category": "spec_clarification",
  "consensus_engines": ["openai/gpt-5-4", "google/gemini-pro"],
  "responses": {
    "openai/gpt-5-4": "...",
    "google/gemini-pro": "..."
  },
  "synthesis": "Both models recommend using a single JSONB column for flexibility, but advise creating indexes on specific keys within the JSONB object for performance.",
  "final_decision": "Proceeding with a JSONB column named 'profile_data' and adding a GIN index on it.",
  "escalated_to_human": false
}
```

### Minimum Viable Shape

The minimum viable implementation places a `decision_consensus` dictionary inside the `delegation:` section of `router-config.yaml`. This dictionary must contain an `enabled` boolean, a list of string `engines`, and a list of string `delegate_categories`. The orchestrator's agent prompt will be updated with a new `GetConsensus` tool and instructions to use it for any question matching a configured category, delegating to the primary agent for synthesis. All decisions made via this flow must be logged with sufficient detail for a human to review the action and its justification.