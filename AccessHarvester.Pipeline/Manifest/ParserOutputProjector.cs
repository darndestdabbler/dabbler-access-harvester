using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.DefaultMembers;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using ParserPropertyKind = AccessHarvester.Vba.Parser.Ast.PropertyKind;
using ResolverPropertyDeclaration = AccessHarvester.Vba.Resolver.Declarations.PropertyDeclaration;
using ResolverProcedureDecl = AccessHarvester.Vba.Resolver.Declarations.ProcedureDeclaration;
using ResolverEnumDeclaration = AccessHarvester.Vba.Resolver.Declarations.EnumDeclaration;
using ResolverEventDeclaration = AccessHarvester.Vba.Resolver.Declarations.EventDeclaration;
using ParserProcedureDecl = AccessHarvester.Vba.Parser.Ast.ProcedureDeclaration;

namespace AccessHarvester.Pipeline.Manifest;

/// <summary>
/// Projects a <see cref="ResolvedModule"/> + its
/// <see cref="FunctionClassification"/> list into the v1.0
/// <c>ParserOutput</c> JSON shape that
/// <see cref="ModuleExport.ParserOutput"/> and
/// <see cref="FormExport.ParserOutput"/> expect.
/// </summary>
public static class ParserOutputProjector
{
    /// <summary>
    /// Projects one VBA module's resolver output into the v1.0
    /// <c>ParserOutput</c> JSON object.
    /// </summary>
    /// <param name="resolvedModule">Resolver output for the module.</param>
    /// <param name="classifications">
    /// Per-procedure classifications for this module (from
    /// <see cref="FunctionClassificationBuilder.BuildAll"/>). May be a
    /// superset; only entries whose <c>ModuleName</c> matches
    /// <paramref name="moduleName"/> are used.
    /// </param>
    /// <param name="moduleName">
    /// Logical module name — used as <c>Source.ModuleName</c> in the
    /// JSON and for filtering <paramref name="classifications"/>.
    /// </param>
    /// <param name="moduleKind">Standard or ClassModule.</param>
    public static JsonNode Project(
        ResolvedModule resolvedModule,
        IReadOnlyList<FunctionClassification> classifications,
        string moduleName,
        ModuleKind moduleKind)
    {
        if (resolvedModule is null) throw new ArgumentNullException(nameof(resolvedModule));
        if (classifications is null) throw new ArgumentNullException(nameof(classifications));
        if (moduleName is null) throw new ArgumentNullException(nameof(moduleName));

        // Classification lookup: (ProcedureName, ProcedureKind) → classification.
        // Filter to only the supplied module's entries.
        var classMap = new Dictionary<(string, ProcedureKind), FunctionClassification>();
        foreach (var c in classifications)
        {
            if (!string.Equals(c.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                continue;
            classMap[(c.ProcedureName, c.Kind)] = c;
        }

        // Group module.References by enclosing ProcedureScope.
        // Module-level references (no ProcedureScope ancestor) are dropped —
        // the v1.0 schema has no module-level item type.
        var refsByProc = GroupReferencesByProcedure(resolvedModule);

        var items = new JsonArray();
        foreach (var item in resolvedModule.Ast.Items)
        {
            JsonNode? node = item switch
            {
                ParserProcedureDecl proc => ProjectProcedure(proc, classMap, refsByProc),
                DeclareStatement decl => ProjectDeclare(decl),
                _ => null,
            };
            if (node is not null)
                items.Add(node);
        }

        // ParserSource.Kind maps the module's host category to the schema enum.
        // ClassModule → Form/Report/Class; Standard → Procedure (Database is unused here).
        string sourceKind = moduleKind switch
        {
            ModuleKind.ClassModule => "Form",   // covers form, report, and class modules
            _ => "Procedure",                   // standard VBA modules
        };

        return new JsonObject
        {
            // 1.1 (008 Session 3, F-3-6): ResolvedReference now carries an
            // optional ResolutionReason on Unresolved references — the
            // resolver's closed reason set (UnresolvedReasons), previously
            // dropped at this projection layer. Additive: the field is
            // optional and the manifest envelope schema's ResolvedReference
            // $def already tolerated it (no additionalProperties:false), so
            // old consumers ignore it. The ParserOutput SchemaVersion is a
            // free pattern string in the schema, not a const, so the bump
            // costs no manifest-envelope (v1.1) churn.
            ["SchemaVersion"] = "1.1",
            ["Source"] = new JsonObject
            {
                ["Kind"] = sourceKind,
                ["Location"] = new JsonObject
                {
                    ["File"] = moduleName,
                    ["Module"] = moduleName,
                    ["StartLine"] = 1,
                    ["StartColumn"] = 1,
                },
            },
            ["Items"] = items,
        };
    }

    // ── Reference grouping ────────────────────────────────────────────

    /// <summary>
    /// Enumerates every procedure-scoped reference (body AND header) that
    /// <see cref="Project"/> groups into the per-procedure
    /// <c>Body.References</c>, paired with the enclosing procedure's name.
    /// DW-11 Layer A: <see cref="UnresolvedItemAggregator"/> consumes this
    /// (wrapped as <see cref="ProcedureScopedReference"/>) to surface
    /// procedure-HEADER unresolved refs — an Optional parameter's default
    /// expression — that the body-only
    /// <see cref="FunctionClassification.UnresolvedReferences"/> feed drops.
    /// Because it reuses the SAME grouping <see cref="Project"/> uses,
    /// the aggregated index is a structural superset of the inline
    /// <c>Body.References</c> by construction. Module-level references (no
    /// enclosing <see cref="ProcedureScope"/>) are excluded here exactly as
    /// <see cref="Project"/> drops them — Layer B owns the module-level home.
    /// </summary>
    public static IEnumerable<(string ProcedureName, ResolvedReference Reference)>
        EnumerateProcedureScopedReferences(ResolvedModule resolvedModule)
    {
        if (resolvedModule is null) throw new ArgumentNullException(nameof(resolvedModule));

        foreach (var kv in GroupReferencesByProcedure(resolvedModule))
            foreach (var reference in kv.Value)
                yield return (kv.Key.Name, reference);
    }

    /// <summary>
    /// Enumerates every MODULE-LEVEL reference — one whose
    /// <see cref="ResolvedReference.EnclosingScope"/> has no enclosing
    /// <see cref="ProcedureScope"/> ancestor — i.e. exactly the references
    /// <see cref="GroupReferencesByProcedure"/> drops. DW-11 Layer B: a
    /// module-level unresolved reference (a <c>Const</c>/<c>Dim</c> initializer,
    /// an <c>Implements &lt;unresolved&gt;</c>, a module <c>Private x As New
    /// &lt;unresolved&gt;</c>) lives on <see cref="ResolvedModule.References"/>
    /// but has no procedure <c>Body</c> to surface in, so it appears in neither
    /// <c>Body.References</c> nor — before S2 — <c>unresolved-items.json</c>.
    /// <see cref="UnresolvedItemAggregator"/> consumes this (wrapped as
    /// <see cref="ModuleLevelReference"/>) to give those references the
    /// run-wide index as their manifest home, at module scope
    /// (<c>ProcedureName == null</c>). These references are, by construction,
    /// disjoint from <see cref="EnumerateProcedureScopedReferences"/> (that
    /// enumerator yields only references WITH an enclosing
    /// <see cref="ProcedureScope"/>), so the two feeds never double-count.
    /// </summary>
    public static IEnumerable<ResolvedReference>
        EnumerateModuleLevelReferences(ResolvedModule resolvedModule)
    {
        if (resolvedModule is null) throw new ArgumentNullException(nameof(resolvedModule));

        foreach (var reference in resolvedModule.References)
            if (FindEnclosingProcedureScope(reference.EnclosingScope) is null)
                yield return reference;
    }

    private static Dictionary<(string Name, ProcedureKind Kind), List<ResolvedReference>>
        GroupReferencesByProcedure(ResolvedModule resolvedModule)
    {
        var result = new Dictionary<(string, ProcedureKind), List<ResolvedReference>>();
        foreach (var reference in resolvedModule.References)
        {
            var ps = FindEnclosingProcedureScope(reference.EnclosingScope);
            if (ps is null) continue;

            var key = ProcedureScopeKey(ps);
            if (!result.TryGetValue(key, out var list))
            {
                list = [];
                result[key] = list;
            }
            list.Add(reference);
        }
        return result;
    }

    private static ProcedureScope? FindEnclosingProcedureScope(Scope scope)
    {
        var current = scope;
        while (current is not null)
        {
            if (current is ProcedureScope ps) return ps;
            current = current.Parent;
        }
        return null;
    }

    // Maps a ProcedureScope's resolver declaration to the (Name, Kind) key
    // that matches FunctionClassification entries.
    private static (string Name, ProcedureKind Kind) ProcedureScopeKey(ProcedureScope ps)
    {
        var kind = ps.Procedure switch
        {
            SubDeclaration => ProcedureKind.Sub,
            FunctionDeclaration => ProcedureKind.Function,
            ResolverPropertyDeclaration p => p.Kind switch
            {
                ParserPropertyKind.Get => ProcedureKind.PropertyGet,
                ParserPropertyKind.Let => ProcedureKind.PropertyLet,
                ParserPropertyKind.Set => ProcedureKind.PropertySet,
                _ => ProcedureKind.Sub,
            },
            _ => ProcedureKind.Sub,
        };
        return (ps.Procedure.Name, kind);
    }

    // ── Procedure projection ──────────────────────────────────────────

    private static JsonNode ProjectProcedure(
        ParserProcedureDecl proc,
        Dictionary<(string, ProcedureKind), FunctionClassification> classMap,
        Dictionary<(string, ProcedureKind), List<ResolvedReference>> refsByProc)
    {
        var classKind = ClassificationKindOf(proc);
        var refs = refsByProc.TryGetValue((proc.Name, classKind), out var list)
            ? list
            : (IReadOnlyList<ResolvedReference>)Array.Empty<ResolvedReference>();

        classMap.TryGetValue((proc.Name, classKind), out var classification);

        string schemaKind = SchemaKindOf(proc);
        JsonNode? eventBinding = null;
        var binding = TryExtractEventBinding(proc.Name);
        if (binding is not null)
        {
            schemaKind = "EventHandler";
            eventBinding = new JsonObject
            {
                ["ControlName"] = binding.Value.Control,
                ["EventName"] = binding.Value.Event,
            };
        }

        string visibility = proc.Access switch
        {
            AccessModifier.Private => "Private",
            AccessModifier.Friend => "Friend",
            _ => "Public",
        };

        var node = new JsonObject
        {
            ["ItemKind"] = "Procedure",
            ["Name"] = proc.Name,
            ["ProcedureKind"] = schemaKind,
            ["Visibility"] = visibility,
            ["IsStatic"] = proc.IsStatic,
            ["Signature"] = BuildSignature(proc.Parameters, proc.ReturnType),
            ["Body"] = BuildBody(refs),
        };

        if (eventBinding is not null)
            node["EventBinding"] = eventBinding;
        if (classification is not null)
            node["Classification"] = BuildClassification(classification);
        if (proc.Provenance.Range is { } range)
            node["StartLine"] = range.Start.Line;

        return node;
    }

    private static JsonNode ProjectDeclare(DeclareStatement decl)
    {
        string schemaKind = decl.Kind == DeclareKind.Function ? "DeclareFunction" : "DeclareSub";
        string visibility = decl.Access switch
        {
            AccessModifier.Private => "Private",
            AccessModifier.Friend => "Friend",
            _ => "Public",
        };

        var sig = BuildSignature(decl.Parameters, decl.ReturnType);

        var node = new JsonObject
        {
            ["ItemKind"] = "Procedure",
            ["Name"] = decl.Name,
            ["ProcedureKind"] = schemaKind,
            ["Visibility"] = visibility,
            ["IsStatic"] = false,
            ["Lib"] = decl.LibraryName,
            ["Signature"] = sig,
            // SqlStrings omitted (F-3-4) — see BuildBody for rationale. A
            // Declare statement has no body to analyze for SQL anyway.
            ["Body"] = new JsonObject
            {
                ["References"] = new JsonArray(),
                ["Calls"] = new JsonArray(),
            },
        };
        if (decl.AliasName is not null)
            node["Alias"] = decl.AliasName;
        return node;
    }

    // ── Signature ─────────────────────────────────────────────────────

    private static JsonNode BuildSignature(
        IReadOnlyList<Parameter> parameters, TypeReference? returnType)
    {
        var paramsArr = new JsonArray();
        foreach (var p in parameters)
            paramsArr.Add(BuildParameter(p));

        var sig = new JsonObject { ["Parameters"] = paramsArr };
        if (returnType is not null)
            sig["ReturnType"] = string.Join(".", returnType.QualifiedName);
        return sig;
    }

    private static JsonNode BuildParameter(Parameter p)
    {
        var node = new JsonObject { ["Name"] = p.Name };
        if (p.Type is not null) node["Type"] = string.Join(".", p.Type.QualifiedName);
        // 006 F-3-5: emit the schema-declared PassingMechanism enum for both explicit
        // pass modes (the prior code emitted a bare "ByRef" boolean only, so explicit
        // ByVal was indistinguishable from default ByRef). Per the output-omission
        // convention, an unspecified pass mode is omitted (consumer reads omission as
        // the VBA default, ByRef).
        if (p.PassMode == ParameterPassMode.ByVal) node["PassingMechanism"] = "ByVal";
        else if (p.PassMode == ParameterPassMode.ByRef) node["PassingMechanism"] = "ByRef";
        if (p.Modifier == ParameterModifier.Optional) node["Optional"] = true;
        if (p.Modifier == ParameterModifier.ParamArray) node["ParamArray"] = true;
        return node;
    }

    // ── Body (References + Calls + SqlStrings) ────────────────────────

    private static JsonNode BuildBody(IReadOnlyList<ResolvedReference> refs)
    {
        var references = new JsonArray();
        var calls = new JsonArray();

        // Sort by full location then expression text to produce a total order
        // that is deterministic regardless of upstream enumeration order.
        var sorted = refs
            .OrderBy(r => r.Location.Start.Line)
            .ThenBy(r => r.Location.Start.Column)
            .ThenBy(r => r.Location.End.Line)
            .ThenBy(r => r.Location.End.Column)
            .ThenBy(r => ExpressionText(r.Expression), StringComparer.Ordinal)
            .ToList();

        foreach (var r in sorted)
        {
            references.Add(BuildResolvedReference(r));
            if (IsCallSite(r))
                calls.Add(BuildCallSite(r));
        }

        // F-3-4: SqlStrings is OMITTED, not emitted as an empty array. The
        // DynamicSqlAnalyzer that would populate it has no production call
        // site yet (deferred to a future dynamic-SQL wiring set — DW-8); an
        // always-empty array synthesizes the forbidden default "this
        // procedure builds no SQL" (project-guidance → Output JSON
        // Conventions: only emit fields the producer actually wrote). The
        // schema marks Body.SqlStrings optional, so omission is valid and
        // reads as "not analyzed" rather than "analyzed, none found".
        return new JsonObject
        {
            ["References"] = references,
            ["Calls"] = calls,
        };
    }

    private static bool IsCallSite(ResolvedReference r)
    {
        var decl = FlattenDeclaration(r.Outcome);
        return decl is ResolverProcedureDecl or DeclareDeclaration;
    }

    private static JsonNode BuildCallSite(ResolvedReference r)
    {
        return new JsonObject
        {
            ["CalleeText"] = ExpressionText(r.Expression),
            ["Location"] = BuildLocation(r.Location),
            ["ResolvedCallee"] = BuildResolvedReference(r),
        };
    }

    private static JsonNode BuildResolvedReference(ResolvedReference r)
    {
        var (status, kind, target, confidence, reason) = MapOutcome(r.Outcome);

        var node = new JsonObject
        {
            ["OriginalText"] = ExpressionText(r.Expression),
            ["Location"] = BuildLocation(r.Location),
            ["ResolutionStatus"] = status,
        };

        if (kind is not null) node["ReferenceKind"] = kind;
        if (target is not null) node["ResolvedTarget"] = target;
        if (confidence is double conf) node["Confidence"] = conf;
        // F-3-6: surface the resolver's reason for an unresolved reference
        // (the closed UnresolvedReasons set). The project principle is
        // "unresolvable constructs are explicit, not silent"; the resolver
        // honored it and this layer previously erased the explanation.
        if (reason is not null) node["ResolutionReason"] = reason;

        return node;
    }

    private static JsonNode BuildLocation(SourceRange range)
    {
        return new JsonObject
        {
            ["StartLine"] = range.Start.Line,
            ["StartColumn"] = range.Start.Column,
            ["EndLine"] = range.End.Line,
            ["EndColumn"] = range.End.Column,
        };
    }

    // ── Outcome mapping ───────────────────────────────────────────────

    private static (string Status, string? Kind, JsonNode? Target, double? Confidence, string? Reason)
        MapOutcome(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Resolved r => (
            "Resolved",
            DeclarationKindString(r.Declaration),
            BuildDeclarationTarget(r.Declaration),
            1.0, null),

        ResolutionOutcome.ResolvedToObjectModel r => (
            "Resolved",
            "ObjectModel",
            BuildDeclarationTarget(r.Declaration),
            1.0, null),

        ResolutionOutcome.ResolvedToBuiltIn r => (
            "Resolved",
            "BuiltIn",
            new JsonObject { ["Name"] = r.Function.Name, ["Category"] = r.Function.Category.ToString() },
            1.0, null),

        ResolutionOutcome.ResolvedToMe r => (
            "Resolved",
            r.OwningClass.Kind == ClassDeclarationKind.ReportModule ? "Report" : "Form",
            new JsonObject
            {
                ["Kind"] = r.OwningClass.Kind == ClassDeclarationKind.ReportModule ? "Report" : "Form",
                ["Name"] = r.OwningClass.Name,
            },
            1.0, null),

        ResolutionOutcome.ResolvedViaWithFlattening f =>
            MapOutcomeWithFlattening(f),

        // The reason string is preserved verbatim from the resolver's
        // closed UnresolvedReasons set so the consumer can distinguish a
        // genuine miss (not-in-scope) from a known capability gap
        // (default-member-not-yet-resolved) without re-deriving it.
        ResolutionOutcome.Unresolved u =>
            ("Unresolved", null, null, null, u.Reason),

        _ => ("Unresolved", null, null, null, null),
    };

    private static (string Status, string? Kind, JsonNode? Target, double? Confidence, string? Reason)
        MapOutcomeWithFlattening(ResolutionOutcome.ResolvedViaWithFlattening f)
    {
        // A flattened implicit-with member can itself end up Unresolved
        // (e.g. the member name was not found on the With target); carry
        // the inner reason forward so the explanation is not lost behind
        // the 0.9 flattening-confidence wrapper.
        var (status, kind, target, _, reason) = MapOutcome(f.InnerOutcome);
        return (status, kind, target, 0.9, reason);
    }

    private static Declaration? FlattenDeclaration(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Resolved r => r.Declaration,
        ResolutionOutcome.ResolvedToObjectModel r => r.Declaration,
        ResolutionOutcome.ResolvedViaWithFlattening f => FlattenDeclaration(f.InnerOutcome),
        _ => null,
    };

    private static JsonNode? BuildDeclarationTarget(Declaration decl)
    {
        // F-3-4: a bang-access default-member binding (`rs!Notes`) carries
        // the default member it resolved through — NOT a fabricated symbol
        // for the bang key. The target conveys, using existing schema
        // fields only: the default member (Name), the type that owns it
        // (Container), the member's return type (Type), and the honest
        // access path (FullyQualifiedName, e.g. `Recordset.Fields("Notes")`).
        // The bang key appears only inside that path string, never as a
        // resolved member Name.
        if (decl is SyntheticDefaultMemberDeclaration dm)
        {
            var target = new JsonObject
            {
                ["Kind"] = "DefaultMember",
                ["Name"] = dm.DefaultMember.MemberName,
                ["Container"] = dm.DefaultMember.TypeName,
                ["FullyQualifiedName"] =
                    $"{dm.DefaultMember.TypeName}.{dm.DefaultMember.MemberName}(\"{dm.AccessedName}\")",
            };
            if (dm.DefaultMember.ReturnType is { } returnType)
                target["Type"] = returnType;
            return target;
        }

        // Omit `Kind` when the declaration type is one DeclarationKindString
        // does not enumerate (the `_ => null` arm) rather than writing
        // `"Kind": null`. A null on disk is never correct (omit-null
        // convention) AND it is schema-invalid: the harvester schema types
        // ResolvedTarget.Kind as a string, so a null tripped
        // ManifestSchemaConformanceTests. Discovered in 011 S2 while running
        // the full schema-conformance gate (S1's F-3-4 wiring routed more
        // declarations through this projector, surfacing the latent null);
        // the fix is an F-3-4 conformance correction, not an F-2-2 delta.
        var node = new JsonObject { ["Name"] = decl.Name };
        var kind = DeclarationKindString(decl);
        if (kind is not null) node["Kind"] = kind;
        return node;
    }

    private static string? DeclarationKindString(Declaration decl) => decl switch
    {
        ResolverProcedureDecl => "Procedure",
        DeclareDeclaration => "External",
        VariableDeclaration => "Variable",
        ParameterDeclaration => "Parameter",
        ResolverEnumDeclaration => "Enum",
        EnumMemberDeclaration => "Constant",
        UserDefinedTypeDeclaration => "Type",
        ClassDeclaration c => c.Kind switch
        {
            ClassDeclarationKind.FormModule => "Form",
            ClassDeclarationKind.ReportModule => "Report",
            _ => "Module",
        },
        ResolverEventDeclaration => "Procedure",
        _ => null,
    };

    // ── Classification ────────────────────────────────────────────────

    private static JsonNode BuildClassification(FunctionClassification c)
    {
        return new JsonObject
        {
            ["Purity"] = c.PrimaryCategory.ToString(),
            ["Complexity"] = c.Metrics.CyclomaticComplexity,
            ["LinesOfCode"] = c.Metrics.LinesOfCode,
        };
    }

    // ── Kind mapping ──────────────────────────────────────────────────

    private static ProcedureKind ClassificationKindOf(ParserProcedureDecl proc) => proc switch
    {
        SubProcedure => ProcedureKind.Sub,
        FunctionProcedure => ProcedureKind.Function,
        PropertyProcedure { Kind: ParserPropertyKind.Get } => ProcedureKind.PropertyGet,
        PropertyProcedure { Kind: ParserPropertyKind.Let } => ProcedureKind.PropertyLet,
        PropertyProcedure { Kind: ParserPropertyKind.Set } => ProcedureKind.PropertySet,
        _ => ProcedureKind.Sub,
    };

    private static string SchemaKindOf(ParserProcedureDecl proc) => proc switch
    {
        SubProcedure => "Sub",
        FunctionProcedure => "Function",
        PropertyProcedure { Kind: ParserPropertyKind.Get } => "PropertyGet",
        PropertyProcedure { Kind: ParserPropertyKind.Let } => "PropertyLet",
        PropertyProcedure { Kind: ParserPropertyKind.Set } => "PropertySet",
        _ => "Sub",
    };

    // ── Event binding ─────────────────────────────────────────────────

    // Checks whether a procedure name matches the VBA event-handler
    // convention (<Control>_<Event>) by splitting on the LAST underscore.
    // Returns (ControlName, EventName) when the name qualifies, null otherwise.
    internal static (string Control, string Event)? TryExtractEventBinding(string name)
    {
        int last = name.LastIndexOf('_');
        if (last <= 0 || last == name.Length - 1)
            return null;

        string control = name[..last];
        string eventName = name[(last + 1)..];

        if (!IsIdentifierLike(control) || !IsIdentifierLike(eventName))
            return null;

        return (control, eventName);
    }

    private static bool IsIdentifierLike(string s)
    {
        if (s.Length == 0) return false;
        if (!char.IsLetter(s[0]) && s[0] != '_') return false;
        foreach (char c in s)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }
        return true;
    }

    // ── Expression text ───────────────────────────────────────────────

    // Best-effort text reconstruction from AST nodes. Used as the
    // OriginalText field in the JSON output; close enough for diagnostic
    // purposes even when whitespace or casing differs from source.
    internal static string ExpressionText(Expression expr) => expr switch
    {
        IdentifierExpression i => i.TypeSuffix is null ? i.Name : i.Name + i.TypeSuffix,
        MeExpression => "Me",
        MemberAccessExpression m => ExpressionText(m.Target) + "." + m.MemberName,
        BangAccessExpression b => ExpressionText(b.Target) + "!" + b.MemberName,
        ImplicitWithMemberExpression w => "." + w.MemberName,
        ImplicitWithBangExpression w => "!" + w.MemberName,
        IndexOrCallExpression c => ExpressionText(c.Target) + "()",
        LiteralExpression l => l.Text,
        TrueExpression => "True",
        FalseExpression => "False",
        NothingExpression => "Nothing",
        NullExpression => "Null",
        EmptyExpression => "Empty",
        DebugExpression => "Debug",
        _ => "<expression>",
    };
}
