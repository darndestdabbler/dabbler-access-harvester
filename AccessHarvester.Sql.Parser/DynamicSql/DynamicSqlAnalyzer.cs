using System;
using System.Collections.Generic;
using System.Text;
using AccessHarvester.Sql.Parser.FormReferences;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.References;
using Declaration = AccessHarvester.Vba.Resolver.Declarations.Declaration;
using ProcedureDeclaration = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

namespace AccessHarvester.Sql.Parser.DynamicSql;

// Intra-procedural data-flow analyzer for VBA-constructed SQL strings.
// Implements Phase 5.4 of the harvester plan: walks a ProcedureDeclaration
// in source order, tracks string-typed local variables, builds a
// SymbolicSqlValue for each, and at every recognized SQL execution call
// site (OpenRecordset, OpenQueryDef, Execute, RunSQL) flattens the
// symbolic value to a list of CandidateTemplates.
//
// Forward symbolic-state evaluation:
//   - Env maps a tracked variable name to its current SymbolicSqlValue.
//   - LetStatement to a tracked name updates the entry.
//   - IfStatement clones Env into per-branch envs (Then, ElseIf*, Else,
//     and an implicit fall-through when no Else is present), walks each
//     branch body, then merges divergent variables into SqlBranches.
//   - SetStatement / CallStatement that look like SQL exec calls
//     snapshot the SQL argument's symbolic value and record an
//     execution site.
//
// Out-of-scope for Session 2 (Phase 5.4 explicitly routes failures to
// the manual-review queue): For/Do/While/With block-level mutation,
// non-string parameter propagation, peering inside builtins like
// Replace() to extract their form-control argument. Session 3 of
// jet-sql-resolution-and-dynamic tightens those gaps if fixture 05's
// expected.json requires it.
public sealed class DynamicSqlAnalyzer
{
    private readonly IAccessObjectModelRegistry _registry;
    private readonly DynamicSqlAnalyzerOptions _options;

    public DynamicSqlAnalyzer(
        IAccessObjectModelRegistry registry,
        DynamicSqlAnalyzerOptions? options = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options = options ?? DynamicSqlAnalyzerOptions.Default;
    }

    public DynamicSqlAnalysisResult Analyze(ProcedureDeclaration procedure)
    {
        if (procedure is null) throw new ArgumentNullException(nameof(procedure));

        var trackedVars = CollectTrackedStringVars(procedure);
        var env = new Dictionary<string, SymbolicSqlValue>(StringComparer.OrdinalIgnoreCase);

        // Seed string-typed parameters as runtime-unknown holes rather
        // than letting them flow through as SqlUnboundVariable on first
        // use. Parameters are inputs to the procedure — they have a
        // value at runtime, just not one the analyzer can see — so the
        // correct rendering is `*` (unknown) not unbound-variable.
        foreach (var p in procedure.Parameters)
        {
            if (IsStringType(p.Type))
                env[p.Name] = new SqlUnknownHole(null, p.Name);
        }

        var sites = new List<DynamicSqlExecutionSite>();
        WalkStatements(procedure.Body, env, trackedVars, sites);

        return new DynamicSqlAnalysisResult(sites);
    }

    // ── Tracked-variable discovery ─────────────────────────────────────

    private static HashSet<string> CollectTrackedStringVars(ProcedureDeclaration procedure)
    {
        var tracked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in procedure.Parameters)
        {
            if (IsStringType(p.Type))
                tracked.Add(p.Name);
        }

        foreach (var name in WalkLocalStringDeclarations(procedure.Body))
            tracked.Add(name);

        return tracked;
    }

    private static IEnumerable<string> WalkLocalStringDeclarations(IReadOnlyList<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case LocalDeclarationStatement decl:
                    foreach (var d in decl.Declarators)
                        if (IsStringType(d.Type))
                            yield return d.Name;
                    break;

                // Recurse into structured statements so a `Dim` inside
                // an `If` (rare in idiomatic VBA but legal) is still
                // discovered. We do not enter procedure-nested
                // procedures because VBA forbids them.
                case IfStatement i:
                    foreach (var n in WalkLocalStringDeclarations(i.ThenBody)) yield return n;
                    foreach (var ei in i.ElseIfClauses)
                        foreach (var n in WalkLocalStringDeclarations(ei.Body)) yield return n;
                    if (i.ElseBody is not null)
                        foreach (var n in WalkLocalStringDeclarations(i.ElseBody)) yield return n;
                    break;
                case ForStatement f:
                    foreach (var n in WalkLocalStringDeclarations(f.Body)) yield return n;
                    break;
                case ForEachStatement fe:
                    foreach (var n in WalkLocalStringDeclarations(fe.Body)) yield return n;
                    break;
                case DoLoopStatement d2:
                    foreach (var n in WalkLocalStringDeclarations(d2.Body)) yield return n;
                    break;
                case WhileWendStatement w:
                    foreach (var n in WalkLocalStringDeclarations(w.Body)) yield return n;
                    break;
                case SelectCaseStatement sc:
                    foreach (var c in sc.Cases)
                        foreach (var n in WalkLocalStringDeclarations(c.Body)) yield return n;
                    if (sc.CaseElseBody is not null)
                        foreach (var n in WalkLocalStringDeclarations(sc.CaseElseBody)) yield return n;
                    break;
                case WithStatement ws:
                    foreach (var n in WalkLocalStringDeclarations(ws.Body)) yield return n;
                    break;
            }
        }
    }

    private static bool IsStringType(TypeReference? t) =>
        t is { QualifiedName: { Count: 1 } qn } &&
        string.Equals(qn[0], "String", StringComparison.OrdinalIgnoreCase);

    // ── Statement walker ───────────────────────────────────────────────

    private void WalkStatements(
        IReadOnlyList<Statement> body,
        Dictionary<string, SymbolicSqlValue> env,
        HashSet<string> tracked,
        List<DynamicSqlExecutionSite> sites)
    {
        foreach (var stmt in body)
            WalkStatement(stmt, env, tracked, sites);
    }

    private void WalkStatement(
        Statement stmt,
        Dictionary<string, SymbolicSqlValue> env,
        HashSet<string> tracked,
        List<DynamicSqlExecutionSite> sites)
    {
        switch (stmt)
        {
            case LetStatement let:
                HandleAssignment(let.Target, let.Value, env, tracked);
                TryRecordExecutionSite(stmt, let.Value, env, sites);
                break;

            case SetStatement set:
                // `Set rs = db.OpenRecordset(sSQL, ...)` is the canonical
                // recordset-execution shape. We don't try to model `rs`
                // as a tracked variable; we just inspect the right-hand
                // side for an execution call.
                TryRecordExecutionSite(stmt, set.Value, env, sites);
                break;

            case CallStatement call:
                // `db.Execute "DELETE FROM tbl"` style — no Set, no
                // assignment, just a call. The call expression may
                // carry args inside an IndexOrCallExpression on the
                // target, or as bare CallStatement.BareArguments.
                TryRecordExecutionFromCall(stmt, call, env, sites);
                break;

            case IfStatement ifStmt:
                HandleIf(ifStmt, env, tracked, sites);
                break;

            case SelectCaseStatement select:
                HandleSelect(select, env, tracked, sites);
                break;

            case WithStatement with:
                // Body may execute SQL but cannot directly assign to
                // tracked vars via an implicit `.Member` (those would
                // not be IdentifierExpression). Walk for execution
                // sites only.
                WalkStatements(with.Body, env, tracked, sites);
                break;

            // Loops are walked so SQL execution sites *inside* a loop
            // are still discovered, but tracked-variable mutations
            // performed inside the body are conservatively invalidated
            // after the loop closes — we cannot decide at static-
            // analysis time how many iterations actually run, so
            // propagating one-iteration state to the post-loop env
            // would fabricate a SQL state that never exists at
            // runtime. Phase 5.4 is explicit: failures route to the
            // manual-review queue rather than producing wrong results.
            case ForStatement f:       WalkLoopBody(f.Body, env, tracked, sites); break;
            case ForEachStatement fe:  WalkLoopBody(fe.Body, env, tracked, sites); break;
            case DoLoopStatement dl:   WalkLoopBody(dl.Body, env, tracked, sites); break;
            case WhileWendStatement w: WalkLoopBody(w.Body, env, tracked, sites); break;
        }
    }

    private void WalkLoopBody(
        IReadOnlyList<Statement> body,
        Dictionary<string, SymbolicSqlValue> env,
        HashSet<string> tracked,
        List<DynamicSqlExecutionSite> sites)
    {
        var preLoop = CloneEnv(env);
        WalkStatements(body, env, tracked, sites);

        // Invalidate any tracked variable whose value the loop body
        // changed (or that the loop body introduced fresh). A loop
        // could have run zero or many times at runtime, so the post-
        // loop value is genuinely unknown — flag it as such rather
        // than carrying the one-iteration symbolic value forward.
        var keys = new List<string>(env.Keys);
        foreach (var key in keys)
        {
            if (!preLoop.TryGetValue(key, out var prev) ||
                !SymbolicSqlValueEquality.DeepEquals(prev, env[key]))
            {
                env[key] = new SqlUnknownHole(null, $"<loop-mutation:{key}>");
            }
        }
    }

    private void HandleAssignment(
        Expression target,
        Expression value,
        Dictionary<string, SymbolicSqlValue> env,
        HashSet<string> tracked)
    {
        if (target is IdentifierExpression id && tracked.Contains(id.Name))
        {
            env[id.Name] = EvalExpr(value, env);
        }
    }

    private void HandleIf(
        IfStatement ifStmt,
        Dictionary<string, SymbolicSqlValue> env,
        HashSet<string> tracked,
        List<DynamicSqlExecutionSite> sites)
    {
        // Snapshot for the implicit "no branch taken" fall-through
        // when no Else clause is present.
        var snapshot = CloneEnv(env);
        var branchEnvs = new List<Dictionary<string, SymbolicSqlValue>>();

        var thenEnv = CloneEnv(snapshot);
        WalkStatements(ifStmt.ThenBody, thenEnv, tracked, sites);
        branchEnvs.Add(thenEnv);

        foreach (var ei in ifStmt.ElseIfClauses)
        {
            var eiEnv = CloneEnv(snapshot);
            WalkStatements(ei.Body, eiEnv, tracked, sites);
            branchEnvs.Add(eiEnv);
        }

        if (ifStmt.ElseBody is not null)
        {
            var elseEnv = CloneEnv(snapshot);
            WalkStatements(ifStmt.ElseBody, elseEnv, tracked, sites);
            branchEnvs.Add(elseEnv);
        }
        else
        {
            // Implicit fall-through: no body executed at runtime.
            // Carry the snapshot so divergence detection sees an
            // alternative that left tracked vars unchanged.
            branchEnvs.Add(CloneEnv(snapshot));
        }

        MergeEnv(env, branchEnvs);
    }

    private void HandleSelect(
        SelectCaseStatement select,
        Dictionary<string, SymbolicSqlValue> env,
        HashSet<string> tracked,
        List<DynamicSqlExecutionSite> sites)
    {
        var snapshot = CloneEnv(env);
        var branchEnvs = new List<Dictionary<string, SymbolicSqlValue>>();

        foreach (var clause in select.Cases)
        {
            var caseEnv = CloneEnv(snapshot);
            WalkStatements(clause.Body, caseEnv, tracked, sites);
            branchEnvs.Add(caseEnv);
        }

        if (select.CaseElseBody is not null)
        {
            var elseEnv = CloneEnv(snapshot);
            WalkStatements(select.CaseElseBody, elseEnv, tracked, sites);
            branchEnvs.Add(elseEnv);
        }
        else
        {
            branchEnvs.Add(CloneEnv(snapshot));
        }

        MergeEnv(env, branchEnvs);
    }

    private static Dictionary<string, SymbolicSqlValue> CloneEnv(
        Dictionary<string, SymbolicSqlValue> source)
    {
        return new Dictionary<string, SymbolicSqlValue>(
            source, StringComparer.OrdinalIgnoreCase);
    }

    private static void MergeEnv(
        Dictionary<string, SymbolicSqlValue> target,
        IReadOnlyList<Dictionary<string, SymbolicSqlValue>> branchEnvs)
    {
        // Collect every tracked variable that appears in any branch.
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in branchEnvs)
            foreach (var k in b.Keys)
                keys.Add(k);

        foreach (var key in keys)
        {
            var alts = new List<SymbolicSqlValue>(branchEnvs.Count);
            foreach (var b in branchEnvs)
            {
                alts.Add(b.TryGetValue(key, out var v) ? v : new SqlUnboundVariable(key));
            }

            // If every alternative is structurally identical, collapse
            // — no real divergence at this If. Otherwise materialize
            // SqlBranches.
            if (AllAlternativesEqual(alts))
                target[key] = alts[0];
            else
                target[key] = new SqlBranches(alts);
        }
    }

    private static bool AllAlternativesEqual(IReadOnlyList<SymbolicSqlValue> alts)
    {
        // Use structural deep-equality rather than record `.Equals`,
        // which falls back to reference equality on the IReadOnlyList
        // fields inside SqlConcat / SqlBranches and on the
        // OriginalExpression Expression node inside hole records. Two
        // semantically-equal symbolic values produced by independent
        // EvalExpr calls are routinely *not* reference-equal in those
        // sub-positions; comparing structurally avoids fabricated
        // divergence.
        for (int i = 1; i < alts.Count; i++)
            if (!SymbolicSqlValueEquality.DeepEquals(alts[0], alts[i])) return false;
        return true;
    }

    // ── Expression evaluation ──────────────────────────────────────────

    private SymbolicSqlValue EvalExpr(
        Expression expr,
        Dictionary<string, SymbolicSqlValue> env)
    {
        switch (expr)
        {
            case LiteralExpression lit when lit.Kind == LiteralKind.String:
                return new SqlLiteral(UnescapeVbaStringLiteral(lit.Text));

            case ParenthesizedExpression p:
                return EvalExpr(p.Inner, env);

            case BinaryExpression b when b.Operator == BinaryOperator.Concat:
                {
                    var leftVal = EvalExpr(b.Left, env);
                    var rightVal = EvalExpr(b.Right, env);
                    return ConcatValues(leftVal, rightVal);
                }

            case IdentifierExpression id:
                if (env.TryGetValue(id.Name, out var current))
                    return current;
                return new SqlUnboundVariable(id.Name);

            case BangAccessExpression:
            case MemberAccessExpression:
                {
                    var hole = TryRecognizeFormControlChain(expr);
                    if (hole is not null) return hole;
                    return new SqlUnknownHole(expr, ReconstructText(expr));
                }

            // Anything else (function calls, arithmetic, NEW, etc.)
            // contributes an unknown hole. Phase 5.4 explicitly defers
            // peering inside builtins like Replace().
            default:
                return new SqlUnknownHole(expr, ReconstructText(expr));
        }
    }

    private static SymbolicSqlValue ConcatValues(SymbolicSqlValue left, SymbolicSqlValue right)
    {
        var parts = new List<SymbolicSqlValue>();
        if (left is SqlConcat lc) parts.AddRange(lc.Parts); else parts.Add(left);
        if (right is SqlConcat rc) parts.AddRange(rc.Parts); else parts.Add(right);
        return new SqlConcat(parts);
    }

    // VBA string literal text comes from the lexer with surrounding
    // double quotes intact and `""` representing an embedded quote.
    // Strip the wrapping quotes if present and collapse `""` to `"`.
    private static string UnescapeVbaStringLiteral(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var s = raw;
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            s = s.Substring(1, s.Length - 2);
        return s.Replace("\"\"", "\"");
    }

    // ── Form-control chain recognition ────────────────────────────────
    //
    // Mirrors the operator-aware resolution from
    // FormReferenceResolver (Session 1): on a Form/Report, `!`
    // segments resolve strictly through the Controls dictionary so a
    // control whose name shadows a type-library member (Name, Visible,
    // Caption, ...) cannot mis-resolve. `.` segments delegate to the
    // registry's standard chain.

    private SqlFormControlHole? TryRecognizeFormControlChain(Expression expr)
    {
        var segments = new List<(string Name, bool IsBang)>();
        Expression cur = expr;
        while (true)
        {
            if (cur is BangAccessExpression bang)
            {
                segments.Insert(0, (bang.MemberName, true));
                cur = bang.Target;
            }
            else if (cur is MemberAccessExpression mem)
            {
                segments.Insert(0, (mem.MemberName, false));
                cur = mem.Target;
            }
            else break;
        }

        if (cur is not IdentifierExpression rootId)
            return null;

        var rootName = rootId.Name;
        if (!string.Equals(rootName, "Forms", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(rootName, "Reports", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (segments.Count == 0)
            return null; // bare `Forms` / `Reports` — not a control reference

        var originalText = ReconstructText(expr);

        var collection = _registry.TryResolveTopLevelMember(
            string.Equals(rootName, "Reports", StringComparison.OrdinalIgnoreCase)
                ? "Reports" : "Forms");
        if (collection is null)
        {
            return new SqlFormControlHole(
                expr, originalText, null,
                FormReferences.UnresolvedReason.HostCollectionNotRegistered);
        }

        Declaration? walker = collection;
        for (int i = 0; i < segments.Count; i++)
        {
            var (name, isBang) = segments[i];
            Declaration? next;
            if (isBang && walker is SyntheticFormDeclaration sf)
                next = sf.Controls.TryGetValue(name, out var c1) ? c1 : null;
            else if (isBang && walker is SyntheticReportDeclaration sr)
                next = sr.Controls.TryGetValue(name, out var c2) ? c2 : null;
            else
                next = _registry.TryResolveMemberOf(walker!, name);

            if (next is null)
            {
                return new SqlFormControlHole(
                    expr, originalText, null,
                    ChooseUnresolvedReason(rootName, i, isBang));
            }
            walker = next;
        }

        return new SqlFormControlHole(expr, originalText, walker, null);
    }

    private static string ChooseUnresolvedReason(string rootName, int failedAtIndex, bool isBang)
    {
        bool isReports = string.Equals(rootName, "Reports", StringComparison.OrdinalIgnoreCase);
        if (failedAtIndex == 0)
        {
            return isReports
                ? FormReferences.UnresolvedReason.ReportNotInMetadata
                : FormReferences.UnresolvedReason.FormNotInMetadata;
        }
        if (failedAtIndex == 1 && isBang)
        {
            return isReports
                ? FormReferences.UnresolvedReason.ControlNotOnReport
                : FormReferences.UnresolvedReason.ControlNotOnForm;
        }
        return FormReferences.UnresolvedReason.MemberNotFoundAtSegment(failedAtIndex);
    }

    // ── Execution-site detection ───────────────────────────────────────

    private void TryRecordExecutionSite(
        Statement host,
        Expression value,
        Dictionary<string, SymbolicSqlValue> env,
        List<DynamicSqlExecutionSite> sites)
    {
        if (value is not IndexOrCallExpression call) return;
        if (!IsRecognizedSqlExecCall(call.Target)) return;
        if (call.Arguments.Count == 0) return;
        var sqlArg = GetArgumentValue(call.Arguments[0]);
        if (sqlArg is null) return;

        var symbolic = EvalExpr(sqlArg, env);
        sites.Add(BuildSite(host, call, sqlArg, symbolic));
    }

    private void TryRecordExecutionFromCall(
        Statement host,
        CallStatement call,
        Dictionary<string, SymbolicSqlValue> env,
        List<DynamicSqlExecutionSite> sites)
    {
        // CallStatement may carry args two ways: nested as
        // IndexOrCallExpression in Target, or in BareArguments. Try
        // each.
        if (call.Target is IndexOrCallExpression nested &&
            IsRecognizedSqlExecCall(nested.Target) &&
            nested.Arguments.Count > 0 &&
            GetArgumentValue(nested.Arguments[0]) is { } sqlArg1)
        {
            sites.Add(BuildSite(host, nested, sqlArg1, EvalExpr(sqlArg1, env)));
            return;
        }

        if (IsRecognizedSqlExecCall(call.Target) &&
            call.BareArguments.Count > 0 &&
            GetArgumentValue(call.BareArguments[0]) is { } sqlArg2)
        {
            sites.Add(BuildSite(host, call.Target, sqlArg2, EvalExpr(sqlArg2, env)));
        }
    }

    private static Expression? GetArgumentValue(Argument arg) => arg switch
    {
        PositionalArgument p => p.Value,
        NamedArgument n      => n.Value,
        _ => null,
    };

    private static bool IsRecognizedSqlExecCall(Expression target)
    {
        // We recognize the leaf member name only — the receiver is
        // unconstrained because the harvester sees DAO.Database,
        // CurrentDb(), nested With-block targets, etc.
        var leafName = GetLeafMemberName(target);
        if (leafName is null) return false;
        return string.Equals(leafName, "OpenRecordset", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leafName, "OpenQueryDef", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leafName, "Execute",       StringComparison.OrdinalIgnoreCase)
            || string.Equals(leafName, "RunSQL",        StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetLeafMemberName(Expression target)
    {
        return target switch
        {
            MemberAccessExpression m => m.MemberName,
            BangAccessExpression b   => b.MemberName,
            IdentifierExpression id  => id.Name,
            ParenthesizedExpression p => GetLeafMemberName(p.Inner),
            _ => null,
        };
    }

    private DynamicSqlExecutionSite BuildSite(
        Statement host,
        Expression call,
        Expression sqlArg,
        SymbolicSqlValue symbolic)
    {
        long cap = _options.MaxFullEnumeration;
        long cardCapped = CardinalityCapped(symbolic, cap);

        bool isAnalyzable;
        string? unanalyzableReason;
        IReadOnlyList<TemplateLeaf> emitted;
        int reportedCardinality;

        if (cardCapped > cap)
        {
            // Combinatorial blowup: enumerate at most
            // MaxRepresentativeSamples leaves without ever
            // materializing the full set. cardCapped is the cap+1
            // sentinel — surface it to consumers as the reported
            // cardinality (a "more than cap" indicator).
            emitted = FlattenWithLimit(symbolic, _options.MaxRepresentativeSamples);
            isAnalyzable = false;
            unanalyzableReason = UnanalyzableReason.CombinatorialBlowup;
            reportedCardinality = (int)Math.Min(cardCapped, int.MaxValue);
        }
        else
        {
            // Within the cap: full enumeration is bounded and cheap.
            var leaves = Flatten(symbolic);
            reportedCardinality = leaves.Count;

            if (leaves.Count == 1 &&
                leaves[0].Pieces.Count == 1 &&
                leaves[0].Pieces[0] is UnboundVarPiece)
            {
                isAnalyzable = false;
                unanalyzableReason = UnanalyzableReason.UnboundVariable;
                emitted = leaves;
            }
            else if (HasNoLiteralContent(leaves))
            {
                isAnalyzable = false;
                unanalyzableReason = UnanalyzableReason.UnknownExpression;
                emitted = leaves;
            }
            else
            {
                isAnalyzable = true;
                unanalyzableReason = null;
                emitted = leaves;
            }
        }

        var templates = new List<CandidateTemplate>(emitted.Count);
        foreach (var leaf in emitted)
            templates.Add(RenderTemplate(leaf));

        return new DynamicSqlExecutionSite(
            host, call, sqlArg, symbolic,
            templates, reportedCardinality,
            isAnalyzable, unanalyzableReason);
    }

    private static bool HasNoLiteralContent(IReadOnlyList<TemplateLeaf> leaves)
    {
        foreach (var leaf in leaves)
        {
            foreach (var piece in leaf.Pieces)
                if (piece is TextPiece) return false;
        }
        return true;
    }

    // Cap-aware cardinality. Returns min(actual, cap + 1) so callers
    // can detect overflow without computing the full product.
    // Saturating arithmetic at every step prevents long overflow on
    // pathological inputs.
    private static long CardinalityCapped(SymbolicSqlValue value, long cap)
    {
        switch (value)
        {
            case SqlBranches b:
            {
                long sum = 0;
                foreach (var alt in b.Alternatives)
                {
                    sum += CardinalityCapped(alt, cap);
                    if (sum > cap) return cap + 1;
                }
                return sum;
            }
            case SqlConcat c:
            {
                long prod = 1;
                foreach (var part in c.Parts)
                {
                    long pc = CardinalityCapped(part, cap);
                    if (pc == 0) return 0;
                    if (pc > cap || prod > (cap + 1) / pc) return cap + 1;
                    prod *= pc;
                    if (prod > cap) return cap + 1;
                }
                return prod;
            }
            default:
                return 1;
        }
    }

    // Limit-aware Flatten used only when CardinalityCapped exceeds the
    // full-enumeration cap. Returns at most `limit` leaves and
    // guarantees no intermediate accumulator ever exceeds `2 * limit`,
    // so it is safe to call on combinatorially-large inputs.
    private static IReadOnlyList<TemplateLeaf> FlattenWithLimit(SymbolicSqlValue value, int limit)
    {
        if (limit <= 0) return System.Array.Empty<TemplateLeaf>();
        return value switch
        {
            SqlLiteral lit       => Single(new TextPiece(lit.Text)),
            SqlFormControlHole h => Single(new FormControlPiece(h)),
            SqlUnknownHole h     => Single(new UnknownPiece(h)),
            SqlUnboundVariable u => Single(new UnboundVarPiece(u.Name)),
            SqlConcat concat     => FlattenConcatWithLimit(concat, limit),
            SqlBranches branches => FlattenBranchesWithLimit(branches, limit),
            _ => System.Array.Empty<TemplateLeaf>(),
        };
    }

    private static IReadOnlyList<TemplateLeaf> FlattenConcatWithLimit(SqlConcat concat, int limit)
    {
        var acc = new List<TemplateLeaf> { new TemplateLeaf(new List<TemplatePiece>()) };
        foreach (var part in concat.Parts)
        {
            var partLeaves = FlattenWithLimit(part, limit);
            var next = new List<TemplateLeaf>();
            foreach (var prefix in acc)
            {
                foreach (var partLeaf in partLeaves)
                {
                    if (next.Count >= limit) break;
                    var combined = new List<TemplatePiece>(prefix.Pieces.Count + partLeaf.Pieces.Count);
                    combined.AddRange(prefix.Pieces);
                    combined.AddRange(partLeaf.Pieces);
                    next.Add(new TemplateLeaf(MergeAdjacentText(combined)));
                }
                if (next.Count >= limit) break;
            }
            acc = next;
            if (acc.Count == 0) break;
        }
        return acc;
    }

    private static IReadOnlyList<TemplateLeaf> FlattenBranchesWithLimit(SqlBranches branches, int limit)
    {
        var result = new List<TemplateLeaf>();
        foreach (var alt in branches.Alternatives)
        {
            int remaining = limit - result.Count;
            if (remaining <= 0) break;
            var altLeaves = FlattenWithLimit(alt, remaining);
            result.AddRange(altLeaves);
            if (result.Count >= limit) break;
        }
        return result;
    }

    // ── Flatten ────────────────────────────────────────────────────────

    // Internal piece model used during flattening. Public surface is
    // CandidateTemplate / ParameterBinding only.
    private abstract record TemplatePiece;
    private sealed record TextPiece(string Text) : TemplatePiece;
    private sealed record FormControlPiece(SqlFormControlHole Hole) : TemplatePiece;
    private sealed record UnknownPiece(SqlUnknownHole Hole) : TemplatePiece;
    private sealed record UnboundVarPiece(string Name) : TemplatePiece;

    private sealed record TemplateLeaf(IReadOnlyList<TemplatePiece> Pieces);

    private static IReadOnlyList<TemplateLeaf> Flatten(SymbolicSqlValue value)
    {
        return value switch
        {
            SqlLiteral lit          => Single(new TextPiece(lit.Text)),
            SqlConcat concat        => FlattenConcat(concat),
            SqlBranches branches    => FlattenBranches(branches),
            SqlFormControlHole h    => Single(new FormControlPiece(h)),
            SqlUnknownHole h        => Single(new UnknownPiece(h)),
            SqlUnboundVariable u    => Single(new UnboundVarPiece(u.Name)),
            _ => Single(new UnknownPiece(new SqlUnknownHole(null!, value.GetType().Name))),
        };
    }

    private static IReadOnlyList<TemplateLeaf> Single(TemplatePiece piece)
    {
        return new[] { new TemplateLeaf(new[] { piece }) };
    }

    private static IReadOnlyList<TemplateLeaf> FlattenConcat(SqlConcat concat)
    {
        // Cross-product across the parts. Start with one empty leaf
        // and extend with each part's flattened leaves.
        var acc = new List<TemplateLeaf> { new TemplateLeaf(new List<TemplatePiece>()) };
        foreach (var part in concat.Parts)
        {
            var next = new List<TemplateLeaf>();
            var partLeaves = Flatten(part);
            foreach (var prefix in acc)
            {
                foreach (var partLeaf in partLeaves)
                {
                    var combined = new List<TemplatePiece>(prefix.Pieces.Count + partLeaf.Pieces.Count);
                    combined.AddRange(prefix.Pieces);
                    combined.AddRange(partLeaf.Pieces);
                    next.Add(new TemplateLeaf(MergeAdjacentText(combined)));
                }
            }
            acc = next;
        }
        return acc;
    }

    private static IReadOnlyList<TemplatePiece> MergeAdjacentText(IReadOnlyList<TemplatePiece> pieces)
    {
        // Collapse adjacent TextPieces to a single piece so the final
        // rendered template text is contiguous and `?`/`*` placeholder
        // counts are stable.
        var merged = new List<TemplatePiece>(pieces.Count);
        foreach (var p in pieces)
        {
            if (p is TextPiece t && merged.Count > 0 && merged[^1] is TextPiece prev)
                merged[^1] = new TextPiece(prev.Text + t.Text);
            else
                merged.Add(p);
        }
        return merged;
    }

    private static IReadOnlyList<TemplateLeaf> FlattenBranches(SqlBranches branches)
    {
        var result = new List<TemplateLeaf>();
        foreach (var alt in branches.Alternatives)
            result.AddRange(Flatten(alt));
        return result;
    }

    // ── Render ─────────────────────────────────────────────────────────

    private static CandidateTemplate RenderTemplate(TemplateLeaf leaf)
    {
        var sb = new StringBuilder();
        var bindings = new List<ParameterBinding>();
        foreach (var piece in leaf.Pieces)
        {
            switch (piece)
            {
                case TextPiece t:
                    sb.Append(t.Text);
                    break;
                case FormControlPiece fc:
                    sb.Append('?');
                    bindings.Add(new ParameterBinding(
                        ParameterBindingSource.FormControl,
                        fc.Hole.OriginalText,
                        fc.Hole.OriginalExpression,
                        fc.Hole.ResolvedTarget,
                        fc.Hole.UnresolvedReason));
                    break;
                case UnknownPiece u:
                    sb.Append('*');
                    bindings.Add(new ParameterBinding(
                        ParameterBindingSource.Unknown,
                        u.Hole.OriginalText,
                        u.Hole.OriginalExpression,
                        null, null));
                    break;
                case UnboundVarPiece uv:
                    sb.Append('*');
                    bindings.Add(new ParameterBinding(
                        ParameterBindingSource.UnboundVariable,
                        uv.Name,
                        null, null, null));
                    break;
            }
        }
        return new CandidateTemplate(sb.ToString(), bindings);
    }

    // ── Surface-text reconstruction ────────────────────────────────────
    //
    // Used only for diagnostics and parameter OriginalText. Not a
    // round-trip serializer: original spacing/casing on operators is
    // not preserved, but identifier and member-name spelling is.

    private static string ReconstructText(Expression expr)
    {
        var sb = new StringBuilder();
        WriteExpr(expr, sb);
        return sb.ToString();
    }

    private static void WriteExpr(Expression expr, StringBuilder sb)
    {
        switch (expr)
        {
            case IdentifierExpression id:
                sb.Append(id.Name);
                if (id.TypeSuffix is not null) sb.Append(id.TypeSuffix);
                break;
            case LiteralExpression lit:
                sb.Append(lit.Text);
                break;
            case ParenthesizedExpression p:
                sb.Append('(');
                WriteExpr(p.Inner, sb);
                sb.Append(')');
                break;
            case MemberAccessExpression m:
                WriteExpr(m.Target, sb);
                sb.Append('.').Append(m.MemberName);
                break;
            case BangAccessExpression b:
                WriteExpr(b.Target, sb);
                sb.Append('!').Append(b.MemberName);
                break;
            case IndexOrCallExpression call:
                WriteExpr(call.Target, sb);
                sb.Append('(');
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    if (GetArgumentValue(call.Arguments[i]) is { } v) WriteExpr(v, sb);
                }
                sb.Append(')');
                break;
            case BinaryExpression bin:
                WriteExpr(bin.Left, sb);
                sb.Append(' ').Append(BinaryOperatorText(bin.Operator)).Append(' ');
                WriteExpr(bin.Right, sb);
                break;
            case UnaryExpression un:
                sb.Append(UnaryOperatorText(un.Operator));
                WriteExpr(un.Operand, sb);
                break;
            default:
                sb.Append('?').Append(expr.GetType().Name).Append('?');
                break;
        }
    }

    private static string BinaryOperatorText(BinaryOperator op) => op switch
    {
        BinaryOperator.Concat   => "&",
        BinaryOperator.Add      => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide   => "/",
        BinaryOperator.Equal    => "=",
        BinaryOperator.NotEqual => "<>",
        BinaryOperator.Less     => "<",
        BinaryOperator.Greater  => ">",
        BinaryOperator.LessOrEqual    => "<=",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.And => "And",
        BinaryOperator.Or  => "Or",
        BinaryOperator.Like => "Like",
        BinaryOperator.Is   => "Is",
        _ => op.ToString(),
    };

    private static string UnaryOperatorText(UnaryOperator op) => op switch
    {
        UnaryOperator.Plus  => "+",
        UnaryOperator.Minus => "-",
        UnaryOperator.Not   => "Not ",
        _ => op.ToString(),
    };
}
