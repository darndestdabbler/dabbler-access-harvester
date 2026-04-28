using System;
using System.Collections.Generic;
using System.Text;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Pure-function classifier that walks a parsed VBA event-handler
/// procedure body and produces an <see cref="EventHandlerClassification"/>.
/// </summary>
/// <remarks>
/// <para>
/// The classifier is deterministic given the same input AST. Patterns
/// that do not deterministically fall into one of the seven Phase 6.3
/// categories (helper-procedure calls, <c>MsgBox</c>, <c>SetFocus</c>,
/// arithmetic-only inside conditions) emit no signal, per
/// <c>project-guidance.md</c> -&gt; VBA Parser Strategy: "Unresolvable
/// constructs are explicit, not silent" — but explicit silence is
/// preferable to a guess. Adding new categories or refining the
/// recognized patterns is the natural place for downstream sessions
/// to extend.
/// </para>
/// <para>
/// The classifier is shape-driven, not name-driven beyond a curated
/// member-name allowlist. Most decisions look at the *last* member of
/// an assignment-target chain or call-target chain — the same shape
/// that dictates the Access semantic — rather than at full identifier
/// resolution, because the resolver does not run before this pipeline
/// stage.
/// </para>
/// </remarks>
public static class EventHandlerClassifier
{
    private static readonly HashSet<string> FilterSearchProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Filter", "FilterOn", "RecordSource", "OrderBy", "OrderByOn",
        "ServerFilter", "ServerFilterOn",
    };

    private static readonly HashSet<string> AppearanceProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Visible", "Enabled", "Caption", "ForeColor", "BackColor", "BorderColor",
        "FontBold", "FontItalic", "FontUnderline", "FontSize", "FontName", "FontWeight",
        "BorderStyle", "BorderWidth", "SpecialEffect", "Locked", "BackStyle",
    };

    private static readonly HashSet<string> DoCmdNavigationMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "OpenForm", "OpenReport", "OpenQuery", "OpenTable", "OpenModule",
        "Close", "RunMacro", "RunCommand",
        "GoToRecord", "GoToControl", "NavigateTo",
        "ShowAllRecords", "FindRecord", "FindNext", "ApplyFilter",
    };

    /// <summary>
    /// Classify a single event-handler procedure. Returns an
    /// <see cref="EventHandlerClassification"/> with the procedure's
    /// name, the deduplicated category list (in order of first
    /// appearance), the mixed flag, and the full signal evidence.
    /// </summary>
    public static EventHandlerClassification Classify(ProcedureDeclaration procedure)
    {
        if (procedure is null) throw new ArgumentNullException(nameof(procedure));

        var signals = new List<EventHandlerClassificationSignal>();
        Visit(procedure.Body, signals);

        var categories = new List<EventHandlerClassificationCategory>();
        foreach (var s in signals)
        {
            if (!categories.Contains(s.Category))
                categories.Add(s.Category);
        }

        return new EventHandlerClassification
        {
            ProcedureName = procedure.Name,
            Categories = categories,
            Mixed = categories.Count > 1,
            Signals = signals,
        };
    }

    // ── Statement walker ───────────────────────────────────────────────

    private static void Visit(IReadOnlyList<Statement> statements, List<EventHandlerClassificationSignal> signals)
    {
        foreach (var stmt in statements)
            Visit(stmt, signals);
    }

    private static void Visit(Statement stmt, List<EventHandlerClassificationSignal> signals)
    {
        switch (stmt)
        {
            case LetStatement let:
                ClassifyAssignment(let.Target, let.Provenance, signals);
                break;
            case SetStatement set:
                ClassifyAssignment(set.Target, set.Provenance, signals);
                break;
            case CallStatement call:
                ClassifyCall(call.Target, call.Provenance, signals);
                break;
            case IfStatement ifs:
                Visit(ifs.ThenBody, signals);
                foreach (var elif in ifs.ElseIfClauses)
                    Visit(elif.Body, signals);
                if (ifs.ElseBody is not null)
                    Visit(ifs.ElseBody, signals);
                break;
            case ForStatement fs:
                Visit(fs.Body, signals);
                break;
            case ForEachStatement fes:
                Visit(fes.Body, signals);
                break;
            case DoLoopStatement dls:
                Visit(dls.Body, signals);
                break;
            case WhileWendStatement wws:
                Visit(wws.Body, signals);
                break;
            case SelectCaseStatement scs:
                foreach (var c in scs.Cases)
                    Visit(c.Body, signals);
                if (scs.CaseElseBody is not null)
                    Visit(scs.CaseElseBody, signals);
                break;
            case WithStatement ws:
                Visit(ws.Body, signals);
                break;
            // Other Statement variants (OnError, Resume, Goto, Exit, Stop, End,
            // RaiseEvent, Local declarations, Redim, Label, Preprocessor,
            // Unknown) carry no classification-relevant signals at this tier.
        }
    }

    // ── Assignment classification ──────────────────────────────────────

    private static void ClassifyAssignment(Expression target, Provenance prov, List<EventHandlerClassificationSignal> signals)
    {
        // Strip any IndexOrCall postfix on the target so a chain like
        // `MyArray(0) = ...` classifies on the underlying chain.
        var bare = StripIndexOrCall(target);

        if (bare is IdentifierExpression ident)
        {
            if (string.Equals(ident.Name, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new EventHandlerClassificationSignal
                {
                    Category = EventHandlerClassificationCategory.Validation,
                    Kind = EventHandlerSignalKind.CancelAssignment,
                    Line = prov.Line,
                    Column = prov.Column,
                    TargetExpression = ident.Name,
                });
                return;
            }
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.Calculation,
                Kind = EventHandlerSignalKind.BareVariableAssignment,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = ident.Name,
            });
            return;
        }

        var lastMember = LastMemberName(bare);
        if (lastMember is not null)
        {
            if (FilterSearchProperties.Contains(lastMember))
            {
                signals.Add(new EventHandlerClassificationSignal
                {
                    Category = EventHandlerClassificationCategory.FilterSearch,
                    Kind = EventHandlerSignalKind.FilterAssignment,
                    Line = prov.Line,
                    Column = prov.Column,
                    TargetExpression = StringifyTarget(target),
                });
                return;
            }
            if (AppearanceProperties.Contains(lastMember))
            {
                signals.Add(new EventHandlerClassificationSignal
                {
                    Category = EventHandlerClassificationCategory.FormattingDisplay,
                    Kind = EventHandlerSignalKind.AppearanceAssignment,
                    Line = prov.Line,
                    Column = prov.Column,
                    TargetExpression = StringifyTarget(target),
                });
                return;
            }
        }

        // Default for any member-access assignment: treat as cascading
        // update. This catches `Me!ctl.RowSource = ...`, `Me.txtFoo = ...`,
        // `Forms!frmFoo!ctl = ...`, and similar. Cosmetic and filter
        // assignments are handled above.
        signals.Add(new EventHandlerClassificationSignal
        {
            Category = EventHandlerClassificationCategory.CascadingUpdate,
            Kind = EventHandlerSignalKind.ControlAssignment,
            Line = prov.Line,
            Column = prov.Column,
            TargetExpression = StringifyTarget(target),
        });
    }

    // ── Call classification ────────────────────────────────────────────

    private static void ClassifyCall(Expression target, Provenance prov, List<EventHandlerClassificationSignal> signals)
    {
        var (root, members) = WalkChain(target);

        // DoCmd.<member>(...)
        if (root is IdentifierExpression rootIdent
            && rootIdent.Name.Equals("DoCmd", StringComparison.OrdinalIgnoreCase)
            && members.Count >= 1
            && DoCmdNavigationMembers.Contains(members[0]))
        {
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.Navigation,
                Kind = EventHandlerSignalKind.DoCmdNavigationCall,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = $"DoCmd.{members[0]}",
            });
            return;
        }

        // Application.Quit  or bare Quit
        if (root is IdentifierExpression appIdent
            && appIdent.Name.Equals("Application", StringComparison.OrdinalIgnoreCase)
            && members.Count >= 1
            && members[0].Equals("Quit", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.Navigation,
                Kind = EventHandlerSignalKind.ApplicationQuitCall,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = "Application.Quit",
            });
            return;
        }
        if (root is IdentifierExpression bareQuit
            && bareQuit.Name.Equals("Quit", StringComparison.OrdinalIgnoreCase)
            && members.Count == 0)
        {
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.Navigation,
                Kind = EventHandlerSignalKind.ApplicationQuitCall,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = "Quit",
            });
            return;
        }

        // Bare FollowHyperlink
        if (root is IdentifierExpression fhId
            && fhId.Name.Equals("FollowHyperlink", StringComparison.OrdinalIgnoreCase)
            && members.Count == 0)
        {
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.Navigation,
                Kind = EventHandlerSignalKind.FollowHyperlinkCall,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = "FollowHyperlink",
            });
            return;
        }

        // Bare RunCommand
        if (root is IdentifierExpression rcId
            && rcId.Name.Equals("RunCommand", StringComparison.OrdinalIgnoreCase)
            && members.Count == 0)
        {
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.Navigation,
                Kind = EventHandlerSignalKind.RunCommandCall,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = "RunCommand",
            });
            return;
        }

        // .Requery (form / subform / control distinction by chain shape)
        if (members.Count >= 1 && members[^1].Equals("Requery", StringComparison.OrdinalIgnoreCase))
        {
            // Inside a `With X` block, `.Requery` carries an implicit
            // receiver this classifier cannot deterministically resolve
            // into form vs. subform vs. control. Per
            // project-guidance.md -> VBA Parser Strategy ("Unresolvable
            // constructs are explicit, not silent"), prefer to emit no
            // signal rather than guess at user intent. A future session
            // that threads With-target through the visitor (or runs
            // post-resolver) can promote this to a definite category.
            if (root is ImplicitWithMemberExpression or ImplicitWithBangExpression)
                return;

            // Me.Requery or bare-Me Requery
            if (root is MeExpression && members.Count == 1)
            {
                signals.Add(new EventHandlerClassificationSignal
                {
                    Category = EventHandlerClassificationCategory.FilterSearch,
                    Kind = EventHandlerSignalKind.FormRequeryCall,
                    Line = prov.Line,
                    Column = prov.Column,
                    TargetExpression = "Me.Requery",
                });
                return;
            }
            // ...Form.Requery (subform refresh)
            if (members.Count >= 2 && members[^2].Equals("Form", StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(new EventHandlerClassificationSignal
                {
                    Category = EventHandlerClassificationCategory.FilterSearch,
                    Kind = EventHandlerSignalKind.SubformRequeryCall,
                    Line = prov.Line,
                    Column = prov.Column,
                    TargetExpression = StringifyTarget(target),
                });
                return;
            }
            // Otherwise: control-level Requery (Me!ctl.Requery, Me.ctl.Requery)
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.CascadingUpdate,
                Kind = EventHandlerSignalKind.ControlRequeryCall,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = StringifyTarget(target),
            });
            return;
        }

        // .Execute on any receiver (db, CurrentDb, conn, recordset)
        if (members.Count >= 1 && members[^1].Equals("Execute", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add(new EventHandlerClassificationSignal
            {
                Category = EventHandlerClassificationCategory.SideEffect,
                Kind = EventHandlerSignalKind.DatabaseExecuteCall,
                Line = prov.Line,
                Column = prov.Column,
                TargetExpression = StringifyTarget(target),
            });
            return;
        }

        // Other calls (helper procedures, MsgBox, SetFocus, ...) carry no
        // deterministic classification — silence is explicit.
    }

    // ── Chain helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Walk an expression target down through any IndexOrCall postfix
    /// and member-access / bang-access chain. Returns the deepest
    /// receiver (an identifier, <c>Me</c>, an implicit-with, etc.) and
    /// the ordered list of member names from receiver toward the last
    /// member. <see cref="ParenthesizedExpression"/> is transparent.
    /// </summary>
    private static (Expression Root, List<string> Members) WalkChain(Expression e)
    {
        var members = new List<string>();
        e = StripIndexOrCall(e);

        while (true)
        {
            switch (e)
            {
                case MemberAccessExpression ma:
                    members.Insert(0, ma.MemberName);
                    e = StripIndexOrCall(ma.Target);
                    continue;
                case BangAccessExpression ba:
                    members.Insert(0, ba.MemberName);
                    e = StripIndexOrCall(ba.Target);
                    continue;
                case ImplicitWithMemberExpression iwm:
                    // .Foo inside `With X` — the implicit receiver is the
                    // With target, which the resolver knows but this
                    // classifier does not. Surface the member name so
                    // property-based classification still works; callers
                    // distinguish implicit-with by checking the root type.
                    members.Insert(0, iwm.MemberName);
                    return (iwm, members);
                case ImplicitWithBangExpression iwb:
                    members.Insert(0, iwb.MemberName);
                    return (iwb, members);
                case ParenthesizedExpression pe:
                    e = StripIndexOrCall(pe.Inner);
                    continue;
                default:
                    return (e, members);
            }
        }
    }

    private static Expression StripIndexOrCall(Expression e)
    {
        while (e is IndexOrCallExpression ioc)
            e = ioc.Target;
        return e;
    }

    private static string? LastMemberName(Expression target)
    {
        target = StripIndexOrCall(target);
        return target switch
        {
            MemberAccessExpression ma => ma.MemberName,
            BangAccessExpression ba => ba.MemberName,
            // Inside a `With X` block, `.Foo = value` parses as an
            // ImplicitWithMemberExpression at the assignment-target leaf;
            // surface its MemberName so property-based classification
            // (FilterSearch, FormattingDisplay) works regardless of
            // whether the assignment is written explicitly or under
            // a With block.
            ImplicitWithMemberExpression iwm => iwm.MemberName,
            ImplicitWithBangExpression iwb => iwb.MemberName,
            _ => null,
        };
    }

    /// <summary>
    /// Render an expression target as a readable chain — preserves dot
    /// vs. bang access, drops indexer arguments, and skips
    /// parentheses. Used as the <c>TargetExpression</c> field on each
    /// signal so the verifier and human reviewers can audit the
    /// classification without re-walking the AST.
    /// </summary>
    internal static string StringifyTarget(Expression e)
    {
        var sb = new StringBuilder();
        Render(e, sb);
        return sb.ToString();
    }

    private static void Render(Expression e, StringBuilder sb)
    {
        switch (e)
        {
            case IndexOrCallExpression ioc:
                Render(ioc.Target, sb);
                // Indexer arguments are deliberately stripped — the
                // chain shape is what matters for classification audit.
                break;
            case MemberAccessExpression ma:
                Render(ma.Target, sb);
                sb.Append('.');
                sb.Append(ma.MemberName);
                break;
            case BangAccessExpression ba:
                Render(ba.Target, sb);
                sb.Append('!');
                sb.Append(ba.MemberName);
                break;
            case ParenthesizedExpression pe:
                Render(pe.Inner, sb);
                break;
            case IdentifierExpression ident:
                sb.Append(ident.Name);
                break;
            case MeExpression:
                sb.Append("Me");
                break;
            case ImplicitWithMemberExpression iwm:
                sb.Append('.');
                sb.Append(iwm.MemberName);
                break;
            case ImplicitWithBangExpression iwb:
                sb.Append('!');
                sb.Append(iwb.MemberName);
                break;
            default:
                sb.Append('?');
                break;
        }
    }
}
