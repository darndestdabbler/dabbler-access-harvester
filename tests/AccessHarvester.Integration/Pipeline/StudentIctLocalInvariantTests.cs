using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Core;
using AccessHarvester.Integration.TestSupport;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Pipeline.Validation;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Integration.Pipeline;

// Local-only INVARIANT gate against the proprietary StudentICTDatabase corpus
// (set 017). StudentICTDatabase is the ONLY split front-end / back-end corpus
// available: 20 FE tables of which 9 are linked attachments to
// StudentICTDatabase_BE.accdb, a second independent custom ribbon ("Help"), and
// a large real DW-18 signal (3000+ intrinsic-constant false-positive unresolved
// refs). It exercises a structural path NEITHER committed corpus does — linked
// tables + a split FE whose Relationships live in the BE.
//
// It is PROPRIETARY and may NEVER be committed: every linked table's Connect
// string embeds a PLAINTEXT PASSWORD and an ABSOLUTE local filesystem path to
// the BE (see docs/planning/deferred-work.md DW-24). The FE, BE, and the
// samples/*.lnk shortcut are all gitignored. So this test does NOT diff a
// committed golden (there is none, by design) and it NEVER asserts the password
// or the absolute path. Instead it runs the production pipeline locally and
// asserts two kinds of checks, exactly like NorthwindLocalInvariantTests:
//   - STRUCTURAL INVARIANTS — corpus-independent properties that must hold on any
//     split-FE real-data run (clean pipeline run; >=1 linked table with a Connect
//     + SourceTableName referencing the BE filename; empty Relationships; a custom
//     ribbon with resolving entry points; FunctionClassification DW-18 witness);
//   - CORPUS-SPECIFIC CANARIES — exact names/ids for THIS corpus (ribbon "Help",
//     module "modRibbonCallbacks") that pin the harvested structure tightly to
//     catch a producer regression; a benign FE edit would legitimately change them.
// DW-26 (Set 018) is now CLOSED: this corpus is the witness that drove it. It is
// the only one with a line-leading (AST column 0) reference/call token, so before
// Set 018 its manifest emitted a 0-based StartColumn below the schema minimum and
// was schema-invalid. The pin now asserts full schema validity (the column base is
// normalised to 1-based at projection) plus a structural witness that no
// StartColumn==0 survives.
//
// SkippableFact-gated: it skips when Access COM is absent OR when EITHER the
// gitignored StudentICTDatabase.accdb (FE) or StudentICTDatabase_BE.accdb (BE)
// is not present locally. Both files are required: a split FE's linked-table
// schema resolves through the BE, so the BE must be present for the run to
// reproduce the linked-table surface. CI and fresh clones stay green.
//
// Serialized with the other HomeHealthCare COM tests via the shared collection
// so the Access instances do not race on ACE OLE DB's file lock.
[Collection("HomeHealthCareCom")]
[SupportedOSPlatform("windows")]
public sealed class StudentIctLocalInvariantTests
{
    private const string BackEndFileName = "StudentICTDatabase_BE.accdb";

    [SkippableFact]
    public void StudentICTDatabase_SplitFeRun_HoldsCorpusInvariants()
    {
        AccessAvailability.SkipIfNoAccess();

        string samples = RepoPaths.Samples();
        string fePath = Path.Combine(samples, "StudentICTDatabase.accdb");
        string bePath = Path.Combine(samples, BackEndFileName);
        Skip.IfNot(
            File.Exists(fePath) && File.Exists(bePath),
            $"StudentICTDatabase split corpus not present at {samples} (FE + BE are " +
            "gitignored / proprietary local-only — the linked-table surface resolves " +
            "through the BE, so BOTH files are required). Drop local copies of " +
            $"StudentICTDatabase.accdb and {BackEndFileName} into samples/ to exercise " +
            "this local-only split-FE invariant gate.");

        string stagingRoot = Path.Combine(
            Path.GetTempPath(), $"ah-studentict-invariant-{Guid.NewGuid():N}");
        try
        {
            var runResult = CorePipeline.Default().Run(fePath, stagingRoot);

            // Invariant 1: the full pipeline succeeds on a split FE with linked
            // tables. A crash or per-category failure here means a change broke
            // split-FE / linked-table extraction in a way neither committed
            // (non-split) corpus surfaces.
            Assert.All(runResult.Extract, c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"extract/{c.Category}: {o.ErrorMessage}")));
            Assert.All(runResult.Translate, c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"translate/{c.Category}: {o.ErrorMessage}")));
            Assert.All(runResult.Enrich, c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"enrich/{c.Category}: {o.ErrorMessage}")));

            // Invariant 2: the manifest is emitted and deserialises.
            string manifestPath = Path.Combine(runResult.BaseDir, "manifest.json");
            Assert.True(File.Exists(manifestPath),
                $"Pipeline did not emit manifest.json (searched {manifestPath}).");
            string manifestJson = File.ReadAllText(manifestPath);

            var manifest = JsonSerializer.Deserialize<DatabaseManifest>(
                manifestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(manifest);

            // Invariant 2b — DW-26 CLOSED (Set 018): the StudentICTDatabase
            // manifest now SCHEMA-VALIDATES. This corpus is the DW-26 witness: it is
            // the only one with a line-leading (AST column 0) reference/call token,
            // so before Set 018 its manifest emitted StartColumn 0 — below the
            // schema's SourceLocation `minimum: 1` — and was schema-INVALID. Set 018
            // normalised the projector's source-location column base to 1-based
            // (ParserOutputProjector.BuildLocation +1 on both Start and End,
            // width-preserving; the synthetic top-level location was already 1), so a
            // line-leading token now projects StartColumn 1 and the manifest conforms.
            // (HHC and NorthwindDevEdition schema-validated even before Set 018, but
            // only because their indented VBA never lands a token at column 0 — they
            // passed by luck, not by honouring a 1-based contract.)
            var schemaResult = new SchemaValidator(RepoPaths.SchemaFile()).Validate(manifestJson);
            Assert.True(schemaResult.IsValid,
                "StudentICTDatabase manifest failed schema validation after DW-26 " +
                "(it must be schema-valid once the column base is normalised to 1-based):\n  - " +
                string.Join("\n  - ", schemaResult.Errors));
            // Structural witness that the DW-26 normalisation actually reached THIS
            // corpus (robust to JSON whitespace and to schema-message changes): no
            // SourceLocation carries a numeric StartColumn of 0 — the line-leading
            // token that was column 0 pre-Set-018 is now column 1. Proves the
            // PRODUCER fix, not merely schema conformance.
            Assert.False(HasZeroStartColumn(manifestJson),
                "A SourceLocation StartColumn == 0 remains in the StudentICTDatabase manifest — " +
                "the DW-26 column-base normalisation did not reach this corpus.");

            // Invariant 3 (split-FE / linked-table surface): at least one table
            // is a linked attachment whose Connect references the BE filename and
            // carries a SourceTableName. The robust linked signal in `run` output
            // is a non-empty Connect + SourceTableName (TableMetadata.IsLinked and
            // the raw Attributes bitmask are NOT carried into the run-pipeline
            // manifest — see DW-25); checking Connect is the contract a consumer
            // must use. We assert presence/shape, not a fixed count (9 today), so
            // the invariant is robust to corpus edits.
            //
            // SECURITY: we deliberately reference only the BE *filename*. We never
            // assert the plaintext password or the absolute BE path the Connect
            // string also carries (DW-24) — those are machine-specific secrets.
            var linkedTables = manifest!.Tables
                .Where(t => !string.IsNullOrWhiteSpace(t.Connect)
                            && !string.IsNullOrWhiteSpace(t.SourceTableName))
                .ToList();
            Assert.NotEmpty(linkedTables);
            Assert.Contains(linkedTables,
                t => t.Connect!.Contains(BackEndFileName, StringComparison.OrdinalIgnoreCase));

            // Invariant 4 (split-FE relationships-absent behaviour): a split FE's
            // relationships live in the BE, so the FE harvest reaches none — the
            // run reported Relationships: 0. Documented in the interpretation
            // guide; pinned here as the split-FE structural invariant. (HHC and
            // Northwind are NOT split and DO carry relationships, so this is a
            // path only this corpus exercises.)
            Assert.Empty(manifest.Relationships);

            // Invariant 5 (second custom ribbon — re-validates DW-13 / 016 on a
            // DIFFERENT ribbon than Northwind). The CAPABILITY-LEVEL invariant
            // (corpus-independent): a custom USysRibbons ribbon is harvested into
            // NavigationGraph.Ribbon with at least one tab.
            var ribbon = manifest.NavigationGraph?.Ribbon;
            Assert.True(ribbon is not null,
                "NavigationGraph.Ribbon is null on a database that HAS a custom USysRibbons " +
                "ribbon — the DW-13 producer did not populate the facet.");
            Assert.NotEmpty(ribbon!.Tabs);

            // CORPUS-SPECIFIC CANARIES (not corpus-independent invariants): the exact
            // ribbon name + tab identity for THIS local corpus. They pin the harvested
            // structure tightly so a regression in the ribbon producer is caught, but
            // a benign FE edit (renaming the ribbon/tab) would legitimately change
            // them — update these values, not the producer, in that case.
            Assert.Equal("Help", ribbon.Name);
            Assert.Contains(ribbon.Tabs, t => t.Id == "tab1" && t.Label == "Help");

            // Invariant 6 (ribbon entry-point resolution — 016 S2): the
            // RibbonEntryPoints producer resolves this ribbon's onAction + root
            // onLoad against the harvested modules. Every entry surfaces an
            // explicit resolution state (none silently dropped).
            var ribbonEntryPoints = manifest.NavigationGraph!.RibbonEntryPoints;
            Assert.True(ribbonEntryPoints is not null && ribbonEntryPoints.Count > 0,
                "NavigationGraph.RibbonEntryPoints is null/empty on a database with a custom " +
                "ribbon that defines onAction callbacks — the DW-13 S2 producer did not run.");
            Assert.All(ribbonEntryPoints!, ep => Assert.True(
                ep.Resolution is RibbonCallbackResolution.Resolved
                    or RibbonCallbackResolution.Unresolved
                    or RibbonCallbackResolution.Ambiguous,
                $"Ribbon entry point '{ep.CallbackName}' has no resolution status."));

            // CAPABILITY-LEVEL invariants (corpus-independent): both ribbon
            // callback kinds (the root onLoad and the button onActions) are
            // surfaced AND resolve, and a Resolved entry always carries a non-empty
            // module (the DW-13 S2 resolution contract). These use "at least one of
            // each kind, all resolved" — NOT an exact count — so a benign FE edit
            // (a second ribbon/onLoad, more buttons) keeps them green.
            var onLoads = ribbonEntryPoints!
                .Where(ep => ep.Kind == RibbonCallbackKind.OnLoad)
                .ToList();
            var onActions = ribbonEntryPoints!
                .Where(ep => ep.Kind == RibbonCallbackKind.OnAction)
                .ToList();
            Assert.NotEmpty(onLoads);
            Assert.NotEmpty(onActions);
            Assert.All(onLoads.Concat(onActions), ep =>
            {
                Assert.Equal(RibbonCallbackResolution.Resolved, ep.Resolution);
                Assert.False(string.IsNullOrEmpty(ep.ResolvedProcedure!.ModuleName));
            });

            // CORPUS-SPECIFIC CANARIES: this corpus's ribbon defines exactly one
            // root onLoad, its buttons all share one dispatch callback
            // ("OnActionButton"), and every entry point resolves into the module
            // "modRibbonCallbacks" (a DIFFERENT module than Northwind's
            // "modRibbonCallback") — pinned to catch a resolution regression; update
            // these on a benign FE edit, not the producer.
            Assert.Single(onLoads);
            Assert.All(onActions, ep => Assert.Equal("OnActionButton", ep.CallbackName));
            Assert.All(ribbonEntryPoints!, ep =>
                Assert.Equal("modRibbonCallbacks", ep.ResolvedProcedure!.ModuleName));

            // Invariant 7 (DW-18 witness): this corpus surfaces a large body of
            // FunctionClassification unresolved references (3000+ today), almost
            // all VBA intrinsic constants the resolver has no registry for. We
            // assert presence (robust to corpus edits), not the count — this is
            // the empirical demonstration of DW-18's scale and the natural
            // validation corpus once a constant registry lands.
            string unresolvedPath = Path.Combine(runResult.BaseDir, "unresolved-items.json");
            Assert.True(File.Exists(unresolvedPath),
                $"Pipeline did not emit unresolved-items.json (searched {unresolvedPath}).");
            string unresolvedJson = File.ReadAllText(unresolvedPath);
            Assert.Contains("\"FunctionClassification\"", unresolvedJson);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    // Structural (whitespace-insensitive) check that the manifest carries at least
    // one SourceLocation with a numeric StartColumn of 0 — the DW-26 trigger. Walks
    // the JSON tree rather than matching a formatted substring so the pin does not
    // depend on the writer's whitespace.
    private static bool HasZeroStartColumn(string manifestJson)
    {
        using var doc = JsonDocument.Parse(manifestJson);
        return HasZeroStartColumn(doc.RootElement);
    }

    private static bool HasZeroStartColumn(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("StartColumn")
                        && prop.Value.ValueKind == JsonValueKind.Number
                        && prop.Value.TryGetInt32(out int col)
                        && col == 0)
                        return true;
                    if (HasZeroStartColumn(prop.Value)) return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (HasZeroStartColumn(item)) return true;
                return false;
            default:
                return false;
        }
    }
}
