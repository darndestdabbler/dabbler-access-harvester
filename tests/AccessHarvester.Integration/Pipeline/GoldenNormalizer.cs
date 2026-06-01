using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AccessHarvester.Integration.Pipeline;

/// <summary>
/// Replaces legitimately-volatile fields in harvester output with stable
/// placeholders so the committed golden corpus and the regression test's
/// re-generated output can be diffed byte-for-byte.
///
/// Session 3 lock (Decision 4 in
/// docs/session-sets/harvester-finish-canonical-golden-output/spec.md):
/// the same normalization that produces the committed corpus is the
/// normalization Session 4's regression test runs against re-generated
/// output, so this class is the single source of truth.
///
/// Empirical scope (drift survey on 2026-05-18, two consecutive runs of
/// the default pipeline against HomeHealthCare.accdb and
/// NorthwindDevEdition.accdb): the ONLY field that drifted across runs
/// was manifest.json -> Database.ExportedAt. HarvesterVersion and
/// Database.FilePath are added preemptively because they vary across
/// commits and hosts respectively. No per-object IR file (Forms, Reports,
/// Queries, Tables, Relationships, Macros, ModulesAndClasses) showed any
/// drift, and unresolved-items.json was a stable empty array, so
/// NormalizePerObjectIr and NormalizeUnresolvedItems carry no field-level
/// substitutions today. Session 2 of
/// harvester-report-baseline-golden-corpus extended the same contract to
/// reports-baseline/: baseline-manifest.json normalizes the database path
/// plus HarvesterVersion, and per-report aggregates.json normalizes only
/// Provenance.HarvesterVersion.
/// </summary>
public static class GoldenNormalizer
{
    public const string ExportedAtPlaceholder = "<NORMALIZED:Database.ExportedAt>";
    public const string FilePathPlaceholder = "<NORMALIZED:Database.FilePath>";
    public const string DatabasePathPlaceholder = "<NORMALIZED:ReportBaseline.Database>";
    public const string HarvesterVersionPlaceholder = "<NORMALIZED:HarvesterVersion>";

    // Match Pipeline.cs EmitManifestArtifacts so re-serialized output is
    // byte-comparable with the production pipeline's output.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Replace the three volatile envelope fields with stable
    /// placeholders and re-emit with the same JSON options Pipeline.Run()
    /// uses, so the result is deterministic given the same manifest.
    /// </summary>
    public static string NormalizeManifest(string json)
    {
        var manifest = ParseObject(json, "Manifest");

        if (manifest["HarvesterVersion"] is not null)
            manifest["HarvesterVersion"] = HarvesterVersionPlaceholder;

        if (manifest["Database"] is JsonObject database)
        {
            if (database["FilePath"] is not null)
                database["FilePath"] = FilePathPlaceholder;
            if (database["ExportedAt"] is not null)
                database["ExportedAt"] = ExportedAtPlaceholder;
        }

        return manifest.ToJsonString(SerializerOptions);
    }

    /// <summary>
    /// Normalize the report-baseline manifest by replacing the absolute
    /// database path and HarvesterVersion with stable placeholders.
    /// </summary>
    public static string NormalizeReportBaselineManifest(string json)
    {
        var manifest = ParseObject(json, "Report-baseline manifest");

        if (manifest["Database"] is not null)
            manifest["Database"] = DatabasePathPlaceholder;
        if (manifest["HarvesterVersion"] is not null)
            manifest["HarvesterVersion"] = HarvesterVersionPlaceholder;

        return manifest.ToJsonString(SerializerOptions);
    }

    /// <summary>
    /// Parse and re-emit the top-level unresolved-items.json (a bare
    /// UnresolvedItem[] array, NOT a manifest envelope) so the committed
    /// corpus and the test-regenerated output use identical whitespace
    /// conventions. No field-level substitutions today; extend if drift
    /// surfaces.
    /// </summary>
    public static string NormalizeUnresolvedItems(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var root = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("UnresolvedItems JSON parsed to null.");

        if (root is not JsonArray)
            throw new InvalidOperationException(
                "UnresolvedItems JSON root is not an array; expected a bare UnresolvedItem[].");

        return root.ToJsonString(SerializerOptions);
    }

    /// <summary>
    /// Normalize per-report aggregates.json by replacing only the
    /// provenance HarvesterVersion field. The row counts, totals,
    /// resolved SQL, and derived default parameters are part of the
    /// deterministic regression contract and remain byte-compared.
    /// </summary>
    public static string NormalizeReportAggregates(string json)
    {
        var aggregates = ParseObject(json, "Report-baseline aggregates");

        if (aggregates["Provenance"] is JsonObject provenance
            && provenance["HarvesterVersion"] is not null)
        {
            provenance["HarvesterVersion"] = HarvesterVersionPlaceholder;
        }

        return aggregates.ToJsonString(SerializerOptions);
    }

    /// <summary>
    /// Per-object IR normalization. Default V1 is a pure pass-through:
    /// the drift survey on 2026-05-18 found no volatile fields in any
    /// per-object IR file across Forms/Reports/Queries/Tables/
    /// Relationships/Macros/ModulesAndClasses. Extend the body in-place
    /// (and bump this comment with the discovery context) if Session 3's
    /// golden surfaces drift later.
    /// </summary>
    public static string NormalizePerObjectIr(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return json;
    }

    private static JsonObject ParseObject(string json, string description)
    {
        ArgumentNullException.ThrowIfNull(json);

        var root = JsonNode.Parse(json)
            ?? throw new InvalidOperationException($"{description} JSON parsed to null.");

        if (root is not JsonObject obj)
            throw new InvalidOperationException($"{description} JSON root is not an object.");

        return obj;
    }
}
