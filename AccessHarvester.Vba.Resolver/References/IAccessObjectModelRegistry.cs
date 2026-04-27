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
}
