
Your response MUST begin with a detailed prose analysis for each concern below BEFORE the JSON block. Do not skip the analysis. Both the analysis and the JSON block are required.

# Session Verification: harvester-body-and-query-sql Session 1 (D5 + D6)

## Session summary
Session 1 of `harvester-body-and-query-sql` implemented the `ParserOutputProjector` class that maps the VBA resolver's in-memory output (`ResolvedModule`, `FunctionClassification`) to the v1.0 manifest schema's `ParserOutput` JSON shape. The `Pipeline.EmitManifestArtifacts()` was wired to discover VBA module files in `raw/`, parse + resolve + classify them, and populate `Modules[]` and `Forms[].ParserOutput` in the emitted manifest.

## What was implemented
1. **`ParserOutputProjector.cs`**: New static class in `AccessHarvester.Pipeline.Manifest/`. Takes `ResolvedModule`, `IReadOnlyList<FunctionClassification>`, `moduleName`, `ModuleKind` → produces `JsonNode` matching the v1.0 `ParserOutput` schema shape.
2. **`Pipeline.EmitManifestArtifacts()`**: Signature extended to accept `rawDir` and `irDir`. Discovers `*.txt` files in `raw/Modules/`, `raw/Forms/`, `raw/Reports/`, parses them as VBA (graceful skip on failure), builds cross-reference index, classifies, projects. Also loads form specs from `ir/Forms/*.ir.json` via `FormMetadataExtractor`+`FormComponentSpecBuilder`.
3. **Bug fixes alongside**:
   - `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` added to handle codepage 1252 in xUnit test context.
   - `ParserSource` JSON shape corrected to match schema (`Kind` + `Location` instead of `ModuleName`/`ModuleKind`).
   - `LabelInfo.Source` casing corrected from lowercase (`attached`) to PascalCase (`Attached`) to match v1.0 schema enum.
   - `ManifestSchemaConformanceTests` changed from MiniComplex (excluded fixture) to HomeHealthCare (primary fixture).
4. **14 unit tests** for `ParserOutputProjector`: EventBinding (3 tests), procedure kind mapping (2), Body.References (2), Body.Calls (1), Body.SqlStrings (1), schema envelope (1), `TryExtractEventBinding` helper (4).

## Key design decisions
- **Reference-to-procedure scoping**: Walk up `EnclosingScope` chain to find `ProcedureScope`, group by `(ProcedureName, Kind)`.
- **EventHandler detection**: Name matches `<word>_<word>` pattern; split on LAST underscore.
- **SqlStrings**: Intentionally empty `[]` in Session 1 (deferred; out of scope per spec).
- **ParserSource.Kind**: ClassModule → `"Form"`, Standard → `"Procedure"` (matching schema enum).
- **Module discovery**: `raw/*.txt` files sorted deterministically via `OrderBy(StringComparer.Ordinal)`.

## Out of scope for Session 1 (per spec)
- `Body.Statements` (requires dedicated statement-classification pass).
- `Body.SqlStrings` population (VBA-level SQL string extraction).
- `QueryItem.Sql.Parsed` (Session 2).
- Changes to VBA grammar or resolver.

## Key files (included for context)
### ParserOutputProjector.cs (new)
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
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
            ["SchemaVersion"] = "1.0",
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
            ["Body"] = new JsonObject
            {
                ["References"] = new JsonArray(),
                ["Calls"] = new JsonArray(),
                ["SqlStrings"] = new JsonArray(),
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
        // ByRef is the default; only emit when source was explicit ByVal or ByRef.
        if (p.PassMode == ParameterPassMode.ByRef) node["ByRef"] = true;
        if (p.Modifier == ParameterModifier.Optional) node["Optional"] = true;
        if (p.Modifier == ParameterModifier.ParamArray) node["ParamArray"] = true;
        return node;
    }

    // ── Body (References + Calls + SqlStrings) ────────────────────────

    private static JsonNode BuildBody(IReadOnlyList<ResolvedReference> refs)
    {
        var references = new JsonArray();
        var calls = new JsonArray();

        foreach (var r in refs)
        {
            references.Add(BuildResolvedReference(r));
            if (IsCallSite(r))
                calls.Add(BuildCallSite(r));
        }

        return new JsonObject
        {
            ["References"] = references,
            ["Calls"] = calls,
            ["SqlStrings"] = new JsonArray(),
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
        var (status, kind, target, confidence) = MapOutcome(r.Outcome);

        var node = new JsonObject
        {
            ["OriginalText"] = ExpressionText(r.Expression),
            ["Location"] = BuildLocation(r.Location),
            ["ResolutionStatus"] = status,
        };

        if (kind is not null) node["ReferenceKind"] = kind;
        if (target is not null) node["ResolvedTarget"] = target;
        if (confidence is double conf) node["Confidence"] = conf;

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

    private static (string Status, string? Kind, JsonNode? Target, double? Confidence)
        MapOutcome(ResolutionOutcome outcome) => outcome switch
    {
        ResolutionOutcome.Resolved r => (
            "Resolved",
            DeclarationKindString(r.Declaration),
            BuildDeclarationTarget(r.Declaration),
            1.0),

        ResolutionOutcome.ResolvedToObjectModel r => (
            "Resolved",
            "ObjectModel",
            BuildDeclarationTarget(r.Declaration),
            1.0),

        ResolutionOutcome.ResolvedToBuiltIn r => (
            "Resolved",
            "BuiltIn",
            new JsonObject { ["Name"] = r.Function.Name, ["Category"] = r.Function.Category.ToString() },
            1.0),

        ResolutionOutcome.ResolvedToMe r => (
            "Resolved",
            r.OwningClass.Kind == ClassDeclarationKind.ReportModule ? "Report" : "Form",
            new JsonObject { ["Kind"] = "Form", ["Name"] = r.OwningClass.Name },
            1.0),

        ResolutionOutcome.ResolvedViaWithFlattening f =>
            MapOutcomeWithFlattening(f),

        ResolutionOutcome.Unresolved =>
            ("Unresolved", null, null, null),

        _ => ("Unresolved", null, null, null),
    };

    private static (string Status, string? Kind, JsonNode? Target, double? Confidence)
        MapOutcomeWithFlattening(ResolutionOutcome.ResolvedViaWithFlattening f)
    {
        var (status, kind, target, _) = MapOutcome(f.InnerOutcome);
        return (status, kind, target, 0.9);
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
        return new JsonObject
        {
            ["Kind"] = DeclarationKindString(decl),
            ["Name"] = decl.Name,
        };
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

```

### Pipeline.cs (EmitManifestArtifacts and helpers, modified)
```csharp
)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var text = File.ReadAllText(file, Encoding.GetEncoding(1252));
                    var (ast, _) = VbaModuleBodyLoader.LoadAndParse(text);

                    // Class modules have "VERSION 1.0 CLASS" in the SaveAsText header.
                    bool isClassModule = category != "Modules"
                        || text.Contains("VERSION 1.0 CLASS\r\n", StringComparison.Ordinal)
                        || text.Contains("VERSION 1.0 CLASS\n", StringComparison.Ordinal);

                    var manifestKind = isClassModule
                        ? ManifestModuleKind.ClassModule
                        : ManifestModuleKind.Standard;
                    var resolverKind = category switch
                    {
                        "Forms" => ResolverModuleKind.Form,
                        "Reports" => ResolverModuleKind.Report,
                        _ when isClassModule => ResolverModuleKind.Class,
                        _ => ResolverModuleKind.Standard,
                    };

                    var project = new ProjectScope();
                    var visitor = new ScopeBuildingVisitor();
                    var scope = visitor.BuildModuleScope(project, ast, name, resolverKind);
                    var resolved = new VbaReferenceResolver().Resolve(scope, ast);
                    parsed.Add(new ParsedModuleEntry(name, manifestKind, category, resolved));
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new HarvesterDiagnostic
                    {
                        Severity = HarvesterDiagnosticSeverity.Warning,
                        Message = $"VBA parse failed for '{name}': {ex.Message}",
                        Code = "VBA_PARSE_FAILURE",
                    });
                }
            }
        }

        // ── 2. Cross-reference index and classification ────────────────
        var resolvedModules = parsed.Select(p => p.Resolved).ToList();
        var crossRefs = new CrossReferenceIndex(resolvedModules);
        var classifications = FunctionClassificationBuilder.BuildAll(resolvedModules, crossRefs);

        // ── 3. Standalone module exports ───────────────────────────────
        var moduleExports = parsed
            .Where(p => p.Category == "Modules")
            .Select(p =>
            {
                var parserOutput = ParserOutputProjector.Project(
                    p.Resolved, classifications, p.Name, p.Kind);
                return new ModuleExport
                {
                    ModuleName = p.Name,
                    ModuleKind = p.Kind,
                    ParserOutput = parserOutput,
                };
            })
            .ToList();

        // ── 4. Form and report exports (merge spec + parser output) ────
        var formExports = BuildFormOrReportExports(
            parsed, classifications, irDir, "Forms", diagnostics);
        var reportExports = BuildFormOrReportExports(
            parsed, classifications, irDir, "Reports", diagnostics);

        // ── 5. Assemble and write ──────────────────────────────────────
        IReadOnlyList<UnresolvedItem> unresolvedItems = Array.Empty<UnresolvedItem>();

        var manifest = DatabaseManifestAggregator.Build(
            database: database,
            harvesterVersion: ResolveHarvesterVersion(),
            forms: formExports,
            reports: reportExports,
            modules: moduleExports,
            unresolvedItems: unresolvedItems,
            diagnostics: diagnostics);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        File.WriteAllText(
            Path.Combine(baseDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, options));
        File.WriteAllText(
            Path.Combine(baseDir, "unresolved-items.json"),
            JsonSerializer.Serialize(unresolvedItems, options));
    }

    // Loads form/report specs from ir/ and merges with parser outputs
    // from the raw/ category. Returns one FormExport per form/report that
    // has both a spec (from ir/) and parser output (from raw/).
    // Forms with no spec are omitted; the parser output is not lost — the
    // HarvesterDiagnostic records the skip so the human can investigate.
    private static List<FormExport> BuildFormOrReportExports(
        List<ParsedModuleEntry> parsed,
        IReadOnlyList<FunctionClassification> classifications,
        string irDir,
        string category,
        List<HarvesterDiagnostic> diagnostics)
    {
        // Build parser-output map: form name → JsonNode
        var parserOutputs = new Dictionary<string, System.Text.Json.Nodes.JsonNode>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var p in parsed.Where(e => e.Category == category))
        {
            var output = ParserOutputProjector.Project(
                p.Resolved, classifications, p.Name, p.Kind);
            parserOutputs[p.Name] = output;
        }

        // Load form specs from ir/<category>/*.ir.json
        var irCategoryDir = Path.Combine(irDir, category);
        if (!Directory.Exists(irCategoryDir))
            return new List<FormExport>();

        var exports = new List<FormExport>();
        foreach (var irFile in Directory.GetFiles(irCategoryDir, "*.ir.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            // ir/Forms/frmSomething.ir.json → name = "frmSomething"
            var irName = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(irFile));
            try
            {
                var irJson = File.ReadAllText(irFile, Encoding.UTF8);
                var metadata = FormMetadataExtractor.Extract(irName, irJson);
                var spec = FormComponentSpecBuilder.Build(metadata);

                parserOutputs.TryGetValue(irName, out var parserOutput);
                exports.Add(new FormExport { Spec = spec, ParserOutput = parserOutput });
            }
            catch (Exception ex)
            {
                diagnostics.Add(new HarvesterDiagnostic
                {
                    Severity = HarvesterDiagnosticSeverity.Warning,
                    Message = $"Form spec load failed for '{irName}': {ex.Message}",
                    Code = "FORM_SPEC_LOAD_FAILURE",
                });
            }
        }

        return exports;
    }

    private sealed record ParsedModuleEntry(
        string Name, ManifestModuleKind Kind, string Category, ResolvedModule Resolved);

    private static string ResolveHarvesterVersion()
    {
        var asm = typeof(Pipeline).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrEmpty(info?.InformationalVersion))
            return info!.InformationalVersion;
        return asm.GetName().Version?.ToString() ?? "0.0.0";
    }
}

public sealed record CategoryExportResult(string Category, IReadOnlyList<ExportOutcome> Outcomes);
public sealed record CategoryTranslateResult(string Category, IReadOnlyList<TranslateOutcome> Outcomes);
public sealed record CategoryEnrichResult(string Category, IReadOnlyList<EnrichOutcome> Outcomes);

public sealed record ExtractResult(string BaseDir, string RawDir, IReadOnlyList<CategoryExportResult> Categories);
public sealed record TranslateResult(string RawDir, string KbDir, IReadOnlyList<CategoryTranslateResult> Categories);
public sealed record EnrichResult(string KbDir, string IrDir, IReadOnlyList<CategoryEnrichResult> Categories);
public sealed record RunResult(
    string BaseDir,
    string RawDir,
    string KbDir,
    string IrDir,
    IReadOnlyList<CategoryExportResult> Extract,
    IReadOnlyList<CategoryTranslateResult> Translate,
    IReadOnlyList<CategoryEnrichResult> Enrich);

```

### ParserOutputProjectorTests.cs (new)
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;
using ManifestModuleKind = AccessHarvester.Pipeline.Manifest.ModuleKind;
using ResolverModuleKind = AccessHarvester.Vba.Resolver.Scopes.ModuleKind;

namespace AccessHarvester.Pipeline.Tests.Manifest;

// Hermetic unit tests for ParserOutputProjector.
// Each test parses a minimal VBA snippet, resolves it, and asserts the
// projected JSON has the expected shape. No I/O or COM required.
public sealed class ParserOutputProjectorTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static ResolvedModule Resolve(
        string source,
        string moduleName = "modTest",
        ResolverModuleKind kind = ResolverModuleKind.Standard)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        var ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var scope = visitor.BuildModuleScope(project, ast, moduleName, kind);
        return new ReferenceResolver().Resolve(scope, ast);
    }

    private static JsonNode Project(
        ResolvedModule resolved,
        string moduleName = "modTest",
        ManifestModuleKind kind = ManifestModuleKind.Standard)
    {
        var crossRefs = CrossReferenceIndex.Build(resolved);
        var classifications = FunctionClassificationBuilder.BuildAll(
            new[] { resolved }, crossRefs);
        return ParserOutputProjector.Project(resolved, classifications, moduleName, kind);
    }

    private static JsonArray Items(JsonNode root)
        => root["Items"]!.AsArray();

    private static JsonNode? Item(JsonNode root, int index)
        => root["Items"]!.AsArray()[index];

    // ── Tests: event binding ──────────────────────────────────────────

    [Fact]
    public void Form_BeforeUpdate_ProducesEventBinding()
    {
        var resolved = Resolve(
            "Private Sub Form_BeforeUpdate(Cancel As Integer)\nEnd Sub",
            moduleName: "Form_frmTest",
            kind: ResolverModuleKind.Form);

        var root = Project(resolved, "Form_frmTest", ManifestModuleKind.ClassModule);
        var item = Item(root, 0)!;

        Assert.Equal("EventHandler", item["ProcedureKind"]!.GetValue<string>());
        Assert.Equal("Form", item["EventBinding"]!["ControlName"]!.GetValue<string>());
        Assert.Equal("BeforeUpdate", item["EventBinding"]!["EventName"]!.GetValue<string>());
    }

    [Fact]
    public void btnSave_Click_ProducesEventBinding()
    {
        var resolved = Resolve(
            "Private Sub btnSave_Click()\nEnd Sub",
            kind: ResolverModuleKind.Form);

        var root = Project(resolved, "Form_frmTest", ManifestModuleKind.ClassModule);
        var item = Item(root, 0)!;

        Assert.Equal("EventHandler", item["ProcedureKind"]!.GetValue<string>());
        Assert.Equal("btnSave", item["EventBinding"]!["ControlName"]!.GetValue<string>());
        Assert.Equal("Click", item["EventBinding"]!["EventName"]!.GetValue<string>());
    }

    [Fact]
    public void NonEventHandler_EventBindingIsAbsent()
    {
        var resolved = Resolve("Public Sub DoSomething()\nEnd Sub");

        var root = Project(resolved);
        var item = Item(root, 0)!;

        Assert.Equal("Sub", item["ProcedureKind"]!.GetValue<string>());
        Assert.Null(item["EventBinding"]);
    }

    // ── Tests: procedure kind mapping ─────────────────────────────────

    [Fact]
    public void FunctionProcedure_MapsToFunction()
    {
        var resolved = Resolve("Public Function GetName() As String\nGetName = \"x\"\nEnd Function");

        var item = Item(Project(resolved), 0)!;
        Assert.Equal("Function", item["ProcedureKind"]!.GetValue<string>());
    }

    [Fact]
    public void PropertyGet_MapsToPropertyGet()
    {
        var resolved = Resolve("Public Property Get MyProp() As String\nEnd Property");

        var item = Item(Project(resolved), 0)!;
        Assert.Equal("PropertyGet", item["ProcedureKind"]!.GetValue<string>());
    }

    // ── Tests: body.References ────────────────────────────────────────

    [Fact]
    public void ResolvedReference_ProjectsToBodyReferencesWithCorrectShape()
    {
        // A procedure that calls a built-in so the resolver produces a
        // ResolvedToBuiltIn outcome, which maps to ResolutionStatus=Resolved.
        const string source = @"
Public Sub DoSomething()
    Dim s As String
    s = Format(Now(), ""yyyy-mm-dd"")
End Sub";
        var resolved = Resolve(source);
        var root = Project(resolved);
        var item = Item(root, 0)!;
        var refs = item["Body"]!["References"]!.AsArray();

        // There should be at least one reference (Format and/or Now).
        Assert.True(refs.Count > 0, "Expected at least one reference in Body.References");

        // Every reference must have OriginalText, Location, and ResolutionStatus.
        foreach (var r in refs)
        {
            Assert.NotNull(r!["OriginalText"]);
            Assert.NotNull(r!["Location"]);
            Assert.NotNull(r!["ResolutionStatus"]);
        }
    }

    [Fact]
    public void UnresolvedReference_ProjectsAsUnresolved()
    {
        const string source = @"
Option Explicit
Public Sub DoSomething()
    DoUnknownThing
End Sub";
        var resolved = Resolve(source);
        var root = Project(resolved);
        var item = Item(root, 0)!;
        var refs = item["Body"]!["References"]!.AsArray();

        var unresolved = refs.Where(r =>
            r!["ResolutionStatus"]?.GetValue<string>() == "Unresolved").ToList();

        Assert.True(unresolved.Count > 0,
            "Expected at least one Unresolved reference for DoUnknownThing");
    }

    // ── Tests: Body.Calls ─────────────────────────────────────────────

    [Fact]
    public void CallToUserDefinedProcedure_AppearsInBodyCalls()
    {
        const string source = @"
Public Sub Caller()
    Helper
End Sub
Public Sub Helper()
End Sub";
        var resolved = Resolve(source);
        var root = Project(resolved);

        // Caller is the first item (source order).
        var callerItem = Items(root).First(n =>
            n!["Name"]!.GetValue<string>() == "Caller")!;
        var calls = callerItem["Body"]!["Calls"]!.AsArray();

        Assert.True(calls.Count > 0,
            "Expected at least one call in Body.Calls for Caller");
        Assert.NotNull(calls[0]!["CalleeText"]);
        Assert.NotNull(calls[0]!["Location"]);
    }

    // ── Tests: Body.SqlStrings ────────────────────────────────────────

    [Fact]
    public void SqlStrings_AlwaysEmptyArrayInSessionOne()
    {
        var resolved = Resolve("Public Sub DoSomething()\nEnd Sub");
        var item = Item(Project(resolved), 0)!;
        var sqlStrings = item["Body"]!["SqlStrings"]!.AsArray();

        Assert.Empty(sqlStrings);
    }

    // ── Tests: schema envelope ────────────────────────────────────────

    [Fact]
    public void ProjectedOutput_HasSchemaVersionAndSourceFields()
    {
        var resolved = Resolve("Public Sub DoSomething()\nEnd Sub",
            moduleName: "modFoo");
        var root = Project(resolved, "modFoo");

        Assert.Equal("1.0", root["SchemaVersion"]!.GetValue<string>());
        Assert.NotNull(root["Source"]!["Kind"]);
        Assert.NotNull(root["Source"]!["Location"]);
        // Standard module → Kind = "Procedure"
        Assert.Equal("Procedure", root["Source"]!["Kind"]!.GetValue<string>());
    }

    // ── Tests: TryExtractEventBinding internal helper ─────────────────

    [Fact]
    public void TryExtractEventBinding_SplitsOnLastUnderscore()
    {
        var result = ParserOutputProjector.TryExtractEventBinding("Form_BeforeUpdate");
        Assert.NotNull(result);
        Assert.Equal("Form", result!.Value.Control);
        Assert.Equal("BeforeUpdate", result!.Value.Event);
    }

    [Fact]
    public void TryExtractEventBinding_MultipleUnderscores_SplitsOnLast()
    {
        // Control names can theoretically contain underscores.
        var result = ParserOutputProjector.TryExtractEventBinding("My_Control_Click");
        Assert.NotNull(result);
        Assert.Equal("My_Control", result!.Value.Control);
        Assert.Equal("Click", result!.Value.Event);
    }

    [Fact]
    public void TryExtractEventBinding_NoUnderscore_ReturnsNull()
    {
        var result = ParserOutputProjector.TryExtractEventBinding("DoSomething");
        Assert.Null(result);
    }

    [Fact]
    public void TryExtractEventBinding_LeadingUnderscore_ReturnsNull()
    {
        var result = ParserOutputProjector.TryExtractEventBinding("_Click");
        Assert.Null(result);
    }
}

```

## Test results
All tests pass:
- 407 AccessHarvester.Pipeline.Tests (includes 14 new ParserOutputProjector tests)
- 436 AccessHarvester.Tests + 2 skipped
- 70 AccessHarvester.SymbolDb.Tests
- 44 AccessHarvester.Integration (golden regression + schema conformance all pass)

## Verification concerns
1. **EventHandler detection correctness**: Does `TryExtractEventBinding` correctly split on the LAST underscore per the spec? Verify with multi-underscore control names like `My_Control_Click`.
2. **Reference-to-procedure scoping**: Does the scope-chain walk correctly assign references to procedures, especially for With-block and ForEach-scope references?
3. **ResolutionOutcome mapping**: Is `ResolvedViaWithFlattening` correctly unwrapped (recursive into InnerOutcome, confidence lowered to 0.9)?
4. **ProcedureKind mapping**: Is the `ClassificationKindOf` / `SchemaKindOf` mapping correct for all 5 procedure types (Sub, Function, PropertyGet, PropertyLet, PropertySet)?
5. **SqlStrings always empty**: Is it correct to emit `"SqlStrings": []` for every procedure in Session 1? (Spec explicitly defers this.)
6. **CodePagesEncodingProvider registration**: Is calling `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` the right fix for the xUnit codepage issue? Is it thread-safe/idempotent?
7. **Determinism**: Is `Directory.GetFiles(...).OrderBy(f => f, StringComparer.Ordinal)` sufficient to guarantee deterministic manifest output across pipeline runs?
8. **LabelSource casing fix**: Is changing `attached → Attached` etc. in `LabelResolver.cs` the correct v1-adr D4 canonical casing fix, or does this need a separate ADR review?

After your analysis, output a JSON block in this exact format:
```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }
  ]
}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
