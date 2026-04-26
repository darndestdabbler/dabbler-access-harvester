# Verification Round 1

# ISSUES_FOUND

Adequate as a Session 1 skeleton; not sufficient to claim full Phase 0.2 acceptance without the fixes below.

Confirmed:
- The parser subprocess contract (`<parser_exe> <source_file>`, JSON on stdout) is reasonable for a future ANTLR-based C# parser, assuming it is published as an executable or wrapped by one.
- The no-parser smoke state is correct: all files become `status="no_parser"`, `pass_rate` is `null` (representing 0/0), and the exit code is `0`.

- **Issue → Non-zero parser exit codes are not consistently treated as failure**
  - **Location →** `tools/parser-harness/harness.py`, `invoke_parser()`
  - **Fix →** After parsing stdout JSON, if `proc.returncode != 0`, force `status="failed"` and append an `exit code N` error regardless of the JSON `status`. The current implementation only fails on non-zero exit when stdout is empty, which violates the stated contract.

- **Issue → Performance mode is not machine-readable, so perf regressions are not CI-stable**
  - **Location →** `tools/parser-harness/harness.py`, `cmd_perf()`
  - **Fix →** Add `--output` for perf mode and emit deterministic JSON containing at least: `runs`, per-file `min/max/avg`, corpus total time, and throughput. Add a schema or a versioned `mode` field so CI can consume perf results without scraping stdout.

- **Issue → The JSON schema only loosely validates the actual run artifact**
  - **Location →** `tools/parser-harness/harness-output.schema.json`
  - **Fix →** Require `parse_time_ms`, `errors`, and `tree_stats` on every file entry, since `harness.py` always emits them. When `tree_stats` is non-null, require `node_count`, `max_depth`, and `rule_counts`. This makes the schema a real contract instead of a permissive approximation.

- **Issue → The harness does not measure or enforce the Phase 0.2 corpus runtime target**
  - **Location →** `tools/parser-harness/harness.py`, `invoke_parser()`, `cmd_run()`, `cmd_perf()`
  - **Fix →** Record total corpus duration in output and add configurable per-file and total-run budgets. The current fixed `60s` per-file timeout can exceed the stated “full corpus under 5 minutes” acceptance target by a wide margin.

- **Issue → Diff semantics are too coarse for stable regression detection**
  - **Location →** `tools/parser-harness/harness.py`, `cmd_diff()`
  - **Fix →** Compare by relative `path` rather than `name`, and define regressions explicitly. Treating any failed→failed message-text change as a regression will create false positives as diagnostics evolve. Prefer structured comparison rules and optionally emit machine-readable diff output.

- **Issue → Phase 0.2 “detailed log for failure analysis” is only partially covered**
  - **Location →** `tools/parser-harness/harness.py`, `cmd_run()` / CLI surface
  - **Fix →** Either formalize the run JSON as the detailed log artifact and make it mandatory in CI, or add a separate log output option. Right now failure detail exists, but only in optional JSON and verbose console output, not as a clearly defined artifact.

---

# Verification Round 2

VERIFIED
