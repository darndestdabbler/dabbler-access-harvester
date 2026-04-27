namespace AccessHarvester.Vba.Resolver.References;

// One curated VBA built-in function entry. Lives in
// BuiltInFunctionRegistry's static dictionary. Sessions later than #8
// may extend the registry by adding more entries, but this record's
// shape (Name + Category + ReturnType) is stable.
//
// Returned by BuiltInFunctionRegistry.TryGet when an identifier
// reference falls through scope-walking and Access-model registry
// fallbacks. Wrapped in ResolvedToBuiltIn at the resolver layer.
public sealed record BuiltInFunction(
    // Source-spelled canonical name (e.g., "Format", "IIf", "Nz"). The
    // registry keys are stored case-insensitively per VBA semantics, but
    // the Name property preserves the canonical casing for downstream
    // consumers that round-trip back to source.
    string Name,
    BuiltInCategory Category,
    // VBA type spelling of the function's return value, or null when the
    // return type is polymorphic / context-dependent (e.g., IIf returns
    // either branch's type, Nz returns the first non-null operand's
    // type). Null is the explicit "polymorphic" signal — consumers
    // should treat it as Variant.
    string? ReturnType);

// Coarse grouping of the curated built-in list. Mirrors the taxonomy
// in MS-VBAL's "Standard Module Members" section. Categories are not
// intrinsic VBA concepts; they exist so downstream documentation
// tooling can group built-ins in human-friendly sections.
public enum BuiltInCategory
{
    String,
    Math,
    DateTime,
    Information,
    Conversion,
    IO,
    Interaction,
    // Members of the Access application object model that read like
    // bare-name uses in source (DoCmd, DLookup, DCount, CurrentDb,
    // CurrentProject, etc.). Recognized in the built-in registry so
    // they resolve cleanly even without #11's full Access registry
    // available.
    AccessSpecific,
}
