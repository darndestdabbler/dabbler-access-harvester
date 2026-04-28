using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using AccessHarvester.Access;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Pure-function extractor that produces structured
/// <see cref="ValidationRule"/> records from table metadata, form metadata,
/// and parsed <c>BeforeUpdate</c> event-handler procedures. Implements
/// Phase 6.4 of <c>docs/VBA-Parser-Migration-Harvester-Plan.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The extractor is deterministic given the same input. Validation-rule
/// expressions the extractor cannot structurally interpret (a JET
/// expression that does not match the curated <c>Range</c> /
/// <c>Pattern</c> / <c>CustomPredicate</c> shapes, a BeforeUpdate handler
/// whose top-level structure is not an <c>If &lt;cond&gt; Then Cancel = True</c>
/// pattern) are emitted as
/// <see cref="ValidationRuleKind.Unstructured"/> with the verbatim source
/// preserved on <see cref="ValidationRule.Expression"/>. This follows the
/// <c>project-guidance.md</c> -&gt; VBA Parser Strategy rule that
/// "unresolvable constructs are explicit, not silent".
/// </para>
/// <para>
/// The three entry points are independent — callers can use any subset:
/// <list type="bullet">
///   <item><see cref="ExtractFromTable"/> for table-side rules
///   (<c>TableField.ValidationRule</c>, <c>TableField.Required</c>,
///   table-level <c>ValidationRule</c>).</item>
///   <item><see cref="ExtractFromForm"/> for form / control rules from
///   the design-time properties bag (<c>ValidationRule</c>,
///   <c>Required</c>, <c>LimitToList</c>, <c>InputMask</c>) and the
///   form-level rule.</item>
///   <item><see cref="ExtractFromBeforeUpdateHandler"/> for parsed VBA
///   patterns inside a single <c>BeforeUpdate</c> handler.</item>
/// </list>
/// A pipeline visitor stitching all three into one
/// <see cref="FormComponentSpec"/>-aligned output is a Session 4
/// deliverable — this file owns only the per-source extraction primitives.
/// </para>
/// </remarks>
public static class ValidationRuleExtractor
{
    // ── Table-side extraction ──────────────────────────────────────────

    /// <summary>
    /// Extract every validation rule visible on a single table — both
    /// per-field <c>Required</c> / <c>ValidationRule</c> entries and the
    /// table-level record-validation rule. The order is: per-field rules
    /// in <see cref="TableMetadata.Fields"/> order (each field's
    /// <c>Required</c> rule before its <c>ValidationRule</c>), then the
    /// table-level rule last.
    /// </summary>
    public static IReadOnlyList<ValidationRule> ExtractFromTable(TableMetadata table)
    {
        if (table is null) throw new ArgumentNullException(nameof(table));

        var rules = new List<ValidationRule>();

        if (table.Fields is not null)
        {
            foreach (var field in table.Fields)
            {
                if (field.Required == true)
                {
                    rules.Add(new ValidationRule
                    {
                        Scope = ValidationRuleScope.TableField,
                        SourceObject = table.Name,
                        FieldOrControlName = field.Name,
                        Origin = ValidationRuleOrigin.TableFieldRequired,
                        Kind = ValidationRuleKind.Required,
                    });
                }

                if (!string.IsNullOrEmpty(field.ValidationRule))
                {
                    rules.Add(BuildExpressionRule(
                        scope: ValidationRuleScope.TableField,
                        sourceObject: table.Name,
                        fieldOrControlName: field.Name,
                        origin: ValidationRuleOrigin.TableFieldValidationRule,
                        expression: field.ValidationRule!,
                        message: field.ValidationText));
                }
            }
        }

        if (!string.IsNullOrEmpty(table.ValidationRule))
        {
            rules.Add(BuildExpressionRule(
                scope: ValidationRuleScope.Table,
                sourceObject: table.Name,
                fieldOrControlName: null,
                origin: ValidationRuleOrigin.TableValidationRule,
                expression: table.ValidationRule!,
                message: table.ValidationText));
        }

        return rules;
    }

    // ── Form / control extraction ──────────────────────────────────────

    /// <summary>
    /// Extract every validation rule visible on a single form — per-control
    /// <c>Required</c> / <c>ValidationRule</c> / <c>LimitToList</c> /
    /// <c>InputMask</c> entries plus the form record-level rule. Walks the
    /// design tree's controls in source order (controls with no properties
    /// bag are skipped). Form-level rule last.
    /// </summary>
    public static IReadOnlyList<ValidationRule> ExtractFromForm(FormMetadata form)
    {
        if (form is null) throw new ArgumentNullException(nameof(form));

        var rules = new List<ValidationRule>();

        if (form.Children is not null)
        {
            foreach (var child in form.Children)
                VisitControls(child, form.Name, rules);
        }

        AddFormLevelRule(form, rules);

        return rules;
    }

    private static void VisitControls(AccessControl node, string formName, List<ValidationRule> rules)
    {
        AddControlLevelRules(node, formName, rules);
        if (node.Children is not null)
        {
            foreach (var child in node.Children)
                VisitControls(child, formName, rules);
        }
    }

    private static void AddControlLevelRules(AccessControl node, string formName, List<ValidationRule> rules)
    {
        var props = node.Properties;
        if (props is null || string.IsNullOrEmpty(node.Name)) return;

        if (ReadBool(props, "Required") == true)
        {
            rules.Add(new ValidationRule
            {
                Scope = ValidationRuleScope.Control,
                SourceObject = formName,
                FieldOrControlName = node.Name,
                Origin = ValidationRuleOrigin.ControlRequired,
                Kind = ValidationRuleKind.Required,
            });
        }

        var rule = ReadString(props, "ValidationRule");
        if (!string.IsNullOrEmpty(rule))
        {
            rules.Add(BuildExpressionRule(
                scope: ValidationRuleScope.Control,
                sourceObject: formName,
                fieldOrControlName: node.Name,
                origin: ValidationRuleOrigin.ControlValidationRule,
                expression: rule!,
                message: ReadString(props, "ValidationText")));
        }

        if (ReadBool(props, "LimitToList") == true)
        {
            rules.Add(new ValidationRule
            {
                Scope = ValidationRuleScope.Control,
                SourceObject = formName,
                FieldOrControlName = node.Name,
                Origin = ValidationRuleOrigin.ControlLimitToList,
                Kind = ValidationRuleKind.Membership,
                Membership = new ValidationMembershipConstraint
                {
                    SourceControlName = node.Name,
                },
            });
        }

        var mask = ReadString(props, "InputMask");
        if (!string.IsNullOrEmpty(mask))
        {
            rules.Add(new ValidationRule
            {
                Scope = ValidationRuleScope.Control,
                SourceObject = formName,
                FieldOrControlName = node.Name,
                Origin = ValidationRuleOrigin.ControlInputMask,
                Kind = ValidationRuleKind.Pattern,
                Expression = mask,
                Pattern = new ValidationPatternConstraint
                {
                    InputMask = mask!,
                },
            });
        }
    }

    private static void AddFormLevelRule(FormMetadata form, List<ValidationRule> rules)
    {
        var props = form.Properties;
        if (props is null) return;

        var rule = ReadString(props, "ValidationRule");
        if (string.IsNullOrEmpty(rule)) return;

        rules.Add(BuildExpressionRule(
            scope: ValidationRuleScope.Form,
            sourceObject: form.Name,
            fieldOrControlName: null,
            origin: ValidationRuleOrigin.FormValidationRule,
            expression: rule!,
            message: ReadString(props, "ValidationText")));
    }

    // ── BeforeUpdate handler extraction ────────────────────────────────

    /// <summary>
    /// Walk a <c>BeforeUpdate</c> event-handler procedure and emit one
    /// <see cref="ValidationRule"/> per <c>If &lt;cond&gt; Then Cancel = True</c>
    /// pattern (also recognised inside <c>ElseIf</c> branches at the same
    /// nesting level). The <paramref name="hostFormName"/> and the
    /// optional <paramref name="hostControlName"/> attribute the rule
    /// back to the originating scope:
    /// <list type="bullet">
    ///   <item><c>Form_BeforeUpdate</c> -&gt; pass the form name only;
    ///   the rule's <see cref="ValidationRule.Scope"/> is
    ///   <see cref="ValidationRuleScope.Form"/>.</item>
    ///   <item><c>txtAge_BeforeUpdate</c> -&gt; pass both the form name and
    ///   the control name; the rule's <see cref="ValidationRule.Scope"/>
    ///   is <see cref="ValidationRuleScope.Control"/>.</item>
    /// </list>
    /// Each emitted rule's <see cref="ValidationRule.FieldOrControlName"/>
    /// names the field whose value the condition guards (extracted from
    /// the condition itself, e.g., <c>IsNull(Me.txtCustomerName)</c> -&gt;
    /// <c>txtCustomerName</c>) when the guard is structurally a
    /// per-field check. For multi-field conditions or pure record-level
    /// guards, that slot is null and the rule reads as a record-level
    /// custom predicate.
    /// </summary>
    public static IReadOnlyList<ValidationRule> ExtractFromBeforeUpdateHandler(
        ProcedureDeclaration procedure,
        string hostFormName,
        string? hostControlName = null)
    {
        if (procedure is null) throw new ArgumentNullException(nameof(procedure));
        if (hostFormName is null) throw new ArgumentNullException(nameof(hostFormName));

        var rules = new List<ValidationRule>();
        VisitForCancel(procedure.Body, procedure.Name, hostFormName, hostControlName, rules);
        return rules;
    }

    // Walk the body looking for `If <cond> Then ... Cancel = True ... End If`
    // patterns. Per Phase 6.4 the recognised shape is the top-level guard
    // pattern; nested constructs (a Cancel inside a For loop, inside a
    // helper Sub) emit nothing — the classifier already flags them as
    // Validation, but they do not yield a structured rule without
    // additional context.
    private static void VisitForCancel(
        IReadOnlyList<Statement> statements,
        string procedureName,
        string hostFormName,
        string? hostControlName,
        List<ValidationRule> rules)
    {
        foreach (var stmt in statements)
        {
            if (stmt is IfStatement ifs)
            {
                if (ContainsCancelTrue(ifs.ThenBody))
                {
                    rules.Add(BuildBeforeUpdateRule(
                        ifs.Condition, ifs.ThenBody, procedureName, hostFormName, hostControlName));
                }
                foreach (var elif in ifs.ElseIfClauses)
                {
                    if (ContainsCancelTrue(elif.Body))
                    {
                        rules.Add(BuildBeforeUpdateRule(
                            elif.Condition, elif.Body, procedureName, hostFormName, hostControlName));
                    }
                }
                // Else without an If condition isn't a guard — skip.
            }
        }
    }

    private static bool ContainsCancelTrue(IReadOnlyList<Statement> body)
    {
        foreach (var s in body)
        {
            if (s is LetStatement let
                && let.Target is IdentifierExpression ident
                && ident.Name.Equals("Cancel", StringComparison.OrdinalIgnoreCase)
                && IsTrueLiteral(let.Value))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTrueLiteral(Expression e) => e is TrueExpression;

    private static ValidationRule BuildBeforeUpdateRule(
        Expression condition,
        IReadOnlyList<Statement> thenBody,
        string procedureName,
        string hostFormName,
        string? hostControlName)
    {
        var (kind, fieldName, range) = ClassifyBeforeUpdateCondition(condition);
        var location = new ValidationRuleLocation
        {
            Line = condition.Provenance.Line,
            Column = condition.Provenance.Column,
            ProcedureName = procedureName,
        };

        // Scope: a per-control BeforeUpdate handler emits Control-scope
        // rules anchored on its control. A form-level BeforeUpdate
        // emits Form-scope rules; the recognised IsNull-of-field guards
        // attribute back to the field they tested via FieldOrControlName.
        var scope = hostControlName is not null
            ? ValidationRuleScope.Control
            : ValidationRuleScope.Form;

        return new ValidationRule
        {
            Scope = scope,
            SourceObject = hostFormName,
            FieldOrControlName = hostControlName ?? fieldName,
            Origin = ValidationRuleOrigin.BeforeUpdateHandler,
            Kind = kind,
            Expression = RenderCondition(condition),
            Message = ExtractMsgBoxLiteral(thenBody),
            Range = range,
            Location = location,
        };
    }

    // Identify the structural shape of a BeforeUpdate guard condition:
    // - IsNull(<field>) [Or <field> = ""]   -> Required, on <field>
    // - everything else                     -> CustomPredicate
    //
    // Note on Range-from-comparison: a BeforeUpdate guard
    // `If <cond> Then Cancel = True` is the *rejection* predicate, not
    // the acceptance predicate. The implicit required rule is
    // Not(<cond>). For single-sided comparisons the negation yields a
    // single contiguous range (e.g., `Me.txtAge < 18` -> required is
    // `Age >= 18`, expressible as Range{Min=18, MinInclusive=true}).
    // Compound `>=...And...<=` guards negate to a *disjunction* of two
    // half-ranges (e.g., `Age < 18 Or Age > 65`) that cannot fit a
    // single ValidationRangeConstraint, so they cannot be folded
    // uniformly. Until a downstream consumer wants the partial
    // negation-aware extraction, the verbatim Expression slot
    // preserves all the information needed to do the inference
    // downstream — explicit silence over a guess.
    private static (ValidationRuleKind Kind, string? FieldName, ValidationRangeConstraint? Range)
        ClassifyBeforeUpdateCondition(Expression condition)
    {
        // IsNull(<field>) shape — bare or paired with `Or <field> = ""`.
        if (TryMatchRequiredGuard(condition, out var fieldName))
            return (ValidationRuleKind.Required, fieldName, null);

        return (ValidationRuleKind.CustomPredicate, null, null);
    }

    private static bool TryMatchRequiredGuard(Expression condition, out string? fieldName)
    {
        fieldName = null;

        if (TryMatchIsNullCall(condition, out fieldName))
            return true;

        // (IsNull(field)) Or (field = "") — guard against a blank text
        // input as well as a null one. Both shapes point at the same field;
        // we report Required for that field.
        if (condition is BinaryExpression { Operator: BinaryOperator.Or } or)
        {
            if (TryMatchIsNullCall(or.Left, out var leftField)
                && TryMatchEmptyStringCompare(or.Right, out var rightField)
                && string.Equals(leftField, rightField, StringComparison.OrdinalIgnoreCase))
            {
                fieldName = leftField;
                return true;
            }
            if (TryMatchIsNullCall(or.Right, out leftField)
                && TryMatchEmptyStringCompare(or.Left, out rightField)
                && string.Equals(leftField, rightField, StringComparison.OrdinalIgnoreCase))
            {
                fieldName = leftField;
                return true;
            }
        }

        return false;
    }

    // IsNull(<field-chain>)  — the call may be parsed as
    // IndexOrCallExpression(IdentifierExpression("IsNull"), [arg]).
    private static bool TryMatchIsNullCall(Expression e, out string? fieldName)
    {
        fieldName = null;
        if (e is not IndexOrCallExpression call) return false;
        if (call.Target is not IdentifierExpression ident) return false;
        if (!ident.Name.Equals("IsNull", StringComparison.OrdinalIgnoreCase)) return false;
        if (call.Arguments.Count != 1) return false;

        var arg = call.Arguments[0];
        if (arg is not PositionalArgument pos) return false;
        fieldName = LastSimpleName(pos.Value);
        return fieldName is not null;
    }

    // <field-chain> = ""  (or "" = <field-chain>)
    private static bool TryMatchEmptyStringCompare(Expression e, out string? fieldName)
    {
        fieldName = null;
        if (e is not BinaryExpression { Operator: BinaryOperator.Equal } eq) return false;

        if (IsEmptyStringLiteral(eq.Right))
        {
            fieldName = LastSimpleName(eq.Left);
            return fieldName is not null;
        }
        if (IsEmptyStringLiteral(eq.Left))
        {
            fieldName = LastSimpleName(eq.Right);
            return fieldName is not null;
        }
        return false;
    }

    private static bool IsEmptyStringLiteral(Expression e) =>
        e is LiteralExpression { Kind: AccessHarvester.Vba.Parser.Ast.LiteralKind.String } lit
        && (lit.Text == "\"\"" || lit.Text == string.Empty);

    // Pull the last identifier out of `Me.foo`, `Me!foo`, `txtFoo`, etc.
    // Returns null for anything that does not look like a field reference
    // (e.g., `Forms!fX!c` resolves to `c` here, which is the right
    // attribution; arithmetic / function calls return null).
    private static string? LastSimpleName(Expression e)
    {
        e = StripIndexOrCallParens(e);
        return e switch
        {
            IdentifierExpression ident => ident.Name,
            MemberAccessExpression ma => ma.MemberName,
            BangAccessExpression ba => ba.MemberName,
            ImplicitWithMemberExpression iwm => iwm.MemberName,
            ImplicitWithBangExpression iwb => iwb.MemberName,
            _ => null,
        };
    }

    private static Expression StripIndexOrCallParens(Expression e)
    {
        while (true)
        {
            switch (e)
            {
                case ParenthesizedExpression pe: e = pe.Inner; continue;
                case IndexOrCallExpression ioc: e = ioc.Target; continue;
                default: return e;
            }
        }
    }

    // Render a VBA condition expression as a readable, single-line string
    // for the rule's verbatim Expression slot. Not a full pretty-printer
    // — just enough to round-trip the patterns the extractor encounters.
    private static string RenderCondition(Expression e)
    {
        var sb = new System.Text.StringBuilder();
        Render(e, sb);
        return sb.ToString();
    }

    private static void Render(Expression e, System.Text.StringBuilder sb)
    {
        switch (e)
        {
            case LiteralExpression lit:
                sb.Append(lit.Text);
                break;
            case TrueExpression: sb.Append("True"); break;
            case FalseExpression: sb.Append("False"); break;
            case NullExpression: sb.Append("Null"); break;
            case NothingExpression: sb.Append("Nothing"); break;
            case EmptyExpression: sb.Append("Empty"); break;
            case MeExpression: sb.Append("Me"); break;
            case IdentifierExpression ident:
                sb.Append(ident.Name);
                break;
            case ParenthesizedExpression pe:
                sb.Append('(');
                Render(pe.Inner, sb);
                sb.Append(')');
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
            case ImplicitWithMemberExpression iwm:
                sb.Append('.');
                sb.Append(iwm.MemberName);
                break;
            case ImplicitWithBangExpression iwb:
                sb.Append('!');
                sb.Append(iwb.MemberName);
                break;
            case IndexOrCallExpression ioc:
                Render(ioc.Target, sb);
                sb.Append('(');
                for (var i = 0; i < ioc.Arguments.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    if (ioc.Arguments[i] is PositionalArgument pos)
                        Render(pos.Value, sb);
                    else
                        sb.Append('?');
                }
                sb.Append(')');
                break;
            case BinaryExpression bin:
                Render(bin.Left, sb);
                sb.Append(' ');
                sb.Append(RenderBinaryOp(bin.Operator));
                sb.Append(' ');
                Render(bin.Right, sb);
                break;
            case UnaryExpression un:
                sb.Append(RenderUnaryOp(un.Operator));
                if (un.Operator == UnaryOperator.Not)
                    sb.Append(' ');
                Render(un.Operand, sb);
                break;
            default:
                sb.Append('?');
                break;
        }
    }

    private static string RenderBinaryOp(BinaryOperator op) => op switch
    {
        BinaryOperator.Power => "^",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.IntDivide => "\\",
        BinaryOperator.Modulo => "Mod",
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Concat => "&",
        BinaryOperator.Equal => "=",
        BinaryOperator.NotEqual => "<>",
        BinaryOperator.Less => "<",
        BinaryOperator.Greater => ">",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.Like => "Like",
        BinaryOperator.Is => "Is",
        BinaryOperator.And => "And",
        BinaryOperator.Or => "Or",
        BinaryOperator.Xor => "Xor",
        BinaryOperator.Eqv => "Eqv",
        BinaryOperator.Imp => "Imp",
        _ => "?",
    };

    private static string RenderUnaryOp(UnaryOperator op) => op switch
    {
        UnaryOperator.Plus => "+",
        UnaryOperator.Minus => "-",
        UnaryOperator.Not => "Not",
        _ => "?",
    };

    // Lift a single MsgBox literal out of the Then-body so it can travel
    // with the rule as the user-visible message. Recognised shape:
    //   MsgBox "literal", ...      (CallStatement / Let / Set positions)
    // Returns null when no MsgBox is present, the first argument is not a
    // string literal, or the body has no recognisable call shape.
    private static string? ExtractMsgBoxLiteral(IReadOnlyList<Statement> body)
    {
        foreach (var s in body)
        {
            if (s is CallStatement call)
            {
                var (target, args) = SplitCall(call);
                if (target is IdentifierExpression ident
                    && ident.Name.Equals("MsgBox", StringComparison.OrdinalIgnoreCase)
                    && args.Count >= 1
                    && args[0] is PositionalArgument pos
                    && pos.Value is LiteralExpression { Kind: AccessHarvester.Vba.Parser.Ast.LiteralKind.String } lit)
                {
                    return UnquoteVbaString(lit.Text);
                }
            }
        }
        return null;
    }

    private static (Expression Target, IReadOnlyList<Argument> Args) SplitCall(CallStatement call)
    {
        // The grammar carries call arguments on either the IndexOrCall
        // postfix on Target, or on CallStatement.BareArguments — mutually
        // exclusive per Statement.cs. Normalise so the comparison above
        // can ignore the carrier.
        if (call.Target is IndexOrCallExpression ioc && call.BareArguments.Count == 0)
            return (ioc.Target, ioc.Arguments);
        return (call.Target, call.BareArguments);
    }

    private static string UnquoteVbaString(string text)
    {
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            return text[1..^1].Replace("\"\"", "\"");
        return text;
    }

    // ── Expression-rule classification (table / form / control) ────────

    // Build a rule for a JET-expression source (table / form / control
    // ValidationRule strings). Tries Range first, falls back to
    // CustomPredicate when the shape is recognisable VBA-like syntax,
    // and Unstructured otherwise.
    private static ValidationRule BuildExpressionRule(
        ValidationRuleScope scope,
        string sourceObject,
        string? fieldOrControlName,
        ValidationRuleOrigin origin,
        string expression,
        string? message)
    {
        var trimmed = expression.Trim();
        if (TryParseRange(trimmed, out var range))
        {
            return new ValidationRule
            {
                Scope = scope,
                SourceObject = sourceObject,
                FieldOrControlName = fieldOrControlName,
                Origin = origin,
                Kind = ValidationRuleKind.Range,
                Expression = expression,
                Message = message,
                Range = range,
            };
        }

        var kind = LooksLikeBooleanPredicate(trimmed)
            ? ValidationRuleKind.CustomPredicate
            : ValidationRuleKind.Unstructured;

        return new ValidationRule
        {
            Scope = scope,
            SourceObject = sourceObject,
            FieldOrControlName = fieldOrControlName,
            Origin = origin,
            Kind = kind,
            Expression = expression,
            Message = message,
        };
    }

    // Recognised range shapes (case-insensitive, whitespace-tolerant):
    //   Between <expr> And <expr>           -> [min, max]
    //   >= <expr> And <= <expr>             -> [min, max]
    //   > <expr> And < <expr>               -> (min, max)
    //   >= <expr> And < <expr>              -> [min, max)
    //   > <expr> And <= <expr>              -> (min, max]
    //   >= <expr>  /  > <expr>              -> [min, ∞) / (min, ∞)
    //   <= <expr>  /  < <expr>              -> (-∞, max] / (-∞, max)
    // Anything else falls through.
    private static bool TryParseRange(string expression, out ValidationRangeConstraint? range)
    {
        range = null;
        if (string.IsNullOrWhiteSpace(expression)) return false;

        var betweenMatch = Regex.Match(
            expression,
            @"^\s*Between\s+(?<lo>.+?)\s+And\s+(?<hi>.+?)\s*$",
            RegexOptions.IgnoreCase);
        if (betweenMatch.Success)
        {
            range = new ValidationRangeConstraint
            {
                Minimum = betweenMatch.Groups["lo"].Value.Trim(),
                MinimumInclusive = true,
                Maximum = betweenMatch.Groups["hi"].Value.Trim(),
                MaximumInclusive = true,
            };
            return true;
        }

        var compoundMatch = Regex.Match(
            expression,
            @"^\s*(?<lop>>=|>)\s*(?<lo>.+?)\s+And\s+(?<hop><=|<)\s*(?<hi>.+?)\s*$",
            RegexOptions.IgnoreCase);
        if (compoundMatch.Success)
        {
            range = new ValidationRangeConstraint
            {
                Minimum = compoundMatch.Groups["lo"].Value.Trim(),
                MinimumInclusive = compoundMatch.Groups["lop"].Value == ">=",
                Maximum = compoundMatch.Groups["hi"].Value.Trim(),
                MaximumInclusive = compoundMatch.Groups["hop"].Value == "<=",
            };
            return true;
        }

        // Half-range upper: `<= N` or `< N`.
        var upperMatch = Regex.Match(expression, @"^\s*(?<op><=|<)\s*(?<v>.+?)\s*$");
        if (upperMatch.Success)
        {
            range = new ValidationRangeConstraint
            {
                Maximum = upperMatch.Groups["v"].Value.Trim(),
                MaximumInclusive = upperMatch.Groups["op"].Value == "<=",
            };
            return true;
        }

        // Half-range lower: `>= N` or `> N`.
        var lowerMatch = Regex.Match(expression, @"^\s*(?<op>>=|>)\s*(?<v>.+?)\s*$");
        if (lowerMatch.Success)
        {
            range = new ValidationRangeConstraint
            {
                Minimum = lowerMatch.Groups["v"].Value.Trim(),
                MinimumInclusive = lowerMatch.Groups["op"].Value == ">=",
            };
            return true;
        }

        return false;
    }

    // A coarse heuristic: a JET expression starts with an operator
    // (>, <, =, Like, Is, Not, Between, In) or contains a comparison
    // operator somewhere. Anything that does NOT match this falls through
    // to Unstructured per the explicit-silence rule.
    private static bool LooksLikeBooleanPredicate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;

        // Starts-with-operator forms.
        if (Regex.IsMatch(expression, @"^\s*(>=|<=|<>|=|>|<|Not\b|Like\b|Is\b|In\b|Between\b)", RegexOptions.IgnoreCase))
            return true;

        // Contains a comparison operator surrounded by non-operator text.
        if (Regex.IsMatch(expression, @"(?:[^\s<>=])\s*(>=|<=|<>|>|<|=)\s*(?:[^\s<>=])"))
            return true;

        // Contains `And` / `Or` between subexpressions.
        if (Regex.IsMatch(expression, @"\s+(And|Or)\s+", RegexOptions.IgnoreCase))
            return true;

        // Looks like a Like-pattern reference.
        if (Regex.IsMatch(expression, @"\bLike\s+\""", RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    // ── Property-bag readers (mirror FormComponentSpecBuilder) ─────────

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var v)) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        var s = v.GetString();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static bool? ReadBool(IReadOnlyDictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
