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

        // NoNavigationEntry — form not present as navigation target and not a nav shell
        if (objectType == "Form"
            && !isNavigationTarget
            && category != UsageCategory.NavigationShell
            && category != UsageCategory.PossiblyUnused)
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"No navigation entry found for Form '{objectName}' — is it launched from VBA code, another form, or an external application?",
                UncertaintySignal = UncertaintySignal.NoNavigationEntry,
            });
        }

        // NoRecordSource — form has no record source and is not NavigationShell or PossiblyUnused
        if (objectType == "Form" && string.IsNullOrWhiteSpace(recordSource)
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
        if (objectType == "Query" && queryTypeCode == 1)
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"Query '{objectName}' is a crosstab query — is it used for reporting, for driving a form or chart control, or for data exports?",
                UncertaintySignal = UncertaintySignal.CrosstabQuery,
            });
        }

        // UnreferencedObject — object is completely absent from all signals
        if (category == UsageCategory.PossiblyUnused)
        {
            questions.Add(new ClarifyingQuestion
            {
                QuestionText    = $"{objectType} '{objectName}' is not referenced by any form, report, query, VBA, or macro — is it still in use or can it be excluded from analysis?",
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

        // UnrecognizedVbaPattern — form/module has VBA but no parseable DoCmd/DFunction calls
        if (hasVbaCode && category == UsageCategory.Unknown
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

    private static bool IsCrypticName(string name)
        => name.Length < 5 && !name.Contains('_') && !char.IsUpper(name.Length > 1 ? name[1] : '\0');
}
