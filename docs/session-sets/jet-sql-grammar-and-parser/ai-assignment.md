### Session 1 of 4: JET SQL Grammar (Synthesis)

**Recommended orchestrator engine:** `claude-code (Opus 4.7)`
**Rationale:** The primary task is a large-scale synthesis of a parser grammar, which is an `architecture`-level task best suited for Opus. Using Opus as the orchestrator provides the best context management for constructing the complex synthesis prompt and handling the output.
**Estimated routed cost:** $2.30

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | Orchestrator | n/a | n/a | n/a | Ingest Phase 5.1 spec, Appendix A, and fixtures `04`, `05` to build synthesis context. |
| 2 | Synthesis | `architecture` | `claude-code` (Opus) | `max` | **(Synthesis task 1/2)** Core grammar authoring. Requires deep language understanding and structural planning. `max` effort per calibration philosophy for novel grammar synthesis. |
| 3 | Mechanical | `refactoring` | `claude-code` (Sonnet) | `medium` | Generate and wire up the `AccessHarvester.Sql.Grammar.csproj` file with `ANTLR4.Build.Tasks`, a mechanical task following an established pattern from `parser-foundations` (#1). |
| 4 | Verification | `session-verification` | `codex` (gpt-5.4) | `high` | Mandatory cross-provider verification of the generated grammar and build wiring against the spec. |

### Actuals (Session 1)
- **Orchestrator used:** claude-code Opus 4.7 @ effort=unknown
- **Total routed cost:** ~$0.33 (analysis $0.018 + verification R1 $0.145 + R2 $0.083 + R3 $0.083)
- **Deviations:** Synthesis authored in-conversation per Sonnet=routing-target Convention. Routed cost dropped from estimated $2.30 to $0.33.
- **Notes:** 3 verification rounds needed. Session 2 should plan for 2-3 rounds.
- **Next session orchestrator recommendation (Session 2):** `claude-code Sonnet 4.6` @ effort=`medium` — Session 2 applies an existing pattern rather than novel design; Sonnet is sufficient.

---
### Session 2 of 4: SQL Parser Infrastructure (Synthesis)

**Recommended orchestrator engine:** `claude-code (Opus 4.7)` *(updated: see below)*
**Rationale:** The session's core is the synthesis of the AST and error recovery infrastructure. This is a complex design task (`architecture`) that benefits from Opus's planning and code generation capabilities, especially when mirroring conventions from a prerequisite (`#6`).
**Estimated routed cost:** $2.20

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | Orchestrator | n/a | n/a | n/a | Ingest Phase 5.2 spec and the structure of `parser-infrastructure` (#6) as the pattern to follow. |
| 2 | Synthesis | `architecture` | `claude-code` (Opus) | `high` | **(Synthesis task 2/2)** Author the immutable AST nodes, visitor pattern, error recovery, and source position tracking classes. This is a systems design task. |
| 3 | Orchestrator | n/a | n/a | n/a | Land generated C# files into the `AccessHarvester.Sql.Parser/` directory structure. |
| 4 | Verification | `session-verification` | `codex` (gpt-5.4) | `high` | Verify the AST shape, immutability, and error recovery strategy against the spec and conventions from `#6`. |

### Actuals
*TBD by orchestrator*

---
### Actuals (Session 2)
- **Orchestrator used:** claude-code Sonnet 4.6 @ effort=unknown
- **Total routed cost:** ~$0.56 (R1 verification $0.12, R2 $0.10, tiebreaker $0.01, next-orch recommendation $0.002)
- **Deviations:** Architecture synthesis done in-conversation (Sonnet = routing target). ParseFragment API removed from scope per Lean-By-Default (deferred to Session 3 when fixture 05 is exercised).
- **Verification:** R1 ISSUES_FOUND (2 real issues fixed), R2 ISSUES_FOUND (1 context-gap false positive), tiebreaker GPT-5-4 → ACCEPT_DISMISSAL. Net: VERIFIED.
- **Next session orchestrator recommendation (Session 3):** `claude-code Sonnet 4.6` @ effort=`medium` — Session 3 is mechanical test harness authoring + schema validation; Sonnet proved sufficient in Session 2 for more complex work.

---
### Session 3 of 4: Test Coverage + Mechanical Tightening

**Recommended orchestrator engine:** `claude-code (Sonnet 4.6)` *(confirmed by Session 2 actuals)*
**Rationale:** This session is primarily mechanical: generating a test harness, running the parser, and validating output. Sonnet is cost-effective and sufficient for orchestrating these `test-generation` and `schema-validation` tasks.
**Estimated routed cost:** $0.65

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | Generation | `test-generation` | `claude-code` (Sonnet) | `medium` | Generate the NUnit test harness project and boilerplate for parsing SQL fixtures and serializing the resulting AST to JSON. |
| 2 | Orchestrator | n/a | n/a | n/a | Execute the new parser from S1/S2 against fixtures `04` and `05` to capture the initial `actual.json` AST outputs. |
| 3 | Validation | `schema-validation` | `claude-code` (Sonnet) | `low` | Pin-pointed task per spec to mechanically verify the captured AST JSON against the expected structure and node conventions from S2. Rename `actual.json` to `expected.json` on success. |
| 4 | Verification | `session-verification` | `codex` (gpt-5.4) | `medium` | Verify that the tests provide meaningful coverage for the core SQL features present in the fixtures. |

### Actuals (Session 3)
- **Orchestrator used:** claude-code Sonnet 4.6 @ effort=unknown
- **Total routed cost:** ~$0.29 (schema-validation $0.21, session-verification $0.055, next-orch recommendation $0.002)
- **Deviations:** `code-review` used for schema-validation step (Sonnet, not Gemini Pro per assignment). One vacuous union test fixed (AllFlags[0]→AllFlags[1]). Added compound WHERE fixture-05 test after schema-validation finding.
- **Verification:** VERIFIED by GPT-5-4 on first round.
- **Next session orchestrator recommendation (Session 4):** `claude-code Sonnet 4.6` @ effort=`medium` — Session 4 is analysis + targeted test-generation (surgical grammar-gap identification); Sonnet proved sufficient for similar scope in Session 3 and the synthesis count is 0.

---
### Session 4 of 4: Cross-Cut + Edge-Case Fixtures

**Recommended orchestrator engine:** `claude-code (Sonnet 4.6)`
**Rationale:** This session focuses on identifying and filling gaps in test coverage, which involves analysis and targeted test generation. Sonnet is a capable and efficient orchestrator for these medium-complexity, non-synthesis tasks.
**Estimated routed cost:** $0.61

| Step | Type | route() task_type | Model | Effort/thinking | Rationale |
|---|---|---|---|---|---|
| 1 | Analysis | `analysis` | `claude-code` (Sonnet) | `medium` | Analyze `JetSql.g4` against the test cases from S3 to identify grammar rules not exercised by fixtures `04`/`05` (e.g., `TRANSFORM PIVOT`). |
| 2 | Generation | `test-generation` | `gemini-cli` (Gemini Pro) | `thinking_level: 2` | Author new, small SQL fixture strings specifically for the dialect surfaces identified in Step 1. |
| 3 | Mechanical | `refactoring` | `claude-code` (Sonnet) | `low` | Integrate the new SQL fixtures into the test harness, including generating their corresponding `expected.json` ASTs. |
| 4 | Verification | `session-verification` | `codex` (gpt-5.4) | `medium` | Verify that the new tests correctly target the previously uncovered grammar rules and that the parser handles them correctly. |
| 5 | Documentation | `summarization` | `gemini-cli` (Gemini Flash) | `thinking_level: 1` | Generate a summary of the session's work for the change log. |

### Actuals
*TBD by orchestrator*

### Session 1 Actuals (filled by orchestrator at end of session)

- **Orchestrator used:** claude-code Opus 4.7 @ effort=unknown
- **Total routed cost:** ~$0.33 (analysis $0.018 + verification R1 $0.145 + R2 $0.083 + R3 $0.083)
- **Deviations from recommendation:** Synthesis was authored
  in-conversation by the Opus orchestrator rather than via
  `route(architecture, complexity_hint=80)`, per the
  "Opus At effort:max + Adaptive Thinking Timeouts on Large
  Synthesis-Document Prompts" Convention in `project-guidance.md`.
  This drove the routed cost from the estimated $2.30 down to $0.33;
  the in-conversation cost is borne by the orchestrator allotment.
- **Notes for next-session calibration:** Verifier returned
  ISSUES_FOUND on R1 (4 real grammar gaps) and R2 (2 overcorrections
  from R1 fixes). R3 VERIFIED cleanly. The pattern suggests Session 2
  should plan for 2-3 verification rounds for new infrastructure code.



## Updated recommendation for Session 2

**Next session orchestrator recommendation (Session 2):**
`claude-code Sonnet 4.6` @ effort=`medium`

**Rationale:** Session 1's actuals showed the planned `route(architecture)` task was successfully bypassed in favor of a more cost-effective, in-conversation synthesis. As Session 2's scope is to apply a well-defined *existing* pattern from `parser-infrastructure` (#6) rather than perform novel design, the more economical Sonnet is better aligned with this revealed workflow and sufficient for the task.