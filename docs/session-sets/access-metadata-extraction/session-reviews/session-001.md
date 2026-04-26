# Verification Round 1

## ISSUES_FOUND

### Major — Control-name coverage is incomplete
**Issue →** Phase 4.1/4.2 explicitly calls for every control with **type, name, properties**. `AccessControl` exposes `Type` and `Properties`, but not a typed `Name`, so control identity is still buried in the raw bag. That weakens deliverable coverage and forces Session 4 / Phase 4.4 consumers to dictionary-scan for a core field.

**Location →** `AccessHarvester.Access/AccessObjectCommon.cs` (`AccessControl`)

**Fix →** Add a promoted `Name` field to `AccessControl`:
- `public string? Name { get; init; }`
- Document it as null for anonymous `Begin` wrappers and any nodes Access did not name.
- Update the class remarks so the nullability rule no longer says “every field except `Type` is nullable” without acknowledging `Name`.

---

### Major — `DefaultView`’s raw-vs-decoded contract is contradictory and risks losing fidelity
**Issue →** The remarks say promoted keys should be removed from `Properties`, but the `DefaultView` docs say consumers can still read `Properties["DefaultView"]`. Those cannot both be true. More importantly, `DefaultView` is a decoded label, not the raw Access-written value, so stripping the raw numeric code from `Properties` would reduce round-trip fidelity versus the existing IR shape.

**Location →**  
- `AccessHarvester.Access/FormMetadata.cs` (`remarks`, `DefaultView`)  
- `AccessHarvester.Access/ReportMetadata.cs` (`remarks`, `DefaultView`)

**Fix →** Pick one contract and document it consistently. Recommended:
- Keep raw `Properties["DefaultView"]` when also exposing decoded `DefaultView`.
- Narrow the “remove promoted keys from `Properties`” guidance to exact-value lifts only (`RecordSource`, `Filter`, `OrderBy`), not decoded transforms.
- At minimum, remove the contradictory sentence that tells consumers to read a key the remarks say extractors should delete.

---

### Minor — Subform/subreport nesting semantics are inconsistent across files
**Issue →** `AccessControl.Children` says children can include “a subform/subreport’s contained design tree,” but `FormMetadata` / `ReportMetadata` explicitly say subforms and subreports are extracted separately and **not nested inline** to keep the schema acyclic. That ambiguity is a landmine for Session 3 fixture authoring and Session 4 extractor wiring.

**Location →**  
- `AccessHarvester.Access/AccessObjectCommon.cs` (`Children` XML doc)  
- `AccessHarvester.Access/FormMetadata.cs` (`Children` / remarks)  
- `AccessHarvester.Access/ReportMetadata.cs` (`Children` / remarks)

**Fix →** Make the contract single-source and explicit. Recommended:
- Update `AccessControl.Children` docs to say subform/subreport controls reference separately extracted objects via `SourceObject`; in this typed schema their `Children` should remain null/empty rather than inline the contained form/report tree.
- Keep the acyclic design; do not introduce a nested-object variant unless you actually want duplicate embedded metadata.

---

# Verification Round 2

## ISSUES_FOUND

### Round-1 findings recheck
- **`session-1-round-1-major-1` — Resolved cleanly.** `AccessControl` now has a typed `Name`, the nullability rule now explicitly includes `Name`, and the extractor no-double-emission rule is documented.
- **`session-1-round-1-major-2` — Resolved cleanly.** `FormMetadata` now defines the exact-value-lift vs decoded-lift split, `ReportMetadata` mirrors it, and the `DefaultView` contradiction is gone.
- **`session-1-round-1-minor-1` — Resolved cleanly.** `AccessControl.Children` is now the single source of truth, and `FormMetadata.Children` / `ReportMetadata.Children` cross-reference that acyclic contract unambiguously.

### Contradiction / ambiguity check
- **`AccessControl.Name` + revised nullable-field remark** — **Accurate now.** The statement “`Type` is the sole required field” matches the type, and the explicit `Name = null` rule for anonymous / unnamed nodes closes the previous gap.
- **Exact-value-vs-decoded promotion rule** — **Consistent between `FormMetadata` and `ReportMetadata`, but still contradicted by `AccessControl.Properties`.** See remaining Major issue below.
- **Single-source acyclic `Children` contract** — **Unambiguous now.** The common-type doc is authoritative and the form/report docs no longer restate a competing rule.

### Major — `AccessControl.Properties` still documents a raw bag the schema no longer exposes
**Issue →** `AccessControl.Name` is now promoted out and explicitly removed from `Properties` when non-null, and `NonDefaultProperties` explicitly says decoded values are rewritten in place inside `Properties`. `AccessControl.Properties` still says it is “the raw property bag exactly as Access wrote it” and that it round-trips faithfully. That contract is now false for control nodes and conflicts with the form/report-level promotion docs.

**Location →** `AccessHarvester.Access/AccessObjectCommon.cs` (`AccessControl.Properties`; reinforced by `Name` and `NonDefaultProperties` docs)

**Fix →** Rewrite the `AccessControl.Properties` documentation to match the actual IR contract: preserve heterogeneous JSON shapes and remaining raw twip values, but explicitly disclose sanctioned mutations. At minimum document that:
- promoted exact-value lifts like `Name` may be removed from `Properties`, and
- property-decoding may rewrite literal `NotDefault` sentinels to decoded values in place.

### Minor — `AccessControl.VbaCode` is redundant with the top-level schema and its documented population point is impossible in the exposed tree shape
**Issue →** `FormMetadata` / `ReportMetadata` already carry top-level `VbaCode`. `AccessControl` is documented as the node type used under `Children`, and the acyclic contract forbids inlining nested form/report roots. That leaves no clear valid place in this schema where an `AccessControl` node representing the form/report root would exist, yet `AccessControl.VbaCode` says it is populated “only on the form / report root”. This is an ambiguous duplicate path for the same payload.

**Location →** `AccessHarvester.Access/AccessObjectCommon.cs` (`AccessControl.VbaCode`) with context in `AccessHarvester.Access/FormMetadata.cs` and `AccessHarvester.Access/ReportMetadata.cs` (`Children`, `VbaCode`)

**Fix →** Remove `VbaCode` from `AccessControl`, or explicitly document a separate API surface where `AccessControl` is used as a standalone form/report root node. If Session 1 exposes only the typed metadata schema shown here, removal is the cleaner fix.

### Nitpick — `GeometryInches` remarks overclaim where the unit is encoded
**Issue →** The remarks say “The unit is in the field name rather than implicit,” but the fields are `X`, `Y`, `W`, `H`; the unit is actually carried by the containing `GeometryIn` / `GeometryInches` name, not by each coordinate field.

**Location →** `AccessHarvester.Access/AccessObjectCommon.cs` (`GeometryInches` remarks and fields)

**Fix →** Either rename the fields to `XIn` / `YIn` / `WIn` / `HIn`, or change the remarks to say the unit is explicit in the containing geometry property/type rather than in each coordinate field.
