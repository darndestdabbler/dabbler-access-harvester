"""Set 012 Session 2 cross-provider verification — ROUND 3 (final).
Confirms the 2 R2 follow-on fixes (bracketed-TempVars + empty form-ref segment).
Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"
OUT = os.path.join(SS, "s2-verification-round-3.md")


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def excerpt(path, anchor, length):
    t = read(path)
    i = t.find(anchor)
    return t[i:i + length] if i >= 0 else f"(anchor {anchor!r} not found)"


def main():
    tempvar_regex = excerpt("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
                            "// TempVars!name or TempVars![bracketed name]", 700)
    extract_tv = excerpt("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
                         "private static IReadOnlyList<string> ExtractTempVars", 900)
    parse_path = excerpt("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs",
                        "// Parses the verbatim reference path", 1300)
    pins = excerpt("tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs",
                  "// ── R2 follow-on fixes", 1700)

    prompt = f"""You are an expert C#/.NET reviewer performing ROUND 3 (final) of an INDEPENDENT
cross-provider verification. In Round 2 the 3 original Major findings were confirmed
RESOLVED, but you raised 2 NEW follow-on issues. Both were addressed. Confirm each is
genuinely resolved and introduced no new defect. Begin with prose per finding, THEN the
JSON block. Prefer false-positive ISSUES_FOUND over false-negative VERIFIED.

# Repo / session
dabbler-access-harvester — mechanical Access-metadata harvester; explicit-unresolved over
silent-drop; deterministic; lean by default. Set 012 S2 chain-edge producers
(NavigationChainBuilder). The two R1-confirmed deviations (consume structured EventMacros;
recover TempVars textually because the JET grammar has no generic postfix-bang) stand.

# Round 2 follow-on findings and how each was addressed

**R2 Major — TempVars extractor still matched inside a BRACKETED identifier
(`[TempVars!fake]`).** Fix: the left negative look-behind now also excludes `[`
(`(?<![A-Za-z0-9_\\[])`), so `TempVars` that opens a bracketed identifier is not matched.
String literals are still blanked first. A real `TempVars![member]` is unaffected (its
`TempVars` is preceded by an operator/space, not `[`). Pinned by
`RecordSource_TempVarsInsideBracketedIdentifier_DoesNotEmitEdge` (asserts `[TempVars!fake]`
yields no edge while `TempVars!real` does). Your scrutiny: is excluding a `[`-preceded
`TempVars` sufficient, given string literals are already blanked and the only remaining
shape is a bracketed identifier opening with `TempVars`? Are there other contexts a
non-token-aware scan would still misfire that matter for a record-source SELECT?

**R2 Minor — `ParseFormRefPath` dropped empty `!` segments, so `[Forms]!![txtId]` collapsed
to a fabricated `txtId` object.** Fix: empty segments are no longer dropped; the parse now
validates positionally — segment[0] must be `Forms`/`Reports`, segment[1] (the object) must
be non-empty — otherwise returns `(null, null)` and the caller emits an explicit
`unresolvable-reference` UnresolvedChainTarget. Pinned by
`QueryParameter_FormReferenceWithEmptySegment_IsUnresolvedNotFabricated`.

# Validation after fixes
- Build: 0 errors. Hermetic: NavigationChainBuilderTests + IrLoaderTests 24/24 pass
  (full Pipeline.Tests hermetic suite 865/865). The two new pins above are included.
- Golden-neutral: HomeHealthCare exercises neither branch (no bracketed-TempVars identifier,
  no `[Forms]!`/`[Reports]!` query parameter), so the committed HHC golden is unchanged;
  the COM CanonicalGoldenRegression + ManifestSchemaConformance gates pass byte-identical.

# Source under review (post-fix)

=== NavigationChainBuilder.cs — TempVars regex (with the bracket-excluding look-behind) ===
```csharp
{tempvar_regex}
```

=== NavigationChainBuilder.cs — ExtractTempVars (string-literal scrub then scan) ===
```csharp
{extract_tv}
```

=== NavigationChainBuilder.cs — ParseFormRefPath (positional, root-validated) ===
```csharp
{parse_path}
```

=== NavigationChainBuilderTests.cs — the two new R2-follow-on pins ===
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

    r = route(content=prompt, task_type="session-verification", complexity_hint=80,
              session_set=SS, session_number=2)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
