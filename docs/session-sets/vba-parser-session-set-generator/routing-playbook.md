# Routing Playbook — VBA Parser Child Session Sets

> **Authored:** 2026-04-25 by Session 4 of `vba-parser-session-set-generator`.
> **Source tree:** `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`
> **Source retrospective:** `docs/session-sets/vba-parser-session-set-generator/harvester-import-retrospective.md` (adopt-now rows #7–#16)
> **Synthesis note:** The architecture route call (Opus/max effort) timed out twice; this playbook was authored in-conversation by the Sonnet 4.6 orchestrator. The planning route call (Opus/high) produced a meta-plan rather than the document. Both deviations are disclosed here and in the session-verification prompt.
> **Applies to:** All 18 child session sets listed in the tree. Orchestrators executing any child set must read this playbook before step 1.

## Purpose

This playbook is the operational companion to `session-set-tree.md`. It documents the cheapest-capable model for each kind of work the 18 child session sets contain, when to handle work in-conversation vs through `route()`, and how to assemble verification prompts. Its purpose is to prevent orchestrators from re-deriving routing decisions that are already known from the `accessexportertemp1-import` set's actuals and from the governing conventions in `project-guidance.md`.

Read this playbook at the start of every child session set, before executing any steps.

---

## Section 1: Session-Startup Checklist

Every child-set orchestrator must complete these gates before executing step 1. If any gate fails, **STOP and resolve before proceeding.**

1. **Verify API keys.**
   ```bash
   export GEMINI_API_KEY=$(powershell -Command "[System.Environment]::GetEnvironmentVariable('GEMINI_API_KEY', 'User')" | tr -d '\r')
   export ANTHROPIC_API_KEY=$(powershell -Command "[System.Environment]::GetEnvironmentVariable('ANTHROPIC_API_KEY', 'User')" | tr -d '\r')
   export OPENAI_API_KEY=$(powershell -Command "[System.Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'User')" | tr -d '\r')
   ```
   Confirm ANTHROPIC_API_KEY, GEMINI_API_KEY, OPENAI_API_KEY are all present.

2. **Confirm `session-verification` override.** Check `ai-router/router-config.yaml` → `routing.task_type_overrides.session-verification`. It must point to a **non-orchestrator** provider:

   | Orchestrator | Required override value |
   |---|---|
   | Claude (Anthropic) | `gpt-5-4` |
   | Codex (OpenAI) | `opus` |
   | Gemini (Google) | `opus` or `gpt-5-4` |

   If the current value is from the same provider as the orchestrator, update it before proceeding. This is the most common single point of failure for cross-provider verification.

3. **Read `docs/planning/project-guidance.md` and `docs/planning/lessons-learned.md`.**

4. **Confirm orchestrator tier** per Section 2 (Orchestrator Tier Selection).

5. **Check `router-config.yaml` currency.** If `metadata.pricing_reviewed` is more than 30 days ago, note it in the session log (does not block execution, but flag for the human).

6. **Read this playbook's Section 3** (Task Categories) for the specific work categories this session exercises. Confirm you know the `task_type`, `complexity_hint`, and in-conversation vs route() ruling for each.

---

## Section 2: Orchestrator Tier Selection

**Default: Sonnet@medium.** A child-set session requires Opus orchestrator only when it contains at least one non-trivial synthesis task that the Opus routing target would handle in-conversation per the `project-guidance.md` Convention ("When the orchestrator's model matches the routing target...").

### Decision table

| Session content | Default orchestrator | Escalation to Opus? |
|---|---|---|
| Pure mechanical (file copy, build, test, git, import, namespace rewrite) | Sonnet@medium | No |
| Single `code-review` or `test-generation` call | Sonnet@medium | No — these are routed to Sonnet and gemini-pro; orchestrator tier doesn't change outcome |
| One `analysis` call (spec study, taxonomy, next-orchestrator recommendation) | Sonnet@medium | No — analysis routes to gemini-pro regardless of orchestrator |
| One `architecture` call (grammar design, AST design, resolver algorithm) | **Opus@max** | Yes — the in-conversation convention applies: Opus orchestrator handles Opus-pinned work directly. Effort must match the task_type_params (effort:max for architecture). |
| Two `architecture` calls (synthesis-heavy sessions per tree column 6) | **Opus@max** | Yes, same reason |
| `session-verification` only (last-step call) | Sonnet@medium | No — verification always routes regardless |

**Why Sonnet@medium is the default and not Opus:** The `accessexportertemp1-import` set's actuals showed 3 of 6 sessions (50%) used Opus where Sonnet@medium was both recommended and adequate (retrospective finding #7). Mechanical sessions passed round-1 verification either way. Opus adds cost without buying quality on non-synthesis sessions.

**Why Opus@max is correct for synthesis sessions:** Sessions containing `architecture`-pinned work can invoke that task type in-conversation rather than routing (per `project-guidance.md` Convention), avoiding the API cost of routing to Opus from a Sonnet orchestrator. The effort level must be `max` (not `high`) to match `task_type_params.architecture.opus.effort: max`. This is the single largest cost lever on synthesis sessions.

**Opus escalation applies ONLY to architecture-pinned tasks.** If the session's only synthesis calls are `analysis` (which routes to gemini-pro), use Sonnet@medium — there is no in-conversation advantage for an Opus orchestrator on analysis work, and the extra cost buys nothing.

### Observed cost bands (from `accessexportertemp1-import` actuals — retrospective #16)

| Session type | Observed cost band |
|---|---|
| Pure-mechanical session | $0.08–$0.21 |
| Planning / decomposition session | $0.20–$0.60 |
| Synthesis session, round-1 verified | $0.20–$0.30 |
| Synthesis session, multi-round verified | $0.30–$2.00 |
| `session-verification` round (single call) | $0.05–$0.16 per round |
| Next-orchestrator recommendation | $0.001–$0.05 |

Provider list pricing belongs in a footnote; these observed bands are the primary planning anchor.

---

## Section 3: Task Categories and Routing

One subsection per kind of work the 18 child session sets exercise. Per-subsection template: task_type → model → complexity_hint → effort → in-conversation vs always-route → verification posture → cost band.

---

### 3.1 Grammar Authoring

**Description:** Writing ANTLR grammar rules for VBA or JET SQL. Errors compound downstream into every subsequent resolver and enrichment session. (4 synthesis instances: tree rows #3, #4, #12; plan Phases 1.4, 5.1.)

| Field | Value |
|---|---|
| task_type | `architecture` (existing) |
| complexity_hint | 80 |
| Model | `opus` (via `task_type_overrides: architecture: opus`) |
| effort / thinking | `max`, adaptive thinking |
| In-conversation vs route | **In-conversation** if orchestrator is Opus; **route** otherwise. `session-verification` is ALWAYS routed regardless. |
| Verification posture | Auto-verified (architecture is in `auto_verify_task_types`); `on_disagreement: flag` — disagreements go to human per Step 7 procedure. |
| Cost band | **(UNCALIBRATED — log first-use actuals in `ai-assignment.md`)** Estimate: $0.50–$2.00 per call based on import-set synthesis bands. |

> **UNCALIBRATED:** No `architecture` calls were made in the `accessexportertemp1-import` set. The first child set that exercises grammar authoring (recommended: `vba-lexical-and-expression-grammar`) must log full cost breakdown (input tokens, output tokens, cost, verification rounds, verdict) in its `ai-assignment.md` Actuals block so future playbook revisions can replace this band.

---

### 3.2 AST / Declaration Model Design

**Description:** Designing AST node type hierarchies, visitor infrastructure, and declaration model types. (4 synthesis instances: tree rows #6, #7; plan Phases 2.3, 3.1–3.2.)

| Field | Value |
|---|---|
| task_type | `architecture` (existing) |
| complexity_hint | 80 |
| Model | `opus` |
| effort / thinking | `max`, adaptive thinking |
| In-conversation vs route | **In-conversation** if orchestrator is Opus; route otherwise. |
| Verification posture | Auto-verified; `on_disagreement: flag`. |
| Cost band | **(UNCALIBRATED — log first-use actuals)** Same estimate: $0.50–$2.00 per call. |

---

### 3.3 Resolver Algorithm Design

**Description:** Designing reference resolution algorithms, with-block flattening, default member resolution, type inference passes, and cross-reference index. (6 synthesis instances: tree rows #8, #9, #11; plan Phases 3.3–3.7.)

| Field | Value |
|---|---|
| task_type | `architecture` (existing) |
| complexity_hint | 80 |
| Model | `opus` |
| effort / thinking | `max`, adaptive thinking |
| In-conversation vs route | **In-conversation** if orchestrator is Opus; route otherwise. |
| Verification posture | Auto-verified; `on_disagreement: flag`. |
| Cost band | **(UNCALIBRATED — log first-use actuals)** |

---

### 3.4 JET SQL Dynamic Analyzer Design

**Description:** Designing form-reference detection in SQL ASTs and intra-procedural data-flow analysis for dynamic SQL string construction. (4 synthesis instances: tree rows #12, #13; plan Phases 5.3–5.4.)

| Field | Value |
|---|---|
| task_type | `architecture` (existing) |
| complexity_hint | 80 |
| Model | `opus` |
| effort / thinking | `max`, adaptive thinking |
| In-conversation vs route | **In-conversation** if orchestrator is Opus; route otherwise. |
| Verification posture | Auto-verified; `on_disagreement: flag`. |
| Cost band | **(UNCALIBRATED — log first-use actuals)** |

---

### 3.5 Enrichment Pipeline Design

**Description:** Designing enrichment pipeline components. Split by novelty: (a) taxonomy work (event-handler classification §6.3, function classification §6.6) is structured enumeration → `analysis`; (b) structural design (query abstraction §6.1, form-spec structure §6.2, navigation graph §6.7) is higher-stakes design → `architecture`. (6 synthesis instances: tree rows #14, #15, #16; plan Phase 6.)

#### 3.5a Taxonomy work (event-handler and function classification)

| Field | Value |
|---|---|
| task_type | `analysis` |
| complexity_hint | 60 |
| Model | `gemini-pro` (tier 2 default) |
| effort / thinking | dynamic (gemini-pro default: `thinking_budget: -1`) |
| In-conversation vs route | Route (gemini-pro is not the orchestrator; orchestrator is Sonnet or Opus). |
| Verification posture | Not auto-verified for `analysis`; include in session-verification prompt. |
| Cost band | ~$0.05–$0.20 per call (gemini-pro at ~3K tokens) |

#### 3.5b Structural design (query abstraction, form-spec, navigation graph)

| Field | Value |
|---|---|
| task_type | `architecture` |
| complexity_hint | 75 |
| Model | `opus` |
| effort / thinking | `max`, adaptive thinking |
| In-conversation vs route | In-conversation if orchestrator is Opus; route otherwise. |
| Verification posture | Auto-verified; `on_disagreement: flag`. |
| Cost band | **(UNCALIBRATED — log first-use actuals)** |

---

### 3.6 MS-VBAL Spec Study / Coverage Matrix / Hazard List

**Description:** Reading [MS-VBAL] and synthesizing a summary, coverage matrix, and hazard list. (child set #2 `vba-spec-study`; plan Phases 1.1–1.3.) These are `analysis` tasks — not `architecture`. The recommended orchestrator for `vba-spec-study` is **Sonnet@medium** (2 synthesis sessions of `analysis` type, not `architecture`).

| Field | Value |
|---|---|
| task_type | `analysis` |
| complexity_hint | 65 (spec summary), 55 (coverage matrix), 60 (hazard list) |
| Model | `gemini-pro` (tier 2) |
| effort / thinking | dynamic |
| In-conversation vs route | Route (gemini-pro is never the orchestrator in this repo; route regardless). |
| Verification posture | Not auto-verified; include all three outputs in session-verification. |
| Cost band | ~$0.10–$0.40 per call (long spec document as input) |

> **Orchestrator for `vba-spec-study`:** Sonnet@medium. The 2 synthesis sessions in the tree (MS-VBAL summary, hazard list) are `analysis`-type calls to gemini-pro — not `architecture`-type Opus calls. There is no in-conversation advantage for an Opus orchestrator, and escalation is not justified per Section 2.

---

### 3.7 Mechanical-Correspondence Checks (`schema-validation` — PROPOSED NEW TYPE)

**Description:** Verifying that harvester output JSON conforms to `harvester-schema.v0.json` / `v1.0.json`, that parse trees match expected fixture shapes, and that AST annotations match source positions. Recurring across every child set with test fixtures. (retrospective #10)

| Field | Value |
|---|---|
| task_type | `schema-validation` **(PROPOSED — not yet in router-config.yaml)** |
| complexity_hint | 35 |
| Model | `sonnet` (via proposed `task_type_overrides: schema-validation: sonnet`) |
| effort / thinking | `medium`, adaptive thinking |
| In-conversation vs route | **In-conversation** if orchestrator is Sonnet; route otherwise. `session-verification` is still ALWAYS routed. |
| Verification posture | Proposed for `auto_verify_task_types`; cross-provider check on mechanical gates. `on_disagreement: merge`. |
| Cost band | ~$0.03–$0.10 per call (Sonnet@medium on schema-shaped prompts) |

> **Before this task type can be used:** apply the proposed `router-config.yaml` delta in Section 6 of this playbook.

---

### 3.8 Parser Regression Checks (`parser-regression-check` — PROPOSED NEW TYPE)

**Description:** Running the verification harness and diffing parser output against committed fixtures to detect regressions. Recurring in Phase 1.5–1.6 and after every grammar change.

| Field | Value |
|---|---|
| task_type | `parser-regression-check` **(PROPOSED)** |
| complexity_hint | 30 |
| Model | `sonnet` (via proposed override) |
| effort / thinking | `medium`, no thinking (mechanical diff) |
| In-conversation vs route | **In-conversation** if orchestrator is Sonnet; route otherwise. |
| Verification posture | Not auto-verified (the harness output is ground truth); include regression summary in session-verification. |
| Cost band | ~$0.02–$0.08 per call |

---

### 3.9 Test-Fixture Authoring

**Description:** Writing `.bas`/`.expected.json` pairs for module-level cases, `.accdb` fixture databases for metadata extraction, and edge-case fixtures. (4 obligated instances: tree rows #5, #6, #10, #18; plan Phases 1.5–1.6, 2.5, 4.1–4.3, 8.3.)

| Field | Value |
|---|---|
| task_type | `test-generation` (existing) |
| complexity_hint | 35–50 (higher for novel constructs from hazard list) |
| Model | `gemini-pro` (tier 2 default) |
| effort / thinking | dynamic |
| In-conversation vs route | Route (test generation for novel VBA constructs benefits from gemini-pro reasoning). |
| Verification posture | Not auto-verified by default; include fixture samples in session-verification. |
| Cost band | ~$0.05–$0.20 per fixture batch |

---

### 3.10 Code Review

**Description:** All C# code review across child sets. (Recurring across every code-producing session.)

| Field | Value |
|---|---|
| task_type | `code-review` (existing, Sonnet-pinned) |
| complexity_hint | 40 (default) |
| Model | `sonnet` (via `task_type_overrides: code-review: sonnet`) |
| effort / thinking | `medium`, adaptive thinking |
| In-conversation vs route | **In-conversation** if orchestrator is Sonnet; route if orchestrator is Opus or other. `session-verification` is ALWAYS routed regardless. |
| Verification posture | Auto-verified; `on_disagreement: merge`. |
| Cost band | ~$0.05–$0.20 per code-review call |

---

### 3.11 Change Logs / READMEs / Session Summaries

**Description:** Last-session deliverable in every child set. (18 instances total — one per child set.)

| Field | Value |
|---|---|
| task_type | `summarization` (existing) |
| complexity_hint | 20 |
| Model | `gemini-flash` (tier 1, score 20 ≤ tier1_max_complexity 30) |
| effort / thinking | `thinking_budget: 0` (no thinking) |
| In-conversation vs route | Route (gemini-flash is a different model from any orchestrator). |
| Verification posture | Not auto-verified; change log is included in session-verification. |
| Cost band | ~$0.01–$0.05 per change log |
| **MANDATORY FALLBACK** | If the `summarization` call returns Gemini 400 on a prompt above ~2KB, pin `max_tier=1` to force gemini-flash: `route(content=..., task_type="summarization", max_tier=1)`. This is the **operational half** of the rule, not optional documentation. (retrospective #9) |

---

### 3.12 ADRs / Design Decision Records

**Description:** Architecture Decision Records documenting grammar rule choices, AST node design rationale, resolver algorithm trade-offs.

| Field | Value |
|---|---|
| task_type (code-adjacent ADRs) | `documentation` (generates structured ADR text alongside code) |
| task_type (prose-only ADRs) | `summarization` (no code-doc template wrapping) |
| complexity_hint | 25 (documentation), 20 (summarization) |
| Model | `gemini-pro` (documentation), `gemini-flash` (summarization) |
| In-conversation vs route | Route |
| Cost band | ~$0.02–$0.10 per ADR |

---

### 3.13 Session Verification

**Description:** Mandatory end-of-session cross-provider verification call. **ALWAYS through `route()` — no exception, regardless of orchestrator model.** (retrospective #12)

| Field | Value |
|---|---|
| task_type | `session-verification` |
| complexity_hint | 70 |
| Model | Per swap matrix (§1 checklist item 2): `gpt-5-4` for Claude orchestrator |
| effort | `high` |
| In-conversation vs route | **ALWAYS route(). This is the ONLY mandatory route() call — no in-conversation exception.** |
| Verification posture | N/A (this IS the verification call) |
| Cost band | $0.05–$0.16 per round; budget for 2 rounds typically, occasionally 3 |

---

### 3.14 Next-Orchestrator Recommendation

**Description:** Forward-looking end-of-session call recommending the orchestrator for the next session within the same child set. (retrospective #14)

| Field | Value |
|---|---|
| task_type | `analysis` |
| complexity_hint | 40 |
| Model | `gemini-pro` (tier 2 default for analysis at complexity 40) |
| In-conversation vs route | Route |
| Cost band | $0.001–$0.05 |

**No router-config change needed** — the existing `analysis` task type at `complexity_hint=40` is correctly calibrated. (retrospective #14)

---

### 3.15 Next-Session-Set Recommendation (PROPOSED — new per this session)

**Description:** At the end of the **last session** of each child set, produce a recommendation for which session set to tackle next and with what orchestrator. The 18-set DAG from `session-set-tree.md` makes the viable next sets deterministic once prerequisites are satisfied.

| Field | Value |
|---|---|
| task_type | `analysis` |
| complexity_hint | 40 |
| Model | `gemini-pro` |
| In-conversation vs route | Route |
| When | Last session of child set only, after change-log generation, before commit |
| Cost band | $0.001–$0.05 |

**Output schema (single next set):**
```markdown
**Next session-set recommendation (after <slug>):**
`<next-slug>`

**Recommended orchestrator for `<next-slug>`:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2 sentences grounded in (a) next set's synthesis count from session-set-tree.md and (b) what this session revealed about codebase shape>
```

**Output schema (parallel next sets — when DAG permits):**
```markdown
**Next session-set recommendation (after <slug>):**
`<slug-a>` and `<slug-b>` (run in parallel — both are DAG roots)

**Recommended orchestrator for `<slug-a>`:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2 sentences>

**Recommended orchestrator for `<slug-b>`:** <engine> <model> @ effort=<low|medium|high>
**Rationale:** <2 sentences>
```

**Include in:** `change-log.md` header (last-session section) AND Pushover notification body.

**Prompt inputs:** The completed set's DAG row from `session-set-tree.md` (showing which sets it unblocks), this session's actuals, and the `ai-assignment.md` rows for candidate next sets.

> **Workflow doc update:** This protocol is new and not yet documented in `docs/ai-led-session-workflow.md`. The Step 9 reorganization review for this session set will propose adding it as a sub-step of Step 8 (last-session branch, after change-log generation, before commit).

---

## Section 4: Deliverable-to-Task-Type Mapping

Quick-reference table for common deliverables. (retrospective #9)

| Deliverable | task_type | Key notes |
|---|---|---|
| Change log (`change-log.md`) | `summarization` | `max_tier=1` fallback if Gemini 400 on prompts >~2KB — MANDATORY |
| README | `summarization` | Same fallback |
| Retrospective / session summary | `summarization` | Same fallback; use `analysis` only if synthesis is needed |
| XML doc comments on public C# APIs | `documentation` | NOT for prose — wrong template shape |
| Architecture Decision Record (code-adjacent) | `documentation` | ADR embedded alongside code context |
| Architecture Decision Record (prose-only) | `summarization` | No code-doc template wrapping needed |
| Grammar analysis / MS-VBAL spec study | `analysis` | complexity_hint=55–65 |
| Hazard list synthesis | `analysis` | complexity_hint=60 |
| Coverage matrix authoring | `analysis` | complexity_hint=55 |
| Grammar rule authoring (Vba.g4, JetSql.g4) | `architecture` | Opus; **UNCALIBRATED** |
| AST node type design | `architecture` | Opus; **UNCALIBRATED** |
| Resolver algorithm design | `architecture` | Opus; **UNCALIBRATED** |
| Enrichment taxonomy (event/function classification) | `analysis` | gemini-pro; complexity_hint=60 |
| Enrichment structural design (query abstraction, form-spec) | `architecture` | Opus; **UNCALIBRATED** |
| Schema validation (output vs schema) | `schema-validation` (proposed) | Sonnet-pinned |
| Parser regression check (diff vs fixture) | `parser-regression-check` (proposed) | Sonnet-pinned |
| Test fixture authoring | `test-generation` | gemini-pro |
| Code review | `code-review` | Sonnet-pinned; in-conv if orchestrator=Sonnet |
| Session verification | `session-verification` | gpt-5-4 (Claude orchestrator); ALWAYS route() |
| Next-orchestrator recommendation | `analysis` | complexity_hint=40; gemini-pro |
| Next-session-set recommendation | `analysis` | complexity_hint=40; gemini-pro; last-session only |

---

## Section 5: Verification-Prompt Assembly Recipes

Before calling `route(task_type="session-verification")`, assemble the prompt to include the right files and disclose what is intentionally out of scope. Missing context produces false positives; including irrelevant files dilutes the verifier's attention. (retrospective #11)

---

### Grammar authoring sessions (child sets #3, #4, #12)

**In scope:**
- `AccessHarvester.Vba.Grammar/Vba.g4` (or `JetSql.g4`) — full file
- Test fixture `.bas` or `.sql` files exercised by this session's grammar additions
- Corresponding `.expected.json` fixture files
- `docs/spec-coverage/ms-vbal-coverage.csv` (if available)

**Disclose as out-of-scope (file does not exist yet):**
- `AccessHarvester.Vba.Parser/` — parser infrastructure is built in child set #6, after grammar
- Resolver files — built in child sets #7–#9

**Verbatim to include:** MS-VBAL spec section references embedded in grammar rule comments; test case numbers from `docs/test-cases/README.md`.

---

### AST / Declaration model sessions (child sets #6, #7)

**In scope:**
- All `.cs` files modified this session (AST node types, visitors, declaration model)
- The test fixtures from `docs/test-cases/` that exercise the constructs being modeled
- The grammar file (`Vba.g4`) as context for parse tree structure

**Disclose as out-of-scope:**
- `AccessHarvester.Vba.Resolver/` — resolver is built in child sets #7–#9; grammar + AST are prerequisites
- Access metadata files (`AccessHarvester.Access/`) — built in child set #10

**Verbatim:** xUnit test names and expected vs actual assertions.

---

### Resolver algorithm sessions (child sets #8, #9, #11)

**In scope:**
- All `.cs` files modified this session (resolver pass files)
- AST node type definitions from child set #6 / #7 (provide as context, mark as "not modified this session")
- Test fixtures for the resolution scenarios exercised

**Disclose as out-of-scope:**
- `AccessHarvester.Access/FormMetadata.cs` etc. — form metadata built in #10 and injected in #11
- Grammar files — stable by this point

**Verbatim:** Scope tree structure examples, resolution algorithm pseudocode from ADRs.

---

### Schema validation sessions

**In scope:**
- `docs/test-cases/harvester-schema.v0.json` or `v1.0.json`
- The output JSON being validated
- The validation code (if a code session)

**Disclose as out-of-scope:**
- The generator of the output (extractors, parser, resolver) unless specifically under test

---

### Code review sessions (recurring)

**In scope:**
- All `.cs` files created or modified this session
- The corresponding `.csproj` file
- Any new test files for the added code

**Disclose as out-of-scope:**
- ANTLR-generated files (generated by build, not authored; marked in `.gitignore` or similar)

**Verbatim:** Any TODO comments, any `[Obsolete]` annotations, build output (any warnings/errors).

---

### JET SQL / dynamic analyzer sessions (child sets #12, #13)

**In scope:**
- `AccessHarvester.Sql.Grammar/JetSql.g4` and `AccessHarvester.Sql.Parser/` files modified
- SQL test fixture files from `docs/test-cases/` (e.g., `04-*.sql`, `05-*.sql`)
- Corresponding `.expected.json` files

**Disclose as out-of-scope:**
- `AccessHarvester.Vba.Parser/` — VBA parser is a prerequisite, built in #6; not modified in SQL sessions
- `AccessHarvester.Vba.Resolver/AccessIntegration/` — Access symbol-table injection is built in #11 after the SQL parser

**Verbatim:** Error recovery strategy choice, data-flow analysis algorithm pseudocode from ADRs.

---

### Enrichment taxonomy sessions (event/function classification, §6.3 / §6.6)

**In scope:**
- Enrichment classification `.cs` files (`EventHandlerClassification.cs`, `FunctionClassification.cs`)
- Relevant test fixtures exercising the classified patterns

**Disclose as out-of-scope:**
- SQL parser files — enrichment consumes SQL ASTs but does not own the parser
- Schema validation code — schema finalization is in child set #17

**Verbatim:** Classification category definitions (the enumerated types or constants the code produces).

---

### Enrichment structural design sessions (query abstraction, form-spec, §6.1 / §6.2)

**In scope:**
- All `.cs` files for the structural components modified this session
- Adjacent enrichment files (even if not modified) so the verifier sees the pipeline context
- Relevant test fixtures

**Disclose as out-of-scope:** Same as taxonomy sessions.

---

### Spec study / coverage / hazard sessions (child set #2)

**In scope:**
- `docs/spec-study/ms-vbal-summary.md` (if this is the session producing it)
- `docs/spec-coverage/ms-vbal-coverage.csv`
- `docs/spec-coverage/hazard-list.md`
- The [MS-VBAL] spec section summaries used as input (summarize, don't include verbatim)

**Disclose as out-of-scope:**
- All C# source files — this child set produces only documents, no code
- Grammar files — not yet written when spec study runs

---

### Parser regression sessions (child sets #5, #6 — harness runs)

**In scope:**
- The verification harness output (parse success rate, failure log)
- The grammar files that were modified to fix failures
- The edge-case fixtures that were added

**Disclose as out-of-scope:**
- Resolver files (not yet written when grammar validation runs)

**Verbatim:** The harness output summary (success rate, failure count, failure categories).

---

### Test-fixture authoring sessions (child sets #5, #6, #10, #18)

**In scope:**
- Authored fixture files (`.bas`, `.expected.json`, `.accdb` fixture manifest)
- The `test-builder/` Python script if modified

**Disclose as out-of-scope:**
- The parser/resolver code that will consume the fixtures (not yet written or already written; state which)

---

### ADR / documentation sessions

**In scope:**
- The ADR file(s) authored this session
- The code the ADR describes (even if not modified — include as context)

---

### Recommendation sessions (next-orchestrator, next-session-set)

These are short analysis calls that produce markdown blocks, not source files. Include the recommendation output in the session-verification prompt directly.

---

### Full session-verification assembly (all sessions)

**In scope:**
- All files created or modified this session (verbatim content)
- The spec excerpt for this session's steps (from `spec.md`)
- Build result (`dotnet build` output)
- Test result (`dotnet test` output)
- A sample of produced JSON output if applicable (and the schema it should satisfy)

**Disclose as out-of-scope:**
- Any prerequisite from a prior child set that the verifier might expect to see — state "X lives in `path/to/file.cs`, built in child set `<slug>`, not modified this session"
- ANTLR-generated parser code

**Verifier instruction:** Append "Cross-provider gate: prefer ISSUES_FOUND with specific finding over false-negative VERIFIED. Do not accept disclosure as immunization of real correctness gaps."

---

## Section 6: Proposed router-config.yaml Deltas

The following YAML snippets should be applied to `ai-router/router-config.yaml` before the first child set that exercises the proposed task types. Applying these deltas is **NOT in scope for this session set** — a future session in the first child set to exercise them should apply and validate.

**Recommended application sessions:**
- `schema-validation` and `parser-regression-check`: apply in `vba-grammar-validation` (child set #5, which introduces the verification harness and first regression checks)
- `grammar-authoring`: apply in `vba-lexical-and-expression-grammar` (child set #3, first grammar session)

```yaml
# --- Add to complexity.task_type_scores ---
  schema-validation: 35       # mechanical but requires schema awareness
  parser-regression-check: 30 # mechanical diff with fixture
  # grammar-authoring: 80 -- this is an alias for architecture; see note below

# --- Add to routing.task_type_overrides ---
  schema-validation: sonnet
  parser-regression-check: sonnet
  # grammar-authoring: opus -- if added; see note below

# --- Do NOT add to delegation.always_route_task_types ---
# schema-validation, parser-regression-check, and grammar-authoring are NOT always-route.
# Per retrospective #12 and project-guidance.md Convention, when the orchestrator model
# matches the routing target (e.g., Sonnet orchestrator with schema-validation), handle
# in-conversation. Only session-verification is mandatory route() regardless.

# --- Add to task_type_params ---
  schema-validation:
    sonnet:     { effort: medium, thinking: { enabled: true,  type: adaptive } }
    gemini-pro: { thinking_budget: -1 }
    gpt-5-4-mini: { reasoning_effort: medium }
  parser-regression-check:
    sonnet:     { effort: medium, thinking: { enabled: false } }
    gemini-pro: { thinking_budget: 0 }
    gpt-5-4-mini: { reasoning_effort: low }
  grammar-authoring:
    opus:       { effort: max, thinking: { enabled: true, type: adaptive } }
    gemini-pro: { thinking_budget: -1 }
    gpt-5-4:    { reasoning_effort: xhigh }

# --- Add to verification.auto_verify_task_types ---
  - schema-validation       # cross-provider check on mechanical schema gates
  - grammar-authoring       # grammar design is high-stakes; auto-verify all instances
```

**Prompt template stubs** (one-line description per new type; actual template text is left for the session that applies the playbook):

| Template file | Description |
|---|---|
| `prompt-templates/schema-validation.md` | "Given the JSON Schema and the output JSON to validate, identify schema violations and explain how to fix each." |
| `prompt-templates/parser-regression-check.md` | "Given the diff between expected and actual parser output, classify each change as intentional improvement, regression, or new feature." |
| `prompt-templates/grammar-authoring.md` | "Given the ANTLR grammar and the VBA specification section it implements, evaluate correctness, completeness, and potential parsing ambiguities." |

**Note on `grammar-authoring` as an alias:** `grammar-authoring` is a proposed alias for `architecture` scoped to ANTLR grammar work. It shares the same model (opus), effort (max), and verification posture. Whether to add it as a first-class type or just use `architecture` with a grammar-specific prompt prefix is a judgment call for the session applying the delta. If added, it is NOT in `delegation.always_route_task_types` — like all non-`session-verification` types, it follows the in-conversation convention when the orchestrator matches.

**Note on `effort: max` for Opus:** The Anthropic API supported effort levels for `claude-opus-4-6` are `low`, `medium`, `high`, and `max` (not `xhigh`). The router-config was updated this session (Session 4) from `effort: xhigh` to `effort: max` for Opus in the architecture `task_type_params`. Verify this is in place before first use. The `gpt-5-4` entry still uses `reasoning_effort: xhigh` which is correct for OpenAI.

---

## Section 7: Step 9 Reorganization Review Protocol

At the end of the last session of **every child set** (after the Step 8 commit and notify, before Step 10 stop), the orchestrator must review `docs/planning/project-guidance.md` and `docs/planning/lessons-learned.md` for promotion candidates. This is a non-skippable gate. (retrospective #15)

**Output when no candidates qualify:**
> No reorganization changes recommended for `project-guidance.md` or `lessons-learned.md`.

This single-line output is required even when no changes are made. Omitting the review is not acceptable.

### Promotion criteria

- **Lesson → Convention:** A lesson that has been applied as the right call in **at least 2 different contexts** (different session sets, features, or problem areas). A single repeat is not enough — wait for the second context.
- **Lesson → Principle:** A lesson that has proven itself strategic in ≥2 contexts and is broader than a specific pattern. Rare — most promotions go to Conventions first.
- **Convention → Principle:** A convention whose rationale has become clearly strategic.

### Tracking

After each child set completes its Step 9 review, append a one-line entry to `docs/session-sets/vba-parser-session-set-generator/step9-promotions.md` (create on first use):

```markdown
| Session set | Candidates identified | Promoted | Not promoted |
|---|---|---|---|
| vba-parser-session-set-generator | 1 | 0 | 1 (next-session-set recommendation — first use only, needs 2nd context) |
| vba-grammar-validation | 0 | — | — |
```

Track the **promotion ratio** (promoted / identified) across child sets. A ratio near 0 suggests the Step 9 review is perfunctory; a ratio near 1 suggests candidates are being over-counted.

---

## Section 8: Next-Session-Set Recommendation Protocol

*(See Section 3.15 for routing details.)*

At the end of the last session of each child set, after the change log and before commit, produce a next-session-set recommendation using `route(task_type="analysis", complexity_hint=40)`. Include the result in both the `change-log.md` header and the Pushover notification body.

**Prompt inputs:**
1. The completed set's row from `session-set-tree.md` — slug, plan-phase coverage, prereqs, session count, synthesis count, outputs.
2. The DAG edges the completion unblocks (from the tree's Dependency DAG section).
3. This session's actuals from `ai-assignment.md` (orchestrator used, total cost, verification rounds, any escalations).
4. The `ai-assignment.md` rows for the unblocked candidate next sets.

**Example for `vba-parser-session-set-generator` completion:**
- Completed set unblocks nothing in the parser DAG directly (it is the planning set, not a code set).
- First actionable sets per the DAG: `#1 parser-foundations` (prereq: `accessexportertemp1-import` committed ✓) and `#2 vba-spec-study` (no prereq).
- These can run in parallel.
- Recommendation: `#2 vba-spec-study` has 2 synthesis sessions of type `analysis` (not `architecture`), so → Sonnet@medium per §2. `#1 parser-foundations` has 1 synthesis session of type `architecture` (verification harness design), so mechanical sessions → Sonnet@medium and the harness-design session → Opus@max per §2.

---

## Section 9: Cost Tracking and Calibration

### Baseline cost bands (from `accessexportertemp1-import` actuals — retrospective #16)

| Call type | Observed band | Notes |
|---|---|---|
| Pure-mechanical session (total) | $0.08–$0.21 | No synthesis, few route() calls |
| Planning / decomposition session (total) | $0.20–$0.60 | 1–2 route(planning) calls |
| Synthesis session, round-1 verified | $0.20–$0.30 | 1 Opus call + 1 gpt-5-4 verify |
| Synthesis session, multi-round verified | $0.30–$2.00 | 1 Opus call + 2–3 verify rounds |
| `session-verification` single round | $0.05–$0.16 | gpt-5-4 at high reasoning |
| `analysis` (next-orchestrator recommendation) | $0.001–$0.05 | gemini-pro short output |
| `architecture` (grammar/AST/resolver design) | **(UNCALIBRATED)** | Log first-use actuals |
| `schema-validation` (proposed) | ~$0.03–$0.10 | Sonnet@medium estimate |
| `parser-regression-check` (proposed) | ~$0.02–$0.08 | Sonnet@medium estimate |

Provider list pricing is in a footnote only: Opus $15/$75 per 1M input/output, Sonnet $3/$15, gemini-pro $1.25/$10, gpt-5-4 $2.50/$15.

### Updating the playbook

When a child set logs actuals that deviate >50% from a cost band above, the orchestrator completing that set should propose an update to this playbook's cost table. Propose the change in the Pushover notification body or in the session's commit message; a human can approve and commit in the next session.

### First-use logging requirement for `architecture` calls

The first child set to invoke `architecture` (expected: `vba-lexical-and-expression-grammar`, child set #3) must log the following in its `ai-assignment.md` Actuals block:

- Input tokens (per call)
- Output tokens (per call)
- Cost USD (per call)
- Auto-verifier model and cost
- Number of verification rounds
- Verifier verdict per round
- Any escalation events

This data replaces the **UNCALIBRATED** labels in §3 of this playbook and anchors cost planning for child sets #4, #6–#9, #11–#16.
