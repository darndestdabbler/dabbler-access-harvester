using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.DefaultMembers;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;

namespace AccessHarvester.Vba.Resolver.Types;

// Lightweight type-inference pass over a ResolvedModule. Phase 3.6 of
// the parser-migration plan. Annotates Variant-typed (or untyped)
// declarations with a likely concrete type plus a confidence score,
// without mutating the original Declaration.
//
// Algorithm and stability commitments documented in
// docs/session-sets/resolver-default-members-and-inference/synthesis/
// session-2-type-inference.md. Session 4 (cross-cut + mechanical
// tightening) is responsible for any resolver-side wiring that
// consumes the resulting map; this pass exposes the data and stops.
public static class TypeInferencePass
{
    public static InferredTypeMap Run(ResolvedModule resolved)
    {
        if (resolved is null) throw new ArgumentNullException(nameof(resolved));

        // 1. Collect candidate declarations from the entire scope tree.
        var candidates = new HashSet<Declaration>(ReferenceEqualityComparer.Instance);
        CollectCandidates(resolved.ModuleScope, candidates);

        // 2. Seed evidence with TypeSuffix entries from declaration sites.
        var evidenceByDecl = new Dictionary<Declaration, List<TypeEvidence>>(
            ReferenceEqualityComparer.Instance);
        foreach (var candidate in candidates)
        {
            evidenceByDecl[candidate] = new List<TypeEvidence>();
            if (TypeSuffixEvidence(candidate) is { } suffixEvidence)
                evidenceByDecl[candidate].Add(suffixEvidence);
        }

        // 3. Walk the AST collecting assignment-RHS evidence.
        var walker = new AssignmentEvidenceWalker(resolved, evidenceByDecl);
        walker.Visit(resolved.Ast);

        // 4. Compute confidence and build the result map. Candidates
        //    that gathered zero evidence get no entry — see synthesis §6.
        var result = new Dictionary<Declaration, InferredType>(
            ReferenceEqualityComparer.Instance);
        foreach (var pair in evidenceByDecl)
        {
            if (pair.Value.Count == 0) continue;
            result[pair.Key] = ConfidenceVote.Compute(pair.Value);
        }

        return new InferredTypeMap(result);
    }

    // ── Candidate discovery ───────────────────────────────────────

    private static void CollectCandidates(Scope scope, HashSet<Declaration> sink)
    {
        foreach (var declaration in scope.Declarations)
        {
            if (IsCandidate(declaration))
                sink.Add(declaration);
        }
        foreach (var child in scope.Children)
            CollectCandidates(child, sink);
    }

    private static bool IsCandidate(Declaration declaration) => declaration switch
    {
        // ConstantDeclaration is a subclass of VariableDeclaration but
        // is excluded — constants carry their declared or
        // literal-derived type and are inferred elsewhere; this pass is
        // about value-bearing variables that may carry different types
        // over time.
        ConstantDeclaration => false,
        VariableDeclaration v => IsVariantOrUntyped(v.Type),
        ImplicitDeclaration i => IsVariantOrUntyped(i.Type),
        _ => false,
    };

    private static bool IsVariantOrUntyped(TypeReference? type)
    {
        if (type is null) return true;
        if (type.QualifiedName.Count == 0) return true;
        var leaf = type.QualifiedName[type.QualifiedName.Count - 1];
        return Declaration.NameComparer.Equals(leaf, "Variant");
    }

    // ── TypeSuffix-derived evidence ───────────────────────────────

    private static TypeEvidence? TypeSuffixEvidence(Declaration declaration)
    {
        var (suffix, range) = declaration switch
        {
            VariableDeclaration v => (v.TypeSuffix, v.Provenance.Range),
            ImplicitDeclaration i => (i.TypeSuffix, i.Provenance.Range),
            _ => (null, null),
        };
        if (suffix is null) return null;
        var typeName = TypeFromSuffix(suffix);
        if (typeName is null) return null;
        if (range is null) return null;
        return new TypeEvidence(TypeEvidenceKind.TypeSuffix, typeName, range);
    }

    private static string? TypeFromSuffix(string? suffix) => suffix switch
    {
        "$" => "String",
        "%" => "Integer",
        "&" => "Long",
        "!" => "Single",
        "#" => "Double",
        "@" => "Currency",
        _ => null,
    };

    // ── Assignment walker ─────────────────────────────────────────

    private sealed class AssignmentEvidenceWalker : VbaSyntaxWalker
    {
        private readonly ResolvedModule _resolved;
        private readonly Dictionary<Declaration, List<TypeEvidence>> _evidenceByDecl;

        public AssignmentEvidenceWalker(
            ResolvedModule resolved,
            Dictionary<Declaration, List<TypeEvidence>> evidenceByDecl)
        {
            _resolved = resolved;
            _evidenceByDecl = evidenceByDecl;
        }

        public override void VisitLetStatement(LetStatement n)
        {
            CollectFromAssignment(n.Target, n.Value);
            DefaultVisit(n);
        }

        public override void VisitSetStatement(SetStatement n)
        {
            CollectFromAssignment(n.Target, n.Value);
            DefaultVisit(n);
        }

        private void CollectFromAssignment(Expression target, Expression rhs)
        {
            var declaration = _resolved.TryGetDeclaration(target);
            if (declaration is null) return;
            if (!_evidenceByDecl.TryGetValue(declaration, out var bucket)) return;

            var evidence = EvaluateRhs(rhs);
            if (evidence is not null) bucket.Add(evidence);
        }

        // Returns evidence produced by treating `rhs` as a
        // value-or-object-yielding expression. Returns null when the
        // expression's type is genuinely indeterminate (Null/Nothing/
        // Empty literals, polymorphic built-ins, unresolved names,
        // arithmetic operators not modeled by this pass).
        private TypeEvidence? EvaluateRhs(Expression rhs)
        {
            switch (rhs)
            {
                case ParenthesizedExpression p:
                    return EvaluateRhs(p.Inner);

                case LiteralExpression lit:
                {
                    var typeName = TypeFromLiteralKind(lit.Kind);
                    if (typeName is null) return null;
                    var range = lit.Provenance.Range;
                    if (range is null) return null;
                    return new TypeEvidence(TypeEvidenceKind.Literal, typeName, range);
                }

                case TrueExpression t:
                    return RangeEvidence(TypeEvidenceKind.Literal, "Boolean", t.Provenance.Range);
                case FalseExpression f:
                    return RangeEvidence(TypeEvidenceKind.Literal, "Boolean", f.Provenance.Range);

                case NewExpression ne:
                {
                    var leaf = LeafOf(ne.Type);
                    if (leaf is null) return null;
                    return RangeEvidence(TypeEvidenceKind.NewExpression, leaf, ne.Provenance.Range);
                }

                case IndexOrCallExpression call:
                    return EvaluateCall(call);

                case IdentifierExpression:
                case MemberAccessExpression:
                case BangAccessExpression:
                case ImplicitWithMemberExpression:
                case ImplicitWithBangExpression:
                case MeExpression:
                    return EvaluateNameReference(rhs);

                // Null/Nothing/Empty/Debug/Unknown/Binary/Unary/AddressOf/
                // TypeOf — no evidence. The pass deliberately does not
                // model arithmetic-type promotion; keeping the surface
                // narrow here means a future session that wants
                // `Long + Long -> Long` evidence can add a single arm
                // without reshaping this method.
                default:
                    return null;
            }
        }

        private TypeEvidence? EvaluateCall(IndexOrCallExpression call)
        {
            // The IndexOrCall node itself does not carry a
            // ResolvedReference (the resolver only emits one for the
            // target's name). Evaluate by inspecting the target's
            // outcome.
            var targetReference = _resolved.TryGetReference(call.Target);
            if (targetReference is null) return null;

            var range = call.Provenance.Range;
            if (range is null) return null;

            // Peek through Session 2 / #8 With-block flattening so a
            // typed call inside `With ...` (e.g.,
            // `Set v = .OpenChild()`) contributes evidence the same
            // way it does outside a With. The flattening wrapper is
            // structurally bounded to one level (session-2 §3
            // invariant), so a single unwrap is sufficient. Round-1
            // verifier finding.
            var outcome = targetReference.Outcome;
            while (outcome is ResolutionOutcome.ResolvedViaWithFlattening flattened)
                outcome = flattened.InnerOutcome;

            switch (outcome)
            {
                case ResolutionOutcome.ResolvedToBuiltIn rb when ConcreteName(rb.Function.ReturnType) is { } rt:
                    return new TypeEvidence(TypeEvidenceKind.BuiltInReturnType, rt, range);

                case ResolutionOutcome.Resolved r when ReturnTypeOf(r.Declaration) is { } rt:
                    return new TypeEvidence(TypeEvidenceKind.ProcedureReturnType, rt, range);

                case ResolutionOutcome.ResolvedToObjectModel rom when ReturnTypeOf(rom.Declaration) is { } rt:
                    return new TypeEvidence(TypeEvidenceKind.ProcedureReturnType, rt, range);

                case ResolutionOutcome.Resolved r when DefaultMemberReturnFor(r.Declaration) is { } dm:
                    return new TypeEvidence(TypeEvidenceKind.DefaultMemberReturnType, dm, range);

                case ResolutionOutcome.ResolvedToObjectModel rom when DefaultMemberReturnFor(rom.Declaration) is { } dm:
                    return new TypeEvidence(TypeEvidenceKind.DefaultMemberReturnType, dm, range);

                default:
                    return null;
            }
        }

        private TypeEvidence? EvaluateNameReference(Expression rhs)
        {
            var reference = _resolved.TryGetReference(rhs);
            if (reference is null) return null;
            return EvaluateReferenceOutcome(reference.Outcome, reference.Location);
        }

        // Evaluate any ResolutionOutcome to evidence. Procedure-shaped
        // declarations (Function / Property Get / Declare) read as
        // zero-argument calls — their return type is the evidence —
        // because VBA permits `x = Foo` as a call without parens.
        // Value-shaped declarations contribute their declared type.
        // Built-in / object-model / With-flattened cases unwrap
        // accordingly. Round-2 verifier finding (bare-name procedure
        // refs were silently dropped here in round 1).
        private TypeEvidence? EvaluateReferenceOutcome(
            ResolutionOutcome outcome, SourceRange range)
        {
            // Peek through With-flattening (one level deep — the
            // session-2 §3 invariant guarantees no nesting).
            while (outcome is ResolutionOutcome.ResolvedViaWithFlattening f)
                outcome = f.InnerOutcome;

            switch (outcome)
            {
                case ResolutionOutcome.Resolved r:
                    return EvaluateDeclarationOutcome(r.Declaration, range);
                case ResolutionOutcome.ResolvedToObjectModel rom:
                    return EvaluateDeclarationOutcome(rom.Declaration, range);
                case ResolutionOutcome.ResolvedToBuiltIn rb when ConcreteName(rb.Function.ReturnType) is { } rt:
                    return new TypeEvidence(TypeEvidenceKind.BuiltInReturnType, rt, range);
                default:
                    return null;
            }
        }

        private static TypeEvidence? EvaluateDeclarationOutcome(
            Declaration declaration, SourceRange range)
        {
            // Procedure-shaped first: `x = SomeFunc` is a call in VBA,
            // not a function reference, so the function's return type
            // is what we record.
            if (ReturnTypeOf(declaration) is { } returnType)
                return new TypeEvidence(
                    TypeEvidenceKind.ProcedureReturnType, returnType, range);
            // Otherwise the declaration is value-bearing; its declared
            // type is the evidence.
            if (TypeOfBoundDeclaration(declaration) is { } valueType)
                return new TypeEvidence(
                    TypeEvidenceKind.BoundDeclarationType, valueType, range);
            return null;
        }
    }

    // ── Type-extraction helpers (shared across the pass) ──────────

    private static TypeEvidence? RangeEvidence(
        TypeEvidenceKind kind, string typeName, SourceRange? range)
    {
        if (range is null) return null;
        return new TypeEvidence(kind, typeName, range);
    }

    private static string? TypeFromLiteralKind(LiteralKind kind) => kind switch
    {
        LiteralKind.String => "String",
        // VBA's implicit integer-literal type widens to Long in modern
        // dialects; older interpreters used Integer for small literals.
        // We use Long uniformly because it survives the most common
        // arithmetic-widening paths; consumers that need the source
        // spelling can read the literal node directly.
        LiteralKind.Integer => "Long",
        LiteralKind.Hex => "Long",
        LiteralKind.Octal => "Long",
        LiteralKind.Float => "Double",
        LiteralKind.Date => "Date",
        _ => null,
    };

    private static string? LeafOf(TypeReference type)
    {
        if (type.QualifiedName.Count == 0) return null;
        return type.QualifiedName[type.QualifiedName.Count - 1];
    }

    private static string? TypeOfBoundDeclaration(Declaration declaration)
    {
        // Source declarations may be typed via either the `As` clause
        // (`Type` is non-null) or a typed-identifier suffix
        // (`Type` is null but `TypeSuffix` is `$`/`%`/`&`/`!`/`#`/`@`).
        // VBA treats the suffix as semantically equivalent to the
        // matching `As` clause, so the inference pass falls back to
        // it when `Type` is missing. Round-1 verifier finding.
        var raw = declaration switch
        {
            VariableDeclaration v => LeafOrNull(v.Type) ?? TypeFromSuffix(v.TypeSuffix),
            ImplicitDeclaration i => LeafOrNull(i.Type) ?? TypeFromSuffix(i.TypeSuffix),
            ParameterDeclaration p => LeafOrNull(p.Type) ?? TypeFromSuffix(p.TypeSuffix),
            _ => null,
        };
        return ConcreteName(raw);
    }

    private static string? ReturnTypeOf(Declaration declaration)
    {
        var raw = declaration switch
        {
            FunctionDeclaration f => LeafOrNull(f.ReturnType),
            // Property Get is the only property kind that returns a value;
            // Let / Set are setters with no return type.
            PropertyDeclaration p when p.Kind == PropertyKind.Get => LeafOrNull(p.ReturnType),
            DeclareDeclaration d => LeafOrNull(d.ReturnType),
            _ => null,
        };
        return ConcreteName(raw);
    }

    private static string? DefaultMemberReturnFor(Declaration declaration)
    {
        // Only typed value-bearing declarations participate. The
        // default-member registry is consulted for the declaration's
        // declared type; only parameterized entries contribute (the
        // canonical form is `rs("X")` resolving through Recordset's
        // parameterized .Fields default).
        var typeLeaf = TypeOfBoundDeclaration(declaration);
        if (typeLeaf is null) return null;
        var entry = DefaultMemberRegistry.TryGet(typeLeaf);
        if (entry is null || !entry.IsParameterized) return null;
        return ConcreteName(entry.ReturnType);
    }

    // Filter "Variant" out of evidence collection: a Variant return
    // tells us nothing useful when the candidate we're inferring is
    // already Variant — the row would just dilute the confidence vote
    // with non-information. Genuine "we know it's Variant" facts come
    // from the original declaration, which the inference pass leaves
    // intact. Round-2 verifier finding. The synthesis doc's earlier
    // note that "Variant means we know it" is superseded by this
    // guard; the practical signal is "we know more than Variant" or
    // "no concrete information."
    private static string? ConcreteName(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        if (Declaration.NameComparer.Equals(typeName, "Variant")) return null;
        return typeName;
    }

    private static string? LeafOrNull(TypeReference? type)
    {
        if (type is null) return null;
        if (type.QualifiedName.Count == 0) return null;
        var leaf = type.QualifiedName[type.QualifiedName.Count - 1];
        return string.IsNullOrEmpty(leaf) ? null : leaf;
    }
}
