# Verification Round 1

1. **Sampler correctness:** `sampler.py` is substantively correct against the stated sampling policy. The stratum→table mapping in `POLICY` matches the spec: `enum_decl/function/module_var/sub/enum_value` come from `Symbols`, `macro` from `Macros`, `query` from `Queries`, and `lookup_table` from `Tables`. `_fetch_stratum()` correctly restricts all symbol strata to `Interpretation1 IS NOT NULL` and `is_dead = 0`, which matches the “Session-4-interpreted” and “live” requirements for the relevant strata. The `module_var` exclusion is implemented correctly via `AND (kind != 'module_var' OR declared_in_fqn IS NULL)`; because the query already pins `kind = ?`, this becomes an effective `declared_in_fqn IS NULL` filter only for `module_var`, excluding procedure-locals as intended. Determinism is also implemented correctly: each population is ordered by `fqn`, a local `random.Random(seed)` instance is used, and sampled subsets are re-sorted before emission, so repeated runs with the same DB contents and seed produce the same sample. I do not see a sampler-policy bug in the provided code.

2. **Neutral-evidence guarantee:** This is the biggest problem area. For `Symbols`, `_build_neutral_prompt()` does correctly pass `callee_summaries=[]`, does supply `caller_summaries` with withheld text rather than prior summaries, and does pass `enum_interpretation=None`. It also does not explicitly pass `Interpretation1` as a separate prompt argument. However, it still loads `Interpretation1` and `Confidence1` into the `SymbolRow` via `_get_symbol()`, then hands that object to the unchanged Session-4 `prompt_builder`. The same pattern exists for `Macros`, `Tables`, and `Queries`: each row is loaded with `Interpretation1` populated and then passed to `build_macro_prompt`, `build_table_prompt`, or `build_query_prompt`. Because Session-4 prompt builders are reused unchanged, Session 5 has not actually stripped prior interpretation context at the object boundary. If any of those builders render `interpretation1` from the row object—as their Session-4 role strongly suggests—they can still leak prior interpretation into the “independent” prompt. So the code partially satisfies the neutral-evidence spec on the explicit summary arguments, but it does **not** guarantee neutrality overall.

3. **Idempotency:** The current `_already_written()` implementation is not robust. It scans all four tables in a fixed order and returns on the first matching `fqn`, but the sampled item already includes its authoritative `table`. If the same `fqn` exists in more than one table, this can produce false skips or false rewrites depending on which table is encountered first. The check should be table-scoped, not global. The skip-history dedup path also has a structural problem: `_already_skipped()` keys deduplication on `(fqn, opinion_slot, reason)`, but `_record_skip()` calls `_db.write_skip()` without passing `opinion_slot`. Unless the out-of-scope helper infers the slot from some other channel, the history row cannot be reliably written with the same key that deduplication later queries, which makes duplication prevention and per-opinion attribution unreliable.

4. **Opinion-slot routing:** This part is correct. `_OPINION_MODEL` maps opinion 2 to `"opus"` and opinion 3 to `"gpt-5-4"`, and `run_opinion()` calls `ar.query(...)` directly with that mapped model. It does not call `route()`, so it bypasses any tier picker as required.

5. **Error handling:** The retry path does correctly guard for transient provider errors on both the initial query and the retry-after-parse-failure query. `_is_transient()` is simplistic but it does cover the explicit 429/503/timeout/rate-limit/overloaded cases. The larger issue is classification: `skip_reason` is always set to `skipped-provider-error-opinionN`, and that same reason is used not only for transient provider exceptions, but also for prompt-construction failures and final parse failures. Those are not provider errors. As written, local prompt bugs and format/parsing failures are recorded in the same bucket as transient provider outages, and `stats["skipped_provider_error"]` is incremented for prompt-build failures as well. That makes the audit trail inaccurate and would also distort the generated report.

6. **Test coverage:** The sampler tests are solid for determinism, stratum filtering, cap enforcement, expected table mapping, and save/load round-trip. The `independent_runner` tests are not adequate for the highest-risk behaviors. They do not actually verify the neutral-evidence guarantee: nothing asserts that `Interpretation1` is stripped from row objects passed to the prompt builder, and nothing inspects the `callee_summaries` / `caller_summaries` / `enum_interpretation` arguments. They also do not cover transient provider-error handling, parse-failure retry behavior, skip-history deduplication, or `_already_written()` behavior across `Macros`, `Tables`, and `Queries`. The opinion→model mapping is tested, but the use of `query()` instead of `route()` is not directly asserted. In short: sampler coverage is good; independent-runner coverage misses the exact invariants that matter most for Session 5.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "independent_runner.py::_build_neutral_prompt and _get_symbol",
      "summary": "Prior interpretation data is still loaded into SymbolRow/MacroRow/TableRow/QueryRow objects and passed to the unchanged Session-4 prompt builders, so neutral-evidence independence is not actually guaranteed.",
      "fix": "Construct sanitized row objects with Interpretation1/Confidence1 cleared (and analogous fields for macros/tables/queries) before calling the prompt builders, or add explicit neutral-mode builder APIs that ignore prior-interpretation fields; add tests that assert these fields are absent from builder inputs."
    },
    {
      "severity": "Major",
      "location": "independent_runner.py::_already_written",
      "summary": "The already-written check is table-blind and can return the wrong result if the same FQN exists in more than one table.",
      "fix": "Change _already_written to accept the sampled item's table and query only that table's InterpretationN column instead of scanning all four tables."
    },
    {
      "severity": "Major",
      "location": "independent_runner.py::_record_skip",
      "summary": "Skip deduplication keys on opinion_slot, but the skip writer is called without opinion_slot, so per-opinion attribution and duplicate guarding depend on hidden helper behavior.",
      "fix": "Extend _db.write_skip to take opinion_slot explicitly and pass it through from _record_skip, then keep _already_skipped keyed on the same persisted fields."
    },
    {
      "severity": "Major",
      "location": "independent_runner.py::run_opinion and report.py::generate_report",
      "summary": "Prompt-build failures and final parse failures are recorded as skipped-provider-error-opinionN, conflating non-provider failures with transient provider errors and preventing accurate reporting.",
      "fix": "Use distinct reasons and counters for transient provider errors, prompt-build failures, and parse failures (for example skipped-provider-error-opinionN and skipped-parse-failure-opinionN), and update report.py to report them separately."
    },
    {
      "severity": "Major",
      "location": "tests/test_independent_runner.py",
      "summary": "The tests do not cover the core Session-5 invariants: neutral prompt sanitization, transient error handling, parse-retry behavior, skip deduplication, or multi-table idempotency.",
      "fix": "Add mocked prompt_builder/router tests that assert sanitized builder inputs, direct query() usage, transient-error skip recording, retry-on-parse-failure behavior, skip dedup across reruns, and _already_written behavior for Macros/Tables/Queries."
    }
  ]
}
```

---

# Verification Round 2

1. `_build_neutral_prompt` for `Symbols` is fixed correctly. In the `Symbols` branch, the code fetches the full `SymbolRow`, then immediately applies `dataclasses.replace(sym, interpretation1=None, confidence1=None)` before calling `_pb.build_symbol_prompt(...)`. That is the correct neutralization point: the builder receives a sanitized row, not the DB-derived row. The new `TestNeutralPromptSanitization.test_symbol_row_interpretation_is_cleared` captures the object passed into `build_symbol_prompt` and asserts both `interpretation1 is None` and `confidence1 is None`, so this concern is verified in both code and test.

2. For `Macros`, `Tables`, and `Queries`, the row fetches do omit `Interpretation1`. The `Macros` query selects only `fqn, source_xml, is_dead`; the `Tables` query selects only structural columns; the `Queries` query selects only SQL/deadness/equivalence fields. Each branch then constructs the corresponding row object with `interpretation1=None` explicitly. That means `Interpretation1` cannot leak through these local SELECTs. Code-wise this concern is verified. Test-wise, only the `MacroRow` sanitization path is directly asserted; there is no equivalent test for `TableRow` or `QueryRow`, and no test that specifically proves SQL-layer omission for those two types.

3. `_already_written` is now correctly table-scoped. Its signature is `_already_written(con, fqn, opinion_slot, table)`, and it executes `SELECT Interpretation{opinion_slot} FROM {table} WHERE fqn = ?`. In `run_opinion`, it is called with the sampled item's `table`, so the existence check is no longer global or cross-table. This concern is verified in code. The expanded tests do exercise both `Symbols` and `Macros`, so they catch basic per-table behavior, but they do not construct a same-`fqn` collision across two tables to prove the scoping invariant under the exact failure mode that M1 was intended to prevent.

4. `_write_skip_for_opinion` is correctly keyed on `(fqn, opinion_slot, reason)` and uses the supplied opinion slot on insert. The guard is `_already_skipped(con, fqn, opinion_slot, reason)`, whose SQL filters on those exact three fields. If no row exists, `_write_skip_for_opinion` inserts the same `opinion_slot` value into `InterpretationHistory`. All three runtime call sites pass the current `opinion_slot`, so the prior slot-0 bug is fixed. This concern is verified in code. The provider-error idempotency test confirms duplicate writes are suppressed for one reason/slot combination, but it does not directly prove that `reason` is part of the dedup key; a regression to dedup-by-`(fqn, opinion_slot)` alone would still satisfy the current provider-only idempotency test.

5. The three skip reasons are distinct, slot-specific, and used consistently in the runner. `_skip_reason_provider`, `_skip_reason_prompt`, and `_skip_reason_parse` all emit different strings, and each includes `opinion{slot}` in the suffix. The runner uses:
   - `_skip_reason_prompt(opinion_slot)` for prompt-build exceptions,
   - `_skip_reason_provider(opinion_slot)` for transient provider failures on both first call and retry,
   - `_skip_reason_parse(opinion_slot)` for parse failure after retry.
   The `stats` structure also keeps separate counters for provider, prompt, and parse skips. Code-wise this concern is verified. `TestSkipReasons` checks distinctness and slot inclusion at the string level, but only the provider path is exercised end-to-end at runtime; prompt-error and parse-failure persistence are not covered by execution tests.

6. The new tests do assert several of the invariants they explicitly check, but coverage is still incomplete relative to the fixes being confirmed. `TestNeutralPromptSanitization` genuinely verifies sanitized `SymbolRow` and `MacroRow` inputs plus `callee_summaries=[]` and `enum_interpretation=None`. `TestTransientErrorHandling` genuinely verifies that a provider-error skip row is recorded for slot 2 and that rerunning does not duplicate that provider skip. `TestAlreadyWritten` genuinely verifies basic table-parameterized behavior on `Symbols` and `Macros`. However, there are still meaningful gaps:
   - no direct `TableRow`/`QueryRow` neutralization test,
   - no same-`fqn` cross-table test for `_already_written`,
   - no test that two different reasons for the same `(fqn, opinion_slot)` are both preserved, which is the actual reason-keyed dedup invariant,
   - `test_transient_error_is_recorded_and_run_continues` does not actually demonstrate continuation past a failed item because the sample contains only one item.
   
Net: C1, M1, M2, and M3 are verified in code. The remaining issues are with M4 test coverage completeness, not with the core fixes themselves.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "tests/test_independent_runner.py::TestNeutralPromptSanitization",
      "summary": "Neutral-prompt sanitization is only asserted for Symbols and Macros; Tables and Queries are verified only by code inspection.",
      "fix": "Add tests that seed non-null Interpretation1 for a Table and a Query, capture the objects passed to build_table_prompt/build_query_prompt, and assert interpretation1 is None."
    },
    {
      "severity": "Minor",
      "location": "tests/test_independent_runner.py::TestAlreadyWritten",
      "summary": "The table-scoping fix is not exercised under a same-FQN cross-table collision, so the specific M1 failure mode is untested.",
      "fix": "Create identical FQNs in two different tables, populate InterpretationN in only one table, and assert _already_written returns True only for the matching table and False for the other."
    },
    {
      "severity": "Minor",
      "location": "tests/test_independent_runner.py::TestTransientErrorHandling",
      "summary": "The tests do not prove that skip deduplication includes reason, and the 'run continues' test does not actually include a second item to verify continuation.",
      "fix": "Add one test that records two different skip reasons for the same (fqn, opinion_slot) and asserts both rows persist, and update the continuation test to use at least two sampled items so a post-failure item is demonstrably processed."
    }
  ]
}
```

---

# Verification Round 3

1. `test_table_row_interpretation_is_cleared` and `test_query_row_interpretation_is_cleared` both implement the intended check correctly. The table test explicitly seeds a non-null database value by inserting `tblTest` into `Tables` with `Interpretation1='pre-existing interpretation'`. The query test does the same for `qryTest` in `Queries` with `Interpretation1='pre-existing interpretation'`. Each test then intercepts the corresponding prompt-builder entrypoint (`build_table_prompt` / `build_query_prompt`) by monkeypatching `interpret.prompt_builder`, captures the row object passed into the builder, invokes `_build_neutral_prompt(...)`, and asserts that `captured[0].interpretation1` is `None`. That is the exact regression condition that needed coverage: verify sanitization of a row that was non-null in storage before prompt construction. They do not separately assert the capture list length, but `captured[0]` makes builder invocation mandatory, so the test would fail if interception did not occur.

2. `test_table_scoping_prevents_cross_table_false_positive` reproduces the cross-table collision scenario correctly. It inserts the same `sharedFQN` into both `Symbols` and `Macros`, sets `Interpretation2` only in the `Symbols` row, commits, and then checks `_already_written(self._con, shared_fqn, 2, "Symbols") is True` and `_already_written(self._con, shared_fqn, 2, "Macros") is False`. That is the right shape for the original bug: identical FQN string across tables, one table populated for the target slot, the other not populated, and assertions scoped by table parameter. The extra `Interpretation1='i1'` in the `Symbols` insert is irrelevant noise but does not undermine the test; the key fact is that slot 2 is written only in `Symbols` and the function is expected to respect table boundaries.

3. `test_two_different_reasons_for_same_fqn_and_slot_are_both_preserved` is sufficient for the reason-keyed dedup gap when read together with the already-present idempotency test for same-reason skips. The new test calls `_write_skip_for_opinion(...)` twice with the same `fqn`, `fqn_kind`, and `opinion_slot`, but with two different `reason` strings, then asserts that two `InterpretationHistory` rows exist for that `(fqn, opinion_slot)`. On its own, that demonstrates distinct reasons are not collapsed. Combined with `test_transient_error_skip_is_idempotent`, which already proves the same reason is deduplicated on rerun, the suite now covers both sides of the dedup contract: same reason dedups, different reasons both persist. That closes the gap the Round 2 note identified.

4. `test_run_continues_past_failed_item` does use a genuine two-item sample and does verify continuation after the first failure in a meaningful way. The sample contains exactly two not-yet-written items: `modTest.MySub` and `AutoExec`. The fake router increments a counter, raises `HTTP 503 transient` on the first `query(...)` call, and returns a valid result on the second call. The test then asserts `skipped_provider_error == 1` and `written == 1`. Those aggregate outcomes are consistent with the intended behavior: one item failed transiently and another item still completed successfully, so the run did not abort after item 1. The test could be even stronger by asserting `call_count == 2` or checking that `AutoExec` specifically received `Interpretation2`, but for the stated regression—confirming the runner continues past a failed first item—the current assertions are adequate.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```