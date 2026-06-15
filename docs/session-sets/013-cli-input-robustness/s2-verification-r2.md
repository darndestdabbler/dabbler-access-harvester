The reported Major appears resolved. The parser now converts any repeated `--key` into the existing usage-error path instead of silently accepting last-write-wins behavior, which was the original defect. The added tests cover the important cases: repeated value option, repeated flag, and case-only variation (`--out` / `--OUT`). Given the stated contract that no option is repeatable, this is the correct behavior, and nothing in the described change suggests regression in the previously accepted areas.

```json
{ "verdict": "VERIFIED", "issues": [] }
```