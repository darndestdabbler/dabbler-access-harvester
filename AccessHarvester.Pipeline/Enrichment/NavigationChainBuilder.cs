using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccessHarvester.Pipeline.UsageIdentification;
using AccessHarvester.Sql.Parser;
using AccessHarvester.Sql.Parser.Ast;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Derives the R2-2 / DW-10 cross-object navigation <b>chain</b> edges from
/// the harvested IR metadata surfaces — the launcher → report → dialog →
/// query → parameter chain a downstream AI consumer walks. Pure and
/// deterministic: no I/O, no environment, no VBA; identical input yields an
/// identical edge set (every collection is sorted before return).
/// </summary>
/// <remarks>
/// <para>Three mechanical producers run over <see cref="IrDatabase"/>:</para>
/// <list type="number">
///   <item><description>
///     <b>Embedded-macro</b> — a control's enriched <c>EventMacros</c>
///     <c>OpenReport</c>/<c>OpenForm</c> action becomes an
///     <see cref="ChainEdgeKind.OpensReport"/>/<see cref="ChainEdgeKind.OpensForm"/>
///     edge from the owning form/report (the launcher → report hop).
///   </description></item>
///   <item><description>
///     <b>Record-source</b> — a form/report <c>RecordSource</c> that names a
///     query/table becomes a <see cref="ChainEdgeKind.FeedsRecordSource"/>
///     edge; an inline-SQL record source is parsed (jet-sql) and emits one
///     <c>FeedsRecordSource</c> edge per FROM/JOIN target plus
///     <see cref="ChainEdgeKind.SuppliesParameter"/> edges for its
///     <c>[Forms]!</c>/<c>[Reports]!</c> references (from the parsed tree)
///     and <c>TempVars!</c> references (textual — the JET grammar models
///     bang-access only for Forms/Reports roots).
///   </description></item>
///   <item><description>
///     <b>Declared-parameter</b> — a query's <c>[Forms]!</c>/<c>[Reports]!</c>
///     parameter becomes a <c>SuppliesParameter</c> edge; a prompt-style
///     declared parameter (runtime user supplies the value) becomes an
///     explicit <see cref="UnresolvedChainTarget"/>.
///   </description></item>
/// </list>
/// <para>
/// Code-behind hops (the report's <c>Report_Open</c> <c>acDialog</c> open,
/// <c>Forms!dlg!ctl</c> reads, and <c>TempVars!x =</c> assignments) need the
/// VBA AST and are the S3 producer's job; this builder touches no VBA.
/// </para>
/// </remarks>
public static class NavigationChainBuilder
{
    /// <summary>The chain edges and unresolved chain targets a build produced.</summary>
    public sealed record Result(
        IReadOnlyList<ChainEdge> Edges,
        IReadOnlyList<UnresolvedChainTarget> UnresolvedTargets);

    // Provenance closed-set values (kebab-case, emitted verbatim).
    private const string ProvEmbeddedMacro = "embedded-macro";
    private const string ProvRecordSource = "record-source";
    private const string ProvQueryFormReference = "query-form-reference";
    private const string ProvQueryDeclaredParameter = "query-declared-parameter";
    private const string ProvTempVarFlow = "tempvar-flow";

    // Unresolved-reason closed-set values.
    private const string ReasonPromptParameter = "prompt-parameter";
    private const string ReasonRecordSourceUnparsed = "record-source-unparsed";
    private const string ReasonEmbeddedMacroUnparsed = "embedded-macro-unparsed";
    private const string ReasonUnresolvableReference = "unresolvable-reference";

    private static readonly Regex InlineSqlPrefix = new(
        @"^\s*(SELECT|PARAMETERS|TRANSFORM)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Builds the chain edges for a harvested database. Returns an empty
    /// result (never null) when no chain relationships exist.
    /// </summary>
    public static Result Build(IrDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);

        var edges = new List<ChainEdge>();
        var unresolved = new List<UnresolvedChainTarget>();

        var queryNames = new HashSet<string>(
            db.Queries.Select(q => q.Name), StringComparer.OrdinalIgnoreCase);
        var tableNames = new HashSet<string>(
            db.Tables.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var obj in db.Forms.Concat(db.Reports))
        {
            CollectEmbeddedMacroEdges(obj, edges, unresolved);
            CollectRecordSourceEdges(obj, queryNames, tableNames, edges, unresolved);
        }

        foreach (var query in db.Queries)
            CollectQueryParameterEdges(query, edges, unresolved);

        return new Result(SortEdges(edges), SortUnresolved(unresolved));
    }

    // ── Producer 1: embedded-macro edges ───────────────────────────────

    private static void CollectEmbeddedMacroEdges(
        IrFormOrReport obj, List<ChainEdge> edges, List<UnresolvedChainTarget> unresolved)
    {
        var ownerKind = NodeKindForObject(obj.ObjectType);

        // Object-level embedded macros (the form/report's own OnOpen / OnLoad /
        // OnClose, e.g. a report's OnOpen that opens a parameter dialog).
        EmitEmbeddedMacroEdges(obj.EventMacros, ownerKind, obj.Name, member: null, edges);
        FlagUnparsedEmbeddedMacros(obj.Properties, obj.EventMacros, ownerKind, obj.Name, member: null, unresolved);

        // Control-level embedded macros (e.g. a launcher button's OnClick).
        foreach (var control in obj.Controls)
        {
            EmitEmbeddedMacroEdges(control.EventMacros, ownerKind, obj.Name, control.Name, edges);
            FlagUnparsedEmbeddedMacros(control.Properties, control.EventMacros, ownerKind, obj.Name, control.Name, unresolved);
        }
    }

    private static void EmitEmbeddedMacroEdges(
        IReadOnlyList<IrControlEventMacro>? eventMacros,
        ChainNodeKind ownerKind, string ownerName, string? member,
        List<ChainEdge> edges)
    {
        if (eventMacros is null) return;
        foreach (var em in eventMacros)
        {
            foreach (var action in em.Actions)
            {
                var (kind, targetKind, nameArg) = action.Action switch
                {
                    var a when Eq(a, "OpenReport") => (ChainEdgeKind.OpensReport, ChainNodeKind.Report, "ReportName"),
                    var a when Eq(a, "OpenForm") => (ChainEdgeKind.OpensForm, ChainNodeKind.Form, "FormName"),
                    _ => (default(ChainEdgeKind), default(ChainNodeKind), (string?)null),
                };
                if (nameArg is null) continue;  // Close/CloseWindow/SetValue/etc. are not chain-open kinds.
                if (!action.Arguments.TryGetValue(nameArg, out var target) || string.IsNullOrWhiteSpace(target))
                    continue;

                edges.Add(new ChainEdge
                {
                    From = new ChainEndpoint { Kind = ownerKind, Name = ownerName, Member = member },
                    To = new ChainEndpoint { Kind = targetKind, Name = target },
                    Kind = kind,
                    Provenance = ProvEmbeddedMacro,
                });
            }
        }
    }

    // A control/object can carry an "<Event>EmMacro" property whose embedded
    // _AXL the enricher did not structure into EventMacros. The raw property
    // is still preserved in the per-object ir/, but no chain edge can be
    // derived from it — surface that explicitly (a possible Opens hop is not
    // silently lost) rather than dropping it.
    private static void FlagUnparsedEmbeddedMacros(
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<IrControlEventMacro>? eventMacros,
        ChainNodeKind ownerKind, string ownerName, string? member,
        List<UnresolvedChainTarget> unresolved)
    {
        var covered = eventMacros is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(eventMacros.Select(e => e.Event), StringComparer.OrdinalIgnoreCase);

        foreach (var key in properties.Keys)
        {
            if (!key.EndsWith("EmMacro", StringComparison.Ordinal) || key.Length <= "EmMacro".Length)
                continue;
            var ev = key[..^"EmMacro".Length];  // "OnClickEmMacro" -> "OnClick"
            if (covered.Contains(ev)) continue;

            unresolved.Add(new UnresolvedChainTarget
            {
                // The embedded macro could not be parsed, so its action kind is
                // unknown; OpensForm is the representative navigational kind.
                Kind = ChainEdgeKind.OpensForm,
                Consumer = new ChainEndpoint { Kind = ownerKind, Name = ownerName, Member = member },
                Provenance = ProvEmbeddedMacro,
                Reason = ReasonEmbeddedMacroUnparsed,
                Detail = ev,
            });
        }
    }

    // ── Producer 2: record-source edges ────────────────────────────────

    private static void CollectRecordSourceEdges(
        IrFormOrReport obj,
        HashSet<string> queryNames,
        HashSet<string> tableNames,
        List<ChainEdge> edges,
        List<UnresolvedChainTarget> unresolved)
    {
        var recordSource = obj.RecordSource;
        if (string.IsNullOrWhiteSpace(recordSource)) return;

        var owner = new ChainEndpoint { Kind = NodeKindForObject(obj.ObjectType), Name = obj.Name };

        if (!InlineSqlPrefix.IsMatch(recordSource))
        {
            // Named record source — a bare query or table name.
            var name = recordSource.Trim().Trim('[', ']').Trim();
            if (name.Length == 0) return;
            edges.Add(new ChainEdge
            {
                From = new ChainEndpoint { Kind = ClassifyName(name, queryNames, tableNames), Name = name },
                To = owner,
                Kind = ChainEdgeKind.FeedsRecordSource,
                Provenance = ProvRecordSource,
            });
            return;
        }

        // Inline SQL record source — parse for FROM/JOIN targets + form refs.
        var collector = new SourceAndRefCollector();
        try
        {
            var result = SqlParserPipeline.Parse(recordSource);
            collector.Visit(result.BuildAst());
        }
        catch
        {
            // Recovering-mode parse threw building/walking the tree; the
            // collector simply carries whatever it gathered before the throw
            // (usually nothing). The zero-source guard below surfaces it.
        }

        foreach (var tableName in collector.TableSources)
        {
            edges.Add(new ChainEdge
            {
                From = new ChainEndpoint { Kind = ClassifyName(tableName, queryNames, tableNames), Name = tableName },
                To = owner,
                Kind = ChainEdgeKind.FeedsRecordSource,
                Provenance = ProvRecordSource,
            });
        }

        foreach (var fr in collector.FormReferences)
        {
            edges.Add(new ChainEdge
            {
                From = fr,
                To = owner,
                Kind = ChainEdgeKind.SuppliesParameter,
                Provenance = ProvQueryFormReference,
            });
        }

        // TempVars! references (textual — not modelled by the JET grammar).
        foreach (var tempVar in ExtractTempVars(recordSource))
        {
            edges.Add(new ChainEdge
            {
                From = new ChainEndpoint { Kind = ChainNodeKind.TempVar, Name = tempVar },
                To = owner,
                Kind = ChainEdgeKind.SuppliesParameter,
                Provenance = ProvTempVarFlow,
            });
        }

        // No FROM/JOIN data source recovered from an inline record source —
        // the parser either failed or recovered to an AST without a usable
        // table source. The record source's data origin is therefore unknown;
        // surface it explicitly (never a silent loss) instead of relying on a
        // "no exception" heuristic that would mask a recovered-but-empty parse.
        if (collector.TableSources.Count == 0)
        {
            unresolved.Add(new UnresolvedChainTarget
            {
                Kind = ChainEdgeKind.FeedsRecordSource,
                Consumer = owner,
                Provenance = ProvRecordSource,
                Reason = ReasonRecordSourceUnparsed,
                Detail = Truncate(recordSource.Trim(), 120),
            });
        }
    }

    // ── Producer 3: declared-parameter edges ───────────────────────────

    private static void CollectQueryParameterEdges(
        IrQuery query,
        List<ChainEdge> edges,
        List<UnresolvedChainTarget> unresolved)
    {
        if (query.Parameters is null) return;
        var consumer = new ChainEndpoint { Kind = ChainNodeKind.Query, Name = query.Name };

        foreach (var p in query.Parameters)
        {
            switch (p.Source)
            {
                case "form-reference":
                case "report-reference":
                    var refKind = p.Source == "report-reference" ? ChainNodeKind.Report : ChainNodeKind.Form;
                    var (refName, refMember) = ParseFormRefPath(p.Path);
                    if (refName is null)
                    {
                        // The reference path was blank or malformed — record it
                        // explicitly rather than fabricating a "?" supplier.
                        unresolved.Add(new UnresolvedChainTarget
                        {
                            Kind = ChainEdgeKind.SuppliesParameter,
                            Consumer = consumer,
                            Provenance = ProvQueryFormReference,
                            Reason = ReasonUnresolvableReference,
                            Detail = p.Path ?? p.Name,
                        });
                    }
                    else
                    {
                        edges.Add(new ChainEdge
                        {
                            From = new ChainEndpoint { Kind = refKind, Name = refName, Member = refMember },
                            To = consumer,
                            Kind = ChainEdgeKind.SuppliesParameter,
                            Provenance = ProvQueryFormReference,
                        });
                    }
                    break;

                case "declared":
                    // Prompt-style declared parameter — the runtime user is
                    // the supplier; record it explicitly as unresolved.
                    unresolved.Add(new UnresolvedChainTarget
                    {
                        Kind = ChainEdgeKind.SuppliesParameter,
                        Consumer = consumer,
                        Provenance = ProvQueryDeclaredParameter,
                        Reason = ReasonPromptParameter,
                        Detail = p.Name,
                    });
                    break;
            }
        }
    }

    // ── SQL collector ──────────────────────────────────────────────────

    // Walks a parsed record-source statement collecting FROM/JOIN target
    // names (every SimpleTableSource, including those nested in JOINs and
    // subqueries) and [Forms]!/[Reports]! references anywhere in the tree.
    private sealed class SourceAndRefCollector : SqlSyntaxWalker
    {
        private readonly List<string> _sources = new();
        private readonly HashSet<string> _seenSources = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ChainEndpoint> _formRefs = new();
        private readonly HashSet<string> _seenRefs = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> TableSources => _sources;
        public IReadOnlyList<ChainEndpoint> FormReferences => _formRefs;

        public override void VisitSimpleTableSource(SimpleTableSource n)
        {
            var name = n.TableName?.Trim().Trim('[', ']').Trim();
            if (!string.IsNullOrEmpty(name) && _seenSources.Add(name))
                _sources.Add(name);
            base.VisitSimpleTableSource(n);
        }

        public override void VisitFormReference(FormReference n)
        {
            var isReport = n.Root is ReportsRoot;
            var (objName, member) = FirstTwoSegments(n.Segments);
            if (objName is not null)
            {
                var key = $"{(isReport ? "R" : "F")}|{objName}|{member}";
                if (_seenRefs.Add(key))
                {
                    _formRefs.Add(new ChainEndpoint
                    {
                        Kind = isReport ? ChainNodeKind.Report : ChainNodeKind.Form,
                        Name = objName,
                        Member = member,
                    });
                }
            }
            base.VisitFormReference(n);
        }

        private static (string? ObjectName, string? Member) FirstTwoSegments(IReadOnlyList<RefSegment> segments)
        {
            // Forms!frmX!ctlY -> (frmX, ctlY); Forms!frmX -> (frmX, null).
            string? obj = segments.Count > 0 ? segments[0].Name : null;
            string? member = segments.Count > 1 ? segments[1].Name : null;
            return (obj, member);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    // Recovers `TempVars!name` / `TempVars![name]` references from a record
    // source. The JET grammar has no generic postfix-bang (only Forms/Reports
    // roots form a FormReference), so these are not parseable nodes — they are
    // recovered with a small TOKEN-AWARE scan that skips string-literal and
    // bracketed-identifier spans wholesale, so neither a quoted literal nor a
    // bracketed field name containing the token can fabricate an edge. A real
    // `TempVars![member]` is matched directly (its `TempVars` is at top level,
    // not inside a bracketed span; the member bracket is consumed by the match).
    private static IReadOnlyList<string> ExtractTempVars(string sql)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        int i = 0, n = sql.Length;
        while (i < n)
        {
            char c = sql[i];
            if (c == '\'' || c == '"') { i = SkipQuoted(sql, i); continue; }
            if (c == '[') { i = SkipBracket(sql, i); continue; }
            if (TryMatchTempVar(sql, i, out int next, out var name))
            {
                if (name is not null && seen.Add(name)) result.Add(name);
                i = next;
                continue;
            }
            i++;
        }
        return result;
    }

    // Skips a single-/double-quoted literal at i (sql[i] is the quote),
    // honouring doubled-quote escapes; returns the index past the closing quote.
    private static int SkipQuoted(string sql, int i)
    {
        char q = sql[i];
        int n = sql.Length;
        for (i++; i < n; i++)
        {
            if (sql[i] != q) continue;
            if (i + 1 < n && sql[i + 1] == q) { i++; continue; }  // escaped quote
            return i + 1;
        }
        return n;  // unterminated
    }

    // Skips a JET bracketed identifier [..] at i (sql[i] == '['); brackets do
    // not nest and have no escape, so the span ends at the first ']'.
    private static int SkipBracket(string sql, int i)
    {
        int close = sql.IndexOf(']', i + 1);
        return close < 0 ? sql.Length : close + 1;
    }

    // At a top-level (non-quoted, non-bracketed) position, matches
    // `TempVars` <ws> `!` <ws> (identifier | [member]) with a left identifier
    // boundary. Returns the TempVar name and the index past the match.
    private static bool TryMatchTempVar(string sql, int i, out int next, out string? name)
    {
        next = i; name = null;
        const string kw = "TempVars";
        if (i + kw.Length > sql.Length) return false;
        if (string.Compare(sql, i, kw, 0, kw.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
        if (i > 0)
        {
            char prev = sql[i - 1];
            if (char.IsLetterOrDigit(prev) || prev == '_') return false;  // substring guard
        }

        int j = SkipWs(sql, i + kw.Length);
        if (j >= sql.Length || sql[j] != '!') return false;
        j = SkipWs(sql, j + 1);
        if (j >= sql.Length) return false;

        if (sql[j] == '[')
        {
            int close = sql.IndexOf(']', j + 1);
            if (close < 0) return false;
            name = sql.Substring(j + 1, close - j - 1).Trim();
            next = close + 1;
            return name.Length > 0;
        }

        if (!(char.IsLetter(sql[j]) || sql[j] == '_')) return false;
        int start = j++;
        while (j < sql.Length && (char.IsLetterOrDigit(sql[j]) || sql[j] == '_')) j++;
        name = sql.Substring(start, j - start);
        next = j;
        return true;
    }

    private static int SkipWs(string sql, int i)
    {
        while (i < sql.Length && char.IsWhiteSpace(sql[i])) i++;
        return i;
    }

    // Parses the verbatim reference path (e.g. "[Forms]![frmCustomers]![txtId]")
    // into (object, control). Returns (null, null) when the path is blank,
    // does not start at a Forms/Reports root, or names no object — the caller
    // then surfaces an explicit unresolved entry rather than fabricating a
    // synthetic endpoint. Positional: empty `!` segments are kept (NOT dropped)
    // so a malformed path like "[Forms]!![txtId]" cannot collapse into a
    // fabricated supplier.
    private static (string? ObjectName, string? Member) ParseFormRefPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (null, null);

        var segments = path.Split('!')
            .Select(s => s.Trim().Trim('[', ']').Trim())
            .ToList();
        // Valid shapes are EXACTLY [Forms|Reports]![obj] or
        // [Forms|Reports]![obj]![member]. A different segment count (e.g. the
        // malformed "[Forms]![frm]!![txtId]") or an empty object/member slot is
        // rejected so it cannot collapse into a fabricated supplier.
        if (segments.Count is not (2 or 3)) return (null, null);
        if (!Eq(segments[0], "Forms") && !Eq(segments[0], "Reports")) return (null, null);
        if (segments[1].Length == 0) return (null, null);
        if (segments.Count == 3 && segments[2].Length == 0) return (null, null);
        return (segments[1], segments.Count == 3 ? segments[2] : null);
    }

    private static ChainNodeKind ClassifyName(string name, HashSet<string> queryNames, HashSet<string> tableNames)
    {
        if (queryNames.Contains(name)) return ChainNodeKind.Query;
        if (tableNames.Contains(name)) return ChainNodeKind.Table;
        return ChainNodeKind.Unknown;
    }

    private static ChainNodeKind NodeKindForObject(string objectType) =>
        Eq(objectType, "Report") ? ChainNodeKind.Report : ChainNodeKind.Form;

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max);

    // ── Determinism ────────────────────────────────────────────────────

    private static IReadOnlyList<ChainEdge> SortEdges(List<ChainEdge> edges)
    {
        edges.Sort(static (a, b) =>
        {
            int c = ((int)a.Kind).CompareTo((int)b.Kind);
            if (c != 0) return c;
            c = CompareEndpoint(a.From, b.From);
            if (c != 0) return c;
            c = CompareEndpoint(a.To, b.To);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Provenance, b.Provenance);
        });
        return edges;
    }

    private static IReadOnlyList<UnresolvedChainTarget> SortUnresolved(List<UnresolvedChainTarget> items)
    {
        items.Sort(static (a, b) =>
        {
            int c = ((int)a.Kind).CompareTo((int)b.Kind);
            if (c != 0) return c;
            c = CompareEndpoint(a.Consumer, b.Consumer);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Provenance, b.Provenance);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Reason, b.Reason);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Detail ?? "", b.Detail ?? "");
        });
        return items;
    }

    private static int CompareEndpoint(ChainEndpoint a, ChainEndpoint b)
    {
        int c = ((int)a.Kind).CompareTo((int)b.Kind);
        if (c != 0) return c;
        // Case-insensitive primary (Access names are case-insensitive), then an
        // Ordinal tie-breaker so case-only-differing names get a total, stable
        // order (List.Sort is not stable).
        c = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
        if (c != 0) return c;
        c = string.CompareOrdinal(a.Name, b.Name);
        if (c != 0) return c;
        c = StringComparer.OrdinalIgnoreCase.Compare(a.Member ?? "", b.Member ?? "");
        if (c != 0) return c;
        return string.CompareOrdinal(a.Member ?? "", b.Member ?? "");
    }
}
