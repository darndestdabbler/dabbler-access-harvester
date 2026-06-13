# 008 Session 3 — Verification ROUND 2 (closeout grade)

**Your response MUST begin with prose analysis BEFORE the JSON block. Both are required.**

Round 1 returned ISSUES_FOUND with one Major: that the F-3-6 requirement to fold
**per-module VBA parse diagnostics (counts + `ModeUsed`) into manifest
`Diagnostics`** was "not shown or test-pinned." Your R1 prose AFFIRMED all four
disclosed design decisions (D1 schema-versioning scope, D2 free-string reason,
D3 Option B, D4 expected golden delta) as correct — thank you.

The Major is a **context gap, not a code gap**: that fold was implemented in
**Session 1** of this same set (the manifest-diagnostics work was split S1/S3 in
the spec — S1 did the module/form/report side as part of the F-3-1 ingestion
fix; S3 completed the query side + ResolutionReason). R1's prompt disclosed this
in prose but did not SHOW the S1 code/test. Here it is. Please confirm whether
this closes the Major, or whether a real gap remains.

## Evidence the per-module parse-diagnostic fold exists and is pinned

### Pipeline.cs — module/form/report parse loop (Session 1; lines ~351-396)
A degraded parse (parse diagnostics present OR lossy fallback) emits a
manifest-level `VBA_PARSE_DEGRADED` diagnostic whose message embeds BOTH the
mode AND the diagnostic count; a thrown parse emits `VBA_PARSE_FAILURE`:
```csharp
// ModeUsed reports which parse *strategy* ran — pipeline default is
// Recovering even for a clean parse — so the degraded signal is the
// presence of parse diagnostics (or the lossy fallback), not non-Strict mode.
if (parseResult.Diagnostics.Count > 0
    || parseResult.ModeUsed == VbaParseMode.Lossy)
{
    diagnostics.Add(new HarvesterDiagnostic
    {
        Severity = HarvesterDiagnosticSeverity.Warning,
        Message = $"VBA parse degraded for '{name}': "
            + $"mode={parseResult.ModeUsed}, "
            + $"diagnostics={parseResult.Diagnostics.Count}",
        Code = "VBA_PARSE_DEGRADED",
    });
}
// ... (success path) ...
catch (Exception ex)
{
    diagnostics.Add(new HarvesterDiagnostic
    {
        Severity = HarvesterDiagnosticSeverity.Warning,
        Message = $"VBA parse failed for '{name}': {ex.Message}",
        Code = "VBA_PARSE_FAILURE",
    });
    // parse-failed ModulesAndClasses still surface as a kind-correct stub
}
```
These `diagnostics` are assembled into `manifest.Diagnostics` (the same list the
query-side `QUERY_SQL_PARSE_FAILURE` from S3 appends to).

### Test pin (Session 1, hermetic, always-running) — ManifestVbaIngestionTests.cs
```csharp
[Fact]
public void DegradedParse_SurfacesVbaParseDegradedDiagnostic()
{
    using var tree = StagedTree.Create();
    tree.AddKbObject("Forms", "frmDegraded", DegradedVbaCode); // in-proc syntax error
    tree.AddIrObject("Forms", "frmDegraded");
    var manifest = tree.EmitAndReadManifest();
    var degraded = manifest.RootElement.GetProperty("Diagnostics").EnumerateArray()
        .Where(d => d.GetProperty("Code").GetString() == "VBA_PARSE_DEGRADED").ToList();
    var hit = Assert.Single(degraded);
    Assert.Contains("frmDegraded", hit.GetProperty("Message").GetString());
}
```

### Golden corroboration
The regenerated HomeHealthCare `manifest.json` `Diagnostics` array carries the
real `VBA_PARSE_DEGRADED`/`VBA_PARSE_FAILURE` entries for the fixture's degraded
modules (e.g. the `rptClientProfile` `.Line` graphics-method parse, DW-2), each
naming the object and reporting mode + count.

## Spec wording (for reference)
Session 3 step 2 reads: "fold per-module `ParseResult.Diagnostics` (or counts +
`ModeUsed`) into the manifest `Diagnostics` array; replace the bare catch in
`BuildQueryExports` with a diagnostic-emitting one." The "(or counts + ModeUsed)"
alternative is exactly what S1 implemented; S3 added the query-side catch.

## Question
Given the above, does the F-3-6 manifest-diagnostics requirement stand satisfied
(S1 module side + S3 query side, both pinned)? Re-issue your verdict.

After your prose, output the JSON block exactly:
```json
{ "verdict": "VERIFIED" | "ISSUES_FOUND", "issues": [ { "severity": "...", "location": "...", "summary": "...", "fix": "..." } ] }
```
If VERIFIED, `issues` must be `[]`. Do not omit the block.
