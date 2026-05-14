using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using AccessHarvester.Core;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Integration test for the table-extractor-coverage session set. Runs the
/// full extract pass against the committed HomeHealthCare.accdb fixture
/// (Tables + Relationships only) and asserts the new fields land at the
/// expected values in the raw JSON. Skipped when Access is not installed.
/// </summary>
[SupportedOSPlatform("windows")]
public class HomeHealthCareTableCoverageTests
{
    [SkippableFact]
    public void TblClients_NewFields_LandAtExpectedValues()
    {
        SkipIfNoAccess();

        var db = ResolveSampleDb("HomeHealthCare.accdb");
        Skip.If(db is null, "samples/HomeHealthCare.accdb not found relative to test bin path.");

        var outRoot = Path.Combine(Path.GetTempPath(), $"ae-test-{Guid.NewGuid():N}");
        try
        {
            var pipeline = Pipeline.Default().WithCategories(new[] { "Tables", "Relationships" });
            var result = pipeline.Run(db!, outRoot);

            Assert.All(result.Extract, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

            var tableFile = Path.Combine(result.RawDir, "Tables", "tblClients.json");
            Assert.True(File.Exists(tableFile), $"Raw table JSON not produced: {tableFile}");

            var root = JsonNode.Parse(File.ReadAllText(tableFile))!.AsObject();

            // Table-level ValidationRule + ValidationText (per spec).
            Assert.Equal(
                "[EnrollmentDate] >= [DateOfBirth]",
                root["ValidationRule"]?.GetValue<string>());
            Assert.Equal(
                "Enrollment date cannot be before the client's date of birth.",
                root["ValidationText"]?.GetValue<string>());

            // DDL string present and shaped like a CREATE TABLE for tblClients.
            var ddl = root["Ddl"]?.GetValue<string>();
            Assert.False(string.IsNullOrEmpty(ddl), "Ddl field missing or empty on tblClients raw JSON.");
            Assert.StartsWith("CREATE TABLE [tblClients] (", ddl);
            Assert.Contains("[ClientID] COUNTER", ddl);
            Assert.Contains("CONSTRAINT [PrimaryKey] PRIMARY KEY ([ClientID])", ddl);

            // Per-field assertions.
            var fields = (JsonArray)root["Fields"]!;
            var firstName = FindField(fields, "FirstName");
            Assert.Equal("Is Not Null", firstName["ValidationRule"]?.GetValue<string>());

            var ssn = FindField(fields, "SSNLast4");
            Assert.Equal("0000", ssn["InputMask"]?.GetValue<string>());
            Assert.Equal("@@@@", ssn["Format"]?.GetValue<string>());
            Assert.Equal(109, ssn["DisplayControl"]?.GetValue<int>());

            // Relationship flags — rel_State_Client lives in Relationships/.
            var relFile = Path.Combine(result.RawDir, "Relationships", "rel_State_Client.json");
            Assert.True(File.Exists(relFile), $"Raw relationship JSON not produced: {relFile}");
            var rel = JsonNode.Parse(File.ReadAllText(relFile))!.AsObject();
            Assert.True(rel["EnforceReferentialIntegrity"]?.GetValue<bool>());
            Assert.True(rel["CascadeUpdate"]?.GetValue<bool>());
            Assert.False(rel["CascadeDelete"]?.GetValue<bool>());
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    private static JsonObject FindField(JsonArray fields, string name)
    {
        foreach (var f in fields)
        {
            if (f is JsonObject obj && obj["Name"]?.GetValue<string>() == name)
                return obj;
        }
        throw new Xunit.Sdk.XunitException($"Field '{name}' not found in Fields array.");
    }

    private static string? ResolveSampleDb(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static void SkipIfNoAccess()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Access COM is Windows-only.");
        Skip.If(Type.GetTypeFromProgID("Access.Application") is null,
            "Microsoft Access is not installed (Access.Application ProgID not registered).");
    }
}
