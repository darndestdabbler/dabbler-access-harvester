using System.Collections.Generic;
using System.Collections.Immutable;

namespace AccessHarvester.Vba.Parser.Ast;

// Typed projection of the canonical VBA module-header block that
// Access SaveAsText / YAML-mirror exports place at the top of
// form/report/class-module source. Populated by
// `VbaModuleBodyLoader.Load` and attached to `Module.Header` by
// `VbaModuleBodyLoader.LoadAndParse`. Null on `Module.Header` means
// either the source carried no header or the source was parsed via
// `VbaParserPipeline.Parse` directly (the raw escape hatch).
//
// Recognized fields are typed; any `Attribute VB_<Other>` line the
// recognized-fields list does not cover lands in
// `AdditionalAttributes` (sorted by key, Ordinal) so the
// downstream-AI consumer can still reach it.
public sealed record ModuleHeaderMetadata
{
    // From `VERSION 1.0 CLASS` — major / minor are the two halves of
    // the version number. Null when the source carries the
    // Attribute-only header shape (the dominant case in this repo's
    // YAML mirrors).
    public int? VersionMajor { get; init; }
    public int? VersionMinor { get; init; }

    // From `MultiUse = -1` inside the `BEGIN ... END` block. Access
    // emits `-1` for True and `0` for False; the loader normalizes
    // both to bool.
    public bool? MultiUse { get; init; }

    // `Attribute VB_Name = "<...>"`. Recorded as metadata only; the
    // YAML-mirror filename remains authoritative for object-graph
    // identity.
    public string? VbName { get; init; }

    public bool? VbGlobalNameSpace { get; init; }
    public bool? VbCreatable { get; init; }
    public bool? VbPredeclaredId { get; init; }
    public bool? VbExposed { get; init; }

    public IReadOnlyDictionary<string, string> AdditionalAttributes { get; init; }
        = ImmutableSortedDictionary<string, string>.Empty;
}
