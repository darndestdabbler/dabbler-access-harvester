# Session Set Assignment: `harvester-sql-migration-generator`

> Authored via `route(task_type="analysis")` (gemini-pro) per workflow Step 3.5.
> Model identifiers normalized to the repo's router aliases
> (`opus` = `claude-opus-4-8`, `sonnet` = `claude-sonnet-4-6`, `gemini-pro`,
> `gpt-5-4`). Cost figures are qualitative; `Actuals` are filled at closeout.

Adds a `generate-migration` CLI subcommand that produces a SQL Server migration
script from harvested artifacts. This is the **final** session set in the
harvester development arc; after it the repo is consumer-driven-only.

## Session 1 of 3: Input contract + Access->SQL Server DDL generation

This session builds the foundational DDL (schema) generation logic: mapping
Access types to SQL Server and handling constraints (PK, FK, nullability,
defaults) with honest degradation flagging for unmappable columns.

*   **Recommended orchestrator**: `opus` (`claude-opus-4-8`), effort `medium`-`high`.
    *   **Rationale**: This session establishes the core DDL generation, type
        mapping, and the hermetic-test foundation for the whole feature.
        Precision on SQL-dialect specifics and broad test-case construction
        warrant a high-capability orchestrator, even though the type map itself
        is mostly mechanical correspondence.
*   **Estimated routed cost (qualitative)**: `analysis` → gemini-pro (tier 2);
    `test-generation` → gemini-pro (tier 2, cross-provider); `session-verification`
    → `gpt-5-4` (tier 3, cross-provider, mandatory). `code-review` is handled
    in-conversation per the repo's same-provider rule (code-review override =
    `sonnet` = the orchestrator's own Anthropic provider).

| Step | Routing | Cheapest capable model |
| :--- | :--- | :--- |
| 1. Confirm sanitize input contract + assemble generator input model | `handle-in-conversation` (mechanical reads of committed artifacts) | `opus` (orchestrator) |
| 2. Implement Access->SQL Server type map + `CREATE TABLE` (types/null/defaults/PK) | `handle-in-conversation` (mechanical; mirrors `TableDdlBuilder`) | `opus` (orchestrator) |
| 3. Implement `FOREIGN KEY` constraint generation from `ir/Relationships` | `handle-in-conversation` (mechanical) | `opus` (orchestrator) |
| 4. Author hermetic tests for DDL generation logic | `route(task_type="test-generation")` | `gemini-pro` |
| 5. Code review | `handle-in-conversation` (per same-provider rule) | `sonnet` |
| 6. Session verification | `route(task_type="session-verification")` | `gpt-5-4` (cross-provider, per rule) |

*   **Actuals (Session 1)**:
    *   **Orchestrator**: Claude Opus 4.8 (`claude-opus-4-8`), effort medium.
    *   **Routed cost**: ~$0.41 total — `analysis` (gemini-pro) $0.019;
        `test-generation` (gemini-pro) $0.040; `session-verification`
        (gpt-5-4) R1 $0.229 + R2 $0.124. Code-review handled in-conversation
        per the same-provider rule. Routing kept reasoning off the Opus
        orchestrator's own (expensive) rate.
    *   **Verification verdict**: VERIFIED (gpt-5-4 Round 2). Round 1 returned
        ISSUES_FOUND with 3 Major findings (platform-dependent newlines;
        unsafe `#...#` default translation; test-coverage gaps) — all fixed
        in-session plus 2 minor honesty/determinism improvements; Round 2
        confirmed each fix with no regressions.

*   **Next-session recommendation**: Proceed to Session 2 — data-load generation,
    FK-safe topological ordering with cycle detection, and `SET IDENTITY_INSERT`
    per `preserveAutonumber`.

## Session 2 of 3: Data-load + identity + FK-safe ordering

Adds the data-loading portion: insertion ordering under FK constraints and
identity-column handling.

*   **Recommended orchestrator**: `opus` (`claude-opus-4-8`), effort `high`.
    *   **Rationale**: The core logic is a topological sort of the table
        dependency graph (parents before children) with cycle detection, plus
        value-escaping/typing in generated `INSERT`s. This algorithmic content
        warrants a high-capability orchestrator.
*   **Estimated routed cost (qualitative)**: `test-generation` → gemini-pro;
    `session-verification` → `gpt-5-4`. `code-review` in-conversation. `analysis`
    of the ordering design may route to gemini-pro if a second opinion is wanted.

| Step | Routing | Cheapest capable model |
| :--- | :--- | :--- |
| 1. Design FK-safe topological sort + cycle detection | `handle-in-conversation` (or `route(task_type="analysis")` for a second opinion) | `opus` / `gemini-pro` |
| 2. Implement FK-safe table ordering | `handle-in-conversation` (mechanical) | `opus` (orchestrator) |
| 3. Implement data-load SQL generation from sanitized CSVs | `handle-in-conversation` (mechanical) | `opus` (orchestrator) |
| 4. Implement `SET IDENTITY_INSERT` per `preserveAutonumber` | `handle-in-conversation` (mechanical) | `opus` (orchestrator) |
| 5. Author hermetic tests (ordering, cycle, identity, escaping, preserve/remap/mask) | `route(task_type="test-generation")` | `gemini-pro` |
| 6. Code review | `handle-in-conversation` (per rule) | `sonnet` |
| 7. Session verification | `route(task_type="session-verification")` | `gpt-5-4` (per rule) |

*   **Actuals (Session 2)**:
    *   **Orchestrator**: Claude Opus 4.8 (`claude-opus-4-8`), effort high.
    *   **Routed cost**: ~$0.285 total — `session-verification` (gpt-5-4) R1 $0.224
        + R2 $0.059; next-orchestrator `analysis` (gemini-pro) $0.002. Code review
        handled in-conversation per the same-provider rule.
    *   **Deviation from recommendation**: hermetic tests were authored
        **in-conversation** rather than via routed `test-generation` — the surface
        is brand-new and intricate (Tarjan SCC cycle detection, RFC 4180
        NULL-vs-empty, typed INSERT literals) where a routed model that cannot see
        the just-written code produces brittle/truncated tests; this is the
        precedented "author in-conversation and disclose" path, disclosed in the
        verification prompt. The R1 verifier judged the tests on merit and found
        them strong (it flagged two missing regressions, both added).
    *   **Verification verdict**: VERIFIED (gpt-5-4 Round 2). Round 1 returned
        ISSUES_FOUND with 2 Major findings — (a) the cycle fallback conflated
        cycle members with their downstream descendants and could misorder them;
        (b) the NULL-vs-empty-string contract was unproven against the real
        producer. Both fixed in-session: (a) replaced with Tarjan SCC +
        condensation ordering (descendants of a cycle now load after it; only true
        SCCs flagged) + a cycle-plus-downstream regression test; (b) documented the
        real `"" -> NULL` collapse and pinned it with a real-`CsvWriter` round-trip
        test (the producer change is out-of-scope sanitize-tier follow-up, which
        the verifier explicitly agreed was not mandatory here). R2 VERIFIED, no
        regressions. Final: 93 Migration tests, 777 hermetic Pipeline.Tests green.
    *   **Note (refactor)**: Session 1's `SqlServerDdlGenerator` was refactored into
        a shared `MigrationSchemaResolver` (single "what-survived" authority) +
        `SqlServerDdlEmitter` (pure text), with `GenerateDdl` delegating; DDL output
        is byte-identical (all 52 Session-1 DDL tests stayed green) so DDL/order/data
        never drift.

*   **Next-session recommendation (Session 3)**: Claude **Sonnet** (`claude-sonnet-4-6`),
    effort medium — routed `analysis` (gemini-pro) confirms the Session-1 plan: the
    work (CLI wiring, golden-script test, docs, closeout synthesis) is mechanical
    integration where Opus would be overkill; change-log prose routes to
    `summarization` (max_tier=1) regardless. Session 3 consumes
    `SqlServerMigrationGenerator.Generate(inputs, openTableCsv)` and emits the
    manifest from its merged degradation list.

## Session 3 of 3: CLI subcommand + end-to-end emit + docs + closeout

Integrates the generator into the CLI, adds end-to-end + golden validation, and
performs project closeout.

*   **Recommended orchestrator**: `sonnet` (`claude-sonnet-4-6`), effort `medium`.
    *   **Rationale**: Primarily mechanical integration, CLI wiring, and
        documentation. A cost-effective orchestrator suits the lower algorithmic
        complexity; closeout synthesis (change-log/disposition) routes to
        `summarization` regardless of orchestrator.
*   **Estimated routed cost (qualitative)**: `test-generation` → gemini-pro;
    `summarization` (change-log) → gemini-flash/pro with `max_tier=1`;
    `session-verification` → `gpt-5-4`; `analysis` for next-set recommendation →
    gemini-pro. `code-review` in-conversation.

| Step | Routing | Cheapest capable model |
| :--- | :--- | :--- |
| 1. Implement `generate-migration` CLI subcommand (exit codes, `--json-status`, manifest) | `handle-in-conversation` (mechanical, mirrors existing commands) | `sonnet` (orchestrator) |
| 2. End-to-end emit against a fixture + hermetic golden-script test | `route(task_type="test-generation")` | `gemini-pro` |
| 3. Update interpretation + install guides | `handle-in-conversation` | `sonnet` (orchestrator) |
| 4. Update ROADMAP + change-log | `route(task_type="summarization", max_tier=1)` for change-log prose | `gemini-flash`/`gemini-pro` |
| 5. Closeout + Step 9 reorg review + next-set recommendation | `handle-in-conversation` + `route(task_type="analysis")` for next-set rec | `sonnet` / `gemini-pro` |
| 6. Code review | `handle-in-conversation` (per rule) | `sonnet` |
| 7. Session verification | `route(task_type="session-verification")` | `gpt-5-4` (per rule) |

*   **Actuals (filled after session)**: TBD

*   **Next-session recommendation**: The harvester development arc is complete.
    The repository becomes consumer-driven-only — new sets only on a concrete
    downstream-consumer request.
