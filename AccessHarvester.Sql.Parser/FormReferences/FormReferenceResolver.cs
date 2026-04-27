using System;
using System.Collections.Generic;
using System.Text;
using AccessHarvester.Sql.Parser.Ast;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;

namespace AccessHarvester.Sql.Parser.FormReferences;

// Resolves form/report-reference expressions in a SQL AST against the
// harvester's IAccessObjectModelRegistry. The visitor walks the tree
// once, gathers every FormReference node via FormReferenceCollector,
// and then resolves each reference's segment chain through the same
// registry the VBA resolver uses (per the spec: "Detection is
// structural; resolution is via the same symbol table the VBA resolver
// uses, not a separate registry.").
//
// Each annotation carries the original AST node, the reconstructed
// source text (for diagnostics and `expected.json` parity), the source
// range, and either a resolved Declaration or an UnresolvedFormReference
// with a stable reason code.
//
// Stateless — instances exist only to satisfy callers that want
// dependency-injection style; the static Resolve(tree, registry)
// overload is the convenience entry point.
public sealed class FormReferenceResolver
{
    private readonly IAccessObjectModelRegistry _registry;

    public FormReferenceResolver(IAccessObjectModelRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    // Walk `tree`, resolve every FormReference encountered, and return
    // the annotations in tree-walk order.
    public IReadOnlyList<FormReferenceAnnotation> Resolve(SqlSyntaxNode tree)
    {
        if (tree is null) throw new ArgumentNullException(nameof(tree));
        var collector = new FormReferenceCollector();
        collector.Visit(tree);
        var annotations = new List<FormReferenceAnnotation>(collector.Collected.Count);
        foreach (var node in collector.Collected)
            annotations.Add(ResolveOne(node));
        return annotations;
    }

    // Convenience overload for callers that have a registry already.
    public static IReadOnlyList<FormReferenceAnnotation> Resolve(
        SqlSyntaxNode tree, IAccessObjectModelRegistry registry)
        => new FormReferenceResolver(registry).Resolve(tree);

    // Resolve a single FormReference against the registry. Public so
    // callers that already have a target node (e.g., from a previous
    // walk) can resolve it without re-walking.
    public FormReferenceAnnotation ResolveOne(FormReference node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));

        var originalText = ReconstructOriginalText(node);
        var location = node.Provenance.Range;

        var rootName = node.Root is ReportsRoot ? "Reports" : "Forms";
        var collection = _registry.TryResolveTopLevelMember(rootName);
        if (collection is null)
        {
            // Real registries always return the host collection; only
            // NullAccessObjectModelRegistry returns null here. Surface
            // the case rather than silently treating it as
            // form-not-in-metadata.
            return new FormReferenceAnnotation(
                node,
                new UnresolvedFormReference(
                    originalText, location,
                    UnresolvedReason.HostCollectionNotRegistered));
        }

        if (node.Segments.Count == 0)
        {
            // Bare `[Forms]` / `[Reports]` reference. The collection
            // itself is not a navigation target for a SQL parameter or
            // expression; surface explicitly.
            return new FormReferenceAnnotation(
                node,
                new UnresolvedFormReference(
                    originalText, location,
                    UnresolvedReason.BareCollectionReference));
        }

        Declaration? current = collection;
        for (int i = 0; i < node.Segments.Count; i++)
        {
            var segment = node.Segments[i];
            var op = node.Operators[i];
            var next = ResolveSegment(current!, segment.Name, op);
            if (next is null)
            {
                return new FormReferenceAnnotation(
                    node,
                    new UnresolvedFormReference(
                        originalText, location,
                        ChooseUnresolvedReason(node, i, op)));
            }
            current = next;
        }

        return new FormReferenceAnnotation(
            node,
            new ResolvedFormReference(
                originalText, location,
                current!,
                ClassifyTarget(current!)));
    }

    // Operator-aware single-segment resolution. JET / VBA distinguish
    // `!` (default-member access — on Forms/Reports collections this means
    // the named form/report; on a Form/Report this means the named
    // control) from `.` (explicit member access against the type
    // surface). The shared IAccessObjectModelRegistry surface is operator-
    // agnostic and prefers the type-library member set before falling back
    // to controls — correct for `.`, wrong for `!` because a control whose
    // name shadows an Access.Form/Access.Report property (`Name`,
    // `Visible`, `Caption`, etc.) would otherwise mis-resolve to the
    // type-library member. Strict control lookup for the `!`-on-Form/Report
    // case below preserves JET semantics; everything else delegates to the
    // registry's standard chain.
    private Declaration? ResolveSegment(
        Declaration parent, string name, RefAccessOperator op)
    {
        if (op == RefAccessOperator.Bang)
        {
            if (parent is SyntheticFormDeclaration form)
                return form.Controls.TryGetValue(name, out var hit) ? hit : null;
            if (parent is SyntheticReportDeclaration report)
                return report.Controls.TryGetValue(name, out var hit) ? hit : null;
        }
        return _registry.TryResolveMemberOf(parent, name);
    }

    // Map the segment index + operator at the failure site to the most
    // informative reason code. The first segment is always the form/report
    // name (its operator from the host collection is semantically Bang
    // even when the spec writer's source text would not bracket it).
    // Segment 1 distinguishes `!` (control-not-on-{form,report}) from
    // `.` (member-not-found, because the writer asked for a type-library
    // property, not a control). Segments 2+ are always
    // `member-not-found-at-segment-N` — deep `!` chains are vanishingly
    // rare in JET SQL and there is no useful sub-classification.
    private static string ChooseUnresolvedReason(
        FormReference node, int failedAtIndex, RefAccessOperator op)
    {
        bool isReports = node.Root is ReportsRoot;
        if (failedAtIndex == 0)
        {
            return isReports
                ? UnresolvedReason.ReportNotInMetadata
                : UnresolvedReason.FormNotInMetadata;
        }
        if (failedAtIndex == 1 && op == RefAccessOperator.Bang)
        {
            return isReports
                ? UnresolvedReason.ControlNotOnReport
                : UnresolvedReason.ControlNotOnForm;
        }
        return UnresolvedReason.MemberNotFoundAtSegment(failedAtIndex);
    }

    private static FormReferenceTargetKind ClassifyTarget(Declaration decl) => decl switch
    {
        SyntheticFormsCollectionDeclaration   => FormReferenceTargetKind.HostCollection,
        SyntheticFormDeclaration              => FormReferenceTargetKind.Form,
        SyntheticReportDeclaration            => FormReferenceTargetKind.Report,
        SyntheticControlDeclaration           => FormReferenceTargetKind.Control,
        SyntheticTypeLibraryMemberDeclaration => FormReferenceTargetKind.TypeLibraryMember,
        // Defensive default: any future synthetic Declaration subtype the
        // registry might return. Resolved-as-Control is the closest
        // category for unknown leaf members; the consumer can re-classify
        // by inspecting ResolvedTarget directly if it cares.
        _ => FormReferenceTargetKind.Control,
    };

    // Reconstruct a canonical surface form for the FormReference. Used
    // only for diagnostics and `expected.json` text — not a full
    // round-trip serializer (the AST does not record original spacing
    // or casing variants of `Forms`/`Reports` that bypassed the
    // bracketed form). Output shape: `[Root]<op><seg>...` where each
    // segment is bracketed if `IsBracketed` was true at parse time.
    internal static string ReconstructOriginalText(FormReference n)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(n.Root is ReportsRoot ? "Reports" : "Forms").Append(']');
        for (int i = 0; i < n.Segments.Count; i++)
        {
            sb.Append(n.Operators[i] == RefAccessOperator.Bang ? '!' : '.');
            var seg = n.Segments[i];
            if (seg.IsBracketed) sb.Append('[').Append(seg.Name).Append(']');
            else sb.Append(seg.Name);
        }
        return sb.ToString();
    }
}
