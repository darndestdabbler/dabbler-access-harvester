using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Assembles per-category in-memory inputs into a single
/// <see cref="DatabaseManifest"/>. Mirrors the contract of
/// <see cref="UnresolvedItemAggregator.Build"/>: every input is
/// optional, missing inputs surface as empty arrays, and the
/// aggregator does not perform I/O.
/// </summary>
/// <remarks>
/// <para>
/// The aggregator does <b>not</b> re-sort the supplied collections.
/// Callers pass already-deterministic ordering; the aggregator
/// preserves the caller's order verbatim. The contrast with
/// <see cref="UnresolvedItemAggregator"/> is intentional — that
/// aggregator merges from multiple sources and must impose order;
/// this aggregator merges nothing — each per-category input has a
/// single producer — so order ownership stays with the caller.
/// </para>
/// <para>
/// The <see cref="DatabaseSummary"/> attached to the result is
/// computed from the populated collections, so the manifest is
/// internally consistent (i.e. <c>Forms.Count == Summary.FormCount</c>).
/// </para>
/// </remarks>
public static class DatabaseManifestAggregator
{
    /// <summary>
    /// Assemble a <see cref="DatabaseManifest"/> from the supplied
    /// inputs. <paramref name="database"/> and
    /// <paramref name="harvesterVersion"/> are required (matching
    /// the schema's <c>DatabaseManifest.required</c> contract);
    /// every other input is optional.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="database"/> is null or
    /// <paramref name="harvesterVersion"/> is null/empty.
    /// </exception>
    public static DatabaseManifest Build(
        DatabaseInfo database,
        string harvesterVersion,
        IEnumerable<FormExport>? forms = null,
        IEnumerable<FormExport>? reports = null,
        IEnumerable<QueryAbstraction>? queries = null,
        IEnumerable<ModuleExport>? modules = null,
        IEnumerable<TableMetadata>? tables = null,
        IEnumerable<RelationshipMetadata>? relationships = null,
        IEnumerable<ValidationRule>? validationRules = null,
        IEnumerable<RowSourceSpec>? rowSourceSpecs = null,
        IEnumerable<FunctionClassification>? functionClassifications = null,
        IEnumerable<EventHandlerClassification>? eventHandlerClassifications = null,
        NavigationGraph? navigationGraph = null,
        IEnumerable<UnresolvedItem>? unresolvedItems = null,
        IEnumerable<HarvesterDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (string.IsNullOrEmpty(harvesterVersion))
            throw new ArgumentException(
                "HarvesterVersion is required by the schema.",
                nameof(harvesterVersion));

        var formsList                       = Materialize(forms);
        var reportsList                     = Materialize(reports);
        var queriesList                     = Materialize(queries);
        var modulesList                     = Materialize(modules);
        var tablesList                      = Materialize(tables);
        var relationshipsList               = Materialize(relationships);
        var validationRulesList             = Materialize(validationRules);
        var rowSourceSpecsList              = Materialize(rowSourceSpecs);
        var functionClassificationsList     = Materialize(functionClassifications);
        var eventHandlerClassificationsList = Materialize(eventHandlerClassifications);
        var unresolvedItemsList             = Materialize(unresolvedItems);
        var diagnosticsList                 = Materialize(diagnostics);

        var summary = new DatabaseSummary
        {
            FormCount            = formsList.Count,
            ReportCount          = reportsList.Count,
            QueryCount           = queriesList.Count,
            ModuleCount          = modulesList.Count,
            TableCount           = tablesList.Count,
            UnresolvedItemCount  = unresolvedItemsList.Count,
        };

        return new DatabaseManifest
        {
            HarvesterVersion              = harvesterVersion,
            Database                      = database,
            Summary                       = summary,
            Forms                         = formsList,
            Reports                       = reportsList,
            Queries                       = queriesList,
            Modules                       = modulesList,
            Tables                        = tablesList,
            Relationships                 = relationshipsList,
            ValidationRules               = validationRulesList,
            RowSourceSpecs                = rowSourceSpecsList,
            FunctionClassifications       = functionClassificationsList,
            EventHandlerClassifications   = eventHandlerClassificationsList,
            NavigationGraph               = navigationGraph,
            UnresolvedItems               = unresolvedItemsList,
            Diagnostics                   = diagnosticsList,
        };
    }

    private static IReadOnlyList<T> Materialize<T>(IEnumerable<T>? source)
        => source is null ? Array.Empty<T>() : source.ToArray();
}
