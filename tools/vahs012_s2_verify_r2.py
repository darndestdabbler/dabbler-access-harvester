"""Set 012 Session 2 cross-provider verification — ROUND 2 (R2-2 part 1 / DW-10).
Confirms resolution of the 5 R1 findings (3 Major + 2 Minor). Persist-before-display."""
import os
from ai_router import route

SS = "docs/session-sets/012-switchboard-and-report-chain-edges"
OUT = os.path.join(SS, "s2-verification-round-2.md")


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def excerpt(path, anchor, length):
    t = read(path)
    i = t.find(anchor)
    return t[i:i + length] if i >= 0 else f"(anchor {anchor!r} not found)"


def main():
    builder = read("AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs")
    tests = read("tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs")
    ir_obj = excerpt("AccessHarvester.Pipeline/UsageIdentification/IrDatabase.cs",
                     "Object-level embedded", 700)
    ir_loader = excerpt("AccessHarvester.Pipeline/UsageIdentification/IrLoader.cs",
                        "EventMacros     = ReadEventMacros(root)", 120)

    prompt = f"""You are an expert C#/.NET reviewer performing ROUND 2 of an INDEPENDENT
cross-provider verification. In Round 1 you returned ISSUES_FOUND with 5 findings
(3 Major + 2 Minor). All 5 were addressed. Your job: confirm each is genuinely
resolved AND that the fixes introduced no new defect. Begin with prose analysis per
finding, THEN the JSON block. Prefer false-positive ISSUES_FOUND over false-negative
VERIFIED.

# Repo / session
dabbler-access-harvester — mechanical Access-metadata harvester; explicit-unresolved
over silent-drop; deterministic; lean by default. Set 012 Session 2 = R2-2 part 1 /
DW-10: chain-edge model + mechanical producers (NavigationChainBuilder) over the IR
surfaces. No VBA (S3 owns the code-behind `vba-call` hops).

# Round 1 findings and how each was addressed (verify each)

**Major 1 — embedded-macro producer only scanned control-level EventMacros; object-level
`OnOpenEmMacro` not representable; raw `*EmMacro` missed by the enricher silently dropped.**
Fix: (a) `IrFormOrReport.EventMacros` added and populated by `IrLoader` from the form/report
ROOT node (`EventMacros = ReadEventMacros(root)`); (b) `CollectEmbeddedMacroEdges` now scans
BOTH object-level (From = the object, Member = null) and control-level via `EmitEmbeddedMacroEdges`;
(c) `FlagUnparsedEmbeddedMacros` emits an explicit `UnresolvedChainTarget`
(reason `embedded-macro-unparsed`) when an `<Event>EmMacro` property exists but no structured
`EventMacros` covers that event. Golden-neutral on HHC (no object-level Opens; every `*EmMacro`
is enricher-structured). NOTE for your scrutiny: the unparsed fallback must pick a `Kind`
(model requires one); it uses `OpensForm` as a representative open kind with a code comment —
is that acceptable, or do you want a different signal?

**Major 2 — TempVars regex not token-aware (could match inside string literals / as a substring).**
Fix: the regex gained a left identifier boundary `(?<![A-Za-z0-9_])`; `ExtractTempVars` blanks
all JET string literals (single/double-quoted, doubled-quote escapes) BEFORE scanning, so a
literal containing `TempVars!x` cannot fabricate an edge. Pinned by
`RecordSource_TempVarsInsideStringLiteral_DoesNotEmitEdge`.

**Major 3 — inline SQL marked "parsed" on any exception-free recovered AST → recovered-but-empty
parse silently lost FeedsRecordSource edges.**
Fix: the `parsed` bool was removed. The producer now emits `record-source-unparsed` whenever an
inline record source yields ZERO FROM/JOIN table sources (whether the parse threw or recovered to
an AST with no usable source) — a stronger success criterion than "no exception". TempVars-only /
form-ref-only sources still flag unparsed if they carry no data source. Pinned by
`RecordSource_InlineSqlWithNoRecoverableSource_EmitsRecordSourceUnparsed`. (Is keying on
"zero table sources" rather than parser diagnostics acceptable? My reasoning: a real record source
always has a FROM; the TempVars case extracts its FROM fine and so is not falsely flagged.)

**Minor 1 — SortEdges/SortUnresolved comparators not total (case-insensitive Name/Member, no
ordinal tiebreak; List.Sort unstable).**
Fix: `CompareEndpoint` now applies `OrdinalIgnoreCase` then an `Ordinal` tiebreaker for Name and
Member, giving a total, stable order for case-only-differing names.

**Minor 2 — `ParseFormRefPath` fabricated `"?"` Form/Report endpoints for blank/malformed paths
(default-injection).**
Fix: `ParseFormRefPath` returns `(null, null)` for blank/malformed paths; the declared-parameter
producer then emits an explicit `UnresolvedChainTarget` (reason `unresolvable-reference`) instead
of a synthetic endpoint. Pinned by `QueryParameter_MalformedFormReference_IsSurfacedUnresolvedNotFabricated`.

# Validation after fixes
- Build: 0 errors. Hermetic: AccessHarvester.Pipeline.Tests 863/863 pass (incl. 18 chain pins +
  the IrLoader EventMacros pin; the 5 new R1-fix pins listed above).
- COM golden gate: CanonicalGoldenRegressionTests + ManifestSchemaConformanceTests 6/6 pass —
  the HHC golden is **byte-identical** to the previously regenerated R2-2 corpus (every fix is
  golden-neutral on this corpus), so no unexplained delta was introduced.

# Closed-set additions (schema + model docs updated)
Reason set extended (append-only): `prompt-parameter | record-source-unparsed |
embedded-macro-unparsed | unresolvable-reference`.

# Full source under review (post-fix)

=== AccessHarvester.Pipeline/Enrichment/NavigationChainBuilder.cs (FULL) ===
```csharp
{builder}
```

=== IrDatabase.cs — IrFormOrReport.EventMacros (object-level) ===
```csharp
{ir_obj}
```

=== IrLoader.cs — object-level EventMacros populated from the root node ===
```csharp
{ir_loader}
```

=== tests/AccessHarvester.Pipeline.Tests/NavigationChainBuilderTests.cs (FULL) ===
```csharp
{tests}
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

    r = route(content=prompt, task_type="session-verification", complexity_hint=85,
              session_set=SS, session_number=2)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(r.content)
    print(f"Wrote {OUT} | model={r.model_name} | in={r.input_tokens} out={r.output_tokens} | cost=${r.cost_usd:.4f}")


if __name__ == "__main__":
    main()
