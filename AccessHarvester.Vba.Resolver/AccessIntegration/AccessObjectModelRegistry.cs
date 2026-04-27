using System;
using System.Collections.Generic;
using AccessHarvester.Access;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;

namespace AccessHarvester.Vba.Resolver.AccessIntegration;

// Implementation of IAccessObjectModelRegistry backed by harvester-
// extracted Access metadata and the curated type-library registry. See
// docs/session-sets/access-resolver-symbol-table-integration/synthesis/
// session-1-synthetic-declaration-injection.md §6 for the dispatch rules.
//
// Construction:
//   - all forms / reports are pre-walked into synthetic declarations
//     by AccessSymbolTableBuilder
//   - dual-key indexes (by form/report Name and by ModuleName) are
//     pre-built for O(1) lookup against either identifier
//   - the Forms / Reports collection top-level decls are built once
//     and cached so identity matches across repeated lookups
//
// Thread-safety: members of `_typeLibMemberCache` are written lazily
// inside TryResolveMemberOf. The registry is intended for
// single-threaded resolver runs (one project's resolution pass) and is
// not designed for concurrent access; callers wanting parallel resolution
// should construct one registry per worker.
public sealed class AccessObjectModelRegistry : IAccessObjectModelRegistry
{
    private readonly Dictionary<string, SyntheticFormDeclaration> _formsByName;
    private readonly Dictionary<string, SyntheticFormDeclaration> _formsByModuleName;
    private readonly Dictionary<string, SyntheticReportDeclaration> _reportsByName;
    private readonly Dictionary<string, SyntheticReportDeclaration> _reportsByModuleName;
    private readonly SyntheticFormsCollectionDeclaration _formsCollection;
    private readonly SyntheticFormsCollectionDeclaration _reportsCollection;
    private readonly ITypeLibraryRegistry _typeLib;

    // Cache of (typeName, memberName) -> wrapped MemberDeclaration so
    // identity is stable across repeated TryResolveMemberOf calls. Same
    // case-insensitive comparer the rest of the resolver uses.
    private readonly Dictionary<(string Type, string Member), SyntheticTypeLibraryMemberDeclaration>
        _typeLibMemberCache = new(TypeMemberKeyComparer.Instance);

    public AccessObjectModelRegistry(
        IReadOnlyList<FormMetadata> forms,
        IReadOnlyList<ReportMetadata> reports,
        ITypeLibraryRegistry typeLibrary)
    {
        if (forms is null) throw new ArgumentNullException(nameof(forms));
        if (reports is null) throw new ArgumentNullException(nameof(reports));
        _typeLib = typeLibrary ?? throw new ArgumentNullException(nameof(typeLibrary));

        _formsByName = new Dictionary<string, SyntheticFormDeclaration>(
            Declaration.NameComparer);
        _formsByModuleName = new Dictionary<string, SyntheticFormDeclaration>(
            Declaration.NameComparer);
        foreach (var form in forms)
        {
            var decl = AccessSymbolTableBuilder.BuildForm(form);
            // First-wins on duplicate Names — Access guarantees unique
            // form names per project; a duplicate is a fixture defect.
            if (!_formsByName.ContainsKey(decl.FormName))
                _formsByName.Add(decl.FormName, decl);
            if (!string.IsNullOrEmpty(decl.ModuleName)
                && !_formsByModuleName.ContainsKey(decl.ModuleName!))
                _formsByModuleName.Add(decl.ModuleName!, decl);
        }

        _reportsByName = new Dictionary<string, SyntheticReportDeclaration>(
            Declaration.NameComparer);
        _reportsByModuleName = new Dictionary<string, SyntheticReportDeclaration>(
            Declaration.NameComparer);
        foreach (var report in reports)
        {
            var decl = AccessSymbolTableBuilder.BuildReport(report);
            if (!_reportsByName.ContainsKey(decl.ReportName))
                _reportsByName.Add(decl.ReportName, decl);
            if (!string.IsNullOrEmpty(decl.ModuleName)
                && !_reportsByModuleName.ContainsKey(decl.ModuleName!))
                _reportsByModuleName.Add(decl.ModuleName!, decl);
        }

        _formsCollection = new SyntheticFormsCollectionDeclaration(
            SyntheticHostCollectionKind.Forms);
        _reportsCollection = new SyntheticFormsCollectionDeclaration(
            SyntheticHostCollectionKind.Reports);
    }

    // ── IAccessObjectModelRegistry ───────────────────────────────────

    public Declaration? TryResolveTopLevelMember(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Declaration.NameComparer.Equals(name, "Forms"))
            return _formsCollection;
        if (Declaration.NameComparer.Equals(name, "Reports"))
            return _reportsCollection;
        if (_formsByName.TryGetValue(name, out var formDecl))
            return formDecl;
        if (_reportsByName.TryGetValue(name, out var reportDecl))
            return reportDecl;
        return null;
    }

    public Declaration? TryResolveControlOnForm(string formName, string controlName)
    {
        if (string.IsNullOrEmpty(formName) || string.IsNullOrEmpty(controlName))
            return null;
        // Dual-key: ModuleName first (Me-via-class path), Name as fallback.
        if (_formsByModuleName.TryGetValue(formName, out var byModule)
            && byModule.Controls.TryGetValue(controlName, out var hit))
            return hit;
        if (_formsByName.TryGetValue(formName, out var byName)
            && byName.Controls.TryGetValue(controlName, out var hit2))
            return hit2;
        return null;
    }

    public Declaration? TryResolveControlOnReport(string reportName, string controlName)
    {
        if (string.IsNullOrEmpty(reportName) || string.IsNullOrEmpty(controlName))
            return null;
        if (_reportsByModuleName.TryGetValue(reportName, out var byModule)
            && byModule.Controls.TryGetValue(controlName, out var hit))
            return hit;
        if (_reportsByName.TryGetValue(reportName, out var byName)
            && byName.Controls.TryGetValue(controlName, out var hit2))
            return hit2;
        return null;
    }

    public Declaration? TryResolveMemberOf(Declaration parent, string memberName)
    {
        if (parent is null || string.IsNullOrEmpty(memberName))
            return null;

        // 1. Subform / Subreport navigation — handled directly because
        //    Access.SubForm / Access.SubReport are not registered in
        //    #11a. Falls through to the type-library path below for
        //    other members on the subform control (`.Name`, `.Visible`,
        //    etc., which live on Access.Control).
        if (parent is SyntheticSubformControlDeclaration sub)
        {
            if (Declaration.NameComparer.Equals(memberName, "Form")
                && sub.ContainedFormName is { } fn
                && _formsByName.TryGetValue(fn, out var formDecl))
                return formDecl;
            if (Declaration.NameComparer.Equals(memberName, "Report")
                && sub.ContainedReportName is { } rn
                && _reportsByName.TryGetValue(rn, out var reportDecl))
                return reportDecl;
            // Otherwise fall through to control-as-type-library member
            // resolution.
        }

        // 2. Synthetic control with a known type-library type.
        if (parent is SyntheticControlDeclaration ctl
            && ctl.TypeLibraryTypeName is { } typeName)
        {
            return ResolveTypeLibraryMember(typeName, memberName);
        }

        // 3. Synthetic form — first try Access.Form's surface (covers
        //    Me.RecordSource, Me.Refresh, etc. when the form has a
        //    class module), else look up named control.
        if (parent is SyntheticFormDeclaration form)
        {
            var hit = ResolveTypeLibraryMember(form.TypeLibraryTypeName, memberName);
            if (hit is not null) return hit;
            return form.Controls.TryGetValue(memberName, out var ctlHit) ? ctlHit : null;
        }

        // 4. Synthetic report — same shape as form.
        if (parent is SyntheticReportDeclaration report)
        {
            var hit = ResolveTypeLibraryMember(report.TypeLibraryTypeName, memberName);
            if (hit is not null) return hit;
            return report.Controls.TryGetValue(memberName, out var ctlHit) ? ctlHit : null;
        }

        // 5. Forms / Reports host collection — `Forms!frmFoo` /
        //    `Reports!rptFoo` shape.
        if (parent is SyntheticFormsCollectionDeclaration coll)
        {
            return coll.Kind == SyntheticHostCollectionKind.Forms
                ? (_formsByName.TryGetValue(memberName, out var f) ? (Declaration)f : null)
                : (_reportsByName.TryGetValue(memberName, out var r) ? (Declaration)r : null);
        }

        // 6. Type-library member chain — `Me.RecordsetClone.MoveNext`
        //    walks via the previous member's ReturnType.
        if (parent is SyntheticTypeLibraryMemberDeclaration member
            && member.ReturnTypeName is { } returnType)
        {
            return ResolveTypeLibraryMember(returnType, memberName);
        }

        // 7. Otherwise — null. Source-defined declarations and
        //    everything else stay on the existing fall-through path.
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private SyntheticTypeLibraryMemberDeclaration? ResolveTypeLibraryMember(
        string typeName, string memberName)
    {
        var key = (typeName, memberName);
        if (_typeLibMemberCache.TryGetValue(key, out var cached))
            return cached;

        if (!_typeLib.TryGetMember(typeName, memberName, out var info) || info is null)
            return null;

        // OwnerTypeName is the lookup-time type name — see the doc
        // comment on SyntheticTypeLibraryMemberDeclaration. The
        // registry's TryGetMember does not expose the declaring type,
        // so reaching that name today would require a separate
        // contract extension; resolver dispatch only consults
        // Member.ReturnType, so the lookup-time name is sufficient
        // for the resolver's needs.
        var decl = new SyntheticTypeLibraryMemberDeclaration(info, typeName);
        _typeLibMemberCache[key] = decl;
        return decl;
    }

    // Case-insensitive equality on the (Type, Member) key tuple.
    private sealed class TypeMemberKeyComparer
        : IEqualityComparer<(string Type, string Member)>
    {
        public static readonly TypeMemberKeyComparer Instance = new();

        public bool Equals((string Type, string Member) x, (string Type, string Member) y)
            => Declaration.NameComparer.Equals(x.Type, y.Type)
            && Declaration.NameComparer.Equals(x.Member, y.Member);

        public int GetHashCode((string Type, string Member) obj)
            => System.HashCode.Combine(
                Declaration.NameComparer.GetHashCode(obj.Type),
                Declaration.NameComparer.GetHashCode(obj.Member));
    }
}
