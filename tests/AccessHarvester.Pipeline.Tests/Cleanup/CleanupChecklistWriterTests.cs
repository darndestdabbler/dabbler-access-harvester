using System.IO;
using System.Text.Json;
using AccessHarvester.Pipeline.Cleanup;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Cleanup;

public class CleanupChecklistWriterTests
{
    private static CleanupFindings SampleFindings() => new()
    {
        Database = "TestDb",
        GeneratedAt = "2026-06-13T00:00:00Z",
        HarvestDate = "2026-06-13",
        DateSignalSource = "synthetic",
        ThresholdDays = 365,
        DegradationNotes = [],
        Findings =
        [
            new CleanupFinding
            {
                ObjectType = "Table",
                ObjectName = "tblOld",
                ConfidenceTier = "Medium",
                Explanation = "Name pattern suggests a backup/temp table.",
                FiredSignals =
                [
                    new NamePatternSignal { Summary = "name matches _old", MatchedSubstring = "_old", Category = "Backup" },
                ],
            },
        ],
    };

    [Fact]
    public void Write_Creates_Both_Output_Files()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDir);
            CleanupChecklistWriter.Write(SampleFindings(), tempDir);
            Assert.True(File.Exists(Path.Combine(tempDir, "object-cleanup.json")));
            Assert.True(File.Exists(Path.Combine(tempDir, "object-cleanup-checklist.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Write_Outputs_Have_No_Utf8_Bom()  // 006 F-2-3 (RFC 8259 §8.1)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDir);
            CleanupChecklistWriter.Write(SampleFindings(), tempDir);
            foreach (var file in new[] { "object-cleanup.json", "object-cleanup-checklist.json" })
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(tempDir, file));
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    $"{file} must not start with a UTF-8 BOM.");
                Assert.True(bytes[0] == (byte)'{' || bytes[0] == (byte)'[',
                    $"{file} must begin with '{{' or '['.");
                // And it must still parse as JSON through a strict (BOM-rejecting) parser.
                using var doc = JsonDocument.Parse(bytes);
                Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
