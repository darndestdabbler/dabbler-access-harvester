## `ai-assignment.md`

### Session 1 of 3: Refresh Inventory + Disposition Manifest + Bulk-Delete Spent Artifacts (non-destructive)

**Recommended Orchestrator:** GPT-4o @ 2 hours

**Rationale:** This session requires strong classification reasoning, precise judgment on delete boundaries, and large structured document authoring. GPT-4o excels at complex reasoning and multi-output generation.

**Estimated Routed Cost:** Moderate

| Step | Action | Routing Decision |
| :--- | :----- | :--------------- |
| 1 | Re-run `003` residue scan at `HEAD` | **AI (GPT-4o)**: Execute script, parse output, identify new/removed paths. |
| 2 | Author `disposition-manifest.md` + `.csv` | **AI (GPT-4o)**: Read scan output, apply classification rules, generate structured manifest. Human review for boundary judgment. |
| 3 | Execute `delete` actions for spent artifacts | **AI (GPT-4o)**: Parse manifest, generate `git rm` commands. Human review/execution of commands. |
| 4 | `dotnet build` then `dotnet test` | **AI (Tool/Script)**: Execute commands, report status. |
| 5 | Cross-provider verification | **Human (Expert)**: Review manifest, delete decisions, and verify build/test status. |
| 6 | Closeout | **AI (GPT-4o)**: Generate session summary and progress key updates. |

**Actuals (Session 1):**

*Placeholder for actual orchestrator used, time, cost, and deviations.*

---

### Session 2 of 3: Test Triage — Re-base or Delete the Corpus-Pinned Tests (non-destructive)

**Recommended Orchestrator:** Claude 3.5 Sonnet @ 3 hours

**Rationale:** This session involves deep code comprehension, careful preservation of test behavior, and precise modification of test files. Claude 3.5 Sonnet's strong code understanding and long context window are well-suited for this detailed work.

**Estimated Routed Cost:** Moderate

| Step | Action | Routing Decision |
| :--- | :----- | :--------------- |
| 1 | Inspect tests, decide re-base/delete, record reason | **AI (Claude 3.5 Sonnet)**: Analyze test code, identify assertions, determine necessary fixture porting or reason for deletion. Human review for critical decisions. |
| 2 | Re-base tests onto HomeHealthCare fixture | **AI (Claude 3.5 Sonnet)**: Generate modified test code, port fixtures, ensure assertion fidelity. Human code review. |
| 3 | Delete spent/duplicative tests | **AI (Claude 3.5 Sonnet)**: Generate `git rm` commands based on manifest/triage plan. Human review/execution. |
| 4 | `dotnet build` then `dotnet test` (twice) | **AI (Tool/Script)**: Execute commands, report status. |
| 5 | Cross-provider verification | **Human (Expert)**: Review re-based tests for correctness, assertion integrity, and coverage. |
| 6 | Closeout | **AI (Claude 3.5 Sonnet)**: Generate `test-triage.md` and session summary. |

---

### Session 3 of 3: Genericize C# Source + Redact Docs + Final Residue Scan + Closeout (non-destructive)

**Recommended Orchestrator:** GPT-4o @ 2.5 hours

**Rationale:** This session requires a mix of targeted code modification, precise redaction in documentation, and a final, rigorous verification scan. GPT-4o's broad capabilities in code generation, content transformation, and structured output make it ideal.

**Estimated Routed Cost:** Moderate

| Step | Action | Routing Decision |
| :--- | :----- | :--------------- |
| 1 | Genericize `RunForNorthwind` | **AI (GPT-4o)**: Analyze usage, propose parameterization or removal, generate refactored code. Human code review. |
| 2 | Redact C# comment/string references | **AI (GPT-4o)**: Identify references, propose generic/HHC replacements, generate modified code. Human code review. |
| 3 | Redact non-session-set reference docs | **AI (GPT-4o)**: Identify proprietary names in documents, propose redactions, generate modified docs. Human content review. |
| 4 | Confirm `.gitignore` + add policy note | **AI (GPT-4o)**: Verify `.gitignore` status, propose wording for policy note. Human review. |
| 5 | Run final `HEAD` residue scan, record | **AI (GPT-4o)**: Execute script, parse output, generate `final-residue-scan.md`. Human review of results. |
| 6 | `dotnet build` then `dotnet test` | **AI (Tool/Script)**: Execute commands, report status. |
| 7 | Cross-provider verification | **Human (Expert)**: Comprehensive review of all changes, final scan results, and overall compliance. |
| 8 | Closeout | **AI (GPT-4o)**: Generate `change-log.md`, `next-session-set-recommendation.md`, update status. |

---

**Next-Session Orchestrator Recommendation (for Session 2):** Claude 3.5 Sonnet