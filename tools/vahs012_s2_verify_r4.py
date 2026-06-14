"""Set 012 Session 2 cross-provider verification — ROUND 4 (bounded-final).
Confirms the token-aware TempVars scanner + strict form-ref path shape that
structurally close the R2/R3 edge cases. Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"
OUT = os.path.join(SS, "s2-verification-round-4.md")


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def excerpt(path, anchor, length):
    t = read(path)
    i = t.find(anchor)
    return t[i:i + length] if i >= 0 else f"(anchor {anchor!r} not found)"


def main():
    scanner = excerpt("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
                      "// Recovers `TempVars!name`", 2900)
    parse_path = excerpt("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
                        "// Parses the verbatim reference path", 1300)
    pins = excerpt("tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs",
                  "// ── R3 follow-on fixes", 1700)

    prompt = f"""You are an expert C#/.NET reviewer. This is the BOUNDED-FINAL round of an
independent cross-provider verification (the workflow caps retries; R1 had 3 Majors +
2 Minors, all resolved; R2 raised 2 follow-ons, R3 raised 2 narrower variants in the SAME
two areas — TempVars text recovery and form-ref path parsing). Rather than another regex
patch, both were replaced with STRUCTURAL fixes. Confirm whether the structural fix closes
the class. Begin with prose, THEN the JSON block. Prefer false-positive ISSUES_FOUND over
false-negative VERIFIED, but judge proportionately: theoretical inputs that cannot occur in
a form/report RecordSource are not defects.

# What changed (structural)

1. **TempVars recovery is now a TOKEN-AWARE single-pass scanner**, not a regex over raw text.
   It walks the SQL and:
   - on `'` or `"`: skips the entire string literal (honouring doubled-quote escapes) via SkipQuoted;
   - on `[`: skips the entire bracketed identifier span to the matching `]` via SkipBracket;
   - otherwise: at a TOP-LEVEL position only, matches `TempVars` <ws> `!` <ws> (identifier | `[member]`)
     with a left identifier boundary (TryMatchTempVar).
   Therefore a `TempVars!x` inside ANY string literal or ANY bracketed identifier (regardless of
   position within the brackets) is structurally unreachable — the scanner skips those spans before
   it can match. A real top-level `TempVars!member` / `TempVars![member]` is matched (the member
   bracket is consumed by the match, not pre-skipped). The JET lexical model for a record-source
   SELECT is exactly: string literals, bracketed identifiers, and bare identifiers/operators — all
   three are handled.

2. **ParseFormRefPath enforces a strict segment shape**: after splitting on `!` and unbracketing,
   the result must be EXACTLY 2 or 3 segments; segment[0] must be `Forms`/`Reports`; segment[1]
   (object) must be non-empty; a present segment[2] (member) must be non-empty. Anything else
   (e.g. the malformed `[Forms]![frm]!![txtId]`, which yields 4 segments) returns `(null, null)`
   and the caller emits an explicit `unresolvable-reference` UnresolvedChainTarget — never a
   fabricated supplier.

# Validation
- Build 0 errors. Hermetic: NavigationChainBuilderTests + IrLoaderTests 27/27 (full Pipeline.Tests
  865/865). New pins: TempVars inside a non-leading bracket span `[prompt TempVars!fake]` excluded
  while top-level `TempVars![real]` resolves; TempVars inside a double-quoted literal excluded;
  `[Forms]![frm]!![txtId]` (4 segments) -> unresolvable-reference.
- COM golden gate: CanonicalGoldenRegression + ManifestSchemaConformance 6/6 pass — the HHC golden
  is byte-identical (the real `TempVars!repCID` / `TempVars!repFY` references still produce the same
  4 tempvar-flow edges; HHC exercises neither new branch). All fixes golden-neutral.

# Source under review (post-fix)

=== NavigationChainBuilder.cs — token-aware TempVars scanner (ExtractTempVars + helpers) ===
```csharp
{scanner}
```

=== NavigationChainBuilder.cs — ParseFormRefPath (strict 2-or-3 segment shape) ===
```csharp
{parse_path}
```

=== NavigationChainBuilderTests.cs — R3 structural-fix pins ===
```csharp
{pins}
```

After your prose analysis, output a JSON block in this exact format:
```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{ "severity": "Critical" | "Major" | "Minor", "location": "<file>", "summary": "<one sentence>", "fix": "<recommended fix>" }}
  ]
}}
```
If verdict is VERIFIED, `issues` must be an empty array []. Do not omit this block.
"""

    r = route(content=prompt, task_type="session-verification", complexity_hint=78,
              session_set=SS, session_number=2)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
