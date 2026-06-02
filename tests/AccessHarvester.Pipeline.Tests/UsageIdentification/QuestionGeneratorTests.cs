using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

public class QuestionGeneratorTests
{
    private static IrDatabase EmptyDb() => new()
    {
        Name = "Test",
        Forms = [], Reports = [], Queries = [], Tables = [],
        Relationships = [], Macros = [], Modules = [],
    };

    private static IReadOnlyList<SignalEntry> NoSignals() => [];

    private static SignalIndex EmptyIndex() => SignalIndex.Build(EmptyDb());

    [Fact]
    public void Generate_No_Questions_For_HighConfidence_MainDataEntry_Form_With_NavTarget()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Form", "frmClients", UsageCategory.MainDataEntry, 0.9,
            NoSignals(), "tblClients", hasVbaCode: true,
            isNavigationTarget: true, queryTypeCode: null, index);
        // Navigation target, has record source — no questions expected
        Assert.DoesNotContain(questions, q => q.UncertaintySignal == UncertaintySignal.NoNavigationEntry);
        Assert.DoesNotContain(questions, q => q.UncertaintySignal == UncertaintySignal.NoRecordSource);
    }

    [Fact]
    public void Generate_OutOfBandEntry_When_Bound_Form_Has_No_Harvested_Opener()
    {
        // A bound (RecordSource) form not reachable through any harvested opener now
        // gets the out-of-band reframing instead of the old NoNavigationEntry hint.
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Form", "frmHidden", UsageCategory.MainDataEntry, 0.7,
            NoSignals(), "tblData", hasVbaCode: false,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
        // The retired signal is no longer emitted.
        Assert.DoesNotContain(questions, q => q.UncertaintySignal == UncertaintySignal.NoNavigationEntry);
        Assert.Contains("frmHidden", questions[0].QuestionText);
    }

    [Fact]
    public void Generate_OutOfBandEntry_For_Report_With_No_Opener()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Report", "rptAdHoc", UsageCategory.Reporting, 0.6,
            NoSignals(), null, hasVbaCode: false,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
    }

    [Fact]
    public void Generate_OutOfBandEntry_Not_Raised_For_NavigationShell()
    {
        // A launcher surface is itself the entry point — no out-of-band question, and
        // the retired NoNavigationEntry signal stays absent.
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Form", "frmSwitchboard", UsageCategory.NavigationShell, 0.8,
            NoSignals(), null, hasVbaCode: true,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.DoesNotContain(questions, q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
        Assert.DoesNotContain(questions, q => q.UncertaintySignal == UncertaintySignal.NoNavigationEntry);
    }

    [Fact]
    public void Generate_OutOfBandEntry_Not_Raised_For_PossiblyUnused()
    {
        // PossiblyUnused objects carry the (reframed) UnreferencedObject question; the
        // out-of-band question must not also fire so the checklist stays focused.
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Query", "qryOrphan", UsageCategory.PossiblyUnused, 0.65,
            NoSignals(), null, hasVbaCode: false,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.DoesNotContain(questions, q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.UnreferencedObject);
    }

    [Fact]
    public void Generate_NoRecordSource_For_Unknown_Form_Without_RecordSource()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Form", "frmMystery", UsageCategory.Unknown, 0.3,
            NoSignals(), null, hasVbaCode: true,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.NoRecordSource);
    }

    [Fact]
    public void Generate_UnreferencedObject_For_PossiblyUnused()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Form", "frmOrphan", UsageCategory.PossiblyUnused, 0.7,
            NoSignals(), null, hasVbaCode: false,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.UnreferencedObject);
    }

    [Fact]
    public void Generate_CrosstabQuery_Question_For_Crosstab_TypeCode()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Query", "qxtSummary", UsageCategory.Reporting, 0.7,
            NoSignals(), null, hasVbaCode: false,
            isNavigationTarget: false, queryTypeCode: 112, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.CrosstabQuery);
        Assert.Contains("crosstab",
                        questions.First(q => q.UncertaintySignal == UncertaintySignal.CrosstabQuery).QuestionText);
    }

    [Fact]
    public void Generate_LookupTablePattern_Question_For_LookupReference_Table()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Table", "tlkpStatuses", UsageCategory.LookupReference, 0.75,
            NoSignals(), null, hasVbaCode: false,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.LookupTablePattern);
    }

    [Fact]
    public void Generate_UnrecognizedVbaPattern_For_Unknown_Form_With_Vba()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Form", "frmWeird", UsageCategory.Unknown, 0.3,
            NoSignals(), null, hasVbaCode: true,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.UnrecognizedVbaPattern);
    }

    [Fact]
    public void Generate_CrypticObjectName_For_Short_Name_No_Prefix()
    {
        var index = EmptyIndex();
        var questions = QuestionGenerator.Generate(
            "Form", "frm", UsageCategory.Unknown, 0.3,
            NoSignals(), null, hasVbaCode: false,
            isNavigationTarget: false, queryTypeCode: null, index);
        Assert.Contains(questions, q => q.UncertaintySignal == UncertaintySignal.CrypticObjectName);
    }
}
