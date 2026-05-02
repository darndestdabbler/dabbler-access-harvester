"""Architecture synthesis route for access-resolver-symbol-table-integration Session 1."""
import pathlib
from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = pathlib.Path('docs/session-sets/access-resolver-symbol-table-integration')
log = SessionLog(str(SESSION_SET), total_sessions=3)

def read(path):
    return open(path, encoding='utf-8').read()

iaccess_registry = read('AccessHarvester.Vba.Resolver/References/IAccessObjectModelRegistry.cs')
ref_resolver = read('AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs')
itype_registry = read('AccessHarvester.Access/ITypeLibraryRegistry.cs')
form_metadata = read('AccessHarvester.Access/FormMetadata.cs')
access_common = read('AccessHarvester.Access/AccessObjectCommon.cs')
declaration = read('AccessHarvester.Vba.Resolver/Declarations/Declaration.cs')
decl_provenance = read('AccessHarvester.Vba.Resolver/Declarations/DeclarationProvenance.cs')
scope_cs = read('AccessHarvester.Vba.Resolver/Scopes/Scope.cs')
module_scope = read('AccessHarvester.Vba.Resolver/Scopes/ModuleScope.cs')
tc01 = read('docs/test-cases/01-simple-validation.bas')
tc02 = read('docs/test-cases/02-cascading-combo-subform.bas')
tc03 = read('docs/test-cases/03-with-block-shadowing.bas')

arch_prompt = """
# Architecture Synthesis: Access Resolver Symbol-Table Integration (Phase 4.4)

## Goal
Design and produce complete, compilable C# .NET 10 code for `AccessHarvester.Vba.Resolver/AccessIntegration/` that implements Phase 4.4: injecting Access metadata (forms, reports, controls) into the VBA resolver as synthetic declarations.

## Context: What already exists

### IAccessObjectModelRegistry.cs (in AccessHarvester.Vba.Resolver/References/)
```csharp
""" + iaccess_registry + """
```

### ReferenceResolver.cs (key: SubChildLookup stub awaits this session)
```csharp
""" + ref_resolver + """
```

### ITypeLibraryRegistry.cs (in AccessHarvester.Access/)
```csharp
""" + itype_registry + """
```

### FormMetadata.cs (in AccessHarvester.Access/)
```csharp
""" + form_metadata + """
```

### AccessObjectCommon.cs (in AccessHarvester.Access/)
```csharp
""" + access_common + """
```

### Declaration.cs (in AccessHarvester.Vba.Resolver/Declarations/)
```csharp
""" + declaration + """
```

### DeclarationProvenance.cs (in AccessHarvester.Vba.Resolver/Declarations/)
```csharp
""" + decl_provenance + """
```

### Scope.cs (in AccessHarvester.Vba.Resolver/Scopes/)
```csharp
""" + scope_cs + """
```

### ModuleScope.cs (in AccessHarvester.Vba.Resolver/Scopes/)
```csharp
""" + module_scope + """
```

## Test cases that must resolve after this integration

### Test Case 01 (frmCustomers form module)
```vba
""" + tc01 + """
```
Expected: `Me.txtCustomerName` and `Me.txtPhone` resolve to control declarations (not Unresolved).

### Test Case 02 (frmCaseManagement form module)
```vba
""" + tc02 + """
```
Expected: `Me!cboClient`, `Me.txtCaseworkerEmail` resolve to control declarations.
`Me.sfrmOpenCases.Form.Filter` and `Me.sfrmOpenCases.Form.FilterOn` resolve via subform navigation.

### Test Case 03 (frmPermitApplication form module)
```vba
""" + tc03 + """
```
Expected: `Me.sfrmInspections.Form` navigates to the inner form class; `.RecordsetClone` on it resolves via ITypeLibraryRegistry. `Me!txtPermitID` resolves to a control.

## Requirements

### Standards from the spec
- Synthetic declarations carry provenance with source = "synthetic-from-access" and artifact = the form/report name so downstream consumers can distinguish them from VBA-source declarations.
- Subform navigation is data, not code branching. The metadata extractor records the subform-to-form binding; the resolver dereferences it.
- Type-library membership flows through ITypeLibraryRegistry. The injection layer holds an ITypeLibraryRegistry reference; it does not import TypeLibraryRegistry.cs directly.

### Project structure
- New files go under `AccessHarvester.Vba.Resolver/AccessIntegration/`
- The resolver .csproj currently references only `AccessHarvester.Vba.Parser`. It needs a project reference to `AccessHarvester.Access`.
- Lean by default: no unnecessary abstractions.
- Nullable reference types enabled.

### Architecture decisions to make

1. **Synthetic provenance type**: SyntheticDeclarationProvenance has no source range (not from VBA source). It must carry source = "synthetic-from-access" and artifact metadata.

2. **Synthetic declaration types**: Should there be a SyntheticControlDeclaration subclass of Declaration? It needs to surface the Access control type (e.g., "TextBox") as a string so DecideMemberOutcome can map it to "Access.TextBox" for ITypeLibraryRegistry lookups. Also need a SyntheticFormClassDeclaration for the form/report class that Me resolves to.

3. **Extending IAccessObjectModelRegistry for subform navigation**: The SubChildLookup method in ReferenceResolver currently returns null. Subform navigation (sfrmOrders.Form) needs a new method on IAccessObjectModelRegistry. The method should accept the parent Declaration and member name, and return the synthetic class declaration for the contained form/report.

4. **AccessMetadataRegistry**: Constructor takes IReadOnlyList<FormMetadata>, IReadOnlyList<ReportMetadata>, ITypeLibraryRegistry. Builds dictionaries at construction time. What indexing is needed for fast lookup at resolution time?

5. **Control-type-to-qualified-name mapping**: AccessControl.Type gives "TextBox"; ITypeLibraryRegistry expects "Access.TextBox". The AccessMetadataRegistry should have an internal mapping from Access class names to qualified type names.

6. **Wire-up**: Update ReferenceResolver.SubChildLookup to call the new registry method. Update NullAccessObjectModelRegistry to implement the new method (return null).

## Output required
Produce complete, production-ready C# code for:
1. `AccessHarvester.Vba.Resolver/AccessIntegration/SyntheticDeclarationProvenance.cs`
2. `AccessHarvester.Vba.Resolver/AccessIntegration/SyntheticControlDeclaration.cs`
3. `AccessHarvester.Vba.Resolver/AccessIntegration/SyntheticFormClassDeclaration.cs` (for the class that Me/subform.Form resolves to)
4. `AccessHarvester.Vba.Resolver/AccessIntegration/AccessMetadataRegistry.cs` (implements IAccessObjectModelRegistry)
5. The updated `IAccessObjectModelRegistry.cs` with the 4th method for subform navigation (full file content)
6. The updated `SubChildLookup` method body in ReferenceResolver (just the helper body, not the full file)
7. The XML ItemGroup to add to `AccessHarvester.Vba.Resolver.csproj`

Each file should be complete and compilable. Follow existing code style.
"""

print("Routing architecture synthesis (Opus)...")
result = route(
    content=arch_prompt,
    task_type="architecture",
    complexity_hint=75,
    session_set=str(SESSION_SET),
    session_number=1,
)

arch_path = SESSION_SET / 'synthesis' / 'session-1-architecture.md'
arch_path.parent.mkdir(parents=True, exist_ok=True)
with open(str(arch_path), 'w', encoding='utf-8') as f:
    f.write(result.content)

print(f"Architecture synthesis written to {arch_path}")
print(f"Model: {result.model_name}, cost: ${result.cost_usd:.4f}")
print(f"Input tokens: {result.input_tokens}, Output tokens: {result.output_tokens}")

log.log_step(1, 2, "session-1/architecture-synthesis", "Routed architecture synthesis to Opus for Phase 4.4 injection design", "complete", [{
    "model": result.model_name,
    "taskType": "architecture",
    "inputTokens": result.input_tokens,
    "outputTokens": result.output_tokens,
    "costUsd": result.cost_usd
}])
print("Step logged.")
