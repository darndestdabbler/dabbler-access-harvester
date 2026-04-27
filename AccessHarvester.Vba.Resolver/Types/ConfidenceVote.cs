using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.Types;

// Pure-logic helper: turn a list of TypeEvidence rows into the final
// (TypeName, Confidence) pair. Factored out so the algorithm is
// strict-unit-testable per project-guidance.md → Testing Conventions
// without standing up a full ResolvedModule fixture.
//
// Public so consumers that hand-assemble evidence (debug tooling,
// downstream enrichment that adds its own evidence to a candidate)
// can produce a consistent (TypeName, Confidence) using the same
// algorithm TypeInferencePass.Run uses internally.
//
// Algorithm matches session-2-type-inference.md §6 verbatim. The two
// callers — TypeInferencePass.Run and the ConfidenceVoteTests — must
// see identical behavior.
public static class ConfidenceVote
{
    public static InferredType Compute(IReadOnlyList<TypeEvidence> evidence)
    {
        if (evidence is null) throw new ArgumentNullException(nameof(evidence));
        if (evidence.Count == 0)
            throw new ArgumentException(
                "ConfidenceVote.Compute requires at least one evidence row; "
                + "the caller should skip emission for zero-evidence candidates.",
                nameof(evidence));

        // Defensive freeze. The caller may pass a mutable List<T>; the
        // InferredType.Evidence property is a public contract documented
        // as immutable (session-2-type-inference.md §3.3, §11). Without
        // freezing here, a consumer could cast Evidence back to List<T>
        // and mutate. Round-1 verifier finding.
        var frozen = new ReadOnlyCollection<TypeEvidence>(
            new List<TypeEvidence>(evidence));

        // Definitive case: a TypeSuffix evidence pins the type at the
        // language level. The first TypeSuffix wins (a declaration
        // syntactically carries at most one suffix anyway). Other
        // evidence rows are still preserved on InferredType.Evidence
        // so a consumer can see the conflict.
        foreach (var ev in frozen)
        {
            if (ev.Kind == TypeEvidenceKind.TypeSuffix)
                return new InferredType(ev.TypeName, InferenceConfidence.High, frozen);
        }

        // Vote on TypeName, case-insensitively. Order of first-seen is
        // tracked so ties break deterministically by source order.
        var counts = new Dictionary<string, int>(Declaration.NameComparer);
        var firstSeen = new Dictionary<string, int>(Declaration.NameComparer);
        var orderedNames = new List<string>();
        for (int i = 0; i < frozen.Count; i++)
        {
            var name = frozen[i].TypeName;
            if (counts.TryGetValue(name, out var c))
            {
                counts[name] = c + 1;
            }
            else
            {
                counts[name] = 1;
                firstSeen[name] = i;
                orderedNames.Add(name);
            }
        }

        // Pick the most-frequent name, ties broken by first-seen index.
        string dominantName = orderedNames[0];
        int dominantCount = counts[dominantName];
        foreach (var name in orderedNames)
        {
            int count = counts[name];
            if (count > dominantCount
                || (count == dominantCount && firstSeen[name] < firstSeen[dominantName]))
            {
                dominantName = name;
                dominantCount = count;
            }
        }

        InferenceConfidence confidence;
        if (counts.Count == 1)
        {
            // Every row agreed.
            confidence = InferenceConfidence.High;
        }
        else if (dominantCount * 2 > frozen.Count)
        {
            // Strict majority — more than half. (2-of-3 = 6/3 > 3 ✓;
            // 2-of-4 = 8/4 = 4, not > 4, so falls through to Low.)
            confidence = InferenceConfidence.Medium;
        }
        else
        {
            confidence = InferenceConfidence.Low;
        }

        return new InferredType(dominantName, confidence, frozen);
    }
}
