using System.Collections.Generic;
using AccessHarvester.SymbolDb.Models;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;

using Declaration = AccessHarvester.Vba.Resolver.Declarations.Declaration;
using ProcedureDeclaration = AccessHarvester.Vba.Resolver.Declarations.ProcedureDeclaration;
using DeclareDeclaration = AccessHarvester.Vba.Resolver.Declarations.DeclareDeclaration;
using EnumDeclaration = AccessHarvester.Vba.Resolver.Declarations.EnumDeclaration;
using EnumMemberDeclaration = AccessHarvester.Vba.Resolver.Declarations.EnumMemberDeclaration;
using VariableDeclaration = AccessHarvester.Vba.Resolver.Declarations.VariableDeclaration;
using ParameterDeclaration = AccessHarvester.Vba.Resolver.Declarations.ParameterDeclaration;
using PropertyDeclaration = AccessHarvester.Vba.Resolver.Declarations.PropertyDeclaration;

namespace AccessHarvester.SymbolDb.Extraction;

// Walks the ResolvedModule.References list for each module and produces
// ReferenceRow and MemberAccessRow lists for Session 2. Caller-kind
// annotation uses the naming-shape heuristic from EntryPointHeuristic:
// procedures in Form/Report modules whose names contain '_' are
// 'ui-event-handler'; all others are 'non-ui'.
internal static class ReferencesExtractor
{
    internal static (IReadOnlyList<ReferenceRow> References, IReadOnlyList<MemberAccessRow> MemberAccesses)
        Extract(IReadOnlyList<ResolvedModule> modules)
    {
        var refs = new List<ReferenceRow>();
        var accesses = new List<MemberAccessRow>();

        foreach (var module in modules)
        {
            foreach (var rr in module.References)
            {
                string fromFqn = FqnResolver.ContainingFqn(rr.EnclosingScope);
                string? callerKind = CallerKind(rr.EnclosingScope);
                string identifier = IdentifierText(rr.Expression);
                string kind = ClassifyKind(rr.Expression, rr.Outcome);
                string resolutionStatus = StatusText(rr.Outcome);
                string? resolutionReason = rr.Outcome is ResolutionOutcome.Unresolved u
                    ? u.Reason : null;
                string? toFqn = ToFqn(rr.Outcome);
                int? sourceLine = rr.Location.Start.Line;
                int? sourceColumn = rr.Location.Start.Column;

                refs.Add(new ReferenceRow(
                    FromFqn: fromFqn,
                    ToFqn: toFqn,
                    Identifier: identifier,
                    Kind: kind,
                    CallerKind: callerKind,
                    SourceLine: sourceLine,
                    SourceColumn: sourceColumn,
                    ResolutionStatus: resolutionStatus,
                    ResolutionReason: resolutionReason));

                // MemberAccesses: specialised rows for dot- and bang-access expressions.
                if (TryBuildMemberAccessRow(rr, fromFqn, out var maRow))
                    accesses.Add(maRow!);
            }
        }

        return (refs, accesses);
    }

    // ────────────────────────────────────────────────────────────────────────
    // caller_kind

    private static string? CallerKind(Scope scope)
    {
        Scope? current = scope;
        while (current is not null)
        {
            if (current is ProcedureScope ps && current.Parent is ModuleScope ms)
            {
                bool isFormOrReport = ms.Kind == ModuleKind.Form || ms.Kind == ModuleKind.Report;
                if (isFormOrReport && ps.Procedure.Name.Contains('_'))
                    return "ui-event-handler";
                return "non-ui";
            }
            current = current.Parent;
        }
        return null; // module-level reference outside any procedure
    }

    // ────────────────────────────────────────────────────────────────────────
    // identifier text

    private static string IdentifierText(Expression expression) => expression switch
    {
        IdentifierExpression id => id.Name,
        MeExpression _ => "Me",
        MemberAccessExpression ma => ma.MemberName,
        BangAccessExpression ba => ba.MemberName,
        ImplicitWithMemberExpression iw => iw.MemberName,
        ImplicitWithBangExpression ib => ib.MemberName,
        _ => "<expr>",
    };

    // ────────────────────────────────────────────────────────────────────────
    // kind classification

    private static string ClassifyKind(Expression expression, ResolutionOutcome outcome)
    {
        // Expression shape takes priority for structural kinds.
        switch (expression)
        {
            case BangAccessExpression _:
            case ImplicitWithBangExpression _:
                return "control_access";
            case MemberAccessExpression _:
            case ImplicitWithMemberExpression _:
                return "member_access";
            case MeExpression _:
                return "variable_read";
        }

        // IdentifierExpression — dispatch on what it resolved to.
        Declaration? decl = DeclarationFromOutcome(outcome);
        return decl switch
        {
            ProcedureDeclaration _ => "call",
            DeclareDeclaration _ => "call",
            EnumMemberDeclaration _ => "enum_value_use",
            EnumDeclaration _ => "enum_value_use",
            VariableDeclaration _ => "variable_read",
            ParameterDeclaration _ => "variable_read",
            _ when outcome is ResolutionOutcome.ResolvedToBuiltIn => "call",
            _ when outcome is ResolutionOutcome.ResolvedToObjectModel => "call",
            _ when outcome is ResolutionOutcome.ResolvedToMe => "variable_read",
            _ => "call", // conservative fallback for unresolved names
        };
    }

    // ────────────────────────────────────────────────────────────────────────
    // resolution_status

    private static string StatusText(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Resolved _ => "resolved",
        ResolutionOutcome.ResolvedToObjectModel _ => "resolved-to-object-model",
        ResolutionOutcome.ResolvedToBuiltIn _ => "resolved-to-built-in",
        ResolutionOutcome.ResolvedToMe _ => "resolved-to-me",
        ResolutionOutcome.ResolvedViaWithFlattening _ => "resolved-via-with-flattening",
        ResolutionOutcome.Unresolved _ => "unresolved",
        _ => "unresolved",
    };

    // ────────────────────────────────────────────────────────────────────────
    // to_fqn

    private static string? ToFqn(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Resolved r => FqnResolver.DeclarationFqn(r.Declaration),
        ResolutionOutcome.ResolvedToObjectModel _ => null, // object-model synthetics are not in Symbols
        ResolutionOutcome.ResolvedToBuiltIn _ => null,
        ResolutionOutcome.ResolvedToMe _ => null,
        ResolutionOutcome.ResolvedViaWithFlattening f => ToFqn(f.InnerOutcome),
        ResolutionOutcome.Unresolved _ => null,
        _ => null,
    };

    // ────────────────────────────────────────────────────────────────────────
    // MemberAccessRow construction

    private static bool TryBuildMemberAccessRow(
        ResolvedReference rr,
        string fromFqn,
        out MemberAccessRow? row)
    {
        string? resolvedTargetFqn = ToFqn(rr.Outcome);
        int? sourceLine = rr.Location.Start.Line;

        switch (rr.Expression)
        {
            case MemberAccessExpression ma:
                row = new MemberAccessRow(
                    FromFqn: fromFqn,
                    Receiver: ReceiverText(ma.Target),
                    Path: ma.MemberName,
                    ResolvedTargetFqn: resolvedTargetFqn,
                    SourceLine: sourceLine);
                return true;

            case BangAccessExpression ba:
                row = new MemberAccessRow(
                    FromFqn: fromFqn,
                    Receiver: ReceiverText(ba.Target),
                    Path: "!" + ba.MemberName,
                    ResolvedTargetFqn: resolvedTargetFqn,
                    SourceLine: sourceLine);
                return true;

            case ImplicitWithMemberExpression _:
            case ImplicitWithBangExpression _:
                // Implicit-with: extract receiver from the synthesized target when available.
                string? receiverFromWith = null;
                string path;
                if (rr.Outcome is ResolutionOutcome.ResolvedViaWithFlattening f)
                {
                    switch (f.SynthesizedTarget)
                    {
                        case MemberAccessExpression sma:
                            receiverFromWith = ReceiverText(sma.Target);
                            path = sma.MemberName;
                            break;
                        case BangAccessExpression sba:
                            receiverFromWith = ReceiverText(sba.Target);
                            path = "!" + sba.MemberName;
                            break;
                        default:
                            receiverFromWith = "<with-block>";
                            path = rr.Expression is ImplicitWithBangExpression ib2
                                ? "!" + ib2.MemberName
                                : ((ImplicitWithMemberExpression)rr.Expression).MemberName;
                            break;
                    }
                }
                else
                {
                    receiverFromWith = "<with-block>";
                    path = rr.Expression is ImplicitWithBangExpression ib3
                        ? "!" + ib3.MemberName
                        : ((ImplicitWithMemberExpression)rr.Expression).MemberName;
                }
                row = new MemberAccessRow(
                    FromFqn: fromFqn,
                    Receiver: receiverFromWith,
                    Path: path,
                    ResolvedTargetFqn: resolvedTargetFqn,
                    SourceLine: sourceLine);
                return true;

            default:
                row = null;
                return false;
        }
    }

    private static string ReceiverText(Expression target) => target switch
    {
        MeExpression _ => "Me",
        IdentifierExpression id => id.Name,
        MemberAccessExpression ma => ma.MemberName,
        BangAccessExpression ba => ba.MemberName,
        _ => "<expr>",
    };

    // ────────────────────────────────────────────────────────────────────────
    // Shared helper: collapse outcome to its Declaration (mirrors ResolvedModule)

    private static Declaration? DeclarationFromOutcome(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Resolved r => r.Declaration,
        ResolutionOutcome.ResolvedToObjectModel r => r.Declaration,
        ResolutionOutcome.ResolvedViaWithFlattening f => DeclarationFromOutcome(f.InnerOutcome),
        _ => null,
    };
}
