using System.Collections.Generic;

namespace AccessHarvester.Vba.Resolver.AccessIntegration;

// Maps the AccessControl.Type literal (the Access class name as
// SaveAsText spells it) to the qualified type-library identifier the
// resolver uses for member lookups. Unknown classes fall back to
// Access.Control's universal control surface — see the synthesis
// design doc §4 for the rationale.
//
// Authored as a static helper rather than a registered service: the
// mapping is a compile-time fact, never customised per harvester run.
// New mappings are additive — adding an entry never invalidates an
// existing one.
public static class AccessControlClassMap
{
    // Universal control surface used for any class whose specific
    // type-library entry is not listed below. Public so callers can
    // distinguish "fallback fired" from "explicit mapping" if needed.
    public const string Fallback = "Access.Control";

    private static readonly IReadOnlyDictionary<string, string> _map
        = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "TextBox",       "Access.TextBox" },
            { "ComboBox",      "Access.ComboBox" },
            { "ListBox",       "Access.ListBox" },
            { "CommandButton", "Access.CommandButton" },
            { "Label",         "Access.Label" },
            // #11a does not currently register Access.SubForm /
            // Access.SubReport; the navigation member (.Form / .Report)
            // is dispatched directly in the registry rather than going
            // through ITypeLibraryRegistry, so the unregistered names
            // here are forward-compatibility hints only.
            { "Subform",       "Access.SubForm" },
            { "Subreport",     "Access.SubReport" },
        };

    // Returns the qualified type-library type name for the named
    // Access control class, or `Fallback` when the class is not
    // explicitly mapped. Case-insensitive (VBA / Access are
    // case-insensitive). Never returns null — callers that want to
    // detect the fallback case can compare against `Fallback`.
    public static string Resolve(string accessClassName)
    {
        if (string.IsNullOrEmpty(accessClassName))
            return Fallback;
        return _map.TryGetValue(accessClassName, out var typeName) ? typeName : Fallback;
    }

    // True when the named Access class has a specific type-library
    // mapping (i.e. Resolve does not fall back to Access.Control).
    // Currently used by the symbol-table builder to skip emitting
    // declarations for purely structural nodes if a future edit
    // narrowed the leaf-vs-wrapper rule; today the wrapper-skip rule
    // operates on the literal class name so this is exposed for
    // diagnostic purposes.
    public static bool IsMapped(string accessClassName)
        => !string.IsNullOrEmpty(accessClassName) && _map.ContainsKey(accessClassName);
}
