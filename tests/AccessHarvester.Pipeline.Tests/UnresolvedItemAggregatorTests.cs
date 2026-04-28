using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Sql.Parser.DynamicSql;
using AccessHarvester.Sql.Parser.FormReferences;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.References;
using Xunit;
using FormRefReason = AccessHarvester.Sql.Parser.FormReferences.UnresolvedReason;
using SqlAst = AccessHarvester.Sql.Parser.Ast;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic tests for UnresolvedItemAggregator. Inputs are constructed
// directly (no parser pipeline) so the tests pin the projector
// behaviour for every reason code without depending on the upstream
// fixtures. The aggregator is the union point for every prior child
// set's unresolved provenance, so coverage targets one reason per
// emitter at minimum and the determinism guarantee top-level.
public sealed class UnresolvedItemAggregatorTests
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private static SourceProvenance Prov(int line = 5, int column = 3) =>
        new(line, column,
            new SourceRange(
                new SourceLocation("modX.bas", line, column),
                new SourceLocation("modX.bas", line, column + 4)));

    private static SourceRange Range(int line = 7, int column = 9) =>
        new(new SourceLocation("modSql.bas", line, column),
            new SourceLocation("modSql.bas", line, column + 6));

    private static StopStatement HostStmt(int line = 11, int column = 4) =>
        new(Prov(line, column));

    private static IdentifierExpression Ident(string name) =>
        new(Prov(), name, TypeSuffix: null);

    // Minimal FormReference with controllable Provenance so the
    // projector (which now reads line/column from Node.Provenance, not
    // from the SQL SourceRange) sees the test-asserted position.
    private static SqlAst.FormReference FakeFormReference(int line = 5, int column = 3)
    {
        var prov = new SqlAst.SourceProvenance(line, column);
        return new SqlAst.FormReference(
            prov,
            new SqlAst.FormsRoot(prov),
            new List<SqlAst.RefSegment>(),
            new List<SqlAst.RefAccessOperator>());
    }

    // ── Resolver projector ────────────────────────────────────────────

    [Fact]
    public void Resolver_warning_diagnostic_surfaces_as_item()
    {
        var d = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Warning,
            UnresolvedReasons.NotInScope,
            "Identifier 'FooBar' not in scope",
            Range(line: 12, column: 4));

        var items = UnresolvedItemAggregator
            .FromResolverDiagnostics(new[] { d }, moduleName: "modAlpha")
            .ToList();

        Assert.Single(items);
        Assert.Equal(UnresolvedSource.Resolver, items[0].Source);
        Assert.Equal(UnresolvedReasons.NotInScope, items[0].Reason);
        Assert.Equal("modAlpha", items[0].Location.ModuleName);
        Assert.Equal(12, items[0].Location.Line);
        Assert.Equal(4, items[0].Location.Column);
        Assert.Equal("modSql.bas", items[0].Location.File);
        Assert.NotEmpty(items[0].SuggestedNextSteps);
        Assert.Contains("severity", items[0].AdditionalContext!.Keys);
        Assert.Equal("Warning", items[0].AdditionalContext!["severity"]);
    }

    [Fact]
    public void Resolver_info_diagnostic_is_dropped()
    {
        var d = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Info,
            UnresolvedReasons.WithBlockFlatteningDeferred,
            "Deferred",
            Range());

        var items = UnresolvedItemAggregator
            .FromResolverDiagnostics(new[] { d }, moduleName: "modX")
            .ToList();

        Assert.Empty(items);
    }

    [Fact]
    public void Resolver_error_diagnostic_surfaces_as_item()
    {
        // Error severity is reserved for future use but the projector
        // surfaces it (only Info is the deferred-by-design class).
        var d = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Error,
            "future-error-code",
            "Future error",
            Range());

        var items = UnresolvedItemAggregator
            .FromResolverDiagnostics(new[] { d }, moduleName: "modX")
            .ToList();

        Assert.Single(items);
        Assert.Equal("Error", items[0].AdditionalContext!["severity"]);
    }

    [Theory]
    [InlineData(UnresolvedReasons.NotInScope)]
    [InlineData(UnresolvedReasons.MeInStandardModule)]
    [InlineData(UnresolvedReasons.DefaultMemberNotYetResolved)]
    [InlineData(UnresolvedReasons.WithBlockFlatteningDeferred)]
    [InlineData(UnresolvedReasons.MemberAccessOnUnresolvedTarget)]
    public void Every_resolver_reason_has_a_specific_description_and_steps(string reason)
    {
        var d = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Warning,
            reason, "msg", Range());

        var item = UnresolvedItemAggregator
            .FromResolverDiagnostics(new[] { d }, moduleName: "m")
            .Single();

        Assert.NotEqual(UnresolvedItemAggregator.GenericSuggestedNextSteps, item.SuggestedNextSteps);
        Assert.NotEqual($"Resolver diagnostic '{reason}'.", item.ConstructDescription);
    }

    // ── SQL form-reference projector ──────────────────────────────────

    [Fact]
    public void Resolved_form_reference_is_skipped()
    {
        // Build a resolved-only set; no UnresolvedFormReference entries
        // are present so the projector returns nothing.
        var anno = new FormReferenceAnnotation(
            Node: FakeFormReference(),
            Resolution: new ResolvedFormReference(
                OriginalText: "[Forms]![frmFoo]",
                Location: null,
                ResolvedTarget: null!,                       // OK — not consulted by the projector
                TargetKind: FormReferenceTargetKind.Form));

        var items = UnresolvedItemAggregator
            .FromFormReferenceAnnotations(new[] { anno }, "modSql", "ProcA")
            .ToList();

        Assert.Empty(items);
    }

    [Fact]
    public void Unresolved_form_reference_surfaces_with_original_text()
    {
        var anno = new FormReferenceAnnotation(
            Node: FakeFormReference(line: 14, column: 6),
            Resolution: new UnresolvedFormReference(
                OriginalText: "[Forms]![frmGhost]![txtBox]",
                Location: null,
                Reason: FormRefReason.FormNotInMetadata));

        var item = UnresolvedItemAggregator
            .FromFormReferenceAnnotations(new[] { anno }, "modSql", "ProcA")
            .Single();

        Assert.Equal(UnresolvedSource.SqlFormReference, item.Source);
        Assert.Equal(FormRefReason.FormNotInMetadata, item.Reason);
        Assert.Equal("[Forms]![frmGhost]![txtBox]", item.SurroundingText);
        Assert.Equal("modSql", item.Location.ModuleName);
        Assert.Equal("ProcA", item.Location.ProcedureName);
        Assert.Equal(14, item.Location.Line);
        Assert.Equal(6, item.Location.Column);
        Assert.NotEmpty(item.SuggestedNextSteps);
    }

    [Theory]
    [InlineData("bare-collection-reference")]
    [InlineData("form-not-in-metadata")]
    [InlineData("report-not-in-metadata")]
    [InlineData("control-not-on-form")]
    [InlineData("control-not-on-report")]
    [InlineData("host-collection-not-registered")]
    public void Every_form_reference_reason_has_a_specific_description(string reason)
    {
        var anno = new FormReferenceAnnotation(
            Node: FakeFormReference(),
            Resolution: new UnresolvedFormReference(
                OriginalText: "x", Location: null, Reason: reason));

        var item = UnresolvedItemAggregator
            .FromFormReferenceAnnotations(new[] { anno }, null, null)
            .Single();

        Assert.DoesNotContain("SQL form reference", item.ConstructDescription);
        Assert.NotEqual(UnresolvedItemAggregator.GenericSuggestedNextSteps, item.SuggestedNextSteps);
    }

    [Fact]
    public void Member_not_found_at_segment_falls_through_with_descriptive_phrase()
    {
        var reason = FormRefReason.MemberNotFoundAtSegment(2);
        var anno = new FormReferenceAnnotation(
            Node: FakeFormReference(),
            Resolution: new UnresolvedFormReference("x", null, reason));

        var item = UnresolvedItemAggregator
            .FromFormReferenceAnnotations(new[] { anno }, null, null)
            .Single();

        Assert.Contains("segment", item.ConstructDescription);
        // member-not-found-at-segment-N is not in the closed lookup, so
        // the generic next-steps fall-through is the documented signal.
        Assert.Equal(UnresolvedItemAggregator.GenericSuggestedNextSteps, item.SuggestedNextSteps);
    }

    // ── Dynamic SQL projector ─────────────────────────────────────────

    [Fact]
    public void Analyzable_dynamic_sql_site_is_skipped()
    {
        var site = new DynamicSqlExecutionSite(
            HostStatement: HostStmt(),
            CallExpression: Ident("Execute"),
            SqlArgumentExpression: Ident("sSql"),
            SymbolicValue: new SqlLiteral("SELECT 1"),
            Templates: new List<CandidateTemplate>(),
            FullCardinality: 1,
            IsAnalyzable: true,
            UnanalyzableReason: null);

        var result = new DynamicSqlAnalysisResult(new[] { site });

        var items = UnresolvedItemAggregator
            .FromDynamicSqlAnalysis(result, "modA", "ProcA")
            .ToList();

        Assert.Empty(items);
    }

    [Theory]
    [InlineData(UnanalyzableReason.CombinatorialBlowup)]
    [InlineData(UnanalyzableReason.UnboundVariable)]
    [InlineData(UnanalyzableReason.UnknownExpression)]
    public void Unanalyzable_dynamic_sql_site_surfaces(string reason)
    {
        var host = HostStmt(line: 22, column: 7);
        var site = new DynamicSqlExecutionSite(
            HostStatement: host,
            CallExpression: Ident("Execute"),
            SqlArgumentExpression: Ident("sSql"),
            SymbolicValue: new SqlUnknownHole(null, "x"),
            Templates: new List<CandidateTemplate>(),
            FullCardinality: 99,
            IsAnalyzable: false,
            UnanalyzableReason: reason);

        var item = UnresolvedItemAggregator
            .FromDynamicSqlAnalysis(
                new DynamicSqlAnalysisResult(new[] { site }), "modSql", "ProcSql")
            .Single();

        Assert.Equal(UnresolvedSource.DynamicSqlSite, item.Source);
        Assert.Equal(reason, item.Reason);
        Assert.Equal(22, item.Location.Line);
        Assert.Equal(7, item.Location.Column);
        Assert.Equal("99", item.AdditionalContext!["fullCardinality"]);
        Assert.NotEmpty(item.SuggestedNextSteps);
        Assert.NotEqual(UnresolvedItemAggregator.GenericSuggestedNextSteps, item.SuggestedNextSteps);
    }

    [Fact]
    public void Unanalyzable_site_with_null_reason_falls_back_to_unknown_expression()
    {
        var site = new DynamicSqlExecutionSite(
            HostStmt(), Ident("Execute"), Ident("sSql"),
            new SqlUnknownHole(null, "x"),
            new List<CandidateTemplate>(), FullCardinality: 0,
            IsAnalyzable: false, UnanalyzableReason: null);

        var item = UnresolvedItemAggregator
            .FromDynamicSqlAnalysis(
                new DynamicSqlAnalysisResult(new[] { site }), null, null)
            .Single();

        Assert.Equal(UnanalyzableReason.UnknownExpression, item.Reason);
    }

    // ── Navigation projector ──────────────────────────────────────────

    [Fact]
    public void Navigation_unresolved_target_surfaces_with_action_context()
    {
        var graph = new NavigationGraph
        {
            EntryPoints = new List<NavigationEntryPoint>(),
            Edges = new List<NavigationEdge>(),
            Macros = new List<MacroChain>(),
            UnresolvedTargets = new[]
            {
                new UnresolvedNavigationTarget
                {
                    SourceModule = "Form_frmMain",
                    SourceProcedure = "btnGo_Click",
                    SourceProcedureKind = ProcedureKind.Sub,
                    SourceLine = 33,
                    SourceColumn = 9,
                    Action = NavigationAction.OpenForm,
                    Reason = "non-literal-target",
                    TargetExpressionText = "sFormName & sSuffix",
                },
            },
        };

        var item = UnresolvedItemAggregator
            .FromNavigationGraph(graph)
            .Single();

        Assert.Equal(UnresolvedSource.NavigationTarget, item.Source);
        Assert.Equal("non-literal-target", item.Reason);
        Assert.Equal("Form_frmMain", item.Location.ModuleName);
        Assert.Equal("btnGo_Click", item.Location.ProcedureName);
        Assert.Equal(33, item.Location.Line);
        Assert.Equal("sFormName & sSuffix", item.SurroundingText);
        Assert.Equal("OpenForm", item.AdditionalContext!["action"]);
        Assert.Equal("Sub", item.AdditionalContext!["sourceProcedureKind"]);
        Assert.Contains("OpenForm", item.ConstructDescription);
    }

    [Fact]
    public void Navigation_missing_target_surfaces_with_specific_description()
    {
        var graph = new NavigationGraph
        {
            EntryPoints = new List<NavigationEntryPoint>(),
            Edges = new List<NavigationEdge>(),
            Macros = new List<MacroChain>(),
            UnresolvedTargets = new[]
            {
                new UnresolvedNavigationTarget
                {
                    SourceModule = "M",
                    SourceProcedure = "P",
                    SourceProcedureKind = ProcedureKind.Sub,
                    SourceLine = 1,
                    SourceColumn = 1,
                    Action = NavigationAction.OpenReport,
                    Reason = "missing-target",
                    TargetExpressionText = null,
                },
            },
        };

        var item = UnresolvedItemAggregator.FromNavigationGraph(graph).Single();
        Assert.Contains("OpenReport", item.ConstructDescription);
        Assert.Contains("no positional arguments", item.ConstructDescription);
    }

    // ── FunctionClassification projector ──────────────────────────────

    [Fact]
    public void Function_classification_unresolved_references_surface()
    {
        var fc = new FunctionClassification
        {
            ModuleName = "modBeta",
            ProcedureName = "DoStuff",
            Kind = ProcedureKind.Sub,
            IsPublic = true,
            IsStatic = false,
            ParameterCount = 0,
            HasReturnValue = false,
            Categories = new[] { ClassificationCategory.LateBound },
            PrimaryCategory = ClassificationCategory.LateBound,
            IsMixed = true,
            Metrics = new ComplexityMetrics
            {
                CyclomaticComplexity = 1, LinesOfCode = 3, StatementCount = 2,
            },
            DirectDependencies = new List<DependencyEdge>(),
            TransitiveDependencies = new List<DependencyEdge>(),
            Callers = new List<DependencyEdge>(),
            UnresolvedReferences = new[]
            {
                new UnresolvedReferenceFact
                {
                    Reason = UnresolvedReasons.MemberAccessOnUnresolvedTarget,
                    Line = 5, Column = 9,
                    MemberName = "OpenRecordset",
                },
            },
        };

        var item = UnresolvedItemAggregator
            .FromFunctionClassifications(new[] { fc })
            .Single();

        Assert.Equal(UnresolvedSource.FunctionClassification, item.Source);
        Assert.Equal(UnresolvedReasons.MemberAccessOnUnresolvedTarget, item.Reason);
        Assert.Equal("modBeta", item.Location.ModuleName);
        Assert.Equal("DoStuff", item.Location.ProcedureName);
        Assert.Equal("OpenRecordset", item.SurroundingText);
        Assert.Equal("OpenRecordset", item.AdditionalContext!["memberName"]);
    }

    [Fact]
    public void Function_classification_without_unresolved_references_emits_nothing()
    {
        var fc = SimpleFunctionClassification(unresolved: null);
        Assert.Empty(UnresolvedItemAggregator.FromFunctionClassifications(new[] { fc }));
    }

    [Fact]
    public void Function_classification_unresolved_with_null_member_name_omits_context()
    {
        var fc = SimpleFunctionClassification(unresolved: new[]
        {
            new UnresolvedReferenceFact
            {
                Reason = UnresolvedReasons.NotInScope,
                Line = 1, Column = 1, MemberName = null,
            },
        });
        var item = UnresolvedItemAggregator
            .FromFunctionClassifications(new[] { fc }).Single();
        Assert.Null(item.AdditionalContext);
        Assert.Null(item.SurroundingText);
    }

    // ── Aggregator top-level: determinism + sort ──────────────────────

    [Fact]
    public void Build_returns_empty_when_no_inputs_supplied()
    {
        Assert.Empty(UnresolvedItemAggregator.Build());
    }

    [Fact]
    public void Build_sorts_by_source_then_module_then_procedure_then_position()
    {
        // Mix items from three sources, with positions deliberately
        // unsorted on input. The aggregator must produce the same
        // sequence regardless of input order.
        var resolverDiag = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Warning,
            UnresolvedReasons.NotInScope, "msg", Range(line: 9, column: 1));

        var anno = new FormReferenceAnnotation(
            FakeFormReference(line: 4, column: 2),
            new UnresolvedFormReference("[Forms]![frmZ]", null,
                FormRefReason.FormNotInMetadata));

        var graph = new NavigationGraph
        {
            EntryPoints = new List<NavigationEntryPoint>(),
            Edges = new List<NavigationEdge>(),
            Macros = new List<MacroChain>(),
            UnresolvedTargets = new[]
            {
                new UnresolvedNavigationTarget
                {
                    SourceModule = "modZ", SourceProcedure = "P",
                    SourceProcedureKind = ProcedureKind.Sub,
                    SourceLine = 30, SourceColumn = 5,
                    Action = NavigationAction.OpenForm,
                    Reason = "non-literal-target", TargetExpressionText = "x",
                },
                new UnresolvedNavigationTarget
                {
                    SourceModule = "modA", SourceProcedure = "P",
                    SourceProcedureKind = ProcedureKind.Sub,
                    SourceLine = 10, SourceColumn = 5,
                    Action = NavigationAction.OpenForm,
                    Reason = "non-literal-target", TargetExpressionText = "y",
                },
            },
        };

        var items = UnresolvedItemAggregator.Build(
            resolverDiagnostics: new[] { resolverDiag },
            resolverModuleName: "modAlpha",
            formReferenceAnnotations: new[] { anno },
            sqlFormReferenceModuleName: "modSql",
            sqlFormReferenceProcedureName: "ProcSql",
            navigationGraph: graph);

        Assert.Equal(4, items.Count);
        // Resolver (0) before SqlFormReference (1) before NavigationTarget (3)
        Assert.Equal(UnresolvedSource.Resolver, items[0].Source);
        Assert.Equal(UnresolvedSource.SqlFormReference, items[1].Source);
        Assert.Equal(UnresolvedSource.NavigationTarget, items[2].Source);
        Assert.Equal(UnresolvedSource.NavigationTarget, items[3].Source);
        // modA before modZ within NavigationTarget bucket
        Assert.Equal("modA", items[2].Location.ModuleName);
        Assert.Equal("modZ", items[3].Location.ModuleName);
    }

    [Fact]
    public void Build_is_deterministic_across_input_permutations()
    {
        var d1 = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Warning,
            UnresolvedReasons.NotInScope, "a", Range(line: 5, column: 1));
        var d2 = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Warning,
            UnresolvedReasons.MemberAccessOnUnresolvedTarget, "b", Range(line: 5, column: 1));
        var d3 = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Warning,
            UnresolvedReasons.MeInStandardModule, "c", Range(line: 8, column: 4));

        var ordered = UnresolvedItemAggregator.Build(
            resolverDiagnostics: new[] { d1, d2, d3 },
            resolverModuleName: "m");
        var permuted = UnresolvedItemAggregator.Build(
            resolverDiagnostics: new[] { d3, d1, d2 },
            resolverModuleName: "m");
        var reversed = UnresolvedItemAggregator.Build(
            resolverDiagnostics: new[] { d3, d2, d1 },
            resolverModuleName: "m");

        Assert.Equal(ordered.Select(i => i.Reason), permuted.Select(i => i.Reason));
        Assert.Equal(ordered.Select(i => i.Reason), reversed.Select(i => i.Reason));
    }

    [Fact]
    public void Build_handles_multiple_dynamic_sql_results()
    {
        var siteA = new DynamicSqlExecutionSite(
            HostStmt(line: 5, column: 2), Ident("Execute"), Ident("sSql"),
            new SqlUnknownHole(null, "x"),
            new List<CandidateTemplate>(), 0, false, UnanalyzableReason.UnknownExpression);
        var siteB = new DynamicSqlExecutionSite(
            HostStmt(line: 12, column: 4), Ident("RunSQL"), Ident("sSql"),
            new SqlUnknownHole(null, "y"),
            new List<CandidateTemplate>(), 0, false, UnanalyzableReason.UnboundVariable);

        var items = UnresolvedItemAggregator.Build(
            dynamicSqlResults: new[]
            {
                new DynamicSqlAnalysisResult(new[] { siteA }),
                new DynamicSqlAnalysisResult(new[] { siteB }),
            },
            dynamicSqlModuleName: "modSql",
            dynamicSqlProcedureName: "ProcSql");

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(UnresolvedSource.DynamicSqlSite, i.Source));
        Assert.Equal(5, items[0].Location.Line);
        Assert.Equal(12, items[1].Location.Line);
    }

    [Fact]
    public void Unknown_reason_falls_through_to_generic_steps_and_description()
    {
        var d = new ResolverDiagnostic(
            ResolverDiagnosticSeverity.Warning,
            "made-up-reason-not-in-lookup", "the message", Range());

        var item = UnresolvedItemAggregator
            .FromResolverDiagnostics(new[] { d }, moduleName: "m").Single();

        Assert.Equal(UnresolvedItemAggregator.GenericSuggestedNextSteps, item.SuggestedNextSteps);
        // Falls through to the producer's message (not the synthetic
        // "Resolver diagnostic 'X'." since a message was supplied).
        Assert.Equal("the message", item.ConstructDescription);
    }

    [Fact]
    public void Suggested_next_steps_lookup_keys_match_known_reason_code_constants()
    {
        // Anchor every reason constant the upstream emitters publish to
        // a lookup entry, so a future addition that forgets the lookup
        // entry fails this test rather than silently emitting generic
        // hints.
        var expected = new[]
        {
            UnresolvedReasons.NotInScope,
            UnresolvedReasons.MeInStandardModule,
            UnresolvedReasons.DefaultMemberNotYetResolved,
            UnresolvedReasons.WithBlockFlatteningDeferred,
            UnresolvedReasons.MemberAccessOnUnresolvedTarget,
            FormRefReason.BareCollectionReference,
            FormRefReason.FormNotInMetadata,
            FormRefReason.ReportNotInMetadata,
            FormRefReason.ControlNotOnForm,
            FormRefReason.ControlNotOnReport,
            FormRefReason.HostCollectionNotRegistered,
            UnanalyzableReason.CombinatorialBlowup,
            UnanalyzableReason.UnboundVariable,
            UnanalyzableReason.UnknownExpression,
            "non-literal-target",
            "missing-target",
        };
        foreach (var key in expected)
        {
            Assert.True(UnresolvedItemAggregator.SuggestedNextStepsLookup.ContainsKey(key),
                $"Missing lookup entry for reason '{key}'");
        }
    }

    [Fact]
    public void Aggregator_sorts_additional_context_keys_for_stable_emission()
    {
        var graph = new NavigationGraph
        {
            EntryPoints = new List<NavigationEntryPoint>(),
            Edges = new List<NavigationEdge>(),
            Macros = new List<MacroChain>(),
            UnresolvedTargets = new[]
            {
                new UnresolvedNavigationTarget
                {
                    SourceModule = "M", SourceProcedure = "P",
                    SourceProcedureKind = ProcedureKind.Sub,
                    SourceLine = 1, SourceColumn = 1,
                    Action = NavigationAction.OpenForm,
                    Reason = "non-literal-target",
                    TargetExpressionText = "x",
                },
            },
        };

        var item = UnresolvedItemAggregator.FromNavigationGraph(graph).Single();
        // Keys are emitted in ordinal-sort order: action, sourceProcedureKind.
        var keys = item.AdditionalContext!.Keys.ToList();
        Assert.Equal(new[] { "action", "sourceProcedureKind" }, keys);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static FunctionClassification SimpleFunctionClassification(
        IReadOnlyList<UnresolvedReferenceFact>? unresolved)
        => new()
        {
            ModuleName = "modX",
            ProcedureName = "P",
            Kind = ProcedureKind.Sub,
            IsPublic = true,
            IsStatic = false,
            ParameterCount = 0,
            HasReturnValue = false,
            Categories = new[] { ClassificationCategory.Pure },
            PrimaryCategory = ClassificationCategory.Pure,
            IsMixed = false,
            Metrics = new ComplexityMetrics
            { CyclomaticComplexity = 1, LinesOfCode = 0, StatementCount = 0 },
            DirectDependencies = new List<DependencyEdge>(),
            TransitiveDependencies = new List<DependencyEdge>(),
            Callers = new List<DependencyEdge>(),
            UnresolvedReferences = unresolved,
        };
}
