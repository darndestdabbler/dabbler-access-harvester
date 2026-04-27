namespace AccessHarvester.Vba.Resolver.Types;

// Confidence band attached to an inferred type. See
// docs/session-sets/resolver-default-members-and-inference/synthesis/
// session-2-type-inference.md §3.2 and §6 for the algorithm.
//
// High   — every evidence row pointed at the same type, OR a
//          declaration TypeSuffix definitively pinned it. Trustworthy
//          enough for translation without further checking.
// Medium — strict majority agreed but a minority dissented. A hint;
//          downstream consumers should surface as "likely T".
// Low    — no majority. Best-effort. Consumers should not depend on
//          the result.
public enum InferenceConfidence
{
    High,
    Medium,
    Low,
}
