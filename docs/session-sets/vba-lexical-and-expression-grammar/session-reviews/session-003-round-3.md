## ISSUES_FOUND

- **Issue → R2 is only partially resolved: the fallback is now deterministic, but no-BOM decoding is still heuristic and can silently misdecode Windows-1252 files whose bytes also form valid UTF-8.**  
  **Location →** `AccessHarvester.Cli/Program.cs`, `RunParse()`, no-BOM path that does strict UTF-8 decode first and only falls back to `Encoding.GetEncoding(1252)` on `DecoderFallbackException`  
  **Fix →** Add explicit encoding selection (`--encoding utf-8|utf-16le|windows-1252|auto`) or, for this Windows/Access-specific CLI, default no-BOM files to Windows-1252 and make UTF-8 opt-in/auto. `Encoding.GetEncoding(1252)` is a correct deterministic replacement for `Encoding.Default`, but it does not resolve the remaining UTF-8-first silent-misdecode case.