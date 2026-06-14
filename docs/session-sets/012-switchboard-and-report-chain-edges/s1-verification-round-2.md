**R1-Major-1 — nullable numerics preserved instead of coerced to 0**

This fix is correct.

- `SwitchboardItem.Command` is now `int?`.
- `SwitchboardItemProvenance.SwitchboardId` and `.ItemNumber` are now `int?`.
- `SwitchboardStructureBuilder` passes `r.Command`, `r.SwitchboardID`, and `r.ItemNumber` straight through with no `?? 0`.

That removes the specific fidelity loss from Round 1: a database `NULL` no longer becomes a synthetic `0` that collides with a real command-0/header row. The new regression pin covers the intended behavior.

On concern **(B)**: I do **not** see a new defect introduced by the nullable numerics themselves. The builder’s ordering policy (`ItemNumber ?? int.MaxValue`, then `SwitchboardID ?? int.MaxValue`) is explicit and deterministic; it affects sort position for rows missing ordinals, but it does not reintroduce value-coercion or identity ambiguity. So the Round 1 numeric fix looks complete.

---

**R1-Major-2 — page-level source identity for multi-table switchboards**

This fix is also correct.

- `SwitchboardPage.Table` has been added.
- The builder populates it from `table.Table`.
- Exporter ordering is deterministic by table name, and builder grouping is deterministic by page key within each table.

That resolves the concrete ambiguity from Round 1: `SwitchboardId` alone is no longer overloaded as if it were globally unique across multiple switchboard-shaped tables. The new regression pin covers the two-table case, and per-item provenance already includes `Table`, so both page-level and row-level traceability are now unambiguous.

On concern **(D)**: I do not see any remaining **multi-table identity** ambiguity in the structured output after this change.

---

**R1-Minor-3 — absent StartUpForm vs real DAO read failure**

This fix is substantively correct.

- `ReadStartUpForm` no longer blanket-catches everything and returns null.
- It first probes `db.Properties` for presence.
- If absent, it returns `null` cleanly for the legitimate “no startup form configured” case.
- If present, a read failure now propagates to `Export`, which records `ExportOutcome("startup-options", false, ...)`.

That addresses the actual Round 1 defect: a genuine failure is no longer silently reported as success with `null`.

On concern **(C)**: yes, this now separates **absent** from **read-error** in the normal/expected DAO path. The only small implementation caveat is that it probes for presence and then re-indexes the collection by name instead of reading `Value` from the matched property object directly; that is slightly less tight than it could be, but I would not call it a defect by itself.

---

**New/remaining issues introduced by the fixes**

I do **not** see a new regression caused by:
- making the numerics nullable,
- adding `SwitchboardPage.Table`, or
- changing the startup-form error handling.

However, there is still one **remaining silent coercion** in the structured projection: `ItemText == null` is converted to `string.Empty` for both `SwitchboardItem.Text` and page `Name`. That means Access `NULL` and a real zero-length string are still indistinguishable there. It is not one of the three Round 1 findings, but it is the same general fidelity-loss pattern.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "SwitchboardStructureBuilder.cs",
      "summary": "The structured projection still coerces a NULL ItemText to \"\" for both page Name and item Text, making Access NULL indistinguishable from a real zero-length label.",
      "fix": "Make SwitchboardPage.Name and SwitchboardItem.Text nullable (or add parallel raw nullable text fields) and remove the `?? string.Empty` coercions so NULL labels are preserved mechanically."
    }
  ]
}
```