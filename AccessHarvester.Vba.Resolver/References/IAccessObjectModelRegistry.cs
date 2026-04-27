using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.References;

// Lookup interface for Access-host declarations the VBA resolver does
// not own — forms, reports, controls, application-level members.
// Session 1 lands the interface and a Null implementation; #11
// (`access-resolver-symbol-table-integration`) supplies the real
// implementation backed by harvester-extracted Access metadata.
//
// All three methods return synthetic `Declaration` records produced by
// #11 from `.accdb` design metadata. Returning a real `Declaration`
// keeps the resolver's outcome shape uniform — `ResolutionOutcome.Resolved`
// covers both source-derived and Access-model declarations, and the
// declaration's `Provenance` distinguishes the two.
public interface IAccessObjectModelRegistry
{
    // Top-level Access identifier resolution. Examples:
    // `Forms`, `Reports`, `DoCmd`, `Application`, `Screen`, plus
    // bare-name forms like `frmCustomers` when host code uses the
    // shorthand. Returns null when the name is not in the registry.
    Declaration? TryResolveTopLevelMember(string name);

    // Control resolution against a known form. The form name comes
    // from a `Me`-resolved class context or an explicit
    // `Forms!frmName` chain. Returns null when the form is unknown
    // to the registry or the named control is not on it.
    Declaration? TryResolveControlOnForm(string formName, string controlName);

    // Same as TryResolveControlOnForm but for report modules.
    Declaration? TryResolveControlOnReport(string reportName, string controlName);

    // Member resolution against an already-resolved Declaration. Used
    // by ReferenceResolver.SubChildLookup to walk member chains —
    // `txtFoo.Value` (control → type-library member),
    // `subformControl.Form` (subform navigation),
    // `Forms!frmFoo` (collection default-member access),
    // `Me.RecordsetClone.MoveNext` (chained type-library walk).
    //
    // Returns a synthetic `Declaration` for any hit (form decl, report
    // decl, control decl, or wrapped type-library member). Returns
    // null when:
    //   - parent is a source-defined declaration the registry has no
    //     opinion on (resolver falls back to the existing
    //     default-member-not-yet-resolved path)
    //   - parent is a synthetic decl whose member set does not
    //     include `memberName`
    //
    // Implementations must not throw on unknown types or unknown
    // members — the resolver swallows null and emits the deferred
    // reason. The NullAccessObjectModelRegistry below is the no-op
    // default.
    Declaration? TryResolveMemberOf(Declaration parent, string memberName);
}

// Default no-op implementation used when callers do not supply a
// registry. Always returns null. Useful as the Session 1 default and
// in tests that want to assert "registry was not consulted" or
// "registry returned nothing".
public sealed class NullAccessObjectModelRegistry : IAccessObjectModelRegistry
{
    public static readonly NullAccessObjectModelRegistry Instance = new();

    public Declaration? TryResolveTopLevelMember(string name) => null;
    public Declaration? TryResolveControlOnForm(string formName, string controlName) => null;
    public Declaration? TryResolveControlOnReport(string reportName, string controlName) => null;
    public Declaration? TryResolveMemberOf(Declaration parent, string memberName) => null;
}
