# Devil's-Advocate Review — Set 013 (CLI Input Robustness) — Gemini 3.1 Pro (Preview)

This is an adversarial evaluation of Set 013 (Sessions 1 & 2), evaluating the contrarian hypotheses presented.

## Item 1 — F-1-3: `--types` value validation (S1)
**UPHELD.** The validation logic correctly flags unknown categories against the pipeline's exact union. While `cats` might be empty after `Split(..., RemoveEmptyEntries)` (e.g., `--types ,,,`), it gracefully handles this because `unknown.Length` will be 0 but `filtered.IsEmpty` correctly throws. The categories implemented in the default pipeline are 1:1 identical across Exporters and Translators (Forms, Reports, Macros, Modules, Queries, Tables, Relationships, Navigation). No edge case exists where an accepted `--type` is a no-op across all tools.

## Item 2 — F-1-2: object-name path sanitisation + collision guard (S1)
**CHALLENGED.** The collision guard is fundamentally destructive. Access allows `/` and `\` in object names. A database containing two legitimate objects named `Sales/Invoice` and `Sales\Invoice` will pass both to `PlanOutputFiles`. `SanitizeFileName` converts both to `Sales_Invoice`. The `OrdinalIgnoreCase` dictionary sees they map to the same file (`Sales_Invoice.txt`), but their original names differ (`Ordinal`). It throws a collision error for the second object and drops it entirely (yielding `ExportOutcome = false`, Exit Code 3). The "fix" causes data loss on valid DBs instead of safely appending a suffix (like `_1.txt`) to the target file.

## Item 3 — O-1-4: shared ParseOptions (unknown + duplicate rejection) (S2)
**CHALLENGED.** Rejecting values that begin with `--` is a critical flaw. Legitimate file paths on Windows and UNIX can perfectly well start with `--`. If a user invokes `AccessHarvester run --db --database.accdb --out foo`, the parser encounters `--database.accdb` as the value for `--db`, treats it as a new flag, and throws `Option --db requires a value.` This mechanism breaks paths and is an overreach.

## Item 4 — O-1-7: single dispatch table (S2)
**UPHELD.** Moving from `ToLowerInvariant` to the `OrdinalIgnoreCase` dictionary is robust. While there are culture-specific differences (like Turkish-I), `OrdinalIgnoreCase` is the correct, invariant string comparison intended for command-line syntax matching in .NET, ensuring environment localization doesn't interfere. 

## Item 5 — F-1-5: OLE DB connection-string building (S2)
**UPHELD.** Empirical testing with `OleDbConnectionStringBuilder` under `Microsoft.ACE.OLEDB.16.0` establishes that it faithfully reproduces the connection string without mangling the version logic. It emits `Provider=Microsoft.ACE.OLEDB.16.0;Data Source=...;Persist Security Info=False` exactly. The lack of a trailing semicolon is immaterial to the ACE OleDB parser, and the provider versions probe flawlessly.

## Item 6 — F-1-6: bracket-aware PARAMETERS tokenisation (S2)
**CHALLENGED.** (High severity). The depth-tracking logic breaks if the parameter name contains an escaped closing bracket, which Access represents as `]]` (e.g. `[My ]] Param]`). The loop `else if (c == ']') { if (depth > 0) depth--; }` hits the first `]` of the pair and zeroes out `depth`. Any subsequent comma in the remainder of the parameter name (like inside `] Param]`) will inappropriately trigger `depth == 0` splitting! Moreover, the downstream `ParseParametersClause` uses `trimmed.IndexOf(']')`, which has *never* supported `]]`. Thus, the "golden-neutral" claim solely reflects the `HomeHealthCare` database's lack of `]]` parameter names. The fix misses the syntax design of MS Access entirely. 

## Item 7 — DW-15 scope call: `identify-cleanup` exit 2 for usage errors
**CHALLENGED.** The entire purpose of the Set 013 initiative was "CLI Input Robustness" for an AI consumer that maps success off predictable exit codes. A consumer interpreting Exit 2 as a catastrophic system/tool crash while it was actually a simple missing `--in` param creates a polluted telemetry signal. Normalizing Exit 1 across the board was exactly the P5 mandate; framing the DW-15 exclusion as "out of scope" actively defeats the session's mandate.

## Overall Verdict
The Set 013 fixes address superficial issues but fail deeply when tested against legitimate Access-specific edge constraints and filesystem edge cases. The highest-value thing to check next is the PARAMETERS tokenization (Item 6), as it directly dictates how queries are interpreted by downstream AI models. It requires an immediate overhaul to handle `]]` escape sequences and safely parse out parameter data types.
