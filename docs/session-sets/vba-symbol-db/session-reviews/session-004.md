# Verification Round 1

### 1. Topology correctness

`order_leaves_first` does produce a leaves-first schedule for the graph shape the runner uses. The input edges are caller → callee, and Tarjan SCC emission in this implementation naturally yields reverse topological order for acyclic chains: for `A -> B -> C`, the result is `C, B, A`, which is what the spec wants. Multi-node SCCs are also handled correctly: a mutually recursive component is emitted before any outside caller that depends on it, and the nodes inside the SCC are made deterministic by sorting their FQNs.

The self-loop behavior is only partially correct. The implementation drops **all** self-loops before SCC detection. That is a defensible heuristic for the VBA function-name return-value pattern (`FunctionName = ...`), which should not be treated as recursion. But it also suppresses genuine self-recursive procedures if any exist, so those would never be tagged as cycle members. There is also an internal inconsistency: the module/docstring says single-node self-loops count as cycles, while the actual code and tests explicitly do not.

- **Issue →** Self-loop handling is intentionally heuristic, but the docs/comments disagree with the implementation, and real self-recursion would be missed.
- **Location →** `tools/symbol-db/interpret/topology.py`
- **Fix →** Keep the leaves-first Tarjan logic, but either update the docstrings/comments/tests to match the current heuristic, or distinguish `FunctionName = expr` from real self-calls using reference-kind/AST data before dropping self-loops.

### 2. Confidence-thresholded escalation

The two threshold helpers themselves match the spec exactly. `_should_run_opinion2` returns true only when opinion 1 exists and `Confidence1 < 0.75`. `_should_run_opinion3` returns true only when opinions 1 and 2 exist and their average is `< 0.70`. There is no off-by-one or wrong-comparator defect here.

The larger issue is scope, not math. This escalation logic is only used in the **symbol** pass. `interpret_macros` and `interpret_lookup_tables` always take a single opinion, and there is no query pass at all. So the threshold logic is correct where it is used, but the overall pipeline does not apply the Session 4 escalation model uniformly across all artifact classes named in the spec.

- **Issue →** The escalation rules are implemented correctly for symbols, but not applied to macros/tables, and queries are unimplemented.
- **Location →** `tools/symbol-db/interpret/runner.py`
- **Fix →** Reuse the same opinion-1/2/3 flow for macros/tables/queries if the spec requires parity, or narrow the spec/output claims to symbols only.

### 3. Skip policy

For **symbols**, the dead-code skip and procedure-local-variable skip are both explicitly recorded and idempotent. `_emit_skip_rows_for_excluded` checks `InterpretationHistory` for an existing `(fqn, opinion_slot=0, reason)` row before inserting, so rerunning will not duplicate `skipped-dead` or `skipped-procedure-local-var` rows.

For the rest of the skip policy, the implementation is incomplete. Deferred modules are skipped silently in `__main__.py` via `if mod.get("is_deferred"): continue`, and there is no compensating pass that writes `skipped-deferred` rows for their symbols. That is a direct miss against the explicit-skip requirement. Low-lookup-score tables are recorded with `skipped-low-lookup-score`, but that emission is **not** idempotent: reruns will append duplicate history rows. The same non-idempotence exists for dead macros and transient provider errors in the macro/table passes.

- **Issue →** Skip recording is only partially compliant: deferred symbols are never written to history, and macro/table skip rows are not idempotent.
- **Location →** `tools/symbol-db/interpret/__main__.py`, `tools/symbol-db/interpret/runner.py`
- **Fix →** Add a deferred-skip emission pass for deferred modules/symbols, and introduce a shared `already_skipped(...)` guard for macros/tables/provider-error skips just like the symbol path uses.

### 4. MD5 twin copy

For **Symbols**, `_copy_interpretations_to_twins` does the core job correctly. It selects rows whose `equivalence_canonical_fqn` points at a canonical symbol that already has `Interpretation1`, copies the opinion-1 fields into the twin, appends a history row with reason `copied-from-md5-twin`, and avoids duplicates on rerun because it only touches rows where `s.Interpretation1 IS NULL`. That part is solid.

The gap is again scope. The schema/model clearly expects equivalence handling beyond Symbols — `QueryRow` includes `equivalence_canonical_fqn` — but there is no query interpretation pass and no query twin-copy pass. So the implementation is correct for symbol twins, but not complete for all artifact types the session goal calls out.

- **Issue →** MD5-twin copy is correct and idempotent for Symbols only; Queries are uncovered.
- **Location →** `tools/symbol-db/interpret/runner.py::_copy_interpretations_to_twins`
- **Fix →** Add query interpretation plus query twin-copy logic, or explicitly narrow the session claims so they no longer promise query parity.

### 5. Output structure

The prompt contract is well formed. `_shared.txt` clearly requires a JSON object at the end, and the per-symbol templates provide concrete evidence blocks: signature, event binding, parameters, caller/callee context, and source body. The parser is also reasonably defensive. `extract_json_block` handles fenced JSON, unfenced trailing JSON, surrounding prose, and returns `None` rather than throwing on truncated/unparseable output. `parse_interpretation` clamps confidence, tolerates missing/invalid confidence, and rejects missing/empty interpretation text.

The main deficiency here is not malformed prompting or fragile parsing. It is missing evidence injection. `prompt_builder` supports a `classification` block, but `runner.interpret_module` never passes any classification data, so the static-analysis evidence path is effectively dead. That weakens grounding versus what the spec’s existing enrichment pipeline could have provided.

- **Issue →** Prompt/parse structure is sound, but a designed-for grounding input (`classification`) is never actually supplied.
- **Location →** `tools/symbol-db/interpret/prompt_builder.py`, `tools/symbol-db/interpret/runner.py`
- **Fix →** Fetch the classification data from the DB/static-analysis output and pass it into `build_symbol_prompt(...)`.

### 6. Idempotency on re-run

For completed rows, rerun behavior is good. `interpret_module` skips symbols whose `Interpretation1` is already non-null, and the macro/table passes do the same. That means a manual rerun after a partial failure will not re-spend tokens on already written artifacts. The symbol twin-copy pass is also idempotent because it only copies into still-null rows.

The weak point is failure handling during the symbol pass itself. Unlike macros/tables, symbol routing calls are not wrapped in transient-error handling. A provider 503 during `_interpret_one` will abort the whole module pass immediately, and `__main__.py` does not close the `BuildRun` in a `finally` block. So rerun idempotency exists for completed work, but operational resilience is weaker than the concern implies: failures are recoverable by rerunning, not gracefully absorbed in-process.

- **Issue →** Already-interpreted rows are skipped on rerun, but symbol-pass transient failures abort the run and may leave `BuildRuns` unclosed.
- **Location →** `tools/symbol-db/interpret/runner.py`, `tools/symbol-db/interpret/__main__.py`
- **Fix →** Catch transient provider exceptions per symbol, emit `skipped-provider-error`, commit, continue, and close the build run in `finally`.

### 7. Spec compliance

This is where the largest gaps are.

The implementation does **not** fully match the Session 4 step list as described. Most importantly, there is no query interpretation pass at all, despite prompt/model support for queries and the spec’s requirement to fill parallel rows on Queries. Deferred skips are missing. Macros and lookup tables only run when CLI flags are provided, instead of being part of the prescribed default sequence. Module ordering is generic (`all Module kinds first, then others`) rather than explicitly `modStrings` first, then `Form_frmCompanyDetail`. The summary generator exists, but the CLI never invokes it, so “Generate a summary report” is not part of the actual pipeline execution path.

The strongest compliance failure is the mismatch between the claimed output artifact and the supplied fixture state. The provided build summary says:

- `Total Symbols.Interpretation1 populated: 0`
- `Total cost across BuildRuns: $0.0000`
- `BuildRuns: []`

But the checked-in `interpretation-summary-northwind.md` claims 84 populated symbols, 3 build runs, and non-zero routed cost. Those cannot both describe the committed DB. As delivered, the session goal has not been demonstrated against the actual fixture.

- **Issue →** Multiple Session 4 steps are missing or not actually executed against the committed database.
- **Location →** `tools/symbol-db/interpret/runner.py`, `tools/symbol-db/interpret/__main__.py`, `tools/symbol-db/interpret/summary.py`, `tools/symbol-db/interpretation-summary-northwind.md`
- **Fix →** Implement the missing query/deferred/default-run behavior, run the pipeline on the canonical fixture, and regenerate/commit both the SQLite DB and summary report from that real run.

### 8. Quality of routed interpretations

There are no actual routed interpretations in the supplied evidence, so quality cannot be verified. Every sample row shown for `modStrings.GetString`, the enum, the enum value, `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate`, `CompanyIsActive`, and `m_lngVendorProductCount` has `Interpretation1 = null`, `Justification1 = null`, `Confidence1 = null`. With `BuildRuns: []`, there is no basis to assess whether the outputs are grounded or speculative.

That absence is itself important. The checked-in summary file says the probe target and its direct callees are populated, but the samples here contradict that. So the correct verification outcome is not “the wording looks speculative”; it is “there is no produced interpretation set to audit, and the session claims are not backed by the current fixture.”

- **Issue →** Groundedness cannot be judged because no actual interpretations exist in the supplied DB state.
- **Location →** Current SQLite contents / provided sample rows / `tools/symbol-db/interpretation-summary-northwind.md`
- **Fix →** Produce a real committed run, then review populated `Interpretation1`/`Justification1` samples against source evidence before claiming quality verification.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "tools/symbol-db/interpret/runner.py, tools/symbol-db/interpret/prompt_builder.py",
      "summary": "Queries are modeled and prompted but never interpreted, so the Session 4 requirement for parallel rows on Queries is unmet.",
      "fix": "Implement an `interpret_queries` pass with dead/deferred/twin handling, wire it into the CLI default flow, and include query counts in the summary report."
    },
    {
      "severity": "Critical",
      "location": "tools/symbol-db/interpretation-summary-northwind.md and committed symbol-db.sqlite state",
      "summary": "The checked-in summary claims populated interpretations and BuildRuns, but the supplied fixture state shows zero interpretations and no BuildRuns.",
      "fix": "Run the pipeline against the canonical fixture, commit the actual updated SQLite DB, and regenerate the summary report from that DB instead of checking in a stale or synthetic report."
    },
    {
      "severity": "Major",
      "location": "tools/symbol-db/interpret/__main__.py and tools/symbol-db/interpret/runner.py::_emit_skip_rows_for_excluded",
      "summary": "Deferred modules and their symbols are skipped silently instead of being recorded as explicit `skipped-deferred` history rows.",
      "fix": "Add a deferred-skip emission pass that writes idempotent `InterpretationHistory` rows for deferred modules/symbols before excluding them from scheduling."
    },
    {
      "severity": "Major",
      "location": "tools/symbol-db/interpret/runner.py::interpret_macros and interpret_lookup_tables",
      "summary": "Macro dead-skip rows and table low-score skip rows are not idempotent, so reruns accumulate duplicate history entries.",
      "fix": "Use the same `already_skipped` guard pattern as the symbol skip path for macros, tables, and provider-error skips."
    },
    {
      "severity": "Major",
      "location": "tools/symbol-db/interpret/runner.py::_interpret_one, interpret_module and tools/symbol-db/interpret/__main__.py",
      "summary": "A transient provider error during symbol interpretation aborts the run instead of being recorded and continued, and the BuildRun is not protected by `finally` closure.",
      "fix": "Catch transient route exceptions per symbol, write `skipped-provider-error`, commit progress, continue the pass, and close the BuildRun in a `finally` block."
    },
    {
      "severity": "Major",
      "location": "tools/symbol-db/interpret/__main__.py",
      "summary": "The default execution flow does not enforce the Session 4 order of `modStrings`, then `Form_frmCompanyDetail`, then macros and lookup tables.",
      "fix": "Make the Session 4 target order explicit in the default CLI path and run macros/lookup tables by default rather than only behind opt-in flags."
    },
    {
      "severity": "Minor",
      "location": "tools/symbol-db/interpret/topology.py",
      "summary": "Self-loop documentation contradicts the implementation, which drops all self-loops and therefore hides genuine self-recursion as well as VBA return-value self-references.",
      "fix": "Align the docstrings/comments/tests with the implemented heuristic, or refine self-loop filtering using AST/reference-kind data so real recursion remains detectable."
    }
  ]
}
```

---

# Verification Round 2

### C1 — Queries are modeled but never interpreted

Resolved.

`runner.py` now has a real `interpret_queries()` pass, and `__main__.py` runs it by default unless `--no-queries` is supplied. The implementation covers the missing pieces from Round 1:

- live, non-twin queries are routed,
- dead queries get explicit `skipped-dead` history rows,
- twin queries are skipped during routing and handled by a post-pass copy,
- reruns are idempotent because:
  - populated `Interpretation1` rows are skipped,
  - dead-skip writes are guarded by `_already_skipped_history()`,
  - twin-copy only targets rows where `Interpretation1 IS NULL`.

The reported counts are internally consistent with that design: 65 queries total, 62 dead, 3 interpreted, 0 twin copies.

I do not see a new regression in the default query path.

### C2 — Stale committed summary / empty fixture

Resolved for the delivered Session 4 artifact.

The root cause in Round 1 was destructive DB regeneration during `dotnet test`. `GenerateArtifacts.cs` now blocks regeneration once the DB already has populated symbol interpretations, which directly prevents the observed wipeout. The new evidence is also internally consistent:

- 84 symbols interpreted
- 1 macro interpreted
- 15 tables interpreted
- 3 queries interpreted

That totals 103 first-opinion writes, which matches:

- `BuildRuns.interpretations_written = 103`
- `InterpretationHistory` reason counts showing `first-opinion: 103`

The committed summary file now agrees with the DB/build-run summary instead of contradicting it.

Idempotence is acceptable here: rerunning `dotnet test` after interpretation should now be a no-op for the DB artifact.

Only caveat: the guard checks `Symbols.Interpretation1` specifically, not macro/table/query interpretation columns. That is sufficient for the current Session 4 pipeline because symbols are always populated in the successful run, but it is not the most future-proof possible guard.

### M1 — Deferred symbols skipped silently

Resolved.

`emit_deferred_skips()` now runs at CLI startup and emits one `skipped-deferred` history row per uninterpreted symbol in each deferred module. It is idempotent because it checks `_already_skipped_history()` before inserting.

That satisfies the Round 1 requirement for explicit audit rows instead of silent exclusion.

The current fixture has zero deferred modules, so this is not runtime-demonstrated by the provided counts, but the implementation shape is correct and the pass is idempotent.

I do not see a regression around module scheduling from this change.

### M2 — Macro / table skip rows not idempotent

Partially resolved.

The original problem for macros/tables is fixed:

- macro `skipped-dead` is now guarded,
- table `skipped-low-lookup-score` is now guarded,
- macro/table/query `skipped-provider-error` writes are guarded,
- symbol dead/procedure-local/unsupported-kind skip rows remain guarded.

The remaining gap is symbol-side transient-provider skips inside `_interpret_one()`. That path still does this:

- catches a transient exception,
- writes `skipped-provider-error`,
- returns early,

but it does **not** call `_already_skipped_history()` first.

Result: repeated transient failures on the same symbol can still duplicate `skipped-provider-error` audit rows across reruns. So the broad claim that “every skip-row write is guarded” is not yet true.

### M3 — Transient symbol error abort + BuildRun not in finally

Partially resolved.

The major operational problem from Round 1 is substantially improved:

- the main per-opinion `route()` call in `_interpret_one()` is now wrapped,
- transient failures on that call no longer abort the whole symbol/module pass,
- `__main__.py` now closes the build run in `finally`, so build-run closure is protected even on exceptions.

On the specific “does build-run accounting still close cleanly?” concern: yes, for the default path it does. The provided `BuildRuns` row is consistent with the reported counts and cost, and the `finally` block addresses the unclosed-run defect.

There is still one remaining transient-error hole: the parse-failure retry call is outside the transient guard.

In `_interpret_one()`:

1. first `route()` returns unparsable content,
2. code builds a reminder prompt,
3. retry `route()` is called,
4. that retry call is **not** wrapped in the same transient handling.

If the retry hits a timeout/503, the exception still bubbles out and aborts the pass. So the abort path is narrowed, but not fully eliminated.

A second, smaller behavioral caveat: if opinion 2 or 3 fails transiently after opinion 1 was already written, the symbol is considered “already interpreted” on rerun and will not be revisited for escalation. That does not recreate the Round 1 abort defect, but it does mean escalation recovery is incomplete.

### M4 — CLI default did not enforce the Session 4 order

Resolved for the current/default Session 4 workflow.

The default CLI now runs:

- module interpretations,
- macros,
- lookup tables,
- queries,

unless explicitly opted out with `--no-*` flags. Module ordering is now `kind == 'Module'` first, then others, which is enough for the supplied fixture to run `modStrings` ahead of `Form_frmCompanyDetail`.

For the stated Northwind target set, that is sufficient.

The passes are also idempotent on rerun because each one skips rows with populated `Interpretation1`.

I do see one **new** surrounding regression in the CLI: `--limit` is now unsafe. `_maybe_wrap_route()` raises `_LimitReached`, but nothing catches that exception around `interpret_module()`. The comment says the caller swallows it later, but that is not what the code does. So capped runs can fail mid-module instead of stopping cleanly. That is not a default-path Session 4 ordering problem, but it is a real new CLI regression.

### m1 — Self-loop docstring contradicted implementation

Not fully resolved.

The module-level docstring in `topology.py` is now aligned with the actual heuristic: single-node self-loops are dropped before SCC detection, and only multi-node cycles are tagged.

However, the function-level docstring on `order_leaves_first()` still says:

> “Single-node SCCs that self-loop also count as cycle members”

That is the old description and still contradicts the implementation below it, which explicitly drops self-loops and only marks `len(component) > 1` as cyclic.

So the documentation mismatch remains, just in a narrower location.

### Sample interpretation groundedness

The symbol examples are mostly improved and generally read as source-grounded:

- `modStrings.GetString` is well grounded.
- `Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate` is well grounded.
- `Form_frmCompanyDetail.CompanyIsActive` is well grounded.

`modStrings.enumStrings` and `modStrings.enumStrings.sCompanyDelete_CompanyTypeChange` are more name-driven, but they are transparent about that and do not overclaim badly.

The weaker outputs are:

- `Form_frmCompanyDetail.m_lngVendorProductCount`:
  - it says the initialization/update mechanism is “not specified” and is “almost certainly” record-activation-based,
  - but the `CompanyIsActive` sample itself says that function assigns the `m_lng...` cache variables,
  - so that variable interpretation contains avoidable speculation and is slightly inconsistent with the surrounding evidence set.
- `AutoExec`, `CompanyTypes`, and `qrycboCompanyType`:
  - their `Interpretation1` text is plausible,
  - but `Justification1` is only `(macro)`, `(table)`, `(query)`,
  - so those rows are not auditable as evidence-grounded from the stored justification alone.

So the sample set is **not** uniformly speculative, but it is also **not** uniformly well-grounded. The macro/table/query justification quality is the clearest remaining output-quality gap.

### Leaves-first topology after the changes

Still correct.

The implementation in `topology.py` is functionally unchanged from Round 1. For caller → callee edges, the Tarjan SCC emission still yields a leaves-first schedule, and multi-node SCC handling is still correct and deterministic.

The only remaining topology issue is documentation consistency around self-loops, not the scheduling behavior itself.

### Build-run accounting

Closes cleanly in the current/default flow.

`__main__.py` now wraps the pipeline body in `try/finally`, and `close_build_run()` is called from the `finally` block. The supplied `BuildRuns` row is internally consistent with the reported interpretation counts and total routed cost.

The remaining operational issues are narrower:

- the symbol parse-retry transient path can still bubble an exception,
- `--limit` can currently terminate the run via an uncaught `_LimitReached`.

Neither of those reintroduces the old “normal execution path leaves BuildRuns unclosed” defect.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "tools/symbol-db/interpret/runner.py::_interpret_one",
      "summary": "Symbol-side `skipped-provider-error` history rows are still not idempotent because the transient-exception path writes them without an `_already_skipped_history()` guard.",
      "fix": "Check for an existing `(fqn, opinion_slot=0, reason='skipped-provider-error')` row before calling `db.write_skip()` in the symbol transient-error branch."
    },
    {
      "severity": "Major",
      "location": "tools/symbol-db/interpret/runner.py::_interpret_one",
      "summary": "A transient provider failure during the parse-failure retry call can still abort the symbol pass because the retry `route()` call is outside the transient-exception handler.",
      "fix": "Wrap the retry `route()` call in the same transient handling as the primary call, or factor both calls through one helper that records `skipped-provider-error` and returns cleanly."
    },
    {
      "severity": "Major",
      "location": "tools/symbol-db/interpret/__main__.py::_maybe_wrap_route and main module loop",
      "summary": "The non-default `--limit` path is broken because `_maybe_wrap_route()` raises `_LimitReached` and nothing catches it around `interpret_module()`, so capped runs can fail mid-module.",
      "fix": "Catch the limit exception around `interpret_module()` and break cleanly, or move limit enforcement out of `route()` wrapping and into an explicit loop-level check before each symbol is routed."
    },
    {
      "severity": "Major",
      "location": "Current interpreted output rows for `AutoExec`, `CompanyTypes`, `qrycboCompanyType`, and `Form_frmCompanyDetail.m_lngVendorProductCount`",
      "summary": "Several sample outputs are still weakly grounded because `Justification1` is placeholder-only for macro/table/query rows and the module-var sample contains avoidable speculation inconsistent with nearby evidence.",
      "fix": "Require evidence-backed `Justification1` text for all artifact classes, reject placeholder justifications at write time, and rerun the affected rows so their stored justifications cite concrete source, schema, or caller/callee evidence."
    },
    {
      "severity": "Minor",
      "location": "tools/symbol-db/interpret/topology.py::order_leaves_first",
      "summary": "The function-level docstring still says single-node self-loops count as cycle members, which contradicts the implemented heuristic and the updated module-level documentation.",
      "fix": "Update the `order_leaves_first()` docstring to state that self-loops are dropped before SCC detection and only multi-node SCCs are tagged as cycle members."
    }
  ]
}
```

---

# Verification Round 3

### M2-1 — Symbol-side `skipped-provider-error` history rows not idempotent

This is fixed correctly and adequately.

The important change is not just the extraction of `_route_with_transient_skip()`, but where it is now used. In `_interpret_one()`, the primary symbol route call now goes through that helper, and the helper does the two checks that matter:

1. `_is_transient(exc)` to decide whether the failure should be absorbed rather than raised.
2. `_already_skipped_history(con, fqn, "skipped-provider-error")` before writing the audit row.

That eliminates the Round-2 duplication bug. On a rerun after the same symbol hits a provider 5xx again, the helper sees the existing opinion-slot-0 skip row and does not write a second `skipped-provider-error` row.

It also behaves correctly operationally: after writing the skip once, it commits and raises `_TransientSkipped`, and `_interpret_one()` catches that sentinel and returns cleanly instead of aborting the module pass.

I do not see a remaining duplicate-write path for symbol-side provider-error skips in the reviewed code. The only exceptions that still propagate are non-transient ones, which is appropriate and separate from the idempotency defect.

### M2-2 — Parse-failure retry `route()` outside the transient handler

This is also fixed correctly.

In Round 2, the remaining hole was the retry path after an unparsable first response. In Round 3, that retry call now also goes through `_route_with_transient_skip()`. That means a transient 503/timeout on the retry path is now handled exactly the same way as on the primary path:

- one idempotent `skipped-provider-error` row at opinion slot 0,
- commit,
- `_TransientSkipped`,
- clean return from `_interpret_one()`.

So the specific abort path from Round 2 is gone. There is no longer a retry-only transient-error escape hatch that can terminate the symbol pass.

### M2-3 — `--limit` raises `_LimitReached` and breaks runs

This fix is correct on the core defect and adequate overall.

`_maybe_wrap_route()` and `_LimitReached` are gone, so the old exception-based control flow no longer exists. The new design moves the cap to the symbol loop itself:

- `interpret_module()` accepts `stop_when_total_reaches` and `running_total`.
- Before routing each symbol, it checks `running_total + stats.interpreted >= stop_when_total_reaches`.
- When the cap is reached, it `break`s cleanly.

At the CLI level, `__main__.py` passes the current aggregated interpreted count into each module call and also stops the outer module loop once the aggregated count has reached the limit. That makes the `--limit` path exception-free.

On observability: the effect is visible in the returned counts and, when another module iteration would occur, in the `hit --limit ...` message. If the cap is reached inside the last or only module, there is no separate “limit hit mid-module” log line, but the run still ends cleanly and the cap is enforced. That is a minor UX nuance, not a correctness problem.

I do not see a regression introduced by the new parameters. The stop condition is correctly placed at the loop boundary and does not disturb normal uncapped runs.

### M2-4 — Sample interpretation justifications were placeholder-only for macro / table / query rows

With the corrected evidence, this Round-2 finding is resolved.

The new `Justification1` text for the three artifact classes that were previously misrepresented is materially different from the placeholder literals and is evidence-grounded:

- **Macro `AutoExec`**: the justification cites the reserved startup macro name, the XML conditions, the `OpenForm` action on `frmStartup`, the `RunCode` action on `Startup()`, and the parsed action list. That is concrete and auditable.
- **Table `CompanyTypes`**: the justification cites the table name, the small record count, and the bound control list showing `Form_frmCompanyDetail.cboCompanyTypeID`. Some inference remains, but it is normal and anchored in schema/UI evidence rather than placeholder text.
- **Query `qrycboCompanyType`**: the justification cites the exact SQL, selected columns, sort order, and the explicit UI consumer. This is clearly evidence-based.

The symbol samples are also mostly grounded. `modStrings.GetString`, `cboCompanyTypeID_BeforeUpdate`, and `CompanyIsActive` are strong. `m_lngVendorProductCount` remains the weakest sample because part of its interpretation still relies on naming and usage inference, but that is a separate quality nuance, not the placeholder-justification defect from Round 2.

So the Round-2 quality claim should be re-evaluated as resolved: the macro/table/query rows no longer fail on “placeholder-only justification.”

### m2-1 — `order_leaves_first()` function-level docstring contradicted implementation

This is fixed.

The function-level docstring now matches the implemented heuristic: self-loops are dropped before SCC detection, and only multi-node SCCs are flagged as cycle members. That aligns with both the code and the updated module-level docstring.

There is no functional topology change here, only documentation alignment, and I do not see any regression from that edit.

### Cross-cut confirmation

`_route_with_transient_skip()` now correctly guards **both** symbol call sites that mattered:

- the primary per-opinion route call,
- the parse-failure retry route call.

That closes both the duplication problem and the transient retry-abort problem from Round 2.

The `--limit` path is now exception-free. Its effect is visible through counts and normal loop termination, with a helpful log line in the multi-module case. No uncaught `_LimitReached` path remains.

The actual stored `Justification1` fields shown here read as evidence-grounded overall, especially for the previously disputed macro/table/query cases. They are no longer placeholders.

The structural refactors do not introduce an obvious regression in the reviewed areas. The helper extraction is narrow and correct, the new limit parameters are threaded consistently, and the topology edit is documentation-only.

Across the audit-row write paths discussed in the review, idempotency now holds:

- **Symbols**
  - `skipped-dead`
  - `skipped-unsupported-kind`
  - `skipped-procedure-local-var`
  - `skipped-deferred`
  - `skipped-provider-error`
- **Macros**
  - `skipped-dead`
  - `skipped-provider-error`
- **Tables**
  - `skipped-low-lookup-score`
  - `skipped-provider-error`
- **Queries**
  - `skipped-dead`
  - `skipped-provider-error`

Each of those writes is now guarded by an existing-history check before insertion. MD5/twin copy paths remain idempotent because they only target rows whose `Interpretation1` is still `NULL`.

Result: the Round-3 fixes are correct and adequate for all five Round-2 findings, and I do not see a remaining duplicate-write or transient-abort path in the reviewed scope.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
