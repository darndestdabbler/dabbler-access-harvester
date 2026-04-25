# Verification Round 1

## ISSUES_FOUND

- **Issue** → Usage errors return exit code `2` instead of the documented `1`.
  **Location** → `AccessHarvester.Cli/Program.cs` — `Main()` catches all exceptions and returns `2`; `ParseOptions()` / `Require()` throw `ArgumentException` for bad CLI input such as missing `--db`, missing `--out`, missing option values, or unexpected positional args.
  **Fix** → Catch `ArgumentException` separately in `Main()` and return `1` for usage errors (optionally also call `PrintUsage()` there), while keeping `2` for genuinely unexpected exceptions.

---

# Verification Round 2

**VERIFIED**
