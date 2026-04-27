using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.References;

// One resolver-emitted diagnostic. The shape is shared across phases:
// Session 3 emits one diagnostic per Unresolved reference; later phases
// (Phase 3.5 default-member resolution, Phase 3.6 type inference, etc.)
// append additional entries during ResolvedModule construction.
//
// `Code` is the stable contract — downstream AI consumers should
// match on it (e.g., `UnresolvedReasons.NotInScope`). `Message` is a
// short human-readable form whose exact wording is not part of the
// contract.
//
// Per session-3-resolved-module.md §4 the severity mapping for each
// Unresolved reason is closed and lives on ResolvedModule; this record
// only records what the constructor decided.
public sealed record ResolverDiagnostic(
    ResolverDiagnosticSeverity Severity,
    string Code,
    string Message,
    SourceRange Location);

// Closed severity scale per session-3 §4.2.
//
// Info    — deferred-by-design partial resolution that a downstream
//           phase will close out (e.g., default-member-not-yet-resolved,
//           with-block-flattening-deferred). Not a source defect.
// Warning — user-visible defect in the source the resolver can name
//           with confidence (e.g., `not-in-scope`,
//           `me-in-standard-module`).
// Error   — defined but unused by Session 3. Reserved for diagnostics
//           emitted by later phases meaning the resolver itself failed
//           in a way the consumer must surface to a human.
public enum ResolverDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}
