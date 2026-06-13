# 008 Session 3 — Cross-Provider Verification (closeout grade)

**Your response MUST begin with a detailed prose analysis for each concern
below BEFORE the JSON block. Do not skip the analysis. Both the prose analysis
and the trailing JSON block are required.**

You are verifying the FINAL session (Session 3 of 3) of session set
`008-manifest-vba-ingestion-and-parser-hardening` in the Dabbler Access
Harvester (C#/.NET 10). This session implements **F-3-6** (diagnostics/reason
surfacing) and decides **F-3-9** (conformance-contract), then authors the next
set and closes the set. Prefer a false-positive ISSUES_FOUND over a
false-negative VERIFIED.

## What Session 3 was scoped to do (from the spec)
1. **F-3-6:** surface `ResolutionReason` on unresolved references in
   ParserOutput; fold per-module parse diagnostics (counts + ModeUsed) into the
   manifest `Diagnostics` (Session 1 already added VBA_PARSE_DEGRADED/FAILURE —
   S3 completes the query side); replace the bare catch in `BuildQueryExports`
   with a diagnostic-emitting one. Schema additions are additive; update the
   consumer-facing schema docs.
2. **F-3-9:** decide (consensus-eligible) between (a) building a conformance
   runner that diffs harvester output vs the hand-authored `<n>.expected.json`
   fixtures, or (b) amending guidance/README to state the actual enforced
   contract. Correct the README case count + schema-version sentence either way.
3. Author the next session set; regenerate the HomeHealthCare golden; re-run
   Integration; closeout.

## Key design decisions to scrutinize (disclosed, please challenge)

**D1 — Schema versioning scope.** `ResolutionReason` lives on `ResolvedReference`,
which is part of the **ParserOutput** sub-document (its own `SchemaVersion`,
validated by the schema as a free *pattern* `^\d+\.\d+...`, NOT a const). I
bumped the ParserOutput sub-version 1.0 -> 1.1 and documented `ResolutionReason`
as an OPTIONAL property on the shared `ResolvedReference` $def IN-PLACE in
`harvester-schema.v1.1.json` (the manifest *envelope* const stays "1.1"). I did
NOT do a full envelope v1.1 -> v1.2 file bump (that would ripple through ~15
test files, the CLI's embedded schema, and the top-level manifest version) —
the field is additive and already valid because `ResolvedReference` has no
`additionalProperties:false`. Is keeping the envelope at v1.1 while bumping only
the ParserOutput sub-version defensible, or does the established additive
discipline require a new envelope file? Is documenting an optional property
in-place in the v1.1 file acceptable?

**D2 — ResolutionReason left as a free string (not an enum) in the schema.** The
resolver's `UnresolvedReasons` set is explicitly append-only ("extended, never
renamed"), so an enum would reject future values. Correct call?

**D3 — F-3-9 = Option B.** Routed cross-provider consensus (gpt-5-4 + gemini-pro)
returned **Option B, high confidence, unanimous** (raw at s3-f39-consensus-*.md).
I amended `project-guidance.md` (VBA Parser Strategy) and the test-cases README
to state the real enforced contract (schema-shape conformance + targeted
behavioral tests + the HomeHealthCare byte golden), re-classified the
`expected.json` files as design-intent/onboarding artifacts, corrected "12
cases" -> 14 and the v0 -> v1.1 schema sentence, and noted the case-12 stop-rule
exclusion. No runner built. Is this honest and complete, or does removing the
"contract" language weaken a real guarantee?

**D4 — Expected golden delta (disclosed, NOT a regression).** Only
`manifest.json` changed (+4644/-2338). All 34 ParserOutput sub-docs are now
"1.1" (0 left at "1.0"); 2306 `ResolutionReason` entries appeared — this is the
**disclosed post-F-3-1 Unresolved wall** (form/report code-behind now parses,
and its cross-module/control/Me references resolve Unresolved until the P3
resolver-wiring set; each now carries an honest reason). kb/ and ir/ trees are
byte-unchanged. Is treating this as expected (not a defect) correct?

## Evidence
- **Build:** clean, 0 errors.
- **Hermetic:** `AccessHarvester.Pipeline.Tests` 798/798; `ManifestVbaIngestionTests`
  6/6 (incl. new QUERY_SQL_PARSE_FAILURE pin + ResolutionReason pins +
  well-formed-query negative pin).
- **Integration (COM, TRX `<Counters>`):** total=61 passed=58 failed=0 error=0;
  3 skipped (2 guarded golden regenerators + the quarantined sanitize test).
- **.accdb:** swept clean before staging.

## Out of scope (do not flag as missing)
- F-3-4 resolver/navigation wiring (P3) — the Unresolved wall is the deliberate
  intermediate state, NOT this set's job.
- F-2-1 query-type decode, F-3-5 ByVal, output-file BOM (P2).
- Encoding correctness (P1b/009 — the next set this session authored).
- DW-1..DW-4 deferred items remain open by design.

---

## DIFF 1 — ParserOutputProjector.cs + Pipeline.cs (F-3-6 code)
```diff
diff --git a/AccessHarvester.Core/Pipeline.cs b/AccessHarvester.Core/Pipeline.cs
index a0786e0..d0e6eba 100644
--- a/AccessHarvester.Core/Pipeline.cs
+++ b/AccessHarvester.Core/Pipeline.cs
@@ -782,7 +782,12 @@ public sealed class Pipeline
                     File.ReadAllText(file, Encoding.UTF8), readOptions)
                     ?? throw new InvalidDataException("Null JSON root.");
 
-                // Parse the SQL; graceful fallback when parse fails.
+                // Parse the SQL; graceful fallback when parse fails. A parse
+                // failure still ships the query (Raw-only Parsed), but no
+                // longer silently — F-3-6: the bare catch here previously
+                // erased the fact that a query downgraded to Raw-only, so a
+                // consumer could not tell a Raw-only query apart from one the
+                // parser never attempted.
                 QueryAbstraction? abstraction = null;
                 try
                 {
@@ -791,9 +796,17 @@ public sealed class Pipeline
                     var sqlAst = parseResult.BuildAst(raw.Name ?? name);
                     abstraction = QueryAbstractionBuilder.Build(raw.Name ?? name, sqlAst);
                 }
-                catch
+                catch (Exception ex)
                 {
-                    // Parse failure → abstraction stays null → Raw-only Parsed
+                    // Parse failure → abstraction stays null → Raw-only Parsed,
+                    // recorded as a diagnostic so the downgrade is visible.
+                    diagnostics.Add(new HarvesterDiagnostic
+                    {
+                        Severity = HarvesterDiagnosticSeverity.Warning,
+                        Message = $"Query SQL parse failed for '{raw.Name ?? name}'; "
+                            + $"shipping Raw-only Sql.Parsed: {ex.Message}",
+                        Code = "QUERY_SQL_PARSE_FAILURE",
+                    });
                 }
 
                 var queryNode = QuerySqlProjector.Project(
diff --git a/AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs b/AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs
index 152fcf2..a224837 100644
--- a/AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs
+++ b/AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs
@@ -89,7 +89,16 @@ public static class ParserOutputProjector
 
         return new JsonObject
         {
-            ["SchemaVersion"] = "1.0",
+            // 1.1 (008 Session 3, F-3-6): ResolvedReference now carries an
+            // optional ResolutionReason on Unresolved references — the
+            // resolver's closed reason set (UnresolvedReasons), previously
+            // dropped at this projection layer. Additive: the field is
+            // optional and the manifest envelope schema's ResolvedReference
+            // $def already tolerated it (no additionalProperties:false), so
+            // old consumers ignore it. The ParserOutput SchemaVersion is a
+            // free pattern string in the schema, not a const, so the bump
+            // costs no manifest-envelope (v1.1) churn.
+            ["SchemaVersion"] = "1.1",
             ["Source"] = new JsonObject
             {
                 ["Kind"] = sourceKind,
@@ -322,7 +331,7 @@ public static class ParserOutputProjector
 
     private static JsonNode BuildResolvedReference(ResolvedReference r)
     {
-        var (status, kind, target, confidence) = MapOutcome(r.Outcome);
+        var (status, kind, target, confidence, reason) = MapOutcome(r.Outcome);
 
         var node = new JsonObject
         {
@@ -334,6 +343,11 @@ public static class ParserOutputProjector
         if (kind is not null) node["ReferenceKind"] = kind;
         if (target is not null) node["ResolvedTarget"] = target;
         if (confidence is double conf) node["Confidence"] = conf;
+        // F-3-6: surface the resolver's reason for an unresolved reference
+        // (the closed UnresolvedReasons set). The project principle is
+        // "unresolvable constructs are explicit, not silent"; the resolver
+        // honored it and this layer previously erased the explanation.
+        if (reason is not null) node["ResolutionReason"] = reason;
 
         return node;
     }
@@ -351,26 +365,26 @@ public static class ParserOutputProjector
 
     // ── Outcome mapping ───────────────────────────────────────────────
 
-    private static (string Status, string? Kind, JsonNode? Target, double? Confidence)
+    private static (string Status, string? Kind, JsonNode? Target, double? Confidence, string? Reason)
         MapOutcome(ResolutionOutcome outcome) => outcome switch
     {
         ResolutionOutcome.Resolved r => (
             "Resolved",
             DeclarationKindString(r.Declaration),
             BuildDeclarationTarget(r.Declaration),
-            1.0),
+            1.0, null),
 
         ResolutionOutcome.ResolvedToObjectModel r => (
             "Resolved",
             "ObjectModel",
             BuildDeclarationTarget(r.Declaration),
-            1.0),
+            1.0, null),
 
         ResolutionOutcome.ResolvedToBuiltIn r => (
             "Resolved",
             "BuiltIn",
             new JsonObject { ["Name"] = r.Function.Name, ["Category"] = r.Function.Category.ToString() },
-            1.0),
+            1.0, null),
 
         ResolutionOutcome.ResolvedToMe r => (
             "Resolved",
@@ -380,22 +394,30 @@ public static class ParserOutputProjector
                 ["Kind"] = r.OwningClass.Kind == ClassDeclarationKind.ReportModule ? "Report" : "Form",
                 ["Name"] = r.OwningClass.Name,
             },
-            1.0),
+            1.0, null),
 
         ResolutionOutcome.ResolvedViaWithFlattening f =>
             MapOutcomeWithFlattening(f),
 
-        ResolutionOutcome.Unresolved =>
-            ("Unresolved", null, null, null),
+        // The reason string is preserved verbatim from the resolver's
+        // closed UnresolvedReasons set so the consumer can distinguish a
+        // genuine miss (not-in-scope) from a known capability gap
+        // (default-member-not-yet-resolved) without re-deriving it.
+        ResolutionOutcome.Unresolved u =>
+            ("Unresolved", null, null, null, u.Reason),
 
-        _ => ("Unresolved", null, null, null),
+        _ => ("Unresolved", null, null, null, null),
     };
 
-    private static (string Status, string? Kind, JsonNode? Target, double? Confidence)
+    private static (string Status, string? Kind, JsonNode? Target, double? Confidence, string? Reason)
         MapOutcomeWithFlattening(ResolutionOutcome.ResolvedViaWithFlattening f)
     {
-        var (status, kind, target, _) = MapOutcome(f.InnerOutcome);
-        return (status, kind, target, 0.9);
+        // A flattened implicit-with member can itself end up Unresolved
+        // (e.g. the member name was not found on the With target); carry
+        // the inner reason forward so the explanation is not lost behind
+        // the 0.9 flattening-confidence wrapper.
+        var (status, kind, target, _, reason) = MapOutcome(f.InnerOutcome);
+        return (status, kind, target, 0.9, reason);
     }
 
     private static Declaration? FlattenDeclaration(ResolutionOutcome outcome) => outcome switch
```

## DIFF 2 — harvester-schema.v1.1.json (additive ResolutionReason)
```diff
diff --git a/docs/test-cases/harvester-schema.v1.1.json b/docs/test-cases/harvester-schema.v1.1.json
index cb63f3f..2c7af89 100644
--- a/docs/test-cases/harvester-schema.v1.1.json
+++ b/docs/test-cases/harvester-schema.v1.1.json
@@ -419,6 +419,10 @@
             { "type": "null" }
           ]
         },
+        "ResolutionReason": {
+          "type": "string",
+          "description": "Additive (008 Session 3, F-3-6). Present on Unresolved references only: the resolver's reason for why the name did not resolve. Lets a consumer triage an Unresolved reference (a genuine miss vs. a known capability gap) without re-deriving it. Drawn from the resolver's UnresolvedReasons set, which is append-only (extended, never renamed): currently 'not-in-scope', 'me-in-standard-module', 'default-member-not-yet-resolved', 'with-block-flattening-deferred', 'member-access-on-unresolved-target'. Left as a free string (not an enum) so future reasons remain schema-valid. Optional, so documents without it stay valid."
+        },
         "ImplicitContext": {
           "type": "object",
           "properties": {
```

## DIFF 3 — tests (new hermetic pins)
```diff
diff --git a/tests/AccessHarvester.Pipeline.Tests/Manifest/ParserOutputProjectorTests.cs b/tests/AccessHarvester.Pipeline.Tests/Manifest/ParserOutputProjectorTests.cs
index 4334b81..1b64412 100644
--- a/tests/AccessHarvester.Pipeline.Tests/Manifest/ParserOutputProjectorTests.cs
+++ b/tests/AccessHarvester.Pipeline.Tests/Manifest/ParserOutputProjectorTests.cs
@@ -165,6 +165,70 @@ End Sub";
             "Expected at least one Unresolved reference for DoUnknownThing");
     }
 
+    // ── Tests: F-3-6 ResolutionReason surfacing ──────────────────────
+
+    [Fact]
+    public void UnresolvedReference_CarriesResolutionReason_NotInScope()
+    {
+        // A bare unknown identifier walks the scope chain without a match →
+        // the resolver's NotInScope reason, which the projector now surfaces.
+        const string source = @"
+Option Explicit
+Public Sub DoSomething()
+    DoUnknownThing
+End Sub";
+        var resolved = Resolve(source);
+        var item = Item(Project(resolved), 0)!;
+        var refs = item["Body"]!["References"]!.AsArray();
+
+        var unresolved = refs.First(r =>
+            r!["ResolutionStatus"]?.GetValue<string>() == "Unresolved")!;
+
+        Assert.Equal("not-in-scope", unresolved["ResolutionReason"]!.GetValue<string>());
+    }
+
+    [Fact]
+    public void MeInStandardModule_CarriesResolutionReason_MeInStandardModule()
+    {
+        // `Me` has no class context in a standard module → the resolver's
+        // MeInStandardModule reason. Distinct from not-in-scope, so this
+        // proves the reason is the resolver's actual value, not a constant.
+        const string source = @"
+Option Explicit
+Public Sub DoSomething()
+    Me.Requery
+End Sub";
+        var resolved = Resolve(source, kind: ResolverModuleKind.Standard);
+        var item = Item(Project(resolved), 0)!;
+        var refs = item["Body"]!["References"]!.AsArray();
+
+        var meRef = refs.FirstOrDefault(r =>
+            r!["ResolutionReason"]?.GetValue<string>() == "me-in-standard-module");
+
+        Assert.NotNull(meRef);
+    }
+
+    [Fact]
+    public void ResolvedReference_HasNoResolutionReason()
+    {
+        // A resolved reference (built-in) must NOT carry a reason — the
+        // field is unresolved-only, by omission semantics.
+        const string source = @"
+Public Sub DoSomething()
+    Dim s As String
+    s = Format(Now(), ""yyyy-mm-dd"")
+End Sub";
+        var resolved = Resolve(source);
+        var item = Item(Project(resolved), 0)!;
+        var refs = item["Body"]!["References"]!.AsArray();
+
+        foreach (var r in refs.Where(r =>
+            r!["ResolutionStatus"]?.GetValue<string>() == "Resolved"))
+        {
+            Assert.Null(r!["ResolutionReason"]);
+        }
+    }
+
     // ── Tests: Body.Calls ─────────────────────────────────────────────
 
     [Fact]
@@ -211,7 +275,11 @@ End Sub";
             moduleName: "modFoo");
         var root = Project(resolved, "modFoo");
 
-        Assert.Equal("1.0", root["SchemaVersion"]!.GetValue<string>());
+        // 1.1: F-3-6 added the optional ResolutionReason on unresolved
+        // references; the ParserOutput sub-document version is bumped to
+        // signal the new field (the schema validates it as a free pattern,
+        // not a const, so no manifest-envelope churn).
+        Assert.Equal("1.1", root["SchemaVersion"]!.GetValue<string>());
         Assert.NotNull(root["Source"]!["Kind"]);
         Assert.NotNull(root["Source"]!["Location"]);
         // Standard module → Kind = "Procedure"
diff --git a/tests/AccessHarvester.Tests/ManifestVbaIngestionTests.cs b/tests/AccessHarvester.Tests/ManifestVbaIngestionTests.cs
index fe7cda0..7d095b6 100644
--- a/tests/AccessHarvester.Tests/ManifestVbaIngestionTests.cs
+++ b/tests/AccessHarvester.Tests/ManifestVbaIngestionTests.cs
@@ -128,6 +128,45 @@ public sealed class ManifestVbaIngestionTests
         Assert.Empty(EnumerateDiagnosticCodes(manifest));
     }
 
+    // F-3-6: a saved query whose SQL fails to parse must surface a
+    // QUERY_SQL_PARSE_FAILURE diagnostic (the downgrade to Raw-only Parsed
+    // used to be silent — the bare catch in BuildQueryExports swallowed it).
+    [Fact]
+    public void QuerySqlParseFailure_SurfacesQuerySqlParseFailureDiagnostic()
+    {
+        using var tree = StagedTree.Create();
+        // The recovering SQL parser builds a statement context with no
+        // recognized DML body, so AstBuilder throws — exercising the inner
+        // catch. (Same unparseable string QuerySqlProjectorTests uses.)
+        tree.AddRawQuery("qryBad", "THIS IS NOT VALID SQL AT ALL @@@@", "Select");
+
+        var manifest = tree.EmitAndReadManifest();
+
+        var failures = manifest.RootElement.GetProperty("Diagnostics").EnumerateArray()
+            .Where(d => d.GetProperty("Code").GetString() == "QUERY_SQL_PARSE_FAILURE")
+            .ToList();
+        var hit = Assert.Single(failures);
+        Assert.Contains("qryBad", hit.GetProperty("Message").GetString());
+
+        // The query is still shipped (Raw-only Parsed), not dropped.
+        var query = Assert.Single(manifest.RootElement.GetProperty("Queries").EnumerateArray());
+        Assert.Equal("qryBad", query.GetProperty("Name").GetString());
+    }
+
+    // A well-formed query parses without emitting the failure diagnostic —
+    // proves the diagnostic is failure-scoped, not unconditional.
+    [Fact]
+    public void WellFormedQuery_EmitsNoParseFailureDiagnostic()
+    {
+        using var tree = StagedTree.Create();
+        tree.AddRawQuery("qryGood", "SELECT a FROM b", "Select");
+
+        var manifest = tree.EmitAndReadManifest();
+
+        Assert.DoesNotContain(
+            EnumerateDiagnosticCodes(manifest), c => c == "QUERY_SQL_PARSE_FAILURE");
+    }
+
     private static System.Collections.Generic.IEnumerable<string> EnumerateDiagnosticCodes(
         JsonDocument manifest)
         => manifest.RootElement.GetProperty("Diagnostics").EnumerateArray()
@@ -169,6 +208,20 @@ public sealed class ManifestVbaIngestionTests
                 Path.Combine(categoryDir, name + ".json"), node.ToJsonString());
         }
 
+        public void AddRawQuery(string name, string sql, string queryTypeName)
+        {
+            string categoryDir = Path.Combine(RawDir, "Queries");
+            Directory.CreateDirectory(categoryDir);
+            var node = new System.Text.Json.Nodes.JsonObject
+            {
+                ["Name"] = name,
+                ["Sql"] = sql,
+                ["QueryTypeName"] = queryTypeName,
+            };
+            File.WriteAllText(
+                Path.Combine(categoryDir, name + ".json"), node.ToJsonString());
+        }
+
         public void AddIrObject(string category, string name)
         {
             string categoryDir = Path.Combine(IrDir, category);
```

## F-3-9 decision record (excerpt) + 009 spec
See `s3-f39-decision.md` (Option B, consensus) and the authored next set
`docs/session-sets/009-output-encoding-correctness/spec.md` (P1b, 1 session,
Hard Constraints carried verbatim).

---

After your prose analysis (required, above), output a JSON block in this exact format:
```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    { "severity": "Critical" | "Major" | "Minor", "location": "<file or test>", "summary": "<one sentence>", "fix": "<recommended fix>" }
  ]
}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit this block.
