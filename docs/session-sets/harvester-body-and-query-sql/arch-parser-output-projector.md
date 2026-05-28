## ParserOutputProjector Design

### Recommended Signature

```csharp
// AccessHarvester.Pipeline.Manifest/ParserOutputProjector.cs
public static class ParserOutputProjector
{
    public static JsonNode Project(
        ResolvedModule resolvedModule,
        IReadOnlyList<FunctionClassification> classifications,
        string moduleName,
        ModuleKind moduleKind);
}
```

Lives in `Pipeline.Manifest` — it's a manifest-emission concern, peers with `DatabaseManifestAggregator`. Returns `JsonNode` directly because `ModuleExport.ParserOutput` and `FormExport.ParserOutput` are `JsonNode?`. Internally, build structured records and serialize at the end for testability — those records can be `internal` with `[InternalsVisibleTo]` for test projects.

---

### Q1: Structured intermediate vs direct JsonNode

Use internal structured records (`ParserOutputRecord`, `ProcedureItemRecord`, etc.) that mirror the schema, built by the projection logic, then serialize to `JsonNode` once at the boundary. This keeps the mapping logic unit-testable against typed properties. The public API stays `JsonNode` to match the consumer types.

---

### Q2: Reference-to-procedure scoping

**Filter `module.References` by `EnclosingScope`.** Walk up each reference's scope chain to find the enclosing `ProcedureScope`.

```csharp
static ProcedureScope? FindEnclosingProcedure(Scope scope)
{
    var current = scope;
    while (current is not null)
    {
        if (current is ProcedureScope ps) return ps;
        current = current.Parent; // or .EnclosingScope
    }
    return null; // module-level reference
}
```

Group references by the resulting `ProcedureScope.Procedure.Name` (+ kind, for property procedures). Match to `FunctionClassification` by `ProcedureName`.

**Why not AST walking:** The resolver already computed scope assignments. Re-walking the AST and calling `TryGetReference` per expression duplicates work, risks missing synthetic references (e.g., from `With` flattening), and introduces coupling between projector and AST visitor mechanics. The scope chain is the resolver's answer to "which procedure owns this reference" — use it.

**Gotcha:** `ProcedureScope.Procedure` is `Declarations.ProcedureDeclaration`, not `Parser.Ast.ProcedureDeclaration`. Match to `FunctionClassification` via `(ProcedureName, ProcedureKind)` string/enum comparison, not object identity.

---

### Q3: ResolutionOutcome → schema mapping

| Outcome variant | `ResolutionStatus` | `ReferenceKind` | `Confidence` |
|---|---|---|---|
| `Resolved(Declaration)` | `"Resolved"` | Derived from `Declaration` type: `"Variable"`, `"Procedure"`, `"Constant"`, `"Type"`, `"Enum"`, `"Module"` | `"Exact"` |
| `ResolvedToObjectModel(Declaration)` | `"Resolved"` | `"ObjectModel"` | `"Exact"` |
| `ResolvedToBuiltIn(BuiltInFunction)` | `"Resolved"` | `"BuiltIn"` | `"Exact"` |
| `ResolvedToMe(ClassDeclaration)` | `"Resolved"` | `"SelfReference"` | `"Exact"` |
| `ResolvedViaWithFlattening(_, inner)` | **Unwrap `inner`** — use inner's status | **Unwrap `inner`** — use inner's kind | `"Inferred"` |
| `Unresolved(Reason)` | `"Unresolved"` | omit (`null`) | omit |

Key decisions:
- **`ResolvedViaWithFlattening`**: Recurse into `InnerOutcome` for status and kind. The `With`-flattening is a resolution *mechanism*, not a different *result*. Flag it via `Confidence: "Inferred"` so consumers know the target was synthesized. If `InnerOutcome` is itself `Unresolved`, status is `"Unresolved"`.
- **`ResolvedToBuiltIn`**: Status is `"Resolved"`, not partial — VBA built-ins have well-defined semantics. `ReferenceKind: "BuiltIn"` distinguishes them.
- **`ResolvedTarget`**: For `Resolved/ResolvedToObjectModel`, emit `{ DeclarationName, DeclaringModule }`. For `ResolvedToBuiltIn`, emit `{ FunctionName }`. For `Unresolved`, omit.
- **No `"Ambiguous"` status in current resolver** — the enum doesn't have an ambiguity variant. Reserve it for future use; don't synthesize it.

---

### Q4: EventHandler ProcedureKind

**`FunctionClassification.Categories` is authoritative. Name pattern is fallback.**

```
if classification.Categories contains EventHandler category:
    ProcedureKind = "EventHandler"
    EventBinding = extract from classification (it has this data)
elif module is form/report code-behind AND name matches ^([A-Za-z]\w*)_([A-Za-z]\w*)$:
    ProcedureKind = "EventHandler"
    EventBinding = { ControlName = match.Group(1), EventName = match.Group(2) }
else:
    ProcedureKind = map from ProcedureKind enum (Sub/Function/PropertyGet/etc.)
```

Reasoning:
- The classifier has form control metadata and can verify `btnSubmit` is an actual control with a `Click` event. The name pattern alone is ambiguous (`Data_Cleanup` might not be an event handler).
- The fallback is needed for modules where classification didn't run or didn't have form metadata.
- **For `Declare` procedures**: Always `"DeclareFunction"` or `"DeclareSub"`, never `"EventHandler"`, regardless of name pattern.

**Gotcha:** Split the name on the **first** underscore only. `Form_Before` → `("Form", "Before")`. VBA event names don't contain underscores, but control names theoretically can (rare). If the classifier confirms the binding, trust it over the split.

---

### Q5: Module discovery strategy

```csharp
// In EmitManifestArtifacts or a helper called by it:
var rawDir = Path.Combine(runDir, "raw");

// 1. Standalone modules
var moduleFiles = Directory.GetFiles(Path.Combine(rawDir, "Modules"), "*.bas");
foreach (var file in moduleFiles)
{
    var (ast, parseResult) = VbaModuleBodyLoader.LoadAndParse(File.ReadAllText(file));
    var moduleKind = DetermineModuleKind(ast); // see below
    // ... resolve, classify, project
}

// 2. Form code-behind (always ClassModule)
var formFiles = Directory.GetFiles(Path.Combine(rawDir, "Forms"), "*.bas");

// 3. Report code-behind (always ClassModule)  
var reportFiles = Directory.GetFiles(Path.Combine(rawDir, "Reports"), "*.bas");
```

**ModuleKind determination for `raw/Modules/`:**
1. Check the parsed `Module` AST — if it exposes `ModuleType` or `IsClass`, use that.
2. Failing that, check VBA header attributes: `VERSION 1.0 CLASS` at the start of the file → `ClassModule`. Absence → `Standard`.
3. `raw/Forms/` and `raw/Reports/` are unconditionally `ClassModule`.

**Gotcha:** Guard against missing directories (`Directory.Exists` check). Some databases have no modules, or no forms. Also guard against empty/corrupt `.bas` files — wrap `LoadAndParse` in a try/catch, log the failure, skip the module, emit `ParserOutput: null`.

---

### Q6: FormExport wiring

The `FormExport` record already exists with `Spec` populated from IR. The projector adds `ParserOutput`. Two data sources, matched by name:

```csharp
// In EmitManifestArtifacts:

// 1. Build lookup of parser outputs by form name
var formParserOutputs = new Dictionary<string, JsonNode>();
foreach (var basFile in Directory.GetFiles(Path.Combine(rawDir, "Forms"), "*.bas"))
{
    var formName = Path.GetFileNameWithoutExtension(basFile);
    var (ast, _) = VbaModuleBodyLoader.LoadAndParse(File.ReadAllText(basFile));
    var resolved = ReferenceResolver.Resolve(ast, declarations, ...);
    var classifications = FunctionClassificationBuilder.BuildAll(resolved, crossRefs);
    var parserOutput = ParserOutputProjector.Project(resolved, classifications, formName, ModuleKind.ClassModule);
    formParserOutputs[formName] = parserOutput;
}

// 2. Merge with existing FormExports (already have Spec from IR)
var forms = existingFormExports.Select(fe =>
    fe with { ParserOutput = formParserOutputs.GetValueOrDefault(fe.Spec.Name) }
).ToList();

// 3. Handle orphans: .bas files with no matching IR spec
foreach (var (name, output) in formParserOutputs)
{
    if (!existingFormExports.Any(fe => fe.Spec.Name == name))
    {
        // Log warning: code-behind without form spec
        // Optionally create a FormExport with null Spec
    }
}

// Same pattern for Reports.
```

**Key:** Use `record with { }` syntax to produce a new `FormExport` with the `ParserOutput` populated while preserving the existing `Spec`. The matching key is `Path.GetFileNameWithoutExtension(basFile)` ↔ `FormComponentSpec.Name`.

---

### Wiring pattern for EmitManifestArtifacts

```csharp
void EmitManifestArtifacts()
{
    var rawDir = Path.Combine(_runDir, "raw");
    
    // Phase 1: Parse all VBA modules
    var allModules = DiscoverAndParseModules(rawDir); // → List<(string Name, ModuleKind, ResolvedModule)>
    
    // Phase 2: Build cross-reference index across all modules
    var crossRefs = CrossReferenceIndex.Build(allModules.Select(m => m.Resolved));
    
    // Phase 3: Classify and project each module
    var moduleExports = new List<ModuleExport>();
    foreach (var (name, kind, resolved) in allModules.Where(m => m.Source == "Modules"))
    {
        var classifications = FunctionClassificationBuilder.BuildAll(resolved, crossRefs);
        var parserOutput = ParserOutputProjector.Project(resolved, classifications, name, kind);
        moduleExports.Add(new ModuleExport(name, kind, parserOutput));
    }
    
    // Phase 4: Merge form/report code-behind with existing IR specs
    var formExports = MergeFormParserOutputs(existingForms, allModules, crossRefs, "Forms");
    var reportExports = MergeReportParserOutputs(existingReports, allModules, crossRefs, "Reports");
    
    // Phase 5: Build manifest
    var manifest = DatabaseManifestAggregator.Build(
        _database, _harvesterVersion,
        forms: formExports,
        reports: reportExports,
        modules: moduleExports);
}
```

---

### Additional gotchas

1. **Module-level references** (scope chain terminates at `ModuleScope` without hitting `ProcedureScope`): Drop them from procedure items. If needed later, add a synthetic `"ModuleLevel"` item kind — but v1.0 schema only has `"Procedure"`, so skip.

2. **`Declare` statements have no body**: Emit `Body: { References: [], Calls: [], SqlStrings: [] }`. Don't attempt to walk their non-existent AST body.

3. **Property procedure grouping**: `PropertyGet Foo`, `PropertyLet Foo`, `PropertySet Foo` are separate items in the schema (different `ProcedureKind`). The `FunctionClassification` key is `(ProcedureName, Kind)`. The scope filter needs to distinguish them — `ProcedureScope.Procedure` includes kind information.

4. **`SqlStrings` in v1.0**: The resolver output doesn't identify SQL strings. For Session 1, emit `SqlStrings: []` for every procedure. SQL extraction is a separate concern (likely Session 2+).

5. **Cross-module resolution order**: `ReferenceResolver.Resolve` may need declarations from other modules. Parse all modules first, collect declarations, then resolve. Don't interleave parse-and-resolve per module.

6. **File encoding**: `SaveAsTextExporter` typically writes Windows-1252. Use `Encoding.GetEncoding(1252)` or let the parser handle BOM detection. UTF-8 with BOM should also work.