using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Types;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.Types;

// Strict unit tests of the confidence-vote algorithm. Synthetic
// TypeEvidence lists exercise each branch of session-2-type-inference.md
// §6 without standing up a ResolvedModule. Per project-guidance.md →
// Testing Conventions ("Factor pure-logic portions out... so they can
// be strict-unit-tested").
public sealed class ConfidenceVoteTests
{
    private static readonly SourceRange Loc = new(
        new SourceLocation("", 1, 1),
        new SourceLocation("", 1, 2));

    private static TypeEvidence Ev(TypeEvidenceKind kind, string typeName)
        => new(kind, typeName, Loc);

    [Fact]
    public void EmptyEvidence_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ConfidenceVote.Compute(Array.Empty<TypeEvidence>()));
    }

    [Fact]
    public void NullEvidence_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ConfidenceVote.Compute(null!));
    }

    [Fact]
    public void TypeSuffix_Dominates_OtherEvidence()
    {
        // String suffix on a declaration that also receives a numeric
        // assignment. The suffix wins; the numeric evidence is preserved
        // so a consumer can see the conflict.
        var evidence = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.TypeSuffix, "String"),
            Ev(TypeEvidenceKind.Literal, "Long"),
        };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal("String", result.TypeName);
        Assert.Equal(InferenceConfidence.High, result.Confidence);
        Assert.Equal(2, result.Evidence.Count);
    }

    [Fact]
    public void AllSameType_IsHigh()
    {
        var evidence = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "String"),
        };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal("String", result.TypeName);
        Assert.Equal(InferenceConfidence.High, result.Confidence);
    }

    [Fact]
    public void SingleEvidence_IsHigh()
    {
        var evidence = new[] { Ev(TypeEvidenceKind.Literal, "Long") };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal("Long", result.TypeName);
        Assert.Equal(InferenceConfidence.High, result.Confidence);
    }

    [Fact]
    public void TwoOfThree_IsMedium()
    {
        // 2 String / 1 Long → strict majority.
        var evidence = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "Long"),
        };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal("String", result.TypeName);
        Assert.Equal(InferenceConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void TwoOfFour_IsLow_FirstSeenWins()
    {
        // 2 String / 1 Long / 1 Double — exactly half is not a strict
        // majority. The most-frequent name (String, with first-seen
        // tie-break) is recorded with Low confidence.
        var evidence = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "Long"),
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "Double"),
        };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal(InferenceConfidence.Low, result.Confidence);
        Assert.Equal("String", result.TypeName);
    }

    [Fact]
    public void ThreeWayTie_IsLow_FirstSeenWins()
    {
        // 1 String / 1 Long / 1 Double — three-way tie; first-seen wins.
        var evidence = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "Long"),
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "Double"),
        };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal(InferenceConfidence.Low, result.Confidence);
        Assert.Equal("Long", result.TypeName);
    }

    [Fact]
    public void FourOfSeven_IsMedium()
    {
        // 4 String / 2 Long / 1 Double — 4 > 7/2, strict majority.
        var evidence = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "Long"),
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "Long"),
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "Double"),
            Ev(TypeEvidenceKind.Literal, "String"),
        };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal("String", result.TypeName);
        Assert.Equal(InferenceConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void CaseInsensitive_GroupsTheSameTypeName()
    {
        // "string" and "String" must collapse — VBA naming is ordinal
        // case-insensitive.
        var evidence = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "String"),
            Ev(TypeEvidenceKind.Literal, "string"),
            Ev(TypeEvidenceKind.Literal, "Long"),
        };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal("String", result.TypeName);
        Assert.Equal(InferenceConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void TypeSuffix_PreservesCasingFromSuffixEvidence()
    {
        var evidence = new[] { Ev(TypeEvidenceKind.TypeSuffix, "Currency") };
        var result = ConfidenceVote.Compute(evidence);
        Assert.Equal("Currency", result.TypeName);
        Assert.Equal(InferenceConfidence.High, result.Confidence);
    }

    [Fact]
    public void InferredType_Evidence_IsImmutable()
    {
        // The pass freezes the input evidence list before storing it
        // on InferredType. A consumer must not be able to cast back to
        // List<T> and mutate. Round-1 verifier finding.
        var input = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "Long"),
        };
        var result = ConfidenceVote.Compute(input);
        Assert.IsNotType<List<TypeEvidence>>(result.Evidence);
    }

    [Fact]
    public void InferredType_Evidence_IsIndependentOfInputList()
    {
        // Mutating the caller's list after Compute returns must not
        // affect the InferredType.Evidence snapshot. Defensive copy
        // contract per session-2-type-inference.md §11.
        var input = new List<TypeEvidence>
        {
            Ev(TypeEvidenceKind.Literal, "Long"),
        };
        var result = ConfidenceVote.Compute(input);
        input.Add(Ev(TypeEvidenceKind.Literal, "String"));
        Assert.Single(result.Evidence);
        Assert.Equal("Long", result.Evidence[0].TypeName);
    }
}
