using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Pipeline.UsageIdentification;

/// <summary>
/// Generates clarifying questions for a classified object based on the
/// uncertainty signals defined in strategy §5-6.
/// </summary>
public static class QuestionGenerator
{
    /// <summary>
    /// Returns the set of clarifying questions for this object given its
    /// classification context and signal entries.
    /// </summary>
    public static IReadOnlyList<ClarifyingQuestion> Generate(
        string objectType, string objectName,
        string category, double confidence,
        IReadOnlyList<SignalEntry> signals,
        string? recordSource, bool hasVbaCode,
        bool isNavigationTarget, int? queryTypeCode,
        SignalIndex index)
    {
        var questions = new List<ClarifyingQuestion>();

        // OutOfBandEntryPossible — no harvested opener was found, but the object may
        // still be reached via application startup, direct object-pane navigation,
        // the ribbon, or VBA in another object. This replaces the older
        // NoNavigationEntry frame, which read like a staleness hint. Lack of an
        // opener is an uncertainty to explain, not a cleanup signal (strategy §5a).
        //
        // Scope keeps the checklist focused: reports/queries get it whenever they
        // have no harvested opener (they had no equivalent question before); forms
        // get it only when bound to a RecordSource (an unbound form's uncertainty is
        // already carried by the NoRecordSource question below, so we do not pile on).
        bool eligibleForOutOfBand =
            objectType is "Report" or "Query"
            || (objectType == "Form" && !string.IsNullOrWhiteSpace(recordSource));
        if (eligibleForOutOfBand
            && confidence < 0.8
            && category != UsageCategory.NavigationShell
            && category != UsageCategory.PossiblyUnused
            && !HasHarvestedOpener(objectName, isNavigationTarget, index))
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"No harvested opener was found for {objectType} '{objectName}'. In Access it may still be reached via application startup, direct navigation in the object pane, a ribbon/menu command, or VBA in another object. Was it opened one of those ways, or is it genuinely unused?",
                UncertaintySignal = UncertaintySignal.OutOfBandEntryPossible,
            });
        }

        // ParameterizedInput-driven questions (set 001 Session 2). The signal is
        // set by the classifier — queries: SQL parameter evidence (declared
        // PARAMETERS, bracketed prompt, form/report control reference, ODBC marker);
        // forms: rule F8 parameter-dialog recognition. For a form it is a recognized
        // parameter/filter dialog; for a query/report it expects run-time input.
        bool parameterizedInput = signals
            .Any(s => s.SignalType == UsageSignalType.ParameterizedInput && s.Fired);
        bool isParamDialog = parameterizedInput && objectType == "Form";

        if (isParamDialog)
        {
            // Specific recognition replaces the generic NoRecordSource /
            // UnrecognizedVbaPattern questions (suppressed below) — this is what
            // makes a parameter dialog distinguishable from a generic unknown form.
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"Form '{objectName}' has no record source and matches a parameter/filter/criteria dialog naming pattern. Which report or query does it collect input for, and how are the entered values applied at run time (for example, opened with acDialog and read by a report's Open event)?",
                UncertaintySignal = UncertaintySignal.ParameterDialogFlow,
            });
        }
        else if (parameterizedInput && objectType is "Query" or "Report")
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"{objectType} '{objectName}' expects run-time parameter input (a declared PARAMETERS clause, a bracketed prompt, a form/report control reference, or an ODBC marker). Where do the values come from at run time — an automatic Access prompt, a parameter/filter form, or a calling procedure?",
                UncertaintySignal = UncertaintySignal.ParameterizedQuery,
            });
        }

        // NoRecordSource — form has no record source and is not NavigationShell or
        // PossiblyUnused (and not a recognized parameter dialog — that gets the
        // specific ParameterDialogFlow question above instead).
        if (objectType == "Form" && string.IsNullOrWhiteSpace(recordSource)
            && !isParamDialog
            && category is not (UsageCategory.NavigationShell or UsageCategory.PossiblyUnused))
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"Form '{objectName}' has no record source — is it a navigation panel, a parameter/filter dialog, or a utility form?",
                UncertaintySignal = UncertaintySignal.NoRecordSource,
            });
        }

        // QueryReferencesNoTables — query SQL references only other queries
        if (objectType == "Query")
        {
            bool noDirectTables = signals
                .FirstOrDefault(s => s.SignalType == UsageSignalType.QueryTableReference)?.Evidence
                .Any(e => e.Contains("references-no-tables")) ?? false;
            if (noDirectTables)
            {
                questions.Add(new ClarifyingQuestion
                {
                    QuestionText    = $"Query '{objectName}' references only other queries — is it an intermediate computation step, a parameter source, or can it be merged into its callers?",
                    UncertaintySignal = UncertaintySignal.QueryReferencesNoTables,
                });
            }
        }

        // CrosstabQuery — query is crosstab type
        if (objectType == "Query" && queryTypeCode == DaoQueryTypeCodes.Crosstab)
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"Query '{objectName}' is a crosstab query — is it used for reporting, for driving a form or chart control, or for data exports?",
                UncertaintySignal = UncertaintySignal.CrosstabQuery,
            });
        }

        // UnreferencedObject — object is absent from all harvested signals. Reframed
        // (set 001 Session 1) so missing references read as out-of-band uncertainty
        // rather than a deletion hint: in Access an object can stay live via startup,
        // direct navigation, or the ribbon even with no inbound harvested reference.
        if (category == UsageCategory.PossiblyUnused)
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"No harvested reference was found for {objectType} '{objectName}'. In Access it may still be reached via application startup, direct object-pane navigation, the ribbon, or other access outside the harvested graph — a missing reference is not by itself evidence of staleness. Is it still in use, or genuinely obsolete?",
                UncertaintySignal = UncertaintySignal.UnreferencedObject,
            });
        }

        // LookupTablePattern — table classified as LookupReference
        if (objectType == "Table" && category == UsageCategory.LookupReference)
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"Table '{objectName}' appears to hold reference/lookup values — should it migrate as static reference data, or be replaced with application constants?",
                UncertaintySignal = UncertaintySignal.LookupTablePattern,
            });
        }

        // UnrecognizedVbaPattern — form/module has VBA but no parseable DoCmd/DFunction calls.
        // Suppressed for a recognized parameter dialog: its VBA (the OK/Cancel dialog
        // logic) is explained by the ParameterDialogFlow recognition above.
        if (hasVbaCode && category == UsageCategory.Unknown
            && !isParamDialog
            && objectType is "Form" or "Module")
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"{objectType} '{objectName}' contains VBA that couldn't be auto-classified — can you describe its primary function?",
                UncertaintySignal = UncertaintySignal.UnrecognizedVbaPattern,
            });
        }

        // CrypticObjectName — object name has no conventional prefix and < 5 chars
        if (IsCrypticName(objectName))
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"{objectType} '{objectName}' has a name that doesn't clearly indicate its purpose — can you describe what it is used for?",
                UncertaintySignal = UncertaintySignal.CrypticObjectName,
            });
        }

        return questions;
    }

    // True when the harvested graph contains any opener/reference that would reach
    // this object: it is a navigation target, referenced from VBA, used as a
    // RecordSource, used as a RowSource, or consumed by another query's SQL
    // (FROM/JOIN). When none hold, the object's entry path is out-of-band
    // (startup/direct/ribbon) and the OutOfBandEntryPossible question explains that
    // rather than implying the object is stale. The query-SQL arm matters because an
    // intermediate query referenced only by another query is an in-graph dependency
    // the harvester already knows about — not an out-of-band entry.
    private static bool HasHarvestedOpener(string name, bool isNavigationTarget, SignalIndex index)
        => isNavigationTarget
        || index.VbaReferencedObjects.Contains(name)
        || index.UsedAsRecordSource.Contains(name)
        || index.UsedAsRowSource.Contains(name)
        || index.UsedInQuerySql.Contains(name);

    private static bool IsCrypticName(string name)
        => name.Length < 5 && !name.Contains('_') && !char.IsUpper(name.Length > 1 ? name[1] : '\0');
}
