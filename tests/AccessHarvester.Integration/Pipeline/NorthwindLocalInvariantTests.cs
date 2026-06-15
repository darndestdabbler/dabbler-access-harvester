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

// Local-only INVARIANT gate against the proprietary NorthwindDevEdition
// corpus. NorthwindDevEdition is 66 modules of real Microsoft VBA + a real
// schema — far richer than the committed HomeHealthCare fixture — but its
// outputs are PROPRIETARY and may NEVER be committed (the .accdb and all its
// derived artifacts are gitignored; sets 003-005 removed the residue). So this
// test does NOT diff a committed golden (there is none, by design). Instead it
// runs the production pipeline locally and asserts CORPUS-INDEPENDENT
// INVARIANTS — properties that must hold on any real-data run — to catch the
// "we only ever exercised the synthetic HHC fixture" gap that hid, until
// 2026-06-15, the fact that DW-11 Layer A/B and DW-13 had never run against a
// database that actually exercises them. (See
// docs/planning/deferred-work.md DW-11/DW-13 and the
// feedback_use_northwind_as_local_test_corpus memory.)
//
// SkippableFact-gated: it skips when Access COM is absent OR when the
// gitignored NorthwindDevEdition.accdb is not present locally, so CI and fresh
// clones stay green. On a developer host that has the .accdb it activates and
// fails loudly if a real-data invariant regresses.
//
// Serialized with the other HomeHealthCare COM tests via the shared collection
// so the two Access instances do not race on ACE OLE DB's file lock.
[Collection("HomeHealthCareCom")]
[SupportedOSPlatform("windows")]
public sealed class NorthwindLocalInvariantTests
{
    [SkippableFact]
    public void NorthwindDevEdition_RealVbaRun_HoldsCorpusInvariants()
    {
        AccessAvailability.SkipIfNoAccess();

        string fixturePath = Path.Combine(RepoPaths.Samples(), "NorthwindDevEdition.accdb");
        Skip.IfNot(
            File.Exists(fixturePath),
            $"NorthwindDevEdition.accdb not present at {fixturePath} (it is gitignored / " +
            "proprietary local-only). Drop a local copy into samples/ to exercise this " +
            "local-only real-data invariant gate.");

        string stagingRoot = Path.Combine(
            Path.GetTempPath(), $"ah-nw-invariant-{Guid.NewGuid():N}");
        try
        {
            var runResult = CorePipeline.Default().Run(fixturePath, stagingRoot);

            // Invariant 1: the full pipeline succeeds on real Microsoft VBA
            // (66 modules). A crash or a per-category failure here means a
            // change broke real-data extraction in a way the synthetic HHC
            // fixture did not surface.
            Assert.All(runResult.Extract, c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"extract/{c.Category}: {o.ErrorMessage}")));
            Assert.All(runResult.Translate, c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"translate/{c.Category}: {o.ErrorMessage}")));
            Assert.All(runResult.Enrich, c => Assert.All(c.Outcomes,
                o => Assert.True(o.Success, $"enrich/{c.Category}: {o.ErrorMessage}")));

            string unresolvedPath = Path.Combine(runResult.BaseDir, "unresolved-items.json");
            Assert.True(File.Exists(unresolvedPath),
                $"Pipeline did not emit unresolved-items.json (searched {unresolvedPath}).");
            string unresolvedJson = File.ReadAllText(unresolvedPath);

            // Invariant 2 (DW-11 Layer B): module-level unresolved references
            // surface on a real corpus. NorthwindDevEdition has module-scope
            // Const references the resolver cannot bind (e.g. vbWhite / vbYellow
            // in modValidation). Pre-DW-11 these were invisible in BOTH the
            // index and per-module ParserOutput; the index home added by 015 S2
            // must carry them. (Generic presence assertion — robust to corpus
            // edits — not a fixed count.)
            Assert.Contains("\"ModuleLevelReference\"", unresolvedJson);

            // Invariant 3 (DW-11 Layer A domain): procedure-scoped unresolved
            // references surface too (the FunctionClassification source carries
            // body + header refs after Layer A). A real corpus always has some.
            Assert.Contains("\"FunctionClassification\"", unresolvedJson);

            // Invariant 4: the manifest is emitted.
            Assert.True(File.Exists(Path.Combine(runResult.BaseDir, "manifest.json")),
                "Pipeline did not emit manifest.json.");

            // Invariant 5 (DW-13 precondition): NorthwindDevEdition's custom
            // ribbon table (USysRibbons) is harvested, so the DW-13 ribbon
            // producer has real input to validate against.
            bool usysRibbonsHarvested = Directory
                .EnumerateFiles(runResult.IrDir, "USysRibbons*", SearchOption.AllDirectories)
                .Any();
            Assert.True(usysRibbonsHarvested,
                "Expected NorthwindDevEdition's USysRibbons table to be harvested under ir/ " +
                "(the DW-13 ribbon producer's input). If this fixture lost its custom ribbon, " +
                "update the DW-13 corpus note in docs/planning/deferred-work.md.");

            // Invariant 6 (DW-13, 016 S1): the producer parses the REAL custom
            // ribbon. The RibbonExporter row-read of USysRibbons.RibbonXML +
            // RibbonXmlParser must populate NavigationGraph.Ribbon, and its
            // tab/control ids + onAction callbacks must match the local ribbon.
            // Validated locally only — never committed as a golden (proprietary).
            string manifestJson = File.ReadAllText(Path.Combine(runResult.BaseDir, "manifest.json"));

            // Invariant 6a: the ribbon-bearing manifest conforms to the updated
            // harvester-schema.v1.1 RibbonStructure $def (HHC carries no ribbon,
            // so this is the only run that exercises the new recursive shape).
            var schemaResult = new SchemaValidator(RepoPaths.SchemaFile()).Validate(manifestJson);
            Assert.True(schemaResult.IsValid,
                "NorthwindDevEdition manifest (with a populated NavigationGraph.Ribbon) failed " +
                "schema validation:\n  - " + string.Join("\n  - ", schemaResult.Errors));

            var manifest = JsonSerializer.Deserialize<DatabaseManifest>(
                manifestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var ribbon = manifest?.NavigationGraph?.Ribbon;
            Assert.True(ribbon is not null,
                "NavigationGraph.Ribbon is null on a database that HAS a custom USysRibbons " +
                "ribbon — the DW-13 producer did not populate the facet.");

            // The single Northwind ribbon row is named "Main" and opens with a
            // custom "Home" tab (id tHome).
            Assert.Equal("Main", ribbon!.Name);
            Assert.Contains(ribbon.Tabs, t => t.Id == "tHome" && t.Label == "Home");

            // Built-in idMso tabs/controls are surfaced as full nodes (not dropped).
            Assert.Contains(ribbon.Tabs, t => t.IdMso == "TabAddIns");
            // The contextualTabs section is modelled.
            Assert.NotEmpty(ribbon.ContextualTabSets);

            // The onAction callbacks (the navigation entry points S2 resolves)
            // are surfaced, including nested ones. cmdAbout_OnAction lives in
            // modRibbonCallback; ddMRU_OnAction is on the MRU dropDown.
            var onActions = CollectOnActions(ribbon);
            Assert.Contains("cmdAbout_OnAction", onActions);
            Assert.Contains("ddMRU_OnAction", onActions);

            // Invariant 7 (DW-13, 016 S2): the ribbon entry-point producer
            // resolves the real onAction callbacks against the harvested
            // modules. NavigationGraph.RibbonEntryPoints must be populated (a
            // ribbon is present) and cmdAbout_OnAction must resolve to a
            // procedure in modRibbonCallback. Every entry surfaces a resolution
            // status — none is silently dropped.
            var ribbonEntryPoints = manifest!.NavigationGraph!.RibbonEntryPoints;
            Assert.True(ribbonEntryPoints is not null && ribbonEntryPoints.Count > 0,
                "NavigationGraph.RibbonEntryPoints is null/empty on a database with a custom " +
                "ribbon that defines onAction callbacks — the DW-13 S2 producer did not run.");

            // Every entry carries one of the three explicit resolution states
            // (never an unset/dropped callback).
            Assert.All(ribbonEntryPoints!, ep => Assert.True(
                ep.Resolution is RibbonCallbackResolution.Resolved
                    or RibbonCallbackResolution.Unresolved
                    or RibbonCallbackResolution.Ambiguous,
                $"Ribbon entry point '{ep.CallbackName}' has no resolution status."));

            var about = Assert.Single(ribbonEntryPoints!,
                ep => ep.CallbackName == "cmdAbout_OnAction");
            Assert.Equal(RibbonCallbackResolution.Resolved, about.Resolution);
            Assert.Equal("modRibbonCallback", about.ResolvedProcedure!.ModuleName);
            Assert.Equal("cmdAbout_OnAction", about.ResolvedProcedure.ProcedureName);

            // Every distinct onAction the ribbon tree carries is surfaced as an
            // entry point — proves the walk reaches nested controls, not just
            // top-level ones. (Entry points may additionally cover the
            // contextualTabs section, which CollectOnActions does not walk, so
            // the entry-point set is a superset of the tab-tree onActions.)
            var epOnActions = ribbonEntryPoints!
                .Where(ep => ep.Kind == RibbonCallbackKind.OnAction)
                .Select(ep => ep.CallbackName)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Superset(epOnActions, onActions.ToHashSet(StringComparer.Ordinal));
            Assert.Contains("ddMRU_OnAction", epOnActions);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    // All onAction callbacks anywhere in the ribbon (tabs/groups/controls and
    // nested splitButton/menu/box children).
    private static IReadOnlyCollection<string> CollectOnActions(RibbonStructure ribbon)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        void Walk(RibbonControl c)
        {
            if (c.OnAction is { } a) found.Add(a);
            foreach (var child in c.Children) Walk(child);
        }
        foreach (var tab in ribbon.Tabs)
            foreach (var group in tab.Groups)
                foreach (var control in group.Controls)
                    Walk(control);
        return found;
    }
}
