1. **O-1-4 correctness + accepted-set preservation**

The consolidation to a single `OptionParser.Parse` is directionally correct. The shared implementation does the important things the lane asked for: it rejects unknown switches, rejects positional tokens, treats option names case-insensitively, distinguishes flag vs. value options, and errors when a value option is missing or followed by another option token. The per-command tables shown also look internally consistent with the command surface described in the session notes; I do not see an obvious accepted-set mismatch such as a command reading an option that is no longer admitted, or a new option being admitted that the command never consumes.

I also do not see a regression in the specific typo class this fix was meant to address. A call like `--datta x` now fails early instead of being silently ignored, which is the right outcome. The explicit `"option-as-value" -> throw` behavior is also at least consistent and tested, even though it means values beginning with `--` are not representable in this CLI form.

The problem I do still see is that duplicate options are silently overwritten:

```csharp
opts[key] = ...
```

That means malformed invocations like `--out a --out b`, `--db first.accdb --db second.accdb`, or `--types forms --types queries` still complete without an honest usage error; the last occurrence simply wins. For this review lane, that is materially the same class of failure as the original bug: invalid or edge CLI input can still produce a wrong result while returning a normal success path. Centralising parsing made this the right place to close that gap, but the current implementation leaves it open.

2. **O-1-7 dispatch equivalence**

Using one `Commands` table for both dispatch and bare-file exclusion is the right structural fix for drift. Given the code shown, dispatch and exclusion now derive from exactly the same set, so the specific prior failure mode—adding a command to one list and not the other—is removed. `StringComparer.OrdinalIgnoreCase` also makes the lookup rules uniform between dispatch and exclusion, which is preferable to two separate hand-maintained comparisons.

I do not see a new functional bug in the table-driven approach itself. If the table contents are complete—and they appear to match the subcommand inventory listed in the session—then this is behaviorally sound. The only theoretical caveat is that if the old bare-file exclusion had been case-sensitive while the dispatch switch was not, mixed-case tokens that collide with command names could behave differently now. But the prompt does not provide enough evidence that this is an unintended regression, and the new approach is at least internally consistent.

3. **F-1-5 correctness**

`OleDbConnectionStringBuilder` is the correct fix here. It will properly quote or escape `Data Source` values containing semicolons or quotes, which string interpolation cannot do safely. The helper also keeps the ordinary case stable: for a normal provider and path, the resulting connection string is semantically equivalent to the old interpolation, just canonicalised by the builder.

I do not see a concerning functional difference for the four readers. They all consume the string as an OLE DB connection string; they do not depend on textual formatting or property order. The hermetic round-trip tests for ordinary, semicolon-bearing, and quote-bearing paths are the right proof for the bug class described. Expanding the fix from two to four identical sites is also the right call, not a scope problem.

4. **F-1-6 correctness + golden-neutrality**

For the bug actually claimed, the new bracket-aware scan is correct. Moving from a lazy regex capture to an explicit depth-0 semicolon search fixes premature termination on `;` inside bracketed parameter names. Replacing `Split(',')` with a depth-0 splitter likewise fixes shredding on `,` inside bracketed names. Ordinary clauses still work, and unbalanced opening brackets degrade to “no declared clause found,” which is consistent with the prior no-match behavior rather than inventing a half-parsed result.

On edge shape:

- **Name-internal `,` / `;`**: fixed correctly.
- **Ordinary two-parameter clauses**: preserved.
- **No terminator**: returns `null`, matching the stated prior behavior.
- **Unbalanced brackets**: treated as invalid/non-match rather than partially parsed, which is acceptable.
- **Nested/literal bracket oddities**: the extractor/splitter track depth, but the per-entry parser is still described as “close at first `]`”. So this is not a general bracket-escaping parser. I do not treat that as a regression because it is unchanged and outside the exact `,` / `;` defect claimed, but it is worth being precise about what was and was not fixed.

The “golden-neutral, no regen” claim is credible. For shipped output, byte-identical COM regression without regenerating the golden is stronger evidence than a verbal assertion. Since the only HHC `PARAMETERS` names named in the prompt do not contain commas or semicolons, there is no reason to expect output drift there, and the COM proof confirms that.

5. **Scope discipline + exit-code honesty**

Keeping `identify-cleanup` on exit code `2` for usage errors is the right scope call. The command already has a documented divergent convention, and this set is about rejecting bad input honestly, not renumbering one command’s public contract. The dedicated pin for `identify-cleanup --bogus x -> exit 2` is exactly what I would want to see.

I do not see evidence that valid inputs now change exit code unexpectedly. Unknown options now fail honestly; that is intended. The OLE DB and `PARAMETERS` fixes do not alter valid-input exit behavior. The remaining honesty gap is the duplicate-option case from concern 1: malformed invocations can still be accepted and silently resolved by “last one wins,” which means the CLI can still produce a wrong-but-successful outcome on bad input. That is the one reason I cannot close this as fully verified.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Cli/OptionParser.cs",
      "summary": "The shared parser silently overwrites repeated options, so malformed invocations like `--out a --out b` or `--types forms --types queries` still degrade to a wrong-but-successful run instead of an honest usage error.",
      "fix": "Reject duplicate keys in `OptionParser.Parse` (for both value and flag options), or explicitly document and test a supported merge policy where repetition is intended."
    }
  ]
}
```