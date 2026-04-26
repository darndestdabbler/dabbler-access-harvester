## ISSUES_FOUND

- **Issue → Bare-file mode does not fully implement the advertised `<exe> <source-file>` contract and is brittle.**  
  **Location →** `AccessHarvester.Cli/Program.cs`, `Dispatch()`, `if (args[0].Contains('.') || args[0].Contains('/') || args[0].Contains('\\')) return RunParse(args);`  
  **Fix →** Replace the dot/separator heuristic with a known-command check. A robust pattern is: if `args.Length == 1`, the token is not a help flag, not a known subcommand, and does not start with `--`, route to `RunParse(args)`. This covers extensionless files and avoids misclassifying multi-arg unknown commands containing `.` as parse requests.

- **Issue → Source decoding is UTF-8-only; non-UTF-8 VBA exports can be silently mangled before lexing.**  
  **Location →** `AccessHarvester.Cli/Program.cs`, `RunParse()`, `File.ReadAllText(fullPath, System.Text.Encoding.UTF8)`  
  **Fix →** Add BOM-aware decoding with a legacy-codepage fallback. For Windows/VBA inputs, detect BOM first, then try strict UTF-8, then fall back to `Encoding.Default` or Windows-1252. Otherwise `ERROR_TOKEN` counts and parse results can be artifacts of mis-decoding rather than grammar behavior.

- **Issue → The schema-validation correspondence table is incomplete for in-scope expression coverage.**  
  **Location →** `docs/spec-coverage/parse-failure-log.md`, “Schema-validation correspondence (in-session, Session 3)” table  
  **Fix →** Add explicit rows for at least `AddressOf` and implicit-`With` leading `.` / `!`, or explicitly state that the coverage matrix has no standalone rows for them and cite the grammar rules that cover them. As written, the table understates Session 2/3 expression-scope coverage.

- **Issue → CLI usage text was not updated to document the new parse entry points.**  
  **Location →** `AccessHarvester.Cli/Program.cs`, `PrintUsage()`  
  **Fix →** Add `parse <source-file>`, `parse --in <source-file>`, and bare-file mode (`AccessHarvester <source-file>`) to the usage output, plus its 0/1 exit-code behavior.