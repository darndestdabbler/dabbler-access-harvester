using AccessHarvester.Sql.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Sql.Parser.FormReferences;

// Annotation produced by FormReferenceResolver for one FormReference node
// in a SQL AST. Carries the original AST node, the reconstructed source
// text, and the resolution outcome. Annotations are emitted in tree-walk
// order; consumers that need lookup by node identity can index by
// reference equality on Node.
public sealed record FormReferenceAnnotation(
    FormReference Node,
    FormReferenceResolution Resolution);

// Resolution outcome. Sealed record hierarchy mirrors the AST style:
// callers `switch (anno.Resolution)` to discriminate.
public abstract record FormReferenceResolution(
    string OriginalText,
    SourceRange? Location);

// The reference resolved fully. ResolvedTarget is the terminal
// declaration (the last segment in the chain); TargetKind classifies it
// for downstream rendering.
public sealed record ResolvedFormReference(
    string OriginalText,
    SourceRange? Location,
    Declaration ResolvedTarget,
    FormReferenceTargetKind TargetKind)
    : FormReferenceResolution(OriginalText, Location);

// The reference did not resolve. Reason is one of the codes in
// `UnresolvedReason`. The annotation is emitted whether the failure
// happened at the form-name level or deeper in a property chain — the
// reason code distinguishes the two.
public sealed record UnresolvedFormReference(
    string OriginalText,
    SourceRange? Location,
    string Reason)
    : FormReferenceResolution(OriginalText, Location);
