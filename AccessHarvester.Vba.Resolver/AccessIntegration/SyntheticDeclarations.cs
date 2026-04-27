using System.Collections.Generic;
using AccessHarvester.Access;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.AccessIntegration;

// Discriminator for the host-owned collection-style top-levels.
// Currently only Forms and Reports — DoCmd / Application / Screen are
// represented as separate cached decls if and when they are added.
public enum SyntheticHostCollectionKind
{
    Forms,
    Reports,
}

// `Forms` / `Reports` host top-level. Returned by
// IAccessObjectModelRegistry.TryResolveTopLevelMember("Forms" | "Reports").
// Default-member access (`Forms!frmName`) routes through
// IAccessObjectModelRegistry.TryResolveMemberOf to the named form/report
// declaration.
public sealed class SyntheticFormsCollectionDeclaration : Declaration
{
    public SyntheticHostCollectionKind Kind { get; }

    public SyntheticFormsCollectionDeclaration(SyntheticHostCollectionKind kind)
        : base(
            name: kind == SyntheticHostCollectionKind.Forms ? "Forms" : "Reports",
            provenance: new SyntheticAccessDeclarationProvenance(
                Artifact: kind == SyntheticHostCollectionKind.Forms ? "Forms" : "Reports"))
    {
        Kind = kind;
    }
}

// One Access form. `Controls` is the case-insensitive map of synthetic
// control declarations on this form. `ModuleName` is non-null when the
// form has a code-behind class module (typically `Form_<Name>`); used
// by the Access object-model registry's dual-key form lookup so the
// resolver's `Me.<control>` path (which calls
// TryResolveControlOnForm with the OwningClass.Name = module name)
// hits the same form a bare-name lookup would.
public sealed class SyntheticFormDeclaration : Declaration
{
    public string FormName { get; }
    public string? ModuleName { get; }
    public string TypeLibraryTypeName { get; }
    public IReadOnlyDictionary<string, Declaration> Controls { get; }

    public SyntheticFormDeclaration(
        string formName,
        string? moduleName,
        IReadOnlyDictionary<string, Declaration> controls)
        : base(
            name: formName,
            provenance: new SyntheticAccessDeclarationProvenance(formName))
    {
        FormName = formName;
        ModuleName = moduleName;
        TypeLibraryTypeName = "Access.Form";
        Controls = controls;
    }
}

// Mirror of SyntheticFormDeclaration for Access reports. The shape is
// intentionally identical; reports differ from forms only in their
// type-library type name and in the host collection they belong to
// (`Reports` vs. `Forms`).
public sealed class SyntheticReportDeclaration : Declaration
{
    public string ReportName { get; }
    public string? ModuleName { get; }
    public string TypeLibraryTypeName { get; }
    public IReadOnlyDictionary<string, Declaration> Controls { get; }

    public SyntheticReportDeclaration(
        string reportName,
        string? moduleName,
        IReadOnlyDictionary<string, Declaration> controls)
        : base(
            name: reportName,
            provenance: new SyntheticAccessDeclarationProvenance(reportName))
    {
        ReportName = reportName;
        ModuleName = moduleName;
        TypeLibraryTypeName = "Access.Report";
        Controls = controls;
    }
}

// One control on a form / report. `AccessClassName` is the literal
// AccessControl.Type (e.g. "TextBox", "ComboBox"); `TypeLibraryTypeName`
// is the qualified type the resolver dispatches to for member lookups
// (e.g. "Access.TextBox"). Null when AccessControlClassMap had no
// entry — the registry's dispatch falls through to
// default-member-not-yet-resolved in that case.
public class SyntheticControlDeclaration : Declaration
{
    public string ControlName { get; }
    public string AccessClassName { get; }
    public string? TypeLibraryTypeName { get; }
    public string OwnerArtifact { get; }

    public SyntheticControlDeclaration(
        string controlName,
        string accessClassName,
        string? typeLibraryTypeName,
        string ownerArtifact)
        : base(
            name: controlName,
            provenance: new SyntheticAccessDeclarationProvenance(ownerArtifact))
    {
        ControlName = controlName;
        AccessClassName = accessClassName;
        TypeLibraryTypeName = typeLibraryTypeName;
        OwnerArtifact = ownerArtifact;
    }
}

// Subform / Subreport control. Adds the contained-object binding lifted
// from the AccessControl's SourceObject property so subform navigation
// (`subformControl.Form` / `subreportControl.Report`) can reach the
// contained object's synthetic declaration. Exactly one of
// ContainedFormName / ContainedReportName is non-null when the
// SourceObject was parseable; both are null when it was absent or
// unparseable.
public sealed class SyntheticSubformControlDeclaration : SyntheticControlDeclaration
{
    public string? ContainedFormName { get; }
    public string? ContainedReportName { get; }

    public SyntheticSubformControlDeclaration(
        string controlName,
        string accessClassName,
        string? typeLibraryTypeName,
        string ownerArtifact,
        string? containedFormName,
        string? containedReportName)
        : base(controlName, accessClassName, typeLibraryTypeName, ownerArtifact)
    {
        ContainedFormName = containedFormName;
        ContainedReportName = containedReportName;
    }
}

// One member hit returned from ITypeLibraryRegistry. Wraps the
// registry's MemberInfo so the resolver's outcome shape stays uniform —
// member lookups against synthetic-typed targets produce
// `ResolutionOutcome.ResolvedToObjectModel(decl)` where the decl is one
// of these.
//
// OwnerTypeName is the lookup-time type name (the type the resolver
// asked the registry about — e.g. `"Access.TextBox"` for a TextBox
// `.Name` lookup, even though `.Name` is declared on `Access.Control`
// and reached through the inheritance chain). The distinction matters
// only for diagnostics; resolver dispatch reads `Member.ReturnType`
// directly. If the consumer needs the truly declaring type, that is a
// future ITypeLibraryRegistry extension — Phase 4.5 left the registry
// surface narrow.
public sealed class SyntheticTypeLibraryMemberDeclaration : Declaration
{
    public AccessHarvester.Access.MemberInfo Member { get; }
    public string OwnerTypeName { get; }
    public string? ReturnTypeName => Member.ReturnType;

    public SyntheticTypeLibraryMemberDeclaration(
        AccessHarvester.Access.MemberInfo member,
        string ownerTypeName)
        : base(
            name: member.Name,
            provenance: new SyntheticAccessDeclarationProvenance(ownerTypeName))
    {
        Member = member;
        OwnerTypeName = ownerTypeName;
    }
}
