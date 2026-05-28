using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Manifest;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Manifest;

// Hermetic tests for DatabaseManifestAggregator. The aggregator is a
// pure assembly function: each test fixes the smallest set of inputs
// that exercises one contract clause and asserts on the produced
// manifest directly. No I/O, no parser pipeline, no Access COM.
public sealed class DatabaseManifestAggregatorTests
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private static DatabaseInfo MinimalDatabase(string name = "Test.accdb")
        => new() { Name = name };

    private static FormComponentSpec MinimalSpec(string name)
        => new() { SourceFormName = name };

    private static FormExport MinimalFormExport(string name)
        => new() { Spec = MinimalSpec(name) };

    private static ModuleExport MinimalModule(string name, ModuleKind kind = ModuleKind.Standard)
        => new() { ModuleName = name, ModuleKind = kind };

    private static TableMetadata MinimalTable(string name)
        => new() { Name = name };

    private static RelationshipMetadata MinimalRelationship(string name)
        => new() { Name = name, ParentTable = "ParentT", ChildTable = "ChildT" };

    private static HarvesterDiagnostic Diag(string msg, HarvesterDiagnosticSeverity sev = HarvesterDiagnosticSeverity.Info)
        => new() { Severity = sev, Message = msg };

    // ── Required-input validation ────────────────────────────────────

    [Fact]
    public void Build_throws_when_database_is_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DatabaseManifestAggregator.Build(database: null!, harvesterVersion: "1.0.0"));
    }

    [Fact]
    public void Build_throws_when_harvester_version_is_null()
    {
        Assert.Throws<ArgumentException>(() =>
            DatabaseManifestAggregator.Build(MinimalDatabase(), harvesterVersion: null!));
    }

    [Fact]
    public void Build_throws_when_harvester_version_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            DatabaseManifestAggregator.Build(MinimalDatabase(), harvesterVersion: string.Empty));
    }

    // ── Constant fields ──────────────────────────────────────────────

    [Fact]
    public void Build_emits_schema_type_and_version_constants()
    {
        var manifest = DatabaseManifestAggregator.Build(MinimalDatabase(), "1.2.3");

        Assert.Equal("DatabaseManifest", manifest.SchemaType);
        Assert.Equal("1.0", manifest.SchemaVersion);
        Assert.Equal("1.2.3", manifest.HarvesterVersion);
    }

    // ── Empty inputs produce empty arrays (never null) ───────────────

    [Fact]
    public void Build_with_no_optional_inputs_produces_empty_arrays_and_zero_summary()
    {
        var manifest = DatabaseManifestAggregator.Build(MinimalDatabase(), "1.0.0");

        Assert.Empty(manifest.Forms);
        Assert.Empty(manifest.Reports);
        Assert.Empty(manifest.Queries);
        Assert.Empty(manifest.Modules);
        Assert.Empty(manifest.Tables);
        Assert.Empty(manifest.Relationships);
        Assert.Empty(manifest.ValidationRules);
        Assert.Empty(manifest.RowSourceSpecs);
        Assert.Empty(manifest.FunctionClassifications);
        Assert.Empty(manifest.EventHandlerClassifications);
        Assert.Empty(manifest.UnresolvedItems);
        Assert.Empty(manifest.Diagnostics);
        Assert.Null(manifest.NavigationGraph);

        Assert.Equal(0, manifest.Summary.FormCount);
        Assert.Equal(0, manifest.Summary.ReportCount);
        Assert.Equal(0, manifest.Summary.QueryCount);
        Assert.Equal(0, manifest.Summary.ModuleCount);
        Assert.Equal(0, manifest.Summary.TableCount);
        Assert.Equal(0, manifest.Summary.UnresolvedItemCount);
    }

    // ── Summary counts mirror populated arrays ───────────────────────

    [Fact]
    public void Summary_counts_mirror_populated_arrays()
    {
        var manifest = DatabaseManifestAggregator.Build(
            MinimalDatabase(),
            "1.0.0",
            forms: new[] { MinimalFormExport("frm1"), MinimalFormExport("frm2") },
            reports: new[] { MinimalFormExport("rpt1") },
            queries: System.Array.Empty<JsonNode>(),
            modules: new[] { MinimalModule("mod1"), MinimalModule("Cls1", ModuleKind.ClassModule), MinimalModule("mod2") },
            tables: new[] { MinimalTable("t1"), MinimalTable("t2"), MinimalTable("t3"), MinimalTable("t4") });

        Assert.Equal(2, manifest.Summary.FormCount);
        Assert.Equal(1, manifest.Summary.ReportCount);
        Assert.Equal(0, manifest.Summary.QueryCount);
        Assert.Equal(3, manifest.Summary.ModuleCount);
        Assert.Equal(4, manifest.Summary.TableCount);
    }

    [Fact]
    public void Summary_unresolved_item_count_includes_all_unresolved_items()
    {
        var u1 = new UnresolvedItem
        {
            Source = UnresolvedSource.Resolver,
            Reason = "test-reason",
            ConstructDescription = "Test",
            Location = new UnresolvedLocation(),
            SuggestedNextSteps = new[] { "look at it" },
        };
        var u2 = u1 with { Reason = "other-reason" };

        var manifest = DatabaseManifestAggregator.Build(
            MinimalDatabase(),
            "1.0.0",
            unresolvedItems: new[] { u1, u2 });

        Assert.Equal(2, manifest.UnresolvedItems.Count);
        Assert.Equal(2, manifest.Summary.UnresolvedItemCount);
    }

    // ── Caller ordering preserved (no re-sort) ───────────────────────

    [Fact]
    public void Forms_preserve_caller_supplied_order()
    {
        var f1 = MinimalFormExport("zForm");
        var f2 = MinimalFormExport("aForm");
        var f3 = MinimalFormExport("mForm");

        var manifest = DatabaseManifestAggregator.Build(
            MinimalDatabase(),
            "1.0.0",
            forms: new[] { f1, f2, f3 });

        Assert.Equal(new[] { "zForm", "aForm", "mForm" },
            manifest.Forms.Select(fe => fe.Spec.SourceFormName).ToArray());
    }

    [Fact]
    public void Tables_preserve_caller_supplied_order()
    {
        var t1 = MinimalTable("ZebraTable");
        var t2 = MinimalTable("AlphaTable");

        var manifest = DatabaseManifestAggregator.Build(
            MinimalDatabase(),
            "1.0.0",
            tables: new[] { t1, t2 });

        Assert.Equal(new[] { "ZebraTable", "AlphaTable" },
            manifest.Tables.Select(t => t.Name).ToArray());
    }

    // ── Materialization is independent of enumeration source ─────────

    [Fact]
    public void Lazy_enumerables_are_materialized_once()
    {
        int callCount = 0;
        System.Collections.Generic.IEnumerable<HarvesterDiagnostic> Source()
        {
            callCount++;
            yield return Diag("first");
            yield return Diag("second");
        }

        var manifest = DatabaseManifestAggregator.Build(
            MinimalDatabase(),
            "1.0.0",
            diagnostics: Source());

        Assert.Equal(2, manifest.Diagnostics.Count);
        // Re-enumerating the materialized result must not re-trigger the source.
        _ = manifest.Diagnostics.ToArray();
        _ = manifest.Diagnostics.ToArray();
        Assert.Equal(1, callCount);
    }

    // ── Database + NavigationGraph passthrough ───────────────────────

    [Fact]
    public void Database_is_passed_through_verbatim()
    {
        var db = new DatabaseInfo
        {
            Name = "X.accdb",
            FilePath = @"C:\Samples\X.accdb",
            ExportedAt = "2026-05-18T12:34:56Z",
        };

        var manifest = DatabaseManifestAggregator.Build(db, "9.9.9");

        Assert.Same(db, manifest.Database);
    }

    [Fact]
    public void NavigationGraph_is_passed_through_verbatim()
    {
        var graph = new NavigationGraph
        {
            EntryPoints = System.Array.Empty<NavigationEntryPoint>(),
            Edges = System.Array.Empty<NavigationEdge>(),
            UnresolvedTargets = System.Array.Empty<UnresolvedNavigationTarget>(),
            Macros = System.Array.Empty<MacroChain>(),
        };

        var manifest = DatabaseManifestAggregator.Build(
            MinimalDatabase(),
            "1.0.0",
            navigationGraph: graph);

        Assert.Same(graph, manifest.NavigationGraph);
    }

    // ── Comprehensive per-collection passthrough mapping ─────────────

    [Fact]
    public void Every_collection_input_lands_in_its_corresponding_manifest_slot()
    {
        // One sentinel value per collection input, each carrying a
        // uniquely-identifying name/label so the test asserts on the
        // exact slot rather than on count alone.
        var form        = MinimalFormExport("F1-passthrough");
        var report      = MinimalFormExport("R1-passthrough");
        var query       = (JsonNode)new JsonObject
        {
            ["ItemKind"]   = "Query",
            ["Name"]       = "Q1-passthrough",
            ["QueryType"]  = "Select",
            ["Sql"]        = new JsonObject { ["OriginalText"] = "SELECT 1" },
        };
        var module      = MinimalModule("M1-passthrough", ModuleKind.ClassModule);
        var table       = MinimalTable("T1-passthrough");
        var relationship = MinimalRelationship("Rel1-passthrough");
        var validation  = new ValidationRule
        {
            Scope            = ValidationRuleScope.Control,
            SourceObject     = "FrmPassthrough",
            Origin           = ValidationRuleOrigin.ControlValidationRule,
            Kind             = ValidationRuleKind.Range,
        };
        var rowSource   = new RowSourceSpec
        {
            SourceFormName = "FrmPassthrough",
            ControlName    = "cboPassthrough",
            ControlType    = "ComboBox",
            Kind           = RowSourceKind.TableOrQueryNamed,
        };
        var function    = new FunctionClassification
        {
            ModuleName              = "ModPassthrough",
            ProcedureName           = "ProcPassthrough",
            Kind                    = ProcedureKind.Sub,
            IsPublic                = true,
            IsStatic                = false,
            ParameterCount          = 0,
            HasReturnValue          = false,
            Categories              = new[] { ClassificationCategory.LateBound },
            PrimaryCategory         = ClassificationCategory.LateBound,
            IsMixed                 = false,
            Metrics                 = new ComplexityMetrics
            {
                CyclomaticComplexity = 1,
                LinesOfCode          = 3,
                StatementCount       = 2,
            },
            DirectDependencies      = new List<DependencyEdge>(),
            TransitiveDependencies  = new List<DependencyEdge>(),
            Callers                 = new List<DependencyEdge>(),
        };
        var eventHandler = new EventHandlerClassification
        {
            ProcedureName = "ProcOnLoad-passthrough",
            Categories    = new[] { EventHandlerClassificationCategory.Validation },
            Mixed         = false,
            Signals       = Array.Empty<EventHandlerClassificationSignal>(),
        };
        var unresolved   = new UnresolvedItem
        {
            Source              = UnresolvedSource.Resolver,
            Reason              = "U1-passthrough-reason",
            ConstructDescription = "U1 sentinel",
            Location            = new UnresolvedLocation(),
            SuggestedNextSteps  = new[] { "noop" },
        };
        var diagnostic   = Diag("D1-passthrough", HarvesterDiagnosticSeverity.Warning);

        var manifest = DatabaseManifestAggregator.Build(
            MinimalDatabase(),
            "1.0.0",
            forms:                       new[] { form },
            reports:                     new[] { report },
            queries:                     new JsonNode[] { query },
            modules:                     new[] { module },
            tables:                      new[] { table },
            relationships:               new[] { relationship },
            validationRules:             new[] { validation },
            rowSourceSpecs:              new[] { rowSource },
            functionClassifications:     new[] { function },
            eventHandlerClassifications: new[] { eventHandler },
            unresolvedItems:             new[] { unresolved },
            diagnostics:                 new[] { diagnostic });

        // Each collection landed in its own slot — and only its own slot —
        // verified by the unique sentinel value on each item.
        Assert.Equal("F1-passthrough",          Assert.Single(manifest.Forms).Spec.SourceFormName);
        Assert.Equal("R1-passthrough",          Assert.Single(manifest.Reports).Spec.SourceFormName);
        Assert.Equal("Q1-passthrough",          Assert.Single(manifest.Queries)["Name"]!.GetValue<string>());
        Assert.Equal("M1-passthrough",          Assert.Single(manifest.Modules).ModuleName);
        Assert.Equal(ModuleKind.ClassModule,    manifest.Modules[0].ModuleKind);
        Assert.Equal("T1-passthrough",          Assert.Single(manifest.Tables).Name);
        Assert.Equal("Rel1-passthrough",        Assert.Single(manifest.Relationships).Name);
        Assert.Equal("FrmPassthrough",          Assert.Single(manifest.ValidationRules).SourceObject);
        Assert.Equal("cboPassthrough",          Assert.Single(manifest.RowSourceSpecs).ControlName);
        Assert.Equal("ProcPassthrough",         Assert.Single(manifest.FunctionClassifications).ProcedureName);
        Assert.Equal("ProcOnLoad-passthrough",  Assert.Single(manifest.EventHandlerClassifications).ProcedureName);
        Assert.Equal("U1-passthrough-reason",   Assert.Single(manifest.UnresolvedItems).Reason);
        Assert.Equal("D1-passthrough",          Assert.Single(manifest.Diagnostics).Message);
        Assert.Equal(HarvesterDiagnosticSeverity.Warning,
                                                manifest.Diagnostics[0].Severity);
    }

    // ── Determinism across re-invocation ─────────────────────────────

    [Fact]
    public void Build_is_deterministic_for_identical_inputs()
    {
        var db = MinimalDatabase("Det.accdb");
        var forms = new[] { MinimalFormExport("a"), MinimalFormExport("b") };
        var tables = new[] { MinimalTable("T1"), MinimalTable("T2") };

        var first  = DatabaseManifestAggregator.Build(db, "1.0.0", forms: forms, tables: tables);
        var second = DatabaseManifestAggregator.Build(db, "1.0.0", forms: forms, tables: tables);

        // Records are structurally compared; the summary block matches
        // by value, the per-category arrays reference the same supplied
        // items, and the constants are constants.
        Assert.Equal(first.SchemaType,    second.SchemaType);
        Assert.Equal(first.SchemaVersion, second.SchemaVersion);
        Assert.Equal(first.Summary,       second.Summary);
        Assert.Equal(first.Forms.Count,   second.Forms.Count);
        Assert.Equal(first.Tables.Count,  second.Tables.Count);
    }
}
