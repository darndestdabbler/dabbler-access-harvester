using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.DefaultMembers;

// Curated static registry of types and their default members. The
// reference resolver consults this when a member-less reference is
// invoked on a typed expression and the resolver needs to know which
// implicit member is being read or written.
//
// Population strategy mirrors `References.BuiltInFunctionRegistry`:
// every type Phase 3.5 of the parser-migration plan calls out by name,
// plus the canonical Access object-model surface that appears in
// fixtures 02, 03, 09, 11. Conservative-add bias — a missing row
// produces a recoverable `Unresolved("default-member-not-yet-resolved")`
// downstream; an over-eager row produces a silent miscoercion.
//
// Keys are case-insensitive. Lookup-by-qualified-name uses only the
// LAST segment per the synthesis doc's leaf-only-match rationale — DAO
// projects are inconsistent about whether they qualify (`As Recordset`
// vs. `As DAO.Recordset`) and the seeded set has no leaf collisions.
public static class DefaultMemberRegistry
{
    // Backing storage is wrapped in ReadOnlyDictionary so a caller
    // cannot cast `All` back to a mutable Dictionary and mutate global
    // registry state. The "curated static registry" contract requires
    // genuine immutability, not just an IReadOnlyDictionary view of a
    // mutable backing field.
    private static readonly ReadOnlyDictionary<string, DefaultMember> _byTypeName
        = new(BuildRegistry());

    public static IReadOnlyDictionary<string, DefaultMember> All => _byTypeName;

    public static DefaultMember? TryGet(string typeName)
    {
        if (typeName is null) throw new ArgumentNullException(nameof(typeName));
        return _byTypeName.TryGetValue(typeName, out var entry) ? entry : null;
    }

    public static DefaultMember? TryGet(IReadOnlyList<string> qualifiedName)
    {
        if (qualifiedName is null) throw new ArgumentNullException(nameof(qualifiedName));
        if (qualifiedName.Count == 0) return null;
        return TryGet(qualifiedName[qualifiedName.Count - 1]);
    }

    private static IDictionary<string, DefaultMember> BuildRegistry()
    {
        var d = new Dictionary<string, DefaultMember>(Declaration.NameComparer);

        void AddProperty(string typeName, string memberName,
                         string? returnType, bool isParameterized)
            => d[typeName] = new DefaultMember(
                TypeName: typeName,
                MemberName: memberName,
                Kind: DefaultMemberKind.Property,
                ReturnType: returnType,
                IsParameterized: isParameterized);

        // No `AddMethod` helper — the seeded set has no rows of kind
        // Method. The enum value stays in `DefaultMemberKind` so a
        // future seed (or a consumer-extension layer) can declare one
        // when warranted; spinning up the helper now would be premature
        // per `project-guidance.md` -> Lean By Default.

        // ── Access controls (UI surface) ────────────────────────────
        AddProperty("TextBox",      "Value",   "Variant", isParameterized: false);
        AddProperty("ComboBox",     "Value",   "Variant", isParameterized: false);
        AddProperty("ListBox",      "Value",   "Variant", isParameterized: false);
        AddProperty("CheckBox",     "Value",   "Variant", isParameterized: false);
        AddProperty("OptionButton", "Value",   "Variant", isParameterized: false);
        AddProperty("ToggleButton", "Value",   "Variant", isParameterized: false);
        AddProperty("Label",        "Caption", "String",  isParameterized: false);
        AddProperty("SubForm",      "Form",    "Form",    isParameterized: false);
        AddProperty("SubReport",    "Report",  "Report",  isParameterized: false);

        // ── DAO / ADO recordset and field ────────────────────────────
        AddProperty("Recordset",  "Fields", "Field",   isParameterized: true);
        AddProperty("Recordset2", "Fields", "Field",   isParameterized: true);
        AddProperty("Field",      "Value",  "Variant", isParameterized: false);
        AddProperty("Field2",     "Value",  "Variant", isParameterized: false);

        // ── VBA Collection / Scripting Dictionary ────────────────────
        // Both expose `Item` as the IDispatch default member with
        // DISPID_VALUE; despite the VBA help calling Collection.Item a
        // "method," the COM type info exposes both as PROPERTYGET, so
        // the registry models them as Property. Downstream consumers
        // that need the read/write distinction use this Property kind
        // plus the in-source PropertyGroup (when applicable) — the
        // Method case stays available in the enum for any future seed
        // whose default is genuinely a side-effecting method call.
        AddProperty("Collection", "Item", "Variant", isParameterized: true);
        AddProperty("Dictionary", "Item", "Variant", isParameterized: true);

        // ── Form / Report / Controls (chained Forms!frmFoo!ctl path)
        AddProperty("Form",     "Controls", "Control", isParameterized: true);
        AddProperty("Report",   "Controls", "Control", isParameterized: true);
        AddProperty("Controls", "Item",     "Control", isParameterized: true);
        // No `Control -> Value` row. Access.Control is a heterogeneous
        // base type — Label, CommandButton, Tab, etc. do not share a
        // uniform `.Value` default. Session 4's wiring narrows to a
        // concrete control subtype (TextBox, ComboBox, etc.) using
        // form/report metadata before applying a second default-member
        // hop. Seeding `Control -> Value` here would silently
        // misresolve every label/button reference.

        return d;
    }
}
