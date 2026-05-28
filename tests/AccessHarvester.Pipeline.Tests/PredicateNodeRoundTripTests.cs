using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Validation;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Round-trip tests for the PredicateNode [JsonPolymorphic] fix.
// Verifies that [JsonPolymorphic] + [JsonDerivedType] attributes on
// PredicateNode produce the NodeKind discriminator field in serialized
// JSON, and that serialize → validate → deserialize → re-serialize
// produces byte-identical output.
//
// Three representative trees per spec:
//   (a) single AndPredicate
//   (b) nested OrPredicate(AndPredicate, ...)
//   (c) leaf ComparisonPredicate
public sealed class PredicateNodeRoundTripTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string LocateSchemaPath()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases", "harvester-schema.v1.0.json");
            if (File.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException(
            $"Could not locate harvester-schema.v1.0.json walking up from '{AppContext.BaseDirectory}'.");
    }

    private static string WrapInManifest(PredicateNode where)
    {
        // Minimal DatabaseManifest with one QueryAbstraction that holds
        // the PredicateNode as its Where clause. Used to validate the
        // full document (including the PredicateNode) against v1.0.
        var manifest = new
        {
            SchemaType = "DatabaseManifest",
            SchemaVersion = "1.0",
            HarvesterVersion = "test",
            Database = new { Name = "RoundTripTest.accdb" },
            Queries = new[]
            {
                new
                {
                    SourceQueryName = "qryTest",
                    Shape = "Select",
                    Where = JsonSerializer.SerializeToDocument(
                        where, typeof(PredicateNode), SerializerOptions).RootElement,
                }
            },
        };
        return JsonSerializer.Serialize(manifest, SerializerOptions);
    }

    // (a) Single AndPredicate
    [Fact]
    public void AndPredicate_RoundTrips_WithNodeKindDiscriminator()
    {
        PredicateNode node = new AndPredicate(new List<PredicateNode>
        {
            new ComparisonPredicate(
                "=",
                new ExpressionDescriptor { Display = "c.StatusID", Kind = ExpressionKind.Column, ColumnName = "StatusID", TableQualifier = "c" },
                new ExpressionDescriptor { Display = "1", Kind = ExpressionKind.Literal, LiteralKind = LiteralKind.Number }),
            new IsNullPredicate(
                new ExpressionDescriptor { Display = "c.ClosedDate", Kind = ExpressionKind.Column, ColumnName = "ClosedDate", TableQualifier = "c" },
                Negated: false),
        });

        string json = JsonSerializer.Serialize(node, typeof(PredicateNode), SerializerOptions);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("NodeKind", out var nodeKind),
            $"Expected NodeKind discriminator in serialized AndPredicate. JSON:\n{json}");
        Assert.Equal("And", nodeKind.GetString());

        PredicateNode? deserialized = JsonSerializer.Deserialize<PredicateNode>(json, SerializerOptions);
        Assert.NotNull(deserialized);
        Assert.IsType<AndPredicate>(deserialized);

        string roundTripped = JsonSerializer.Serialize(deserialized, typeof(PredicateNode), SerializerOptions);
        Assert.Equal(json, roundTripped);
    }

    // (b) Nested OrPredicate(AndPredicate, ...)
    [Fact]
    public void OrPredicateWithNestedAnd_RoundTrips_WithNodeKindDiscriminator()
    {
        PredicateNode node = new OrPredicate(new List<PredicateNode>
        {
            new AndPredicate(new List<PredicateNode>
            {
                new ComparisonPredicate(
                    ">",
                    new ExpressionDescriptor { Display = "c.Priority", Kind = ExpressionKind.Column, ColumnName = "Priority" },
                    new ExpressionDescriptor { Display = "0", Kind = ExpressionKind.Literal, LiteralKind = LiteralKind.Number }),
                new ComparisonPredicate(
                    "<",
                    new ExpressionDescriptor { Display = "c.DaysOpen", Kind = ExpressionKind.Column, ColumnName = "DaysOpen" },
                    new ExpressionDescriptor { Display = "90", Kind = ExpressionKind.Literal, LiteralKind = LiteralKind.Number }),
            }),
            new IsNullPredicate(
                new ExpressionDescriptor { Display = "c.AssignedTo", Kind = ExpressionKind.Column, ColumnName = "AssignedTo" },
                Negated: false),
        });

        string json = JsonSerializer.Serialize(node, typeof(PredicateNode), SerializerOptions);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("NodeKind", out var nodeKind));
        Assert.Equal("Or", nodeKind.GetString());

        // Each nested operand must also carry NodeKind.
        var operands = doc.RootElement.GetProperty("Operands");
        foreach (var operand in operands.EnumerateArray())
            Assert.True(operand.TryGetProperty("NodeKind", out _),
                $"Nested operand missing NodeKind: {operand}");

        PredicateNode? deserialized = JsonSerializer.Deserialize<PredicateNode>(json, SerializerOptions);
        Assert.IsType<OrPredicate>(deserialized);

        string roundTripped = JsonSerializer.Serialize(deserialized, typeof(PredicateNode), SerializerOptions);
        Assert.Equal(json, roundTripped);
    }

    // (c) Leaf ComparisonPredicate
    [Fact]
    public void ComparisonPredicate_RoundTrips_WithNodeKindDiscriminator()
    {
        PredicateNode node = new ComparisonPredicate(
            "=",
            new ExpressionDescriptor { Display = "c.DistrictID", Kind = ExpressionKind.Column, ColumnName = "DistrictID", TableQualifier = "c" },
            new ExpressionDescriptor { Display = "[Forms]![frmReport]![cboDistrict]", Kind = ExpressionKind.FormReference, FormReferencePath = "[Forms]![frmReport]![cboDistrict]" });

        string json = JsonSerializer.Serialize(node, typeof(PredicateNode), SerializerOptions);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("NodeKind", out var nodeKind));
        Assert.Equal("Comparison", nodeKind.GetString());

        PredicateNode? deserialized = JsonSerializer.Deserialize<PredicateNode>(json, SerializerOptions);
        Assert.IsType<ComparisonPredicate>(deserialized);

        string roundTripped = JsonSerializer.Serialize(deserialized, typeof(PredicateNode), SerializerOptions);
        Assert.Equal(json, roundTripped);
    }

    // (d) Schema validation: a DatabaseManifest containing a PredicateNode
    // validates against harvester-schema.v1.0.json. This is the concrete
    // proof that [JsonPolymorphic] produces output the schema accepts.
    [Fact]
    public void PredicateNodeInManifest_ValidatesAgainstV1Schema()
    {
        PredicateNode where = new AndPredicate(new List<PredicateNode>
        {
            new ComparisonPredicate(
                "=",
                new ExpressionDescriptor { Display = "StatusID", Kind = ExpressionKind.Column, ColumnName = "StatusID" },
                new ExpressionDescriptor { Display = "1", Kind = ExpressionKind.Literal, LiteralKind = LiteralKind.Number }),
        });

        string manifest = WrapInManifest(where);
        string schemaPath = LocateSchemaPath();

        var validator = new SchemaValidator(schemaPath);
        var result = validator.Validate(manifest);

        Assert.True(result.IsValid,
            $"DatabaseManifest with PredicateNode failed v1.0 validation:\n  - " +
            string.Join("\n  - ", result.Errors) +
            $"\n\nManifest JSON:\n{manifest}");
    }
}
