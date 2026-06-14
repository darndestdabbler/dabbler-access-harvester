A. `SwitchboardStructureBuilder` is mostly correct for the disclosed single-table canonical and HomeHealthCare-style custom cases, but I do not think it is fully clean yet.

- For canonical tables, `HasParentId = false` and grouping by `SwitchboardID` is the right mechanical rule.
- For custom tables like HHC, `HasParentId = true` and grouping by `ParentID` is also the right mechanical rule given the probe results.
- Name selection by the lowest `ItemNumber` row, with `SwitchboardID` as tie-breaker, is deterministic and reasonable for both shapes.
- Null-parent handling is good: rows with no page key are not silently lost from the raw artifact, and `UnlinkedItemCount` makes their omission from `Pages` explicit.

The problems are:

1. The structured projection silently coerces nullable raw numerics to `0`:
   - `Command = r.Command ?? 0`
   - provenance `SwitchboardId = r.SwitchboardID ?? 0`
   - provenance `ItemNumber = r.ItemNumber ?? 0`

   That is not faithful mechanical preservation. A raw `NULL` command becomes indistinguishable from a real command code `0`, which directly conflicts with the model comment saying the command is preserved “exactly as stored” and weakens the “must never be silently dropped or defaulted” constraint.

2. The multi-table case is under-modelled. Detection/export explicitly allows more than one shape-matched table, but `SwitchboardStructureBuilder` just appends pages table-by-table. `SwitchboardStructure.Pages` is documented as “ordered by page key”, but in reality it is ordered by `(table-name order, then page key within table)`. More importantly, `SwitchboardPage.SwitchboardId` is being used as a generic page key for custom tables, even though its name/comment says it is the stored `SwitchboardID`. If two tables both have page key `0` or `1`, page identity is ambiguous at page level.

B. On the Hard Constraints and design decisions #2 and #3:

- Decision #2, using DAO instead of opening a second OLE DB connection, is defensible. The important constraints are the discipline, not the transport:
  - identifier escaping,
  - narrow fixed-column acquisition,
  - ordered deterministic read,
  - per-object degradation through `ExportOutcome`.
  
  Using the already-open DAO/COM session is operationally safer than opening a second connection against the same file.

- Decision #3, not emitting `UnresolvedNavigationTarget` for switchboard rows, is also defensible in principle. That record shape is VBA-dispatch-oriented, and inventing “unknown command” semantics for custom switchboard tables would be interpretive. Preserving raw `Command` and `Argument` values is the right no-interpretation posture.

- However, that argument only holds if the structured projection does not silently normalize away source nullability. Right now it does, so the raw-preservation guarantee is slightly compromised even though the absence of `UnresolvedNavigationTarget` itself is not the core problem.

C. `SwitchboardExporter` is generally sound, with one notable weakness.

What looks good:
- signature detection is by columns, not table name;
- system tables are excluded;
- field introspection gates row-read;
- acquisition is a fixed SELECT over canonical columns plus optional `ParentID`;
- recordset close is in `finally`;
- `DBNull`/`null` handling in `ReadInt`/`ReadString` is correct;
- table payloads are ordered deterministically before serialization.

What I would push back on:
- `ReadStartUpForm` catches all exceptions and converts them to `null`, and `Export` always reports `startup-options` as success. That means an unexpected DAO/property read failure is silently turned into “no startup form configured”, which can wrongly remove the default-form entrypoint. Missing property is fine to normalize to null; unexpected failures should be surfaced as a failed `ExportOutcome` or diagnostic.

I do not see a concrete problem with the DAO recordset close or fixed-column SQL shape. `EscapeIdentifier` use is plausible, assuming it returns an escaped identifier body rather than already-bracketed text, which appears consistent with the call pattern.

D. `MacroChainBuilder` looks solid overall.

- Positional argument mapping for known actions is fine.
- Deterministic fallback keys `Argument{n}` are good and preserve unknown action slots.
- Scalar/array/absent argument normalization is fine.
- `RunCode` preserves the raw argument while deriving `CallsProcedureName` by stripping parens; that is the right split.
- Comment-only `Begin` nodes are skipped because they have no `Action`.
- File ordering and tree walk are deterministic.

My only mild caution is that action collection keys off `Properties.Action` rather than explicitly checking `Type == "Begin"`. That is a bit permissive, but I do not see enough evidence here to call it a defect.

E. I do not think this materially drifts toward a general row-export surface.

It remains narrow because:
- detection is shape-gated by a very specific column signature;
- only a fixed canonical column set is read;
- only tables matching that signature are read;
- there is no arbitrary table/data dump capability introduced.

Supporting multiple shape-matched tables does widen the set of switchboard-like tables captured, but that is still within the explicitly approved generic mechanical design, not a general export surface.

F. On additive-only schema/model changes and envelope bump:

- The additive fields themselves (`SwitchboardItem.Provenance`, `UnlinkedItemCount`) look compatible with an `additionalProperties`-tolerant schema, so no envelope bump is needed for those additions alone.
- Macro `Arguments` fallback naming is additive and deterministic.
- The one thing that is not purely additive in spirit is reusing `SwitchboardPage.SwitchboardId` as a generic page key when `ParentID` exists. Because the field was previously unpopulated in production, this may be operationally acceptable now, but it is a semantic widening of an existing field and should at least be documented more explicitly. Ideally the model would expose an explicit page-key concept or page provenance.

Overall: the session is close, and the narrow DAO-based extractor approach is defensible, but I would not independently mark this fully verified because the builder currently violates the no-silent-default principle for nullable numeric source data, and the page-level model is not robust for the multi-table case that the detector/exporter explicitly allow.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Enrichment/SwitchboardStructureBuilder.cs",
      "summary": "The structured switchboard projection silently coerces nullable raw numerics to 0, so a NULL Command or missing provenance ID becomes indistinguishable from a real zero value.",
      "fix": "Make the affected structured fields nullable or surface malformed rows explicitly via diagnostics/an anomaly model; at minimum stop mapping `r.Command ?? 0`, `r.SwitchboardID ?? 0`, and `r.ItemNumber ?? 0` into loss-bearing defaults."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Enrichment/SwitchboardStructureBuilder.cs",
      "summary": "Multi-table switchboards are appended table-by-table without page-level source identity, and `SwitchboardPage.SwitchboardId` is overloaded as a generic page key, making page identity/order ambiguous when more than one shape-matched table exists.",
      "fix": "Add explicit page-level provenance or a `PageKey` concept (or enforce a single-table build with a diagnostic), and sort pages deterministically across all pages rather than only within each table."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Core/Extraction/SwitchboardExporter.cs",
      "summary": "`ReadStartUpForm` catches all exceptions and `Export` still records success, so genuine DAO/property read failures collapse silently to `null` and can remove the default-form entrypoint without any failure signal.",
      "fix": "Differentiate 'property absent' from unexpected read errors and surface the latter through `ExportOutcome` or a diagnostic instead of always serializing a successful null result."
    }
  ]
}
```