using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Access;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Pure-function runner that stitches the three Phase 6.3–6.5 enrichers
/// together for a single form:
/// <list type="number">
///   <item><see cref="EventHandlerClassifier"/> — classifies each event handler
///   and populates <see cref="EventHandlerReference.Classification"/>.</item>
///   <item><see cref="ValidationRuleExtractor"/> — extracts design-time and
///   <c>BeforeUpdate</c>-handler rules, populating
///   <see cref="ValidationRuleReference.Classification"/>.</item>
///   <item><see cref="RowSourceSpecBuilder"/> — builds row-source specs annotated
///   with cascading-driver cross-references from the event-handler classifications.</item>
/// </list>
/// Ordering: event-handler classification runs before
/// <see cref="RowSourceSpecBuilder.BuildAll"/> so cascading drivers are detected
/// from the same classifications that populate the spec.
/// </summary>
/// <remarks>
/// Calling convention: pass the <see cref="FormComponentSpec"/> already built by
/// <see cref="FormComponentSpecBuilder"/> (which leaves <c>Classification</c> slots
/// null), the corresponding <see cref="FormMetadata"/>, and the parsed VBA
/// <see cref="ProcedureDeclaration"/> list for the form's class module. Procedures
/// not referenced by any <see cref="EventHandlerReference"/> in <paramref name="spec"/>
/// are ignored.
/// </remarks>
public static class EnrichmentPipelineRunner
{
    /// <summary>
    /// Run all Phase 6.3–6.5 enrichers over a single form and return the
    /// composite <see cref="EnrichmentResult"/>. The call is pure: inputs are
    /// not mutated, and outputs are new record instances.
    /// </summary>
    /// <param name="spec">The form component spec from the previous pipeline stage
    /// (Classification slots are null on input).</param>
    /// <param name="form">The form's design-time metadata (used by
    /// <see cref="ValidationRuleExtractor.ExtractFromForm"/> and
    /// <see cref="RowSourceSpecBuilder.BuildAll"/>).</param>
    /// <param name="procedures">Parsed VBA procedures from the form's class module.
    /// Null or empty means no event-handler classification or BeforeUpdate extraction
    /// occurs — design-time validation rules are still extracted from
    /// <paramref name="form"/>.</param>
    public static EnrichmentResult Run(
        FormComponentSpec spec,
        FormMetadata form,
        IReadOnlyList<ProcedureDeclaration>? procedures = null)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(form);

        var procedureMap = BuildProcedureMap(procedures);

        // Step 1: classify event handlers
        var classifications = ClassifyHandlers(spec, procedureMap);
        var classificationByName = classifications.ToDictionary(
            c => c.ProcedureName,
            c => FormatHandlerClassification(c),
            StringComparer.OrdinalIgnoreCase);

        // Step 2: extract validation rules (design-time + BeforeUpdate handlers)
        var allRules = new List<ValidationRule>(ValidationRuleExtractor.ExtractFromForm(form));
        var beforeUpdateClassifications = ExtractBeforeUpdateRules(spec, form.Name, procedureMap, allRules);

        // Step 3: populate Classification slots on EventHandlerReference records
        var enrichedHandlers = EnrichHandlers(spec.EventHandlers, classificationByName);

        // Step 4: populate Classification slots on ValidationRuleReference records
        var enrichedValidationRefs = EnrichValidationRefs(
            spec.ValidationRules, allRules, beforeUpdateClassifications);

        // Step 5: build RowSourceSpecs (cascading drivers use the classifications)
        var rowSourceSpecs = RowSourceSpecBuilder.BuildAll(form, classifications);

        var enrichedSpec = spec with
        {
            EventHandlers = enrichedHandlers,
            ValidationRules = enrichedValidationRefs,
        };

        return new EnrichmentResult
        {
            EnrichedSpec = enrichedSpec,
            ValidationRules = allRules,
            RowSourceSpecs = rowSourceSpecs,
            EventHandlerClassifications = classifications,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static Dictionary<string, ProcedureDeclaration> BuildProcedureMap(
        IReadOnlyList<ProcedureDeclaration>? procedures)
    {
        if (procedures is null || procedures.Count == 0)
            return new Dictionary<string, ProcedureDeclaration>(StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, ProcedureDeclaration>(
            procedures.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var p in procedures)
            map[p.Name] = p;
        return map;
    }

    private static IReadOnlyList<EventHandlerClassification> ClassifyHandlers(
        FormComponentSpec spec,
        Dictionary<string, ProcedureDeclaration> procedureMap)
    {
        if (spec.EventHandlers is null || spec.EventHandlers.Count == 0 || procedureMap.Count == 0)
            return Array.Empty<EventHandlerClassification>();

        var result = new List<EventHandlerClassification>();
        foreach (var handler in spec.EventHandlers)
        {
            if (handler.VbaProcedureName is not null &&
                procedureMap.TryGetValue(handler.VbaProcedureName, out var procedure))
            {
                result.Add(EventHandlerClassifier.Classify(procedure));
            }
        }
        return result;
    }

    private static string FormatHandlerClassification(EventHandlerClassification c)
        => c.Mixed ? "Mixed" : c.Categories.Count > 0 ? c.Categories[0].ToString() : "Mixed";

    // Returns a dict of procName -> classification string for BeforeUpdate handlers.
    // Side-effect: appends extracted rules to allRules.
    private static Dictionary<string, string?> ExtractBeforeUpdateRules(
        FormComponentSpec spec,
        string formName,
        Dictionary<string, ProcedureDeclaration> procedureMap,
        List<ValidationRule> allRules)
    {
        var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (spec.ValidationRules is null || procedureMap.Count == 0) return cache;

        foreach (var vref in spec.ValidationRules)
        {
            if (vref.Source != ValidationRuleSource.BeforeUpdateHandler) continue;

            var procName = vref.ControlName is not null
                ? $"{vref.ControlName}_BeforeUpdate"
                : "Form_BeforeUpdate";

            if (cache.ContainsKey(procName)) continue;

            if (!procedureMap.TryGetValue(procName, out var proc))
            {
                cache[procName] = null;
                continue;
            }

            var rules = ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(
                proc, formName, vref.ControlName);
            allRules.AddRange(rules);

            if (rules.Count == 0)
            {
                cache[procName] = null;
            }
            else
            {
                var kinds = rules.Select(r => r.Kind).Distinct().ToList();
                cache[procName] = kinds.Count == 1 ? kinds[0].ToString() : "Mixed";
            }
        }

        return cache;
    }

    private static IReadOnlyList<EventHandlerReference>? EnrichHandlers(
        IReadOnlyList<EventHandlerReference>? handlers,
        Dictionary<string, string> classificationByName)
    {
        if (handlers is null) return null;
        if (classificationByName.Count == 0) return handlers;

        var enriched = new List<EventHandlerReference>(handlers.Count);
        bool anyChanged = false;
        foreach (var h in handlers)
        {
            if (h.VbaProcedureName is not null &&
                classificationByName.TryGetValue(h.VbaProcedureName, out var cls))
            {
                enriched.Add(h with { Classification = cls });
                anyChanged = true;
            }
            else
            {
                enriched.Add(h);
            }
        }
        return anyChanged ? enriched : handlers;
    }

    private static IReadOnlyList<ValidationRuleReference>? EnrichValidationRefs(
        IReadOnlyList<ValidationRuleReference>? refs,
        IReadOnlyList<ValidationRule> extractedRules,
        Dictionary<string, string?> beforeUpdateClassifications)
    {
        if (refs is null) return null;

        var enriched = new List<ValidationRuleReference>(refs.Count);
        bool anyChanged = false;
        foreach (var vref in refs)
        {
            var cls = ResolveValidationClassification(vref, extractedRules, beforeUpdateClassifications);
            if (cls is not null)
            {
                enriched.Add(vref with { Classification = cls });
                anyChanged = true;
            }
            else
            {
                enriched.Add(vref);
            }
        }
        return anyChanged ? enriched : refs;
    }

    private static string? ResolveValidationClassification(
        ValidationRuleReference vref,
        IReadOnlyList<ValidationRule> extractedRules,
        Dictionary<string, string?> beforeUpdateClassifications)
    {
        return vref.Source switch
        {
            ValidationRuleSource.RequiredField =>
                ValidationRuleKind.Required.ToString(),
            ValidationRuleSource.LimitToList =>
                ValidationRuleKind.Membership.ToString(),
            ValidationRuleSource.ControlValidationRule =>
                extractedRules
                    .FirstOrDefault(r => r.Origin == ValidationRuleOrigin.ControlValidationRule
                        && string.Equals(r.FieldOrControlName, vref.ControlName, StringComparison.OrdinalIgnoreCase))
                    ?.Kind.ToString(),
            ValidationRuleSource.RecordValidationRule =>
                extractedRules
                    .FirstOrDefault(r => r.Origin == ValidationRuleOrigin.FormValidationRule)
                    ?.Kind.ToString(),
            ValidationRuleSource.BeforeUpdateHandler =>
                ResolveBeforeUpdateClassification(vref, beforeUpdateClassifications),
            _ => null,
        };
    }

    private static string? ResolveBeforeUpdateClassification(
        ValidationRuleReference vref,
        Dictionary<string, string?> beforeUpdateClassifications)
    {
        var procName = vref.ControlName is not null
            ? $"{vref.ControlName}_BeforeUpdate"
            : "Form_BeforeUpdate";
        return beforeUpdateClassifications.TryGetValue(procName, out var cls) ? cls : null;
    }
}
