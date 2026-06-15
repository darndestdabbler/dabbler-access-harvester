using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Sql.Parser.DynamicSql;
using AccessHarvester.Sql.Parser.FormReferences;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.References;
using FormReferenceUnresolvedReason = AccessHarvester.Sql.Parser.FormReferences.UnresolvedReason;
using SqlProvenance = AccessHarvester.Sql.Parser.Ast.Provenance;
using VbaSourceRange = AccessHarvester.Vba.Parser.Ast.SourceRange;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Aggregates per-subsystem unresolved-construct emissions into the
/// run-wide <c>UnresolvedItems</c> array per Phase 6.8 of
/// <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>. The aggregator
/// is the union point for every prior child set's unresolved
/// provenance: callers feed it the resolver diagnostics, SQL
/// form-reference annotations, dynamic-SQL analysis result(s),
/// navigation graph, and function classifications a single harvester
/// run produced, and it returns one deterministically-sorted
/// <see cref="UnresolvedItem"/> sequence ready for serialisation.
/// </summary>
/// <remarks>
/// <para>
/// Determinism is non-negotiable: two runs over the same inputs must
/// produce byte-identical output (per
/// <c>docs/planning/project-guidance.md</c> -> VBA Parser Strategy).
/// To enforce that, every <c>From*</c> projector returns items in
/// source order, and <see cref="Build"/> sorts the combined sequence
/// by (<see cref="UnresolvedSource"/>, module, procedure, line, column,
/// reason) before returning. The sort is case-insensitive on string
/// keys to match the rest of the pipeline.
/// </para>
/// <para>
/// Reason-to-suggestion lookup is closed: every reason code the
/// upstream subsystems emit has a matching entry in
/// <see cref="SuggestedNextStepsLookup"/>. Unrecognised codes fall
/// through to a generic two-step suggestion ("review the source",
/// "decide whether to extend the producer's reason set") so the
/// aggregator never returns an empty hint list, but the fall-through
/// is observable via <see cref="UnresolvedItem.SuggestedNextSteps"/>
/// containing the generic text — that is the signal a new producer
/// reason needs to be wired in.
/// </para>
/// </remarks>
public static class UnresolvedItemAggregator
{
    // ── Public surface ────────────────────────────────────────────────

    /// <summary>
    /// Combines the per-subsystem inputs into a sorted, deduplicated
    /// <see cref="UnresolvedItem"/> sequence. Any input may be null or
    /// empty; the aggregator skips it. Returns an empty list when no
    /// input contributes any item.
    /// </summary>
    public static IReadOnlyList<UnresolvedItem> Build(
        IEnumerable<ResolverDiagnostic>? resolverDiagnostics = null,
        string? resolverModuleName = null,
        IEnumerable<FormReferenceAnnotation>? formReferenceAnnotations = null,
        string? sqlFormReferenceModuleName = null,
        string? sqlFormReferenceProcedureName = null,
        IEnumerable<DynamicSqlAnalysisResult>? dynamicSqlResults = null,
        string? dynamicSqlModuleName = null,
        string? dynamicSqlProcedureName = null,
        NavigationGraph? navigationGraph = null,
        IEnumerable<FunctionClassification>? functionClassifications = null,
        IEnumerable<ProcedureScopedReference>? procedureScopedReferences = null,
        IEnumerable<ModuleLevelReference>? moduleLevelReferences = null)
    {
        var items = new List<UnresolvedItem>();

        if (resolverDiagnostics is not null)
            items.AddRange(FromResolverDiagnostics(resolverDiagnostics, resolverModuleName));

        if (formReferenceAnnotations is not null)
            items.AddRange(FromFormReferenceAnnotations(
                formReferenceAnnotations,
                sqlFormReferenceModuleName,
                sqlFormReferenceProcedureName));

        if (dynamicSqlResults is not null)
        {
            foreach (var result in dynamicSqlResults)
                items.AddRange(FromDynamicSqlAnalysis(
                    result, dynamicSqlModuleName, dynamicSqlProcedureName));
        }

        if (navigationGraph is not null)
            items.AddRange(FromNavigationGraph(navigationGraph));

        if (functionClassifications is not null)
            items.AddRange(FromFunctionClassifications(functionClassifications));

        // DW-11 Layer A (dedup-additive feed): the FunctionClassification
        // feed above is procedure-BODY-scoped — FunctionClassificationBuilder
        // walks `procedureAst.Body` only — so an unresolved reference in a
        // procedure HEADER (an Optional parameter's default expression) never
        // reaches the index even though ParserOutputProjector groups it into
        // Body.References. Feed the full procedure-scoped reference set (body
        // + header) and suppress any reference the body-only feed already
        // emitted, matched on the shared (module, procedure, line, column,
        // reason) key, so no reference is double-counted. The only net-new
        // items are the header refs, which makes the index a structural
        // superset of the inline Body.References[Unresolved] set.
        if (procedureScopedReferences is not null)
        {
            // Suppress ONLY references the body-only feed already emitted —
            // never dedup the procedure-scoped feed against itself. The key
            // set is built once from the FunctionClassification items and is
            // NOT mutated as we iterate: two legitimately-distinct unresolved
            // refs that happen to share a key (e.g. the deeper links of a
            // chained member access `Foo.Bar` / `Foo.Bar.Baz`, whose
            // member-access provenance anchors at the chain start) must BOTH
            // survive, or the index would undercount the inline references it
            // is meant to be a superset of. (gpt-5-4 R1, 015 S1.)
            var bodyKeys = new HashSet<(string, string, int, int, string, string)>();
            foreach (var item in items)
                if (item.Source == UnresolvedSource.FunctionClassification)
                    bodyKeys.Add(DedupKey(item));

            foreach (var item in FromProcedureScopedReferences(procedureScopedReferences))
                if (!bodyKeys.Contains(DedupKey(item)))
                    items.Add(item);
        }

        // DW-11 Layer B (015 S2): module-level unresolved references — those on
        // ResolvedModule.References with NO enclosing ProcedureScope (a Const/Dim
        // initializer, an `Implements <unresolved>`, etc.) — are dropped by the
        // projector (no procedure Body to surface in) and so reach NEITHER
        // Body.References NOR the procedure-scoped feeds above. Surface them here
        // under UnresolvedSource.ModuleLevelReference at module scope
        // (ProcedureName=null). No dedup is needed against the procedure-scoped
        // feeds: a module-level reference has no enclosing ProcedureScope, so it
        // is disjoint from both procedure-scoped feeds by construction (those
        // only ever carry references WITH a ProcedureScope).
        if (moduleLevelReferences is not null)
            items.AddRange(FromModuleLevelReferences(moduleLevelReferences));

        return SortDeterministically(items);
    }

    // The dedup key the procedure-scoped feed shares with the
    // FunctionClassification feed. Both feeds source Line/Column from the
    // SAME AST node's Provenance, the same (post-with-flattening) Unresolved
    // reason, and the same member/identifier name for the same reference, so
    // a body ref carried by both produces an identical key and is suppressed;
    // a header ref (absent from the body-only feed) has no match and survives.
    // The member name (SurroundingText) is part of the key because position +
    // reason alone do NOT uniquely identify a reference: a chained member
    // access emits several Unresolved links that share a start position and
    // reason but differ by member name (`Bar` vs `Baz`) — without the name
    // they would collide and one distinct reference would be lost.
    private static (string, string, int, int, string, string) DedupKey(UnresolvedItem item) =>
        (item.Location.ModuleName ?? string.Empty,
         item.Location.ProcedureName ?? string.Empty,
         item.Location.Line ?? 0,
         item.Location.Column ?? 0,
         item.Reason,
         item.SurroundingText ?? string.Empty);

    // ── Per-subsystem projectors ──────────────────────────────────────

    /// <summary>
    /// Projects resolver diagnostics into <see cref="UnresolvedItem"/>
    /// records. Only Warning-severity entries surface; Info entries are
    /// deferred-by-design and Error entries (reserved for future use)
    /// also pass through as items so the consumer sees them.
    /// </summary>
    public static IEnumerable<UnresolvedItem> FromResolverDiagnostics(
        IEnumerable<ResolverDiagnostic> diagnostics,
        string? moduleName)
    {
        if (diagnostics is null) throw new ArgumentNullException(nameof(diagnostics));

        foreach (var d in diagnostics)
        {
            if (d.Severity == ResolverDiagnosticSeverity.Info)
                continue;

            var (line, column, file) = ExtractLocation(d.Location);

            yield return new UnresolvedItem
            {
                Source = UnresolvedSource.Resolver,
                Reason = d.Code,
                ConstructDescription = DescribeResolverReason(d.Code, d.Message),
                Location = new UnresolvedLocation
                {
                    File = file,
                    ModuleName = moduleName,
                    ProcedureName = null,
                    Line = line,
                    Column = column,
                },
                SurroundingText = null,
                SuggestedNextSteps = LookupSuggestedNextSteps(d.Code),
                AdditionalContext = SortContext(new Dictionary<string, string>
                {
                    ["severity"] = d.Severity.ToString(),
                    ["message"] = d.Message,
                }),
            };
        }
    }

    /// <summary>
    /// Projects SQL form-reference annotations into
    /// <see cref="UnresolvedItem"/> records, skipping resolved ones.
    /// </summary>
    public static IEnumerable<UnresolvedItem> FromFormReferenceAnnotations(
        IEnumerable<FormReferenceAnnotation> annotations,
        string? moduleName,
        string? procedureName)
    {
        if (annotations is null) throw new ArgumentNullException(nameof(annotations));

        foreach (var anno in annotations)
        {
            if (anno.Resolution is not UnresolvedFormReference unresolved)
                continue;

            // SQL FormReference SourceRange carries character offsets,
            // not line/column — pull line/column from the Node's
            // Provenance instead. SQL provenance has no File slot.
            int? line = anno.Node.Provenance.Line;
            int? column = anno.Node.Provenance.Column;

            yield return new UnresolvedItem
            {
                Source = UnresolvedSource.SqlFormReference,
                Reason = unresolved.Reason,
                ConstructDescription = DescribeFormReferenceReason(unresolved.Reason),
                Location = new UnresolvedLocation
                {
                    File = null,
                    ModuleName = moduleName,
                    ProcedureName = procedureName,
                    Line = line,
                    Column = column,
                },
                SurroundingText = unresolved.OriginalText,
                SuggestedNextSteps = LookupSuggestedNextSteps(unresolved.Reason),
                AdditionalContext = null,
            };
        }
    }

    /// <summary>
    /// Projects an unanalyzable dynamic-SQL execution site into an
    /// <see cref="UnresolvedItem"/>. Sites that flatten to a usable
    /// template (<see cref="DynamicSqlExecutionSite.IsAnalyzable"/> ==
    /// true) do not surface as manual-review items — only their
    /// individual unresolved bindings would, and those are already
    /// captured via the form-reference projector when present.
    /// </summary>
    public static IEnumerable<UnresolvedItem> FromDynamicSqlAnalysis(
        DynamicSqlAnalysisResult result,
        string? moduleName,
        string? procedureName)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));

        foreach (var site in result.ExecutionSites)
        {
            if (site.IsAnalyzable)
                continue;

            var reason = site.UnanalyzableReason ?? UnanalyzableReason.UnknownExpression;
            var (line, column, file) = ExtractLocation(site.HostStatement.Provenance);

            var ctx = new Dictionary<string, string>
            {
                ["fullCardinality"] = site.FullCardinality.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["templateCount"] = site.Templates.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

            yield return new UnresolvedItem
            {
                Source = UnresolvedSource.DynamicSqlSite,
                Reason = reason,
                ConstructDescription = DescribeDynamicSqlReason(reason),
                Location = new UnresolvedLocation
                {
                    File = file,
                    ModuleName = moduleName,
                    ProcedureName = procedureName,
                    Line = line,
                    Column = column,
                },
                SurroundingText = null,
                SuggestedNextSteps = LookupSuggestedNextSteps(reason),
                AdditionalContext = SortContext(ctx),
            };
        }
    }

    /// <summary>
    /// Projects the navigation graph's
    /// <see cref="NavigationGraph.UnresolvedTargets"/> into
    /// <see cref="UnresolvedItem"/> records.
    /// </summary>
    public static IEnumerable<UnresolvedItem> FromNavigationGraph(NavigationGraph graph)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));

        foreach (var target in graph.UnresolvedTargets)
        {
            var ctx = new Dictionary<string, string>
            {
                ["action"] = target.Action.ToString(),
                ["sourceProcedureKind"] = target.SourceProcedureKind.ToString(),
            };

            yield return new UnresolvedItem
            {
                Source = UnresolvedSource.NavigationTarget,
                Reason = target.Reason,
                ConstructDescription = DescribeNavigationReason(target.Reason, target.Action),
                Location = new UnresolvedLocation
                {
                    File = null,
                    ModuleName = target.SourceModule,
                    ProcedureName = target.SourceProcedure,
                    Line = target.SourceLine,
                    Column = target.SourceColumn,
                },
                SurroundingText = target.TargetExpressionText,
                SuggestedNextSteps = LookupSuggestedNextSteps(target.Reason),
                AdditionalContext = SortContext(ctx),
            };
        }
    }

    /// <summary>
    /// Projects per-procedure
    /// <see cref="FunctionClassification.UnresolvedReferences"/> into
    /// <see cref="UnresolvedItem"/> records. Classifications without
    /// unresolved references contribute nothing.
    /// </summary>
    public static IEnumerable<UnresolvedItem> FromFunctionClassifications(
        IEnumerable<FunctionClassification> classifications)
    {
        if (classifications is null) throw new ArgumentNullException(nameof(classifications));

        foreach (var fc in classifications)
        {
            if (fc.UnresolvedReferences is null) continue;

            foreach (var fact in fc.UnresolvedReferences)
            {
                var ctx = fact.MemberName is null
                    ? null
                    : SortContext(new Dictionary<string, string>
                    {
                        ["memberName"] = fact.MemberName,
                    });

                yield return new UnresolvedItem
                {
                    Source = UnresolvedSource.FunctionClassification,
                    Reason = fact.Reason,
                    ConstructDescription = DescribeResolverReason(fact.Reason, message: null),
                    Location = new UnresolvedLocation
                    {
                        File = null,
                        ModuleName = fc.ModuleName,
                        ProcedureName = fc.ProcedureName,
                        Line = fact.Line,
                        Column = fact.Column,
                    },
                    SurroundingText = fact.MemberName,
                    SuggestedNextSteps = LookupSuggestedNextSteps(fact.Reason),
                    AdditionalContext = ctx,
                };
            }
        }
    }

    /// <summary>
    /// Projects procedure-scoped resolver references (body AND header) into
    /// <see cref="UnresolvedItem"/> records, surfacing only those whose
    /// outcome is (after with-flattening) <c>Unresolved</c>. DW-11 Layer A:
    /// this is the additive feed that carries procedure-HEADER unresolved
    /// refs the body-only <see cref="FromFunctionClassifications"/> feed
    /// drops. The emitted shape mirrors <see cref="FromFunctionClassifications"/>
    /// exactly (same <see cref="UnresolvedSource.FunctionClassification"/>
    /// source, description, member-name surrounding text), so a header ref
    /// is indistinguishable in the index from a body ref — both are
    /// procedure-scoped VBA references — and a body ref carried by both
    /// feeds collapses under <see cref="Build"/>'s dedup.
    /// </summary>
    public static IEnumerable<UnresolvedItem> FromProcedureScopedReferences(
        IEnumerable<ProcedureScopedReference> references)
    {
        if (references is null) throw new ArgumentNullException(nameof(references));

        foreach (var psr in references)
        {
            var reason = UnresolvedReasonOf(psr.Reference.Outcome);
            if (reason is null) continue;   // resolved references do not surface

            var expr = psr.Reference.Expression;
            var memberName = ExtractReferenceName(expr);
            var ctx = memberName is null
                ? null
                : SortContext(new Dictionary<string, string>
                {
                    ["memberName"] = memberName,
                });

            yield return new UnresolvedItem
            {
                Source = UnresolvedSource.FunctionClassification,
                Reason = reason,
                ConstructDescription = DescribeResolverReason(reason, message: null),
                Location = new UnresolvedLocation
                {
                    File = null,
                    ModuleName = psr.ModuleName,
                    ProcedureName = psr.ProcedureName,
                    Line = expr.Provenance.Line,
                    Column = expr.Provenance.Column,
                },
                SurroundingText = memberName,
                SuggestedNextSteps = LookupSuggestedNextSteps(reason),
                AdditionalContext = ctx,
            };
        }
    }

    /// <summary>
    /// Projects MODULE-LEVEL resolver references (those with no enclosing
    /// procedure) into <see cref="UnresolvedItem"/> records, surfacing only
    /// those whose outcome is (after with-flattening) <c>Unresolved</c>.
    /// DW-11 Layer B: this is the feed that gives a module-level unresolved
    /// reference a manifest home in <c>unresolved-items.json</c>. The emitted
    /// shape mirrors <see cref="FromProcedureScopedReferences"/> (same Reason /
    /// description / member-name surrounding text) EXCEPT the source is
    /// <see cref="UnresolvedSource.ModuleLevelReference"/> and
    /// <see cref="UnresolvedLocation.ProcedureName"/> is null — the reference
    /// has no enclosing procedure. Built by the caller from
    /// <see cref="AccessHarvester.Pipeline.Manifest.ParserOutputProjector.EnumerateModuleLevelReferences"/>.
    /// </summary>
    public static IEnumerable<UnresolvedItem> FromModuleLevelReferences(
        IEnumerable<ModuleLevelReference> references)
    {
        if (references is null) throw new ArgumentNullException(nameof(references));

        foreach (var mlr in references)
        {
            var reason = UnresolvedReasonOf(mlr.Reference.Outcome);
            if (reason is null) continue;   // resolved references do not surface

            var expr = mlr.Reference.Expression;
            var memberName = ExtractReferenceName(expr);
            var ctx = memberName is null
                ? null
                : SortContext(new Dictionary<string, string>
                {
                    ["memberName"] = memberName,
                });

            yield return new UnresolvedItem
            {
                Source = UnresolvedSource.ModuleLevelReference,
                Reason = reason,
                ConstructDescription = DescribeResolverReason(reason, message: null),
                Location = new UnresolvedLocation
                {
                    File = null,
                    ModuleName = mlr.ModuleName,
                    ProcedureName = null,
                    Line = expr.Provenance.Line,
                    Column = expr.Provenance.Column,
                },
                SurroundingText = memberName,
                SuggestedNextSteps = LookupSuggestedNextSteps(reason),
                AdditionalContext = ctx,
            };
        }
    }

    // Returns the (post-with-flattening) Unresolved reason, or null when the
    // reference resolved. Mirrors FunctionClassificationBuilder.DispatchOutcome's
    // flattening so the two feeds agree on the reason for the SAME reference —
    // which is what lets the dedup key collapse every body ref the
    // FunctionClassification feed already carries.
    private static string? UnresolvedReasonOf(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Unresolved u => u.Reason,
        ResolutionOutcome.ResolvedViaWithFlattening f => UnresolvedReasonOf(f.InnerOutcome),
        _ => null,
    };

    // Mirror of FunctionClassificationBuilder.ClassificationVisitor.ExtractName
    // so a header ref's SurroundingText / memberName matches the body-ref shape
    // byte-for-byte.
    private static string? ExtractReferenceName(Expression e) => e switch
    {
        IdentifierExpression i => i.Name,
        MemberAccessExpression m => m.MemberName,
        BangAccessExpression b => b.MemberName,
        ImplicitWithMemberExpression w => w.MemberName,
        ImplicitWithBangExpression w => w.MemberName,
        _ => null,
    };

    // ── Sorting / lookup helpers ─────────────────────────────────────

    // internal (not private) so UnresolvedItemAggregatorTests can pin the
    // total-order property directly with hand-built items (the public feeds
    // derive most fields from the key components, so a same-key/different-
    // residual tie is hard to force end-to-end — see the determinism pin).
    internal static IReadOnlyList<UnresolvedItem> SortDeterministically(List<UnresolvedItem> items)
    {
        // Precompute a canonical RESIDUAL key per item ONCE (covering every
        // field NOT in the semantic comparison below), then sort. The semantic
        // comparisons give a human-readable primary order; the residual is the
        // final tiebreaker that makes the comparer a PROVABLY TOTAL order over
        // the whole UnresolvedItem — two items that compare equal here are
        // byte-identical, so List.Sort's non-stable introsort can never produce
        // a different ordering for the same input SET regardless of incoming
        // order or list size. (gpt-5-4 R1, 015 S2: SurroundingText alone left
        // the key non-total — e.g. two Resolver diagnostics at one location
        // with SurroundingText=null but different messages would still tie.)
        var keyed = new List<(UnresolvedItem Item, string Residual)>(items.Count);
        foreach (var it in items)
            keyed.Add((it, CanonicalResidual(it)));

        keyed.Sort(static (a, b) =>
        {
            var (ia, ib) = (a.Item, b.Item);
            int c = ((int)ia.Source).CompareTo((int)ib.Source);
            if (c != 0) return c;
            c = string.Compare(ia.Location.ModuleName ?? string.Empty,
                               ib.Location.ModuleName ?? string.Empty,
                               StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            c = string.Compare(ia.Location.ProcedureName ?? string.Empty,
                               ib.Location.ProcedureName ?? string.Empty,
                               StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            c = (ia.Location.Line ?? 0).CompareTo(ib.Location.Line ?? 0);
            if (c != 0) return c;
            c = (ia.Location.Column ?? 0).CompareTo(ib.Location.Column ?? 0);
            if (c != 0) return c;
            c = string.Compare(ia.Reason, ib.Reason, StringComparison.Ordinal);
            if (c != 0) return c;
            // SurroundingText (member/identifier name): the common
            // distinguisher for chained member-access links that share a start
            // position and reason (e.g. `.Fields` vs `.Value`).
            c = string.Compare(ia.SurroundingText ?? string.Empty,
                               ib.SurroundingText ?? string.Empty,
                               StringComparison.Ordinal);
            if (c != 0) return c;
            // Final total-order tiebreaker: the canonical residual over every
            // remaining field (File, ConstructDescription, SuggestedNextSteps,
            // AdditionalContext). Equal residual == byte-identical item.
            return string.Compare(a.Residual, b.Residual, StringComparison.Ordinal);
        });

        var result = new List<UnresolvedItem>(keyed.Count);
        foreach (var k in keyed) result.Add(k.Item);
        return result;
    }

    // Options for the canonical-residual serialization (below). A plain,
    // culture-invariant, compact serialization; deterministic because the only
    // collection member, AdditionalContext, is a SortedDictionary (key order is
    // stable) and UnresolvedSource serializes as a string via its type-level
    // [JsonStringEnumConverter].
    private static readonly System.Text.Json.JsonSerializerOptions ResidualJsonOptions =
        new() { WriteIndented = false };

    // The final, PROVABLY-TOTAL tiebreaker for SortDeterministically: an
    // INJECTIVE canonical serialization of the WHOLE item. The semantic
    // comparisons above give a human-readable primary order but are lossy by
    // design — module/procedure compared OrdinalIgnoreCase, null Line/Column
    // and null SurroundingText folded to 0/"" — so two items differing ONLY in
    // a folded distinction (module-name case, null-vs-empty, null-vs-0) compare
    // equal there. Serializing the entire item closes every such class at once:
    // two items with identical JSON are byte-identical in the OUTPUT (so their
    // order is immaterial), and any real difference — including the folded ones
    // and any AdditionalContext key/value content (System.Text.Json quotes and
    // escapes each separately, so there is no `key=value` delimiter ambiguity)
    // — yields a distinct, deterministic residual. (gpt-5-4 R1+R2, 015 S2.)
    private static string CanonicalResidual(UnresolvedItem item) =>
        System.Text.Json.JsonSerializer.Serialize(item, ResidualJsonOptions);

    private static IReadOnlyDictionary<string, string>? SortContext(IDictionary<string, string> ctx)
    {
        if (ctx.Count == 0) return null;
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in ctx) sorted[kv.Key] = kv.Value;
        return sorted;
    }

    private static (int? line, int? column, string? file) ExtractLocation(VbaSourceRange? range)
    {
        if (range is null) return (null, null, null);
        return (range.Start.Line, range.Start.Column, NullIfEmpty(range.Start.File));
    }

    private static (int? line, int? column, string? file) ExtractLocation(Provenance? prov)
    {
        if (prov is null) return (null, null, null);
        if (prov.Range is { } r) return (r.Start.Line, r.Start.Column, NullIfEmpty(r.Start.File));
        return (prov.Line, prov.Column, null);
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

    // ── Reason → description / next-steps lookups ────────────────────

    private static string DescribeResolverReason(string reason, string? message) => reason switch
    {
        UnresolvedReasons.NotInScope =>
            "Bare identifier reference whose declaration was not in scope.",
        UnresolvedReasons.MeInStandardModule =>
            "`Me` reference used inside a standard module, which has no class context.",
        UnresolvedReasons.DefaultMemberNotYetResolved =>
            "Member access whose resolution depends on default-member or type-inference passes that have not yet run.",
        UnresolvedReasons.WithBlockFlatteningDeferred =>
            "Implicit-with member reference outside any visible With block (resolver fallback).",
        UnresolvedReasons.MemberAccessOnUnresolvedTarget =>
            "Member access whose target itself failed to resolve.",
        _ => message is { Length: > 0 } ? message : $"Resolver diagnostic '{reason}'.",
    };

    private static string DescribeFormReferenceReason(string reason) => reason switch
    {
        FormReferenceUnresolvedReason.BareCollectionReference =>
            "Bare `Forms` or `Reports` collection reference (no navigation target).",
        FormReferenceUnresolvedReason.FormNotInMetadata =>
            "Form reference whose first segment did not match any form in the harvested metadata.",
        FormReferenceUnresolvedReason.ReportNotInMetadata =>
            "Report reference whose first segment did not match any report in the harvested metadata.",
        FormReferenceUnresolvedReason.ControlNotOnForm =>
            "Form reference whose control name did not match any control on the resolved form.",
        FormReferenceUnresolvedReason.ControlNotOnReport =>
            "Report reference whose control name did not match any control on the resolved report.",
        FormReferenceUnresolvedReason.HostCollectionNotRegistered =>
            "Form/Report reference resolved against the null Access object-model registry.",
        _ when reason.StartsWith("member-not-found-at-segment-", StringComparison.Ordinal) =>
            $"Form/Report property chain broke at a deeper segment ({reason}).",
        _ => $"SQL form reference '{reason}'.",
    };

    private static string DescribeDynamicSqlReason(string reason) => reason switch
    {
        UnanalyzableReason.CombinatorialBlowup =>
            "Dynamic SQL execution site whose branch cross-product exceeded the analyzer's enumeration cap.",
        UnanalyzableReason.UnboundVariable =>
            "Dynamic SQL execution site whose SQL string was a never-assigned local variable.",
        UnanalyzableReason.UnknownExpression =>
            "Dynamic SQL execution site whose SQL argument did not yield any literal content.",
        _ => $"Dynamic SQL execution site '{reason}'.",
    };

    private static string DescribeNavigationReason(string reason, NavigationAction action) => reason switch
    {
        "non-literal-target" =>
            $"`DoCmd.{action}` dispatch whose target was a non-literal expression.",
        "missing-target" =>
            $"`DoCmd.{action}` dispatch with no positional arguments.",
        _ => $"Navigation dispatch '{reason}'.",
    };

    private static IReadOnlyList<string> LookupSuggestedNextSteps(string reason)
    {
        return SuggestedNextStepsLookup.TryGetValue(reason, out var steps)
            ? steps
            : GenericSuggestedNextSteps;
    }

    /// <summary>
    /// Closed reason -> next-step lookup. Suggestions are mechanical
    /// hints about what kind of follow-up the construct typically
    /// needs; consumers may render them verbatim. Two items with the
    /// same reason carry the same hint list so the output is stable.
    /// </summary>
    /// <remarks>
    /// New producer reasons must be added here at the same time they
    /// are added upstream. Reasons not in the lookup fall through to
    /// <see cref="GenericSuggestedNextSteps"/>; the generic text is
    /// the in-output signal that wiring is missing.
    /// </remarks>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> SuggestedNextStepsLookup { get; } =
        new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            // Resolver reason codes
            [UnresolvedReasons.NotInScope] = new[]
            {
                "Confirm whether the identifier is declared in another module's Public scope.",
                "If the identifier is intentional dead code, mark it as such.",
            },
            [UnresolvedReasons.MeInStandardModule] = new[]
            {
                "Move the `Me` reference into a class / form / report module, or rewrite to name the target explicitly.",
            },
            [UnresolvedReasons.DefaultMemberNotYetResolved] = new[]
            {
                "Re-run after the default-member resolution pass has executed; if still unresolved, supply a type annotation for the receiver.",
            },
            [UnresolvedReasons.WithBlockFlatteningDeferred] = new[]
            {
                "Confirm the implicit-with member is reachable from a containing With block; if not, rewrite to name the receiver explicitly.",
            },
            [UnresolvedReasons.MemberAccessOnUnresolvedTarget] = new[]
            {
                "Resolve the underlying target reference first; the member access cannot be bound until then.",
            },

            // SQL form-reference reason codes
            [FormReferenceUnresolvedReason.BareCollectionReference] = new[]
            {
                "Replace the bare collection reference with a specific form/report navigation target.",
            },
            [FormReferenceUnresolvedReason.FormNotInMetadata] = new[]
            {
                "Confirm the form was harvested into the metadata snapshot; if it loads dynamically at runtime, register it manually.",
            },
            [FormReferenceUnresolvedReason.ReportNotInMetadata] = new[]
            {
                "Confirm the report was harvested into the metadata snapshot; if it loads dynamically at runtime, register it manually.",
            },
            [FormReferenceUnresolvedReason.ControlNotOnForm] = new[]
            {
                "Confirm the control name; if it is created at runtime, surface the synthetic-control declaration explicitly.",
            },
            [FormReferenceUnresolvedReason.ControlNotOnReport] = new[]
            {
                "Confirm the control name; if it is created at runtime, surface the synthetic-control declaration explicitly.",
            },
            [FormReferenceUnresolvedReason.HostCollectionNotRegistered] = new[]
            {
                "Re-run with a non-null IAccessObjectModelRegistry so the Forms/Reports collections are registered.",
            },

            // Dynamic SQL reason codes
            [UnanalyzableReason.CombinatorialBlowup] = new[]
            {
                "Inspect the representative templates the analyzer emitted and decide whether to raise the enumeration cap or rewrite the SQL builder to flatten the branch set.",
            },
            [UnanalyzableReason.UnboundVariable] = new[]
            {
                "Trace the SQL variable's assignment path; the analyzer never observed it being set before the execution site.",
            },
            [UnanalyzableReason.UnknownExpression] = new[]
            {
                "Inspect the SQL argument's surface text and decide whether to extend the analyzer with a builtin recogniser (e.g., Replace, IIf) or to rewrite the call site.",
            },

            // Navigation reason codes (closed set on UnresolvedNavigationTarget.Reason)
            ["non-literal-target"] = new[]
            {
                "Trace the target expression to its source; if it resolves at runtime, replace with a literal name or register the dynamic target explicitly.",
            },
            ["missing-target"] = new[]
            {
                "Inspect the dispatch site — `DoCmd` calls without a target argument almost always indicate a partial edit and should be repaired before harvest.",
            },
        };

    /// <summary>
    /// Fallback hint list for reason codes the lookup does not know
    /// about. Surfaces verbatim in <see cref="UnresolvedItem.SuggestedNextSteps"/>
    /// so the output itself signals the missing wiring.
    /// </summary>
    public static IReadOnlyList<string> GenericSuggestedNextSteps { get; } = new[]
    {
        "Review the construct at the recorded source location.",
        "Decide whether to extend the producer's reason set with a more specific code (the aggregator's lookup is missing this reason).",
    };
}

/// <summary>
/// A resolver <see cref="ResolvedReference"/> paired with the module and
/// procedure that lexically own it (body OR header). DW-11 Layer A: the
/// <see cref="UnresolvedItemAggregator"/> consumes these — additively to the
/// body-only <see cref="FunctionClassification.UnresolvedReferences"/> feed —
/// so an unresolved reference in a procedure HEADER (an Optional parameter's
/// default expression) reaches <c>unresolved-items.json</c>. Built by the
/// caller from <see cref="AccessHarvester.Pipeline.Manifest.ParserOutputProjector.EnumerateProcedureScopedReferences"/>,
/// which returns exactly the reference set the projector groups into
/// <c>Body.References</c>, so the index is a structural superset of the
/// inline references by construction.
/// </summary>
public sealed record ProcedureScopedReference(
    string ModuleName,
    string ProcedureName,
    ResolvedReference Reference);

/// <summary>
/// A resolver <see cref="ResolvedReference"/> that lives at MODULE scope —
/// it has no enclosing procedure — paired with the module that owns it.
/// DW-11 Layer B (015 S2): the <see cref="UnresolvedItemAggregator"/> consumes
/// these so a module-level unresolved reference (a <c>Const</c>/<c>Dim</c>
/// initializer, an <c>Implements &lt;unresolved&gt;</c>, etc.) reaches
/// <c>unresolved-items.json</c> at module scope (<c>ProcedureName == null</c>).
/// Built by the caller from
/// <see cref="AccessHarvester.Pipeline.Manifest.ParserOutputProjector.EnumerateModuleLevelReferences"/>,
/// which returns exactly the references the projector drops from
/// <c>Body.References</c>. Disjoint from <see cref="ProcedureScopedReference"/>
/// (that one carries only references WITH an enclosing procedure), so the two
/// feeds never double-count.
/// </summary>
public sealed record ModuleLevelReference(
    string ModuleName,
    ResolvedReference Reference);
