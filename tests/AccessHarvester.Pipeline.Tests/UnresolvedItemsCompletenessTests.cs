using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;
using ScopeModuleKind = AccessHarvester.Vba.Resolver.Scopes.ModuleKind;
using ManifestModuleKind = AccessHarvester.Pipeline.Manifest.ModuleKind;

namespace AccessHarvester.Pipeline.Tests;

// DW-11 Layer A — procedure-HEADER unresolved references reach the index.
//
// `unresolved-items.json` is built from FunctionClassification.UnresolvedReferences,
// and FunctionClassificationBuilder walks `procedureAst.Body` ONLY. So an
// unresolved reference in a procedure HEADER (an Optional parameter's default
// expression) was dropped from the index even though ParserOutputProjector
// groups it into Body.References. 015 S1 closes the gap with a dedup-additive
// feed: the aggregator additionally consumes the full procedure-scoped resolver
// reference set (body + header) and suppresses the body overlap on the shared
// (module, procedure, line, column, reason) key, so the only net-new items are
// the header refs.
//
// These tests are hermetic (parse -> scope -> resolve -> classify -> aggregate);
// no Access COM is required. They drive the SAME chain the production manifest
// path runs (Pipeline.EmitManifestArtifacts), including the procedure-scoped
// feed wired there, so they pin the production behaviour without file IO.
public sealed class UnresolvedItemsCompletenessTests
{
    // ── Hermetic pipeline assembly (inline source) ──────────────────────

    private sealed record Chain(
        ResolvedModule Resolved,
        string ModuleName,
        ManifestModuleKind ManifestKind,
        IReadOnlyList<FunctionClassification> Classifications,
        NavigationGraph Navigation);

    private static Chain Build(
        string source, string moduleName, ScopeModuleKind kind = ScopeModuleKind.Standard)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        ModuleScope scope = new ScopeBuildingVisitor().BuildModuleScope(project, ast, moduleName, kind);
        ResolvedModule resolved = new ReferenceResolver().Resolve(scope, ast);
        CrossReferenceIndex index = CrossReferenceIndex.Build(resolved);
        var classifications = FunctionClassificationBuilder.BuildAll(new[] { resolved }, index).ToList();
        var navigation = NavigationGraphBuilder.Build(new[] { resolved }, index);
        var manifestKind = kind == ScopeModuleKind.Standard
            ? ManifestModuleKind.Standard
            : ManifestModuleKind.ClassModule;
        return new Chain(resolved, moduleName, manifestKind, classifications, navigation);
    }

    // The body-ONLY aggregation that shipped before 015 S1 (the production
    // call before the procedure-scoped feed was added). Used to demonstrate
    // the gap is real, not assumed.
    private static IReadOnlyList<UnresolvedItem> AggregateBodyOnly(Chain c) =>
        UnresolvedItemAggregator.Build(
            navigationGraph: c.Navigation,
            functionClassifications: c.Classifications);

    // The production aggregation after 015 S1 — mirrors Pipeline.cs exactly:
    // adds the procedure-scoped feed (body + header) built from the SAME
    // projector grouping that produces Body.References.
    private static IReadOnlyList<UnresolvedItem> AggregateProductionEquivalent(Chain c)
    {
        var procedureScoped = ParserOutputProjector
            .EnumerateProcedureScopedReferences(c.Resolved)
            .Select(scoped => new ProcedureScopedReference(
                c.Resolved.ModuleScope.ModuleName, scoped.ProcedureName, scoped.Reference))
            .ToList();

        return UnresolvedItemAggregator.Build(
            navigationGraph: c.Navigation,
            functionClassifications: c.Classifications,
            procedureScopedReferences: procedureScoped);
    }

    // The inline Body.References[ResolutionStatus="Unresolved"] view a consumer
    // reads, derived from the projector exactly as the manifest emits it.
    // Returns (ProcedureName, OriginalText, ResolutionReason) per unresolved ref.
    private static List<(string Procedure, string OriginalText, string Reason)>
        InlineUnresolvedBodyReferences(Chain c)
    {
        var node = ParserOutputProjector.Project(c.Resolved, c.Classifications, c.ModuleName, c.ManifestKind);
        var result = new List<(string, string, string)>();
        foreach (var item in node["Items"]!.AsArray())
        {
            if (item is null || (string?)item["ItemKind"] != "Procedure") continue;
            string procName = (string)item["Name"]!;
            var body = item["Body"];
            var refs = body?["References"]?.AsArray();
            if (refs is null) continue;
            foreach (var r in refs)
            {
                if (r is null) continue;
                if ((string?)r["ResolutionStatus"] != "Unresolved") continue;
                string original = (string)r["OriginalText"]!;
                string reason = (string?)r["ResolutionReason"] ?? "";
                result.Add((procName, original, reason));
            }
        }
        return result;
    }

    // ── Layer A: the header-ref gap and its close ───────────────────────

    [Fact]
    public void LayerA_HeaderUnresolvedRef_DroppedByBodyOnlyFeed_RecoveredByProcedureScopedFeed()
    {
        // DW-11 proven reproducer: a trivial body, one unresolved reference in
        // the HEADER (the Optional parameter's default expression MissingThing).
        var c = Build(
            "Public Sub Demo(Optional ByVal x As Variant = MissingThing)\r\nEnd Sub\r\n",
            "modHeader");

        // (1) The reference IS present inline on Body.References (the projector
        //     groups header-scoped refs into the procedure's Body.References).
        var inline = InlineUnresolvedBodyReferences(c);
        Assert.Contains(inline, r => r.OriginalText == "MissingThing");

        // (2) RED EVIDENCE: the body-only feed that shipped before 015 S1 drops
        //     it — the index was NOT a superset of the inline references.
        var bodyOnly = AggregateBodyOnly(c);
        Assert.DoesNotContain(bodyOnly, u => u.SurroundingText == "MissingThing");

        // (3) GREEN: the production-equivalent aggregation surfaces it.
        var withHeaders = AggregateProductionEquivalent(c);
        var header = Assert.Single(
            withHeaders.Where(u => u.SurroundingText == "MissingThing"));
        Assert.Equal(UnresolvedSource.FunctionClassification, header.Source);
        Assert.Equal(UnresolvedReasons.NotInScope, header.Reason);
        Assert.Equal("modHeader", header.Location.ModuleName);
        Assert.Equal("Demo", header.Location.ProcedureName);
    }

    [Fact]
    public void LayerA_BodyUnresolvedRef_EmittedExactlyOnce_NoDoubleCount()
    {
        // A BODY unresolved reference is carried by BOTH the FunctionClassification
        // feed and the new procedure-scoped feed. The dedup must collapse it to
        // exactly one item (the deliberate no-double-count invariant the original
        // functionClassifications-only feed protected).
        var c = Build(
            "Public Sub Demo2()\r\n    MsgBox UnknownThing\r\nEnd Sub\r\n",
            "modBody");

        var bodyOnly = AggregateBodyOnly(c);
        var withHeaders = AggregateProductionEquivalent(c);

        // Present once in the body-only feed, and STILL exactly once after the
        // additive feed (not twice).
        Assert.Single(bodyOnly.Where(u => u.SurroundingText == "UnknownThing"));
        Assert.Single(withHeaders.Where(u => u.SurroundingText == "UnknownThing"));

        // The additive feed introduced no net-new item for this body-only module
        // (every procedure-scoped ref was already covered) — proving inertness on
        // a header-free module. This is why LAYER A was golden-neutral on HHC
        // (HHC has zero procedure-HEADER unresolved cases). NOTE: Layer B was NOT
        // golden-neutral — HHC has 2 module-LEVEL unresolved refs (vbObjectError
        // in basDataOperations), regenerated into the golden by 015 S2.
        Assert.Equal(bodyOnly.Count, withHeaders.Count);
    }

    [Fact]
    public void LayerA_Index_IsStructuralSuperset_OfInlineUnresolvedBodyReferences()
    {
        // A procedure carrying BOTH a header unresolved ref and a body unresolved
        // ref. Every inline Body.References[Unresolved] entry must have a matching
        // index entry (the superset property the spec pins, not assumes).
        var c = Build(
            "Public Sub Demo3(Optional ByVal y As Variant = MissingHeader)\r\n" +
            "    MsgBox MissingBody\r\n" +
            "End Sub\r\n",
            "modBoth");

        var inline = InlineUnresolvedBodyReferences(c);
        // Sanity: the reproducer really does carry both an inline header and body
        // unresolved reference (so the superset assertion is non-vacuous).
        Assert.Contains(inline, r => r.OriginalText == "MissingHeader");
        Assert.Contains(inline, r => r.OriginalText == "MissingBody");

        var index = AggregateProductionEquivalent(c);
        var indexKeys = index
            .Where(u => u.Source == UnresolvedSource.FunctionClassification)
            .Select(u => (u.Location.ProcedureName, u.SurroundingText, u.Reason))
            .ToHashSet();

        // Match inline -> index on (procedure, name, reason). For bare-identifier
        // references OriginalText == SurroundingText (the member/identifier name).
        foreach (var (proc, original, reason) in inline)
        {
            Assert.Contains((proc, (string?)original, reason), indexKeys);
        }

        // And no double-count: the body ref appears exactly once.
        Assert.Single(index.Where(u => u.SurroundingText == "MissingBody"));
    }

    [Fact]
    public void LayerA_ChainedHeaderUnresolvedRefs_AllReachIndex_NoIntraFeedDrop()
    {
        // gpt-5-4 R1 (015 S1): a CHAINED unresolved member access in a HEADER
        // produces several legitimately-distinct procedure-scoped references
        // (Foo, Foo.Bar, Foo.Bar.Baz). If the dedup keyed only on
        // (module, procedure, line, column, reason) AND member-access
        // provenance anchored at the chain start, the deeper links would share
        // a key and one would be silently dropped — recreating an undercount
        // inside Layer A. None are in the body-only feed (they are header refs),
        // so the index must carry EVERY inline unresolved reference: the
        // superset property must hold for chained header refs too.
        var c = Build(
            "Public Sub Demo(Optional ByVal x As Variant = Foo.Bar.Baz)\r\nEnd Sub\r\n",
            "modChain");

        var inline = InlineUnresolvedBodyReferences(c);
        var index = AggregateProductionEquivalent(c)
            .Where(u => u.Source == UnresolvedSource.FunctionClassification)
            .ToList();

        // Every inline unresolved reference (matched on procedure + member name
        // + reason) must be present in the index, with no count shrinkage.
        var indexKeys = index
            .Select(u => (u.Location.ProcedureName, u.SurroundingText, u.Reason))
            .ToList();
        foreach (var (proc, original, reason) in inline)
        {
            // OriginalText for the chain links is "Foo" / "Foo.Bar" / "Foo.Bar.Baz";
            // SurroundingText is the member/identifier name "Foo" / "Bar" / "Baz".
            // Correlate on the trailing name so the assertion is robust to the
            // OriginalText-vs-memberName difference for member-access nodes.
            string name = original.Contains('.') ? original[(original.LastIndexOf('.') + 1)..] : original;
            Assert.Contains((proc, (string?)name, reason), indexKeys);
        }

        // And the index carries at least as many procedure-scoped unresolved
        // items as the inline view (no link collapsed away).
        Assert.True(index.Count >= inline.Count,
            $"index procedure-scoped unresolved count ({index.Count}) must be >= inline " +
            $"unresolved count ({inline.Count}); a smaller index means a chain link was dropped.");
    }

    [Fact]
    public void LayerA_HeaderRef_IndexPositionCorrelatesWithInlineReference()
    {
        // Pin the position correlation between the two views of a header ref so a
        // consumer can map an unresolved-items.json entry back to the manifest's
        // inline Body.References location. (HHC has zero header refs, so the golden
        // gate never exercises this path — it is pinned only here.)
        //
        // DW-26 (Set 018): the manifest's inline Body.References Location is now
        // emitted 1-BASED (ParserOutputProjector.BuildLocation +1's the AST's
        // 0-based ANTLR column to honour the schema's SourceLocation minimum:1 and
        // the 1-based line:col contract). The unresolved-items.json index, by
        // contrast, preserves every producer's RAW column VERBATIM and ALL feeds
        // (resolver, SQL form-reference, navigation-graph, AND function-
        // classification — this header ref is FunctionClassification-sourced) carry
        // the 0-based ANTLR column. So the index is UNIFORMLY 0-based and the two
        // views correlate at a fixed +1 column offset for ANY procedure-scoped ref:
        // inline (1-based) == index (0-based) + 1. The LINE is 1-based on both and
        // matches exactly. This cross-artifact offset is out of DW-26's scope
        // (normalising the index is a uniform +1 but golden-affecting, deferred to a
        // resolver-provenance set); it is recorded as DW-27. A consumer correlating
        // the two surfaces by column must apply the +1 until DW-27 lands.
        var c = Build(
            "Public Sub Demo(Optional ByVal x As Variant = MissingThing)\r\nEnd Sub\r\n",
            "modHeaderPos");

        var node = ParserOutputProjector.Project(c.Resolved, c.Classifications, c.ModuleName, c.ManifestKind);
        var inlineRef = node["Items"]!.AsArray()
            .Select(i => i!["Body"]?["References"]?.AsArray())
            .Where(a => a is not null)
            .SelectMany(a => a!)
            .Single(r => (string?)r!["OriginalText"] == "MissingThing");
        int inlineLine = (int)inlineRef!["Location"]!["StartLine"]!;
        int inlineColumn = (int)inlineRef["Location"]!["StartColumn"]!;

        var index = AggregateProductionEquivalent(c);
        var header = index.Single(u => u.SurroundingText == "MissingThing");

        Assert.NotNull(header.Location.Line);
        Assert.NotNull(header.Location.Column);
        // Line: identical 1-based value on both surfaces.
        Assert.Equal(inlineLine, header.Location.Line);
        // Column: the inline manifest column is 1-based; the index column is the
        // raw 0-based producer value. They correlate at a fixed +1 (DW-26/DW-27).
        Assert.Equal(inlineColumn, header.Location.Column + 1);
    }

    // ── Layer B: module-level unresolved references reach the index ─────
    //
    // DW-11 Layer B — a module-level unresolved reference (a Const/Dim
    // initializer, an `Implements <unresolved>`, etc.) lives on
    // resolvedModule.References with NO enclosing ProcedureScope, so
    // ParserOutputProjector.GroupReferencesByProcedure drops it from
    // Body.References ("the v1.0 schema has no module-level item type") AND
    // it never reaches unresolved-items.json. 015 S2 closes the gap with a
    // module-level feed mirroring the Layer A pattern: a new projector
    // enumerator (EnumerateModuleLevelReferences) returns exactly the set the
    // procedure grouping drops, and the aggregator surfaces them under the
    // new UnresolvedSource.ModuleLevelReference with ProcedureName=null.

    // The production aggregation including the Layer B module-level feed —
    // mirrors Pipeline.cs after 015 S2.
    private static IReadOnlyList<UnresolvedItem> AggregateWithModuleLevel(Chain c)
    {
        var procedureScoped = ParserOutputProjector
            .EnumerateProcedureScopedReferences(c.Resolved)
            .Select(scoped => new ProcedureScopedReference(
                c.Resolved.ModuleScope.ModuleName, scoped.ProcedureName, scoped.Reference))
            .ToList();

        var moduleLevel = ParserOutputProjector
            .EnumerateModuleLevelReferences(c.Resolved)
            .Select(reference => new ModuleLevelReference(
                c.Resolved.ModuleScope.ModuleName, reference))
            .ToList();

        return UnresolvedItemAggregator.Build(
            navigationGraph: c.Navigation,
            functionClassifications: c.Classifications,
            procedureScopedReferences: procedureScoped,
            moduleLevelReferences: moduleLevel);
    }

    [Fact]
    public void LayerB_ModuleLevelUnresolvedRef_DroppedByProcedureScopedSurfaces_RecoveredByModuleLevelFeed()
    {
        // DW-11 proven reproducer: a module-level Const initializer referencing
        // an undeclared name, plus a trivial procedure (so the module is not
        // otherwise empty).
        var c = Build(
            "Public Const DemoConst = MissingThing\r\n" +
            "Public Sub Demo()\r\nEnd Sub\r\n",
            "modModuleLevel");

        // (1) The resolver DOES produce the reference on the raw module — it is
        //     simply module-scoped (no enclosing ProcedureScope).
        Assert.Contains(
            c.Resolved.References,
            r => ParserOutputProjector.ExpressionText(r.Expression) == "MissingThing");

        // (2) RED EVIDENCE: it is in NEITHER inline Body.References (the
        //     projector drops module-scoped refs) NOR the post-S1 index.
        var inline = InlineUnresolvedBodyReferences(c);
        Assert.DoesNotContain(inline, r => r.OriginalText == "MissingThing");

        var withHeaders = AggregateProductionEquivalent(c);
        Assert.DoesNotContain(withHeaders, u => u.SurroundingText == "MissingThing");

        // (3) GREEN: the module-level feed surfaces it under the new source,
        //     at module scope (ProcedureName == null).
        var withModuleLevel = AggregateWithModuleLevel(c);
        var item = Assert.Single(
            withModuleLevel.Where(u => u.SurroundingText == "MissingThing"));
        Assert.Equal(UnresolvedSource.ModuleLevelReference, item.Source);
        Assert.Equal(UnresolvedReasons.NotInScope, item.Reason);
        Assert.Equal("modModuleLevel", item.Location.ModuleName);
        Assert.Null(item.Location.ProcedureName);
    }

    [Fact]
    public void LayerB_ModuleLevelFeed_DoesNotDoubleCount_ProcedureScopedRefs()
    {
        // A module carrying BOTH a module-level unresolved ref and a body
        // unresolved ref. The module-level feed must surface ONLY the
        // module-scoped reference; the body ref stays a single
        // FunctionClassification item (no double-count across feeds).
        var c = Build(
            "Public Const DemoConst = MissingModule\r\n" +
            "Public Sub Demo()\r\n    MsgBox MissingBody\r\nEnd Sub\r\n",
            "modBoth2");

        var index = AggregateWithModuleLevel(c);

        // The module-level ref appears exactly once, under the module-level source.
        var moduleItem = Assert.Single(index.Where(u => u.SurroundingText == "MissingModule"));
        Assert.Equal(UnresolvedSource.ModuleLevelReference, moduleItem.Source);

        // The body ref appears exactly once, under FunctionClassification — the
        // module-level feed did not also claim it.
        var bodyItem = Assert.Single(index.Where(u => u.SurroundingText == "MissingBody"));
        Assert.Equal(UnresolvedSource.FunctionClassification, bodyItem.Source);
    }

    [Fact]
    public void LayerB_ModuleFreeOfModuleLevelRefs_IsInert()
    {
        // A module with NO module-level unresolved ref: the Layer B feed adds
        // nothing (inertness on a module-level-ref-free module). NOTE: HHC itself
        // is NOT module-level-ref-free — basDataOperations has 2 (vbObjectError in
        // Const decls), so Layer B WAS golden-affecting (+2 ModuleLevelReference
        // items, regenerated by 015 S2). This pin covers the clean-module case; it
        // does not claim HHC-wide golden-neutrality. (Corrected per set-wide
        // adversarial review, GPT-5.4 H9.)
        var c = Build(
            "Public Sub Demo()\r\n    MsgBox UnknownThing\r\nEnd Sub\r\n",
            "modNoModuleLevel");

        var withHeaders = AggregateProductionEquivalent(c);
        var withModuleLevel = AggregateWithModuleLevel(c);

        Assert.Equal(withHeaders.Count, withModuleLevel.Count);
        Assert.DoesNotContain(withModuleLevel, u => u.Source == UnresolvedSource.ModuleLevelReference);
    }
}
