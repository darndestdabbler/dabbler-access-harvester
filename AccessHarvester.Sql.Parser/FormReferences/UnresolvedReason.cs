namespace AccessHarvester.Sql.Parser.FormReferences;

// Reason codes attached to UnresolvedFormReference. Stable strings so
// downstream tools and `expected.json` fixtures can match on them
// without parsing prose. New reasons are additive — never repurpose an
// existing code.
public static class UnresolvedReason
{
    // The reference was bare (e.g. `[Forms]` with no segments). The
    // host collection itself is not a navigation target.
    public const string BareCollectionReference = "bare-collection-reference";

    // First segment of a `[Forms]!...` chain did not match any form in
    // the registry. The form may be missing from the harvested metadata,
    // or the reference targets a runtime-loaded form the harvester
    // never saw.
    public const string FormNotInMetadata = "form-not-in-metadata";

    // First segment of a `[Reports]!...` chain did not match any
    // report in the registry. Mirror of FormNotInMetadata.
    public const string ReportNotInMetadata = "report-not-in-metadata";

    // The form resolved but the named control is not on it. Surfaces
    // both controls Access knows about under a different name and
    // controls dynamically created at runtime.
    public const string ControlNotOnForm = "control-not-on-form";

    // The report resolved but the named control is not on it.
    public const string ControlNotOnReport = "control-not-on-report";

    // The chain reached a deeper segment that the registry does not
    // know about (e.g., `[Forms]![frmFoo].Form.UnknownProperty`). The
    // segment index tells the consumer where the chain broke.
    public static string MemberNotFoundAtSegment(int zeroBasedIndex)
        => $"member-not-found-at-segment-{zeroBasedIndex}";

    // The registry did not contain a top-level entry for `Forms` or
    // `Reports`. With a real AccessObjectModelRegistry this is
    // impossible; surfaces only when callers pass
    // NullAccessObjectModelRegistry.Instance, which is the documented
    // way to opt out of resolution.
    public const string HostCollectionNotRegistered = "host-collection-not-registered";
}
