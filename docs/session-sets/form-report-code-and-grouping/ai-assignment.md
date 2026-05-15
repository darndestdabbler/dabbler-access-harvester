# AI Assignment — form-report-code-and-grouping

> Generated 2026-05-15 via route(analysis)

| Session | Step | Step Key | Routed? | Recommended model | task_type | Rationale |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | 2 | `session-1/analyze-code` | Yes | `gemini-flash` | `analysis` | Cross-file analysis to confirm the implementation problem is a reasoning task. Tier 1 is sufficient for code reading and summarization. |
| 1 | 3 | `session-1/rewrite-buildgrouplevels` | Yes | `gemini-pro` | `code_gen` | A significant logic rewrite with new regex and data access patterns requires more capability than a mechanical edit. Tier 2 is appropriate. |
| 1 | 4 | `session-1/add-module-name-helper` | No | `claude-code` | `direct_edit` | Simple, self-contained helper function in a single file. Orchestrator can handle directly. |
| 1 | 5 | `session-1/wire-module-name` | No | `claude-code` | `direct_edit` | Trivial change to call the new helper. Orchestrator can handle directly. |
| 1 | 6 | `session-1/update-report-tests` | Yes | `gemini-pro` | `test_gen` | Rewriting test fixtures (IR JSON shape) and assertions requires understanding the core logic change. This is a complex update. |
| 1 | 7 | `session-1/update-form-tests` | No | `claude-code` | `direct_edit` | Adding a few simple, new test cases is a mechanical task for the orchestrator. |
| 1 | 8 | `session-1/code-review` | Yes | `gemini-pro` | `code_review` | Reviewing the core logic change and associated test rewrites requires a capable model to catch potential errors. |
| 1 | 10 | `session-1/verification` | Yes | `gpt-5-4` | `verification` | Cross-provider verification warrants using a Tier 3 model to ensure the highest quality check and avoid model family bias. |
| 2 | 2 | `session-2/review-decoding-tables` | Yes | `gemini-flash` | `code_review` | A targeted review of simple key-value decoding tables is a reasoning task suitable for a fast Tier 1 model. |
| 2 | 3 | `session-2/add-integration-test` | Yes | `gemini-pro` | `test_gen` | Writing a new integration test requires understanding the full pipeline and existing test patterns (`SkippableFact`). Tier 2 is suitable. |
| 2 | 4 | `session-2/update-assert-helper` | No | `claude-code` | `direct_edit` | A small, targeted change to a single test helper method is a mechanical edit. |
| 2 | 5 | `session-2/code-review` | Yes | `gemini-pro` | `code_review` | Code review for a new integration test is a reasoning task appropriate for Tier 2. |
| 2 | 7 | `session-2/verification` | Yes | `gpt-5-4` | `verification` | Cross-provider verification warrants using a Tier 3 model. |
| 3 | 2 | `session-3/update-schema-doc` | Yes | `gemini-flash` | `doc_update` | Updating technical documentation, including worked examples and explaining nuances, is a reasoning task. Tier 1 is ideal for this. |
| 3 | 3 | `session-3/doc-review` | Yes | `gemini-flash` | `doc_review` | Reviewing a markdown document for clarity and correctness is a fast and cheap task for a Tier 1 model. |
| 3 | 5 | `session-3/verification` | Yes | `gpt-5-4` | `verification` | Final session verification warrants using a Tier 3 model for a robust final check. |
| 3 | 6 | `session-3/generate-final-docs` | Yes | `sonnet` | `planning` | Synthesizing a change log and recommending next steps requires reasoning about the entire session set. Tier 2 is appropriate. |

### Next orchestrator recommendation
- **Recommended engine**: `claude-code`
- **Recommended model**: `claude-3.5-sonnet-20240620` (Sonnet 4.6)
- **Rationale**: The orchestrator is performing well on C# edits and there is no specified need for a change in capability.
