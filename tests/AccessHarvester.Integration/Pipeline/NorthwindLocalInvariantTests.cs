using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using AccessHarvester.Core;
using AccessHarvester.Integration.TestSupport;
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
            // producer has real input to validate against. This documents that
            // the ground-truth custom-ribbon corpus the DW-13 deferral claimed
            // did not exist is, in fact, present and harvestable. When DW-13
            // lands its producer, extend this test to assert the parsed
            // NavigationGraph.Ribbon matches the local USysRibbons (validated
            // here, never committed as a golden).
            bool usysRibbonsHarvested = Directory
                .EnumerateFiles(runResult.IrDir, "USysRibbons*", SearchOption.AllDirectories)
                .Any();
            Assert.True(usysRibbonsHarvested,
                "Expected NorthwindDevEdition's USysRibbons table to be harvested under ir/ " +
                "(the DW-13 ribbon producer's input). If this fixture lost its custom ribbon, " +
                "update the DW-13 corpus note in docs/planning/deferred-work.md.");
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }
}
