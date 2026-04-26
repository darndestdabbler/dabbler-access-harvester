## ISSUES_FOUND

- **Issue → R1-2 is not fully resolved: source decoding is still heuristic and can silently misdecode some Windows-1252 files.**  
  **Location →** `AccessHarvester.Cli/Program.cs`, `RunParse()`, decoding block that does BOM check → strict UTF-8 decode → `Encoding.Default` fallback  
  **Fix →** Make encoding selection deterministic. Add an explicit parse encoding option (for example `--encoding utf-8|utf-16le|windows-1252|ansi|auto`) and, in `auto`, use BOM detection first, then a fixed fallback code page rather than `Encoding.Default`. As written, a Windows-1252 byte stream that also happens to be valid UTF-8 will be accepted as UTF-8 and silently changed, and `Encoding.Default` makes fallback behavior machine-locale-dependent rather than guaranteed Windows-1252.