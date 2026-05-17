# VBA Lexical and Expression Grammar Spec

> **Purpose:** Author the lexical and expression sections of the ANTLR4 `Vba.g4` grammar — tokens, comments, line continuation, identifiers, literals, and expression precedence (primary → member access → call → unary → binary → assignment) — covering everything the 12 module-level fixtures exercise at the lexical and expression level.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/vba-lexical-and-expression-grammar/`
> **Prerequisite:** docs/session-sets/parser-foundations/, docs/session-sets/vba-spec-study/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #3. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `vba-lexical-and-expression-grammar` |
| **Plan-phase coverage (column 3)** | Phase 1.4 lexical + expression rules |
| **Prereqs (column 4)** | #1, #2 |
| **Sessions (rough) (column 5)** | 4 |
| **Synthesis count (column 6)** | 2 (lexical grammar, expression grammar) |
| **Outputs (column 7)** | `AccessHarvester.Vba.Grammar/Vba.g4` (lexical + expression sections), unit tests under `tests/AccessHarvester.Vba.Grammar.Tests/` |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements the lexical and expression portions of **Phase 1.4** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. Produces `AccessHarvester.Vba.Grammar/Vba.g4` (lexical + expression sections) plus unit tests under `tests/AccessHarvester.Vba.Grammar.Tests/`. The statement and module sections are owned by the next child set, `vba-statement-and-module-grammar` (#4); deferring those keeps each child to ≤2 synthesis sessions per retrospective adopt-now `#1`.

The grammar is clean-room from MS-VBAL. The `vba-spec-study` (#2) summary, coverage matrix, and hazard list are the source of truth for which constructs to cover; the 12 test fixtures (especially `01`, `02`, `03`, `06`, `07`) are the regression corpus.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #3.

---

## Feature 1: Lexical Grammar

### Scope

Tokens, identifiers, literals (numeric, string, date, boolean, special), comments, line continuation (`_`), line numbers, statement separators (newline + colon), case-insensitive matching. Contextual keywords: VBA reserved words usable as identifiers in some positions are handled via parser rules where MS-VBAL allows.

Test-case anchors: `01-modCustomer.bas` (line numbers + type-decl chars), `07-modLegacyMixed.bas` (legacy syntax).

### Standards

- Lexical rules are foundational — every later child set inherits whatever this session lands. Bias toward MS-VBAL fidelity over expedience.
- Type-declaration character suffixes (`$`, `%`, `&`, `!`, `#`, `@`) are covered now, not deferred.
- Preprocessor directives are tokenized but not evaluated; their full handling lives in `parser-infrastructure` (#6) per Phase 2.5.

## Feature 2: Expression Grammar

### Scope

Bottom-up: primary → member access → call → unary → binary (by precedence) → assignment. Bang operator (`!`) for implicit collection/default member access. Member chains with `.` and `!` mixed (e.g., `Forms!frm!ctl.Property`). `Nz`/`IIf`/`Switch`/`Choose` callable forms in expression context.

Test-case anchors: `02-clsCustomerForm.cls` (bang, member chains), `03-modUtilities.bas` (`.`/`!` mix), `06-qryCustomerSummary.sql` (Nz/IIf/Switch in non-VBA contexts — informs lexical reuse for #12 JET SQL).

### Standards

- Operator precedence follows MS-VBAL §5.6 verbatim. Where ANTLR's precedence climbing differs from MS-VBAL's, comment the deviation in the grammar.
- The bang operator's lexical-vs-parse status is a known hazard (per `vba-spec-study` hazard list). Resolve in this session, not later.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
```

## Sessions
### Session 1 of 4: Lexical Grammar

- Read the relevant `vba-spec-study` outputs (summary §lexical, coverage matrix lexical rows, hazard list lexical entries) plus `01`, `07` fixtures.
- Route an `architecture` synthesis call producing lexical rules (1 of 2 synthesis sessions).
- Land lexical section of `Vba.g4` + targeted unit tests under `tests/AccessHarvester.Vba.Grammar.Tests/Lexical/`.

### Session 2 of 4: Expression Grammar

- Read coverage matrix expression rows + hazard list expression entries + `02`, `03`, `06` fixtures.
- Route an `architecture` synthesis call producing expression rules (2 of 2 synthesis sessions).
- Land expression section of `Vba.g4` + tests under `tests/AccessHarvester.Vba.Grammar.Tests/Expressions/`.

### Session 3 of 4: Mechanical Tightening

- Run the `parser-foundations` harness against `01`, `02`, `03`, `06`, `07`. Classify failures; fix grammar bugs in priority order.
- Route `schema-validation` (mechanical) on the resulting grammar against the coverage matrix rows for lexical + expression.
- Land tightening commits + parse-failure log entry under `docs/spec-coverage/parse-failure-log.md` (full file authored by #5).

### Session 4 of 4: Verifier-Surfaced Cleanup + Cross-Set Handoff

- Resolve any cross-provider verifier findings deferred from prior sessions.
- Land a brief handoff note in this set's change-log naming the lexical/expression entry-point rules `vba-statement-and-module-grammar` (#4) plugs into.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Lexical grammar synthesis (Session 1) and expression grammar synthesis (Session 2):** `route(task_type="architecture", complexity_hint=80)`. -- `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band. Per the playbook (Session 4 of generator) and retrospective `#13`, grammar authoring is xhigh-effort Opus.
- **Mechanical tightening (Session 3):** `route(task_type="schema-validation")` for grammar-vs-matrix correspondence. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`. Harness diff runs are mechanical and not routed.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)` per `task_type_overrides`. Verification prompts must include the relevant test-case fixtures and the coverage matrix excerpt — per `lessons-learned.md` "Send Related Files To The Cross-Provider Verifier".
- **Change-log (last session):** `route(task_type="summarization")` with `max_tier=1` fallback.

## Open questions for the human

None — grammar scope is the lexical + expression portions of MS-VBAL Phase 1.4 per the tree.
