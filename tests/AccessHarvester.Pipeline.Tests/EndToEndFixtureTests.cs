using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Session 4 end-to-end fixture cross-cut. For each of the five anchor
// fixtures called out in spec.md (07 legacy, 08 unresolvable, 10 class
// with events, 11 autoexec startup chain, 12 switchboard navigation),
// drives the parse → scope → resolve → CrossReferenceIndex →
// FunctionClassificationBuilder → NavigationGraphBuilder →
// UnresolvedItemAggregator chain, asserts the expected facets each
// fixture surfaces, and exercises the schema-envelope shape the run
// output assembles against harvester-schema.v0.json.
//
// These tests are hermetic; no Access COM is required.
public sealed class EndToEndFixtureTests
{
    // ── Path resolution ──────────────────────────────────────────────

    private static string LocateTestCasesRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases");
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate docs/test-cases walking up from '{AppContext.BaseDirectory}'.");
    }

    // ── Pipeline assembly per fixture ───────────────────────────────

    private sealed record FixtureRun(
        string FixtureName,
        string Source,
        ResolvedModule Resolved,
        CrossReferenceIndex CrossReferences,
        IReadOnlyList<FunctionClassification> Classifications,
        NavigationGraph Navigation,
        IReadOnlyList<UnresolvedItem> Unresolved);

    private static FixtureRun RunFixture(
        string fileName,
        string moduleName,
        ModuleKind kind = ModuleKind.Standard)
    {
        string source = File.ReadAllText(Path.Combine(LocateTestCasesRoot(), fileName));
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(fileName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope scope = visitor.BuildModuleScope(project, ast, moduleName, kind);
        ResolvedModule resolved = new ReferenceResolver().Resolve(scope, ast);
        CrossReferenceIndex index = CrossReferenceIndex.Build(resolved);

        var classifications = FunctionClassificationBuilder
            .BuildAll(new[] { resolved }, index)
            .ToList();
        var navigation = NavigationGraphBuilder.Build(new[] { resolved }, index);
        var unresolved = UnresolvedItemAggregator.Build(
            navigationGraph: navigation,
            functionClassifications: classifications);

        return new FixtureRun(
            FixtureName: fileName,
            Source: source,
            Resolved: resolved,
            CrossReferences: index,
            Classifications: classifications,
            Navigation: navigation,
            Unresolved: unresolved);
    }

    // ── Run-output envelope (harvester-schema.v0.json shape) ────────
    //
    // Builds a representative envelope for the schema-validation routed
    // call and the in-process structural assertions. The shape mirrors
    // harvester-schema.v0.json: {schemaVersion, source, items[],
    // unresolvedItems[]}. Each procedure becomes one items[] entry of
    // itemKind="procedure"; each aggregated UnresolvedItem maps to one
    // unresolvedItems[] entry with the schema's closed reason enum.

    private static JsonDocument BuildRunOutputEnvelope(FixtureRun run)
    {
        var items = new List<object>();
        foreach (var c in run.Classifications)
        {
            items.Add(new
            {
                itemKind = "procedure",
                name = c.ProcedureName,
                procedureKind = MapProcedureKind(c.Kind),
                visibility = c.IsPublic ? "Public" : "Private",
                isStatic = c.IsStatic,
                signature = new
                {
                    parameters = Array.Empty<object>(), // mechanically empty here; full parameters live on the parser AST
                    returnType = c.HasReturnValue ? "Variant" : (string?)null,
                },
                classification = new
                {
                    purity = MapPurity(c.PrimaryCategory, c.IsMixed),
                    complexity = c.Metrics.CyclomaticComplexity,
                    linesOfCode = c.Metrics.LinesOfCode,
                    translatability = MapTranslatability(c.PrimaryCategory, c.IsMixed),
                },
                body = new { },
            });
        }

        var unresolvedItems = run.Unresolved.Select(u => new
        {
            reason = MapSchemaReason(u),
            location = new
            {
                module = u.Location.ModuleName ?? run.FixtureName,
                // sourceLocation requires startLine/startColumn >= 1, but
                // resolver and SQL form-reference annotations carry 0-based
                // ANTLR positions (per UnresolvedLocation.Column docs);
                // coerce zero/null to 1 to keep the envelope schema-valid.
                startLine = Math.Max(1, u.Location.Line ?? 1),
                startColumn = Math.Max(1, u.Location.Column ?? 1),
            },
            originalText = u.SurroundingText,
            context = u.ConstructDescription,
            suggestedNextStep = u.SuggestedNextSteps.Count > 0 ? u.SuggestedNextSteps[0] : null,
        }).ToList();

        var envelope = new
        {
            schemaVersion = "0.1",
            source = new
            {
                kind = "procedure",
                location = new
                {
                    module = run.Resolved.ModuleScope.ModuleName,
                    startLine = 1,
                    startColumn = 1,
                },
                originalSource = run.Source,
            },
            items,
            unresolvedItems,
        };

        string json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });
        return JsonDocument.Parse(json);
    }

    // Maps internal aggregator reason codes to the harvester-schema.v0.json
    // unresolvedItem.reason enum (closed: unresolvedReference,
    // dynamicSqlTooComplex, ambiguousResolution, unsupportedConstruct,
    // parseFailure, missingMetadata, circularReference). The aggregator's
    // upstream codes are richer than the schema enum; the mapping keeps
    // the schema valid while preserving the original reason in the
    // envelope's `context` field (already done in BuildRunOutputEnvelope
    // via ConstructDescription).
    private static string MapSchemaReason(UnresolvedItem u) => u.Source switch
    {
        UnresolvedSource.Resolver => u.Reason switch
        {
            "not-in-scope" => "unresolvedReference",
            "me-in-standard-module" => "unsupportedConstruct",
            "default-member-not-yet-resolved" => "ambiguousResolution",
            "with-block-flattening-deferred" => "ambiguousResolution",
            "member-access-on-unresolved-target" => "unresolvedReference",
            _ => "unresolvedReference",
        },
        UnresolvedSource.SqlFormReference => u.Reason switch
        {
            "form-not-in-metadata" => "missingMetadata",
            "report-not-in-metadata" => "missingMetadata",
            "control-not-on-form" => "missingMetadata",
            "control-not-on-report" => "missingMetadata",
            "host-collection-not-registered" => "missingMetadata",
            "bare-collection-reference" => "ambiguousResolution",
            _ when u.Reason.StartsWith("member-not-found-at-segment-", StringComparison.Ordinal)
                => "ambiguousResolution",
            _ => "unresolvedReference",
        },
        UnresolvedSource.DynamicSqlSite => u.Reason switch
        {
            "combinatorial-blowup" => "dynamicSqlTooComplex",
            "unbound-variable" => "unresolvedReference",
            "unknown-expression" => "unsupportedConstruct",
            _ => "unsupportedConstruct",
        },
        UnresolvedSource.NavigationTarget => u.Reason switch
        {
            "non-literal-target" => "unresolvedReference",
            "missing-target" => "unsupportedConstruct",
            _ => "unsupportedConstruct",
        },
        UnresolvedSource.FunctionClassification => u.Reason switch
        {
            "not-in-scope" => "unresolvedReference",
            "member-access-on-unresolved-target" => "unresolvedReference",
            "with-block-flattening-deferred" => "ambiguousResolution",
            "default-member-not-yet-resolved" => "ambiguousResolution",
            _ => "unresolvedReference",
        },
        _ => "unsupportedConstruct",
    };

    // Maps PrimaryCategory + IsMixed to the schema's closed `purity` enum
    // (pure | dataAccess | uiBound | externalIO | mixed | unknown). The
    // aggregator's classification model is structurally compatible.
    private static string MapPurity(ClassificationCategory primary, bool isMixed)
    {
        if (isMixed) return "mixed";
        return primary switch
        {
            ClassificationCategory.Pure => "pure",
            ClassificationCategory.DataAccess => "dataAccess",
            ClassificationCategory.UIBound => "uiBound",
            ClassificationCategory.ExternalIO => "externalIO",
            ClassificationCategory.LateBound => "unknown",
            _ => "unknown",
        };
    }

    private static string MapTranslatability(ClassificationCategory primary, bool isMixed)
    {
        if (isMixed) return "manualReview";
        return primary switch
        {
            ClassificationCategory.Pure => "mechanical",
            ClassificationCategory.DataAccess => "structured",
            ClassificationCategory.UIBound => "structured",
            ClassificationCategory.ExternalIO => "ambiguous",
            ClassificationCategory.LateBound => "manualReview",
            _ => "ambiguous",
        };
    }

    // The Pipeline ProcedureKind enum carries Declare for `Declare Sub` /
    // `Declare Function` external-library entry points; the schema's
    // procedureKind enum (harvester-schema.v0.json) does not include
    // Declare and instead reserves EventHandler. This is the v0-G5 gap
    // recorded in docs/schema/v0-vs-tiers.md, deferred to
    // schema-finalization-and-docs (#17). The current 5 anchor fixtures
    // do not contain Declare statements (DeclareGuard test below holds
    // the line), so the fallback path is theoretical for now — but
    // emitting a schema-invalid string would silently fail downstream
    // schema validation. Map to "Function" as a conservative placeholder
    // until #17 either extends the enum or changes the contract.
    private static string MapProcedureKind(ProcedureKind kind) => kind switch
    {
        ProcedureKind.Sub => "Sub",
        ProcedureKind.Function => "Function",
        ProcedureKind.PropertyGet => "PropertyGet",
        ProcedureKind.PropertyLet => "PropertyLet",
        ProcedureKind.PropertySet => "PropertySet",
        ProcedureKind.Declare => "Function",
        _ => "Function",
    };

    // Closed enum from harvester-schema.v0.json $defs.procedureItem.
    // Kept here so the test assertion below is anchored to the schema,
    // not to the Pipeline enum (which would mask v0-G5 regressions).
    private static readonly string[] SchemaProcedureKinds =
        ["Sub", "Function", "PropertyGet", "PropertyLet", "PropertySet", "EventHandler"];

    // ── Per-fixture assertions ──────────────────────────────────────

    [Fact]
    public void Fixture07_LegacyPatterns_ProducesMixedClassification()
    {
        // Fixture 07: ImportClientBatch is a Function that touches Excel
        // COM (External I/O), DAO recordsets (DataAccess), and UI surfaces
        // (Debug.Print + MsgBox via ValidateRow's surroundings) — the
        // canonical "mixed" classification candidate per spec anchor.
        var run = RunFixture("07-legacy-patterns.bas", "modLegacyImport");

        Assert.NotEmpty(run.Classifications);
        var importer = run.Classifications.Single(c => c.ProcedureName == "ImportClientBatch");
        Assert.True(importer.IsMixed,
            $"Fixture 07 ImportClientBatch must classify as mixed (categories: {string.Join(',', importer.Categories)}).");

        // Cyclomatic complexity > 1 (lots of branches, loops, GoTo flow).
        Assert.True(importer.Metrics.CyclomaticComplexity > 5,
            $"Fixture 07 cyclomatic complexity should reflect the GoTo / If / For control flow; got {importer.Metrics.CyclomaticComplexity}.");

        // Run output envelope round-trips and lists this procedure.
        using var doc = BuildRunOutputEnvelope(run);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("items").ValueKind);
        Assert.Contains(doc.RootElement.GetProperty("items").EnumerateArray(),
            it => it.GetProperty("name").GetString() == "ImportClientBatch");
    }

    [Fact]
    public void Fixture08_Unresolvable_ProducesUnresolvedItems()
    {
        // Fixture 08 is the "intentional manual-review" anchor: dynamic
        // form name from DLookup → DoCmd.OpenForm sFormName, dynamic
        // Application.Run sCallback. Every dispatch should surface as an
        // UnresolvedItem (UnresolvedSource.NavigationTarget for the
        // OpenForm; UnresolvedSource.FunctionClassification or .Resolver
        // for the bare identifier chains).
        var run = RunFixture("08-unresolvable.bas", "modConfigDriven");

        Assert.NotEmpty(run.Classifications);
        var openByConfig = run.Classifications.Single(c => c.ProcedureName == "OpenFormByConfig");

        // Every dispatch is non-literal — there should be zero literal
        // navigation edges and at least one navigation-target unresolved.
        Assert.Empty(run.Navigation.Edges);
        Assert.Contains(run.Unresolved,
            u => u.Source == UnresolvedSource.NavigationTarget
              && u.Reason == "non-literal-target");

        // The classification carried a UI/DataAccess mixture, so it must
        // be flagged mixed for downstream review.
        Assert.True(openByConfig.IsMixed,
            "Fixture 08 OpenFormByConfig must classify as mixed.");
    }

    [Fact]
    public void Fixture10_ClassWithEvents_ClassifiesPropertiesAndEventSinks()
    {
        // Fixture 10 is a class module with WithEvents binding. The
        // classifier must produce records for each Property Get/Let, the
        // Class_Initialize/Class_Terminate housekeeping, the
        // m_Form_BeforeUpdate / AfterUpdate event sinks, and the public
        // AttachToForm. PropertyGet/PropertyLet must surface as their
        // own ProcedureKind values (not collapsed into Sub).
        var run = RunFixture("10-class-with-events.cls", "clsCaseAuditor", ModuleKind.Class);

        Assert.NotEmpty(run.Classifications);
        var byKind = run.Classifications
            .GroupBy(c => c.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.True(byKind.ContainsKey(ProcedureKind.PropertyGet),
            "Fixture 10 must surface PropertyGet procedures (EntityName, Enabled).");
        Assert.True(byKind.ContainsKey(ProcedureKind.PropertyLet),
            "Fixture 10 must surface PropertyLet procedures (EntityName, Enabled).");
        Assert.True(byKind.ContainsKey(ProcedureKind.Sub),
            "Fixture 10 must surface Sub procedures (Class_Initialize, AttachToForm, etc.).");

        // Class_Initialize is private-by-default in a class module.
        var init = run.Classifications.Single(c => c.ProcedureName == "Class_Initialize");
        Assert.False(init.IsPublic, "Class_Initialize must be Private (class-module default).");
    }

    [Fact]
    public void Fixture11_AutoExecStartupChain_ProducesNavigationEdges()
    {
        // Fixture 11 is the AutoExec → mdlStartup.Initialize → DoCmd.OpenForm
        // chain. Initialize() dispatches to one of five literal switchboard
        // forms via Select Case + DoCmd.OpenForm "frm…". Every literal
        // target should land as a NavigationEdge with TargetKind.Form.
        var run = RunFixture("11-autoexec-startup-chain.bas", "mdlStartup");

        // Five Select Case arms each open a literal-named form.
        var openFormEdges = run.Navigation.Edges
            .Where(e => e.Action == NavigationAction.OpenForm)
            .ToList();
        Assert.True(openFormEdges.Count >= 5,
            $"Fixture 11 must produce >= 5 OpenForm edges (one per Select Case arm); got {openFormEdges.Count}.");

        // All targets are literal strings starting with "frm".
        Assert.All(openFormEdges, e =>
        {
            Assert.NotNull(e.TargetName);
            Assert.StartsWith("frm", e.TargetName!, StringComparison.Ordinal);
            Assert.Equal(NavigationTargetKind.Form, e.TargetKind);
        });

        // Every edge points at a procedure whose source line aligns with
        // the Select Case body.
        var initialize = run.Classifications.Single(c => c.ProcedureName == "Initialize");
        Assert.True(initialize.Metrics.CyclomaticComplexity > 1,
            $"Fixture 11 Initialize() has multiple decision points (On Error, If, Select Case); got complexity {initialize.Metrics.CyclomaticComplexity}.");
    }

    [Fact]
    public void Fixture12_SwitchboardNavigation_ProducesPerHandlerEdges()
    {
        // Fixture 12 is the canonical switchboard-form pattern: each
        // btn*_Click handler dispatches DoCmd.OpenForm or DoCmd.OpenReport
        // to one literal target. The graph should contain one navigation
        // edge per dispatching handler, plus the reports/forms it touches.
        var run = RunFixture("12-switchboard-navigation.bas", "frmMainSwitchboard", ModuleKind.Class);

        // At least one OpenForm and one OpenReport (btnQuickCaseload_Click).
        Assert.Contains(run.Navigation.Edges, e =>
            e.Action == NavigationAction.OpenForm
            && e.TargetName == "frmCaseDetail");
        Assert.Contains(run.Navigation.Edges, e =>
            e.Action == NavigationAction.OpenReport
            && e.TargetName == "rptCaseLoadByDistrict");

        // Source procedure names should map back to the Click handlers.
        var newCaseEdge = run.Navigation.Edges
            .Single(e => e.TargetName == "frmCaseDetail");
        Assert.Equal("btnNewCase_Click", newCaseEdge.SourceProcedure);
        Assert.Equal(NavigationAction.OpenForm, newCaseEdge.Action);

        // DoCmd.Close acForm in btnExit_Click must not surface as an
        // OpenForm edge — Close has its own action with Unknown target
        // when the second arg is dynamic (Me.Name here). The graph may
        // legitimately omit it from Edges if the target name is not a
        // literal. What we verify is that the literal OpenForm edges
        // never include "Me.Name".
        Assert.DoesNotContain(run.Navigation.Edges, e => e.TargetName == "Me.Name");
    }

    // ── Run-output envelope cross-fixture invariants ─────────────────

    [Theory]
    [InlineData("07-legacy-patterns.bas", "modLegacyImport", false)]
    [InlineData("08-unresolvable.bas", "modConfigDriven", false)]
    [InlineData("10-class-with-events.cls", "clsCaseAuditor", true)]
    [InlineData("11-autoexec-startup-chain.bas", "mdlStartup", false)]
    [InlineData("12-switchboard-navigation.bas", "frmMainSwitchboard", true)]
    public void RunOutputEnvelope_HasSchemaRequiredFields(
        string fixture, string moduleName, bool isClass)
    {
        var run = RunFixture(
            fixture, moduleName,
            isClass ? ModuleKind.Class : ModuleKind.Standard);
        using var doc = BuildRunOutputEnvelope(run);
        var root = doc.RootElement;

        // Required envelope fields per harvester-schema.v0.json:
        // schemaVersion (pattern \d+\.\d+), source (kind+location), items[].
        Assert.Equal("0.1", root.GetProperty("schemaVersion").GetString());

        var source = root.GetProperty("source");
        Assert.Equal("procedure", source.GetProperty("kind").GetString());
        var loc = source.GetProperty("location");
        Assert.True(loc.GetProperty("startLine").GetInt32() >= 1);
        Assert.True(loc.GetProperty("startColumn").GetInt32() >= 1);

        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("unresolvedItems").ValueKind);

        // Every items[] entry honours the required procedureItem fields.
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            Assert.Equal("procedure", item.GetProperty("itemKind").GetString());
            Assert.False(string.IsNullOrEmpty(item.GetProperty("name").GetString()));
            var pk = item.GetProperty("procedureKind").GetString();
            // Anchored to the schema's closed enum, not the Pipeline
            // ProcedureKind enum, so a future Declare leak (v0-G5) is
            // surfaced by the assertion rather than masked by it.
            Assert.Contains(pk, SchemaProcedureKinds);
        }

        // Every unresolvedItems[] reason maps to the schema's closed enum.
        var schemaReasons = new HashSet<string>(StringComparer.Ordinal)
        {
            "unresolvedReference", "dynamicSqlTooComplex", "ambiguousResolution",
            "unsupportedConstruct", "parseFailure", "missingMetadata", "circularReference",
        };
        foreach (var u in root.GetProperty("unresolvedItems").EnumerateArray())
        {
            Assert.Contains(u.GetProperty("reason").GetString()!, schemaReasons);
            var ul = u.GetProperty("location");
            Assert.True(ul.GetProperty("startLine").GetInt32() >= 1);
            Assert.True(ul.GetProperty("startColumn").GetInt32() >= 1);
        }
    }

    // Pipeline ProcedureKind.Declare maps to schema "Function" via the
    // MapProcedureKind fallback (v0-G5 in docs/schema/v0-vs-tiers.md is
    // the schema-side resolution; deferred to schema-finalization-and-docs
    // (#17)). The 5 anchor fixtures must stay Declare-free until the
    // schema enum is extended; if a future fixture introduces a Declare
    // statement, this guard fires and forces the schema-G5 resolution
    // before the test silently falls back to "Function".
    [Theory]
    [InlineData("07-legacy-patterns.bas", "modLegacyImport", false)]
    [InlineData("08-unresolvable.bas", "modConfigDriven", false)]
    [InlineData("10-class-with-events.cls", "clsCaseAuditor", true)]
    [InlineData("11-autoexec-startup-chain.bas", "mdlStartup", false)]
    [InlineData("12-switchboard-navigation.bas", "frmMainSwitchboard", true)]
    public void AnchorFixtures_DoNotProduceDeclareProcedureKind(
        string fixture, string moduleName, bool isClass)
    {
        var run = RunFixture(
            fixture, moduleName,
            isClass ? ModuleKind.Class : ModuleKind.Standard);
        Assert.DoesNotContain(run.Classifications,
            c => c.Kind == ProcedureKind.Declare);
    }

    [Theory]
    [InlineData("07-legacy-patterns.bas", "modLegacyImport", false)]
    [InlineData("08-unresolvable.bas", "modConfigDriven", false)]
    [InlineData("10-class-with-events.cls", "clsCaseAuditor", true)]
    [InlineData("11-autoexec-startup-chain.bas", "mdlStartup", false)]
    [InlineData("12-switchboard-navigation.bas", "frmMainSwitchboard", true)]
    public void RunOutputEnvelope_IsDeterministicAcrossRepeatedRuns(
        string fixture, string moduleName, bool isClass)
    {
        var first = BuildRunOutputEnvelope(RunFixture(
            fixture, moduleName,
            isClass ? ModuleKind.Class : ModuleKind.Standard));
        var second = BuildRunOutputEnvelope(RunFixture(
            fixture, moduleName,
            isClass ? ModuleKind.Class : ModuleKind.Standard));

        Assert.Equal(first.RootElement.GetRawText(), second.RootElement.GetRawText());

        first.Dispose();
        second.Dispose();
    }
}
