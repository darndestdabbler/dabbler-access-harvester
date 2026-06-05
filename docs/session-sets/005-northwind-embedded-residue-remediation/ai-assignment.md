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

- Orchestrator used: claude-code / Claude Opus 4.8 @ effort=high (not the routed GPT-4o recommendation; human ran Claude).
- Verification: gpt-5-4 (OpenAI), Round 1 VERIFIED, 0 issues.
- Deviations: material scope expansion — the fresh `HEAD` scan surfaced an order-entry PILOT ARC (packages + 33 characterization tests + a buildable MudBlazor project) not in the `003` inventory or the `005` spec; maintainer authorized deleting the whole arc. 198 deletes landed.
- Notes: the disposition manifest is the authoritative input to Session 2/3.

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

**Actuals (Session 2):**

- Orchestrator used: claude-code / Claude Opus 4.8 @ effort=high (not the routed Sonnet recommendation; human ran Claude).
- Triage refined the manifest's blanket "re-base" into per-file decisions: 6 keep-clean (manifest over-included), 5 redact-literals, 6 re-base (5 SymbolDb tests + FixtureLoader), 3 edit-trim-arm, 1 file delete (GenerateArtifacts), 1 project delete (TranslationProbe.Tests).
- Cross-provider consensus on the triage strategy: route(analysis)→gemini-pro APPROVE; query→gpt-5-4 APPROVE-WITH-CHANGES (all 3 execution constraints adopted). Two manifest-deviating deletes maintainer-confirmed.
- The SymbolDb tests were re-based onto the committed HHC kb fixture and now run **live** (68/68 pass, 0 skipped) instead of skipping on the absent proprietary fixture — a coverage gain.
- Deviation from triage doc: CrossModuleResolutionTests re-based onto an inline synthetic 2-module fixture (not HHC) because HHC's forms qualify cross-module calls and so cannot exercise *bare-name* cross-module binding; inline synthetic preserves that exact semantic hermetically.
- Total routed cost: see `ai_router/router-metrics.jsonl` (triage consensus + recommendations; ~$0.08).

**Next-Session Orchestrator Recommendation (for Session 3):** Claude Sonnet 4.6 @ effort=medium.
Rationale (routed via gemini-pro): Session 3 is mechanical-to-moderate redaction of source comments + reference docs plus one small CLI-helper genericization and a final residue scan; Sonnet provides sufficient code/text intelligence at much lower cost than Opus, with cross-provider verification retained.