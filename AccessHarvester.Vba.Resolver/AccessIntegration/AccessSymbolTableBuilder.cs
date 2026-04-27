using System;
using System.Collections.Generic;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.AccessIntegration;

// Builds the synthetic-declaration trees for one project's worth of
// forms and reports. Pure transformer — no registry queries, no
// resolver state. The output is consumed by AccessObjectModelRegistry
// which wires these decls into IAccessObjectModelRegistry's lookup
// surface.
//
// The walk skips structural wrappers (Section, FormHeader/FormFooter,
// PageHeader/PageFooter, GroupLevel*Header/Footer, the anonymous Begin
// wrapper) and emits one synthetic control declaration per leaf
// control with a non-null Name. See the synthesis design doc §5 for
// the exact rules.
public static class AccessSymbolTableBuilder
{
    // Class names that are structural wrappers, not user-visible
    // controls. A node whose Type matches here is recursed into but
    // does not itself emit a declaration. The set is closed at this
    // layer — Access has a fixed set of section / wrapper names.
    private static readonly HashSet<string> _wrapperTypes
        = new(StringComparer.OrdinalIgnoreCase)
        {
            "Section",
            "FormHeader", "FormFooter",
            "PageHeader", "PageFooter",
            "Detail",        // surfaced as Type="Section" with Name="Detail" in practice; included defensively
            "Begin",         // anonymous default-property wrapper from SaveAsText
        };

    // True when the Type is a wrapper. Group-level header/footer names
    // (`GroupLevel0Header`, `GroupLevel1Footer`, …) are matched by prefix
    // since the suffix is variable.
    private static bool IsWrapper(string type)
    {
        if (string.IsNullOrEmpty(type)) return true; // defensive — anonymous nodes
        if (_wrapperTypes.Contains(type)) return true;
        return type.StartsWith("GroupLevel", StringComparison.OrdinalIgnoreCase)
            && (type.EndsWith("Header", StringComparison.OrdinalIgnoreCase)
                || type.EndsWith("Footer", StringComparison.OrdinalIgnoreCase));
    }

    // Build a SyntheticFormDeclaration from FormMetadata. Walks the
    // design tree; emits a synthetic decl per named control. Subforms
    // are surfaced as SyntheticSubformControlDeclaration with the
    // SourceObject parsed into ContainedFormName / ContainedReportName.
    public static SyntheticFormDeclaration BuildForm(FormMetadata form)
    {
        if (form is null) throw new ArgumentNullException(nameof(form));
        var controls = new Dictionary<string, Declaration>(
            Declaration.NameComparer);
        if (form.Children is not null)
        {
            foreach (var child in form.Children)
                WalkNode(child, ownerArtifact: form.Name, controls);
        }
        return new SyntheticFormDeclaration(
            formName: form.Name,
            moduleName: form.ModuleName,
            controls: controls);
    }

    public static SyntheticReportDeclaration BuildReport(ReportMetadata report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        var controls = new Dictionary<string, Declaration>(
            Declaration.NameComparer);
        if (report.Children is not null)
        {
            foreach (var child in report.Children)
                WalkNode(child, ownerArtifact: report.Name, controls);
        }
        return new SyntheticReportDeclaration(
            reportName: report.Name,
            moduleName: report.ModuleName,
            controls: controls);
    }

    private static void WalkNode(
        AccessControl node,
        string ownerArtifact,
        Dictionary<string, Declaration> controls)
    {
        var type = node.Type;
        if (IsWrapper(type))
        {
            if (node.Children is not null)
                foreach (var c in node.Children) WalkNode(c, ownerArtifact, controls);
            return;
        }

        // Leaf control with no Name (anonymous template, etc.) — skip.
        if (string.IsNullOrEmpty(node.Name))
        {
            // Some Access nodes carry both child controls and no name
            // (rare). Recurse defensively so a misclassified non-wrapper
            // does not silently drop its children.
            if (node.Children is not null)
                foreach (var c in node.Children) WalkNode(c, ownerArtifact, controls);
            return;
        }

        var typeLibName = AccessControlClassMap.Resolve(type);
        Declaration decl;
        if (string.Equals(type, "Subform", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Subreport", StringComparison.OrdinalIgnoreCase))
        {
            var (containedForm, containedReport) = ParseSourceObject(
                type, node.Properties);
            decl = new SyntheticSubformControlDeclaration(
                controlName: node.Name!,
                accessClassName: type,
                typeLibraryTypeName: typeLibName,
                ownerArtifact: ownerArtifact,
                containedFormName: containedForm,
                containedReportName: containedReport);
        }
        else
        {
            decl = new SyntheticControlDeclaration(
                controlName: node.Name!,
                accessClassName: type,
                typeLibraryTypeName: typeLibName,
                ownerArtifact: ownerArtifact);
        }

        // First-wins on duplicate names — Access disallows duplicates,
        // so a duplicate is a fixture defect; preserving the first
        // matches the order Access wrote the design tree in.
        if (!controls.ContainsKey(node.Name!))
            controls.Add(node.Name!, decl);

        // Subform / Subreport nodes do not inline their contained
        // form/report — that's a separate FormMetadata/ReportMetadata
        // entry — so we do NOT recurse into their Children for
        // declaration emission. Other control types may still hold
        // children (e.g. TabControl pages, NavigationControl); recurse
        // so any named controls inside them are surfaced too.
        if (!IsContainedObjectControl(type) && node.Children is not null)
            foreach (var c in node.Children) WalkNode(c, ownerArtifact, controls);
    }

    private static bool IsContainedObjectControl(string type)
        => string.Equals(type, "Subform", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "Subreport", StringComparison.OrdinalIgnoreCase);

    // Extract the contained-object name from the SourceObject property
    // on a Subform / Subreport control. Access stores SourceObject as
    // a string — typically the bare contained form/report name, but
    // sometimes prefixed with "Report." to disambiguate when a Subform
    // control hosts a report-class object inside it. Returns
    // (containedForm, containedReport) — exactly one is non-null when
    // the SourceObject is parseable; both are null otherwise.
    private static (string? Form, string? Report) ParseSourceObject(
        string controlType, IReadOnlyDictionary<string, JsonElement>? properties)
    {
        if (properties is null) return (null, null);
        if (!TryGetStringProperty(properties, "SourceObject", out var raw)
            || string.IsNullOrWhiteSpace(raw))
            return (null, null);

        var trimmed = raw!.Trim();

        // Access prefixes report-class subordinates with "Report.";
        // Form-class subordinates are usually bare but may carry
        // "Form." defensively.
        if (trimmed.StartsWith("Report.", StringComparison.OrdinalIgnoreCase))
            return (null, trimmed.Substring("Report.".Length));
        if (trimmed.StartsWith("Form.", StringComparison.OrdinalIgnoreCase))
            return (trimmed.Substring("Form.".Length), null);

        // No prefix: derive form-vs-report from the control type.
        // Subreport without a "Report." prefix still hosts a report.
        return string.Equals(controlType, "Subreport", StringComparison.OrdinalIgnoreCase)
            ? (null, trimmed)
            : (trimmed, null);
    }

    private static bool TryGetStringProperty(
        IReadOnlyDictionary<string, JsonElement> bag, string key, out string? value)
    {
        // The Properties dict uses Access's casing; lookups are
        // case-insensitive everywhere else in this codebase, so be
        // tolerant here too.
        foreach (var kv in bag)
        {
            if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                continue;
            if (kv.Value.ValueKind == JsonValueKind.String)
            {
                value = kv.Value.GetString();
                return value is not null;
            }
            // Non-string values (rare for SourceObject) are not
            // parseable in the navigation sense.
            value = null;
            return false;
        }
        value = null;
        return false;
    }
}
