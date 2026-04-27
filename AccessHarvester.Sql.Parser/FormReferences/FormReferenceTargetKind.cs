namespace AccessHarvester.Sql.Parser.FormReferences;

// Classifies the terminal declaration a FormReference resolved to. Used by
// downstream consumers to decide how to render or further analyse the
// reference (e.g., a Control resolution typically becomes a parameter
// binding; a TypeLibraryMember resolution typically becomes a property
// access in the migration target).
public enum FormReferenceTargetKind
{
    // The reference resolved to the Forms or Reports host collection itself
    // (`[Forms]` or `[Reports]` with no further segments).
    HostCollection,

    // The reference resolved to a SyntheticFormDeclaration
    // (e.g., `[Forms]![frmFoo]`).
    Form,

    // The reference resolved to a SyntheticReportDeclaration
    // (e.g., `[Reports]![rptFoo]`).
    Report,

    // The reference resolved to a SyntheticControlDeclaration on a form or
    // report (e.g., `[Forms]![frmFoo]![txtBar]`). Includes
    // SyntheticSubformControlDeclaration.
    Control,

    // The reference resolved to a type-library member reached via a member
    // chain (e.g., `[Forms]![frmFoo].Form.RecordSource` resolves the
    // terminal RecordSource against Access.Form's surface).
    TypeLibraryMember,
}
