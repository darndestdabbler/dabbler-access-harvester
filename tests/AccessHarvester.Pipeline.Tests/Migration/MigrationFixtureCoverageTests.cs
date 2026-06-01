using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.Pipeline.Migration;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.Migration;

/// <summary>
/// Re-confirms the spec's stop-rule against the <em>actual</em> committed fixture
/// columns (HomeHealthCare + NorthwindDevEdition golden IR). The rule: if a fixture
/// column carries an Access type with no safe SQL Server mapping that also cannot be
/// honestly represented as a flagged degradation, the work stops. This test makes
/// that conclusion durable: every fixture column either maps cleanly or is flagged
/// as a known degradation — nothing is silently unhandled.
/// </summary>
public class MigrationFixtureCoverageTests
{
    public static IEnumerable<object[]> Fixtures => new[]
    {
        new object[] { "HomeHealthCare" },
        new object[] { "NorthwindDevEdition" },
    };

    private static string LocateGoldenFixture(string fixture)
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "samples", "GoldenOutput", fixture);
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate samples/GoldenOutput/{fixture} walking up from '{AppContext.BaseDirectory}'.");
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Every_fixture_column_maps_cleanly_or_is_an_honest_degradation(string fixture)
    {
        var tables = MigrationIrReader.ReadTables(Path.Combine(LocateGoldenFixture(fixture), "ir", "Tables"));
        Assert.NotEmpty(tables);

        var unmappable = new List<string>();
        foreach (var table in tables.Where(t => !t.IsSystem && !t.IsLinked))
        {
            foreach (var field in table.Fields)
            {
                var mapping = SqlServerTypeMapper.Map(field);
                if (mapping.Mappable) continue;

                // Unmappable is acceptable ONLY when it is the honest, documented
                // degradation path (multi-value / Attachment). Anything else is a
                // stop-rule trip that must surface to a human.
                bool honest = mapping.DegradationKind == MigrationDegradationKind.UnmappableColumnType
                              && field.TypeCode == 101; // dbAttachment (multi-value)
                if (!honest)
                    unmappable.Add($"{table.Name}.{field.Name} (TypeCode {field.TypeCode} '{field.TypeName}')");
            }
        }

        Assert.True(unmappable.Count == 0,
            "Stop-rule trip: fixture columns with no safe mapping and no honest degradation:\n  "
            + string.Join("\n  ", unmappable));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void GenerateDdl_over_the_full_fixture_produces_create_tables(string fixture)
    {
        var inputs = MigrationIrReader.Read(LocateGoldenFixture(fixture));
        var result = SqlServerDdlGenerator.GenerateDdl(inputs);

        Assert.Contains("CREATE TABLE", result.Sql);
        // No system table leaks into the output.
        Assert.DoesNotContain("CREATE TABLE [MSys", result.Sql);

        // Any degradations are confined to the known, honest kinds.
        var allowed = new[]
        {
            MigrationDegradationKind.UnmappableColumnType, MigrationDegradationKind.MultiValueColumn,
            MigrationDegradationKind.LinkedTableSkipped, MigrationDegradationKind.UntranslatedDefault,
            MigrationDegradationKind.AssumedTypePrecision, MigrationDegradationKind.ForeignKeyDropped,
        };
        Assert.All(result.Degradations, d => Assert.Contains(d.Kind, allowed));
    }
}
