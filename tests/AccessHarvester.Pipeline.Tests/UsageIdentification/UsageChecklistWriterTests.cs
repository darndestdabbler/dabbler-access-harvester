using System.IO;
using System.Text.Json;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

public class UsageChecklistWriterTests
{
    private static ObjectUseFindings SampleFindings() => new()
    {
        Database = "TestDb",
        GeneratedAt = "2026-05-28T00:00:00Z",
        ExcludedObjects = ["frmOld"],
        Objects =
        [
            new UsageFinding
            {
                ObjectType = "Form",
                ObjectName = "frmClients",
                Classification = UsageCategory.MainDataEntry,
                Confidence = 0.85,
                UsageSummary = "Main data entry form backed by tblClients",
                Signals = [],
                Questions =
                [
                    new ClarifyingQuestion
                    {
                        QuestionText = "No navigation entry found for Form 'frmClients'",
                        UncertaintySignal = UncertaintySignal.NoNavigationEntry,
                    }
                ],
            },
            new UsageFinding
            {
                ObjectType = "Table",
                ObjectName = "tlkpStatuses",
                Classification = UsageCategory.LookupReference,
                Confidence = 0.75,
                UsageSummary = "Lookup/reference table 'tlkpStatuses'",
                Signals = [],
                Questions = [],
            },
        ],
    };

    [Fact]
    public void BuildChecklist_ProjectName_Uses_Database_Name()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.Equal("TestDb — Object Use Checklist", doc.ProjectName);
    }

    [Fact]
    public void BuildChecklist_One_Review_Per_Finding()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.Equal(2, doc.Review.Length);
    }

    [Fact]
    public void BuildChecklist_Review_Maps_ObjectType_To_FunctionalArea()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.Equal("Form",  doc.Review[0].FunctionalArea);
        Assert.Equal("Table", doc.Review[1].FunctionalArea);
    }

    [Fact]
    public void BuildChecklist_Review_Maps_ObjectName_To_Subarea()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.Equal("frmClients",   doc.Review[0].Subarea);
        Assert.Equal("tlkpStatuses", doc.Review[1].Subarea);
    }

    [Fact]
    public void BuildChecklist_Review_Maps_UsageSummary_To_ItemLabel()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.Equal("Main data entry form backed by tblClients", doc.Review[0].ItemLabel);
    }

    [Fact]
    public void BuildChecklist_Questions_Mapped_To_Steps()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.Single(doc.Review[0].Steps);
        Assert.Equal(1, doc.Review[0].Steps[0].StepNumber);
        Assert.Contains("frmClients", doc.Review[0].Steps[0].Action);
    }

    [Fact]
    public void BuildChecklist_Object_With_No_Questions_Has_Empty_Steps()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.Empty(doc.Review[1].Steps);
    }

    [Fact]
    public void BuildChecklist_IsJudgmentItem_Always_True()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.All(doc.Review, r => Assert.True(r.IsJudgmentItem));
    }

    [Fact]
    public void BuildChecklist_Passes_Always_False()
    {
        var doc = UsageChecklistWriter.BuildChecklist(SampleFindings());
        Assert.All(doc.Review, r => Assert.False(r.Passes));
    }

    [Fact]
    public void Write_Creates_Both_Output_Files()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDir);
            UsageChecklistWriter.Write(SampleFindings(), tempDir);
            Assert.True(File.Exists(Path.Combine(tempDir, "object-use.json")));
            Assert.True(File.Exists(Path.Combine(tempDir, "object-use-checklist.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Write_ObjectUse_Json_Is_Valid_Json()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDir);
            UsageChecklistWriter.Write(SampleFindings(), tempDir);
            string json = File.ReadAllText(Path.Combine(tempDir, "object-use.json"));
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Write_Checklist_Json_Contains_Review_Array()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDir);
            UsageChecklistWriter.Write(SampleFindings(), tempDir);
            string json = File.ReadAllText(Path.Combine(tempDir, "object-use-checklist.json"));
            using var doc = JsonDocument.Parse(json);
            // The checklist must be PascalCase to load in the uat-checklist-editor
            // (its schema requires {"ProjectName","Review":[...]}). A camelCase
            // "review" key would load as an empty checklist. See CleanupChecklistWriter.
            Assert.False(doc.RootElement.TryGetProperty("review", out _),
                "object-use-checklist.json must not use camelCase 'review' — the editor requires PascalCase 'Review'.");
            Assert.True(doc.RootElement.TryGetProperty("ProjectName", out _));
            Assert.True(doc.RootElement.TryGetProperty("Review", out var review));
            Assert.Equal(JsonValueKind.Array, review.ValueKind);
            Assert.Equal(2, review.GetArrayLength());
            Assert.True(review[0].TryGetProperty("FunctionalArea", out _));
            Assert.True(review[0].TryGetProperty("Passes", out _));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
