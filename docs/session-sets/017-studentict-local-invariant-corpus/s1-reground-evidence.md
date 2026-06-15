# S1 Re-grounding Evidence — local FE harvest of StudentICTDatabase

> Step 1 of the session plan: re-run the **local** FE harvest
> (`run --db samples/StudentICTDatabase.accdb --out C:/temp/…`, foreground COM,
> output discarded) on the current tree and re-confirm the four Project Overview
> facts. Run date 2026-06-15. Output was inspected then discarded — **no
> StudentICT-derived artifact is committed** (FE/BE/`.lnk` are gitignored;
> `git check-ignore` confirmed all three).

## Harvest ran clean

`run` reported **0 failed** across every category (extract / translate / enrich):
Forms 36, Reports 7, Macros 2, ModulesAndClasses 25, Queries 41, Tables 20,
**Relationships 0**, Navigation. Exit 0.

## The four facts (re-confirmed, with one correction)

1. **Split FE/BE with linked tables — CONFIRMED (count corrected).** Of the 20 FE
   tables, **9** carry a non-empty `Connect` + `SourceTableName` pointing at
   `StudentICTDatabase_BE.accdb`; **11** are local (incl. `USysRibbons`). The spec's
   Project Overview said "13 of 20 … 7 local" — the actual current harvest is
   **9 linked / 11 local**. The earlier exploratory count was off; the test asserts
   a robust *presence* invariant (≥1 linked table), not a fixed count, so the
   correction does not affect the deliverable. `Relationships` is **0** on the FE
   (a split FE's relationships live in the BE). Linked tables (Connect→BE):
   tblAdministrators, tblClasses, tblICTConstantsBKP, tblStudentClasses,
   tblStudentLeavers, tblStudents, tblTableLinkTypesBKP, tblTableLinksBKP,
   tblTutorGroups.
2. **Embedded credential + absolute path — CONFIRMED (hard never-commit reason).**
   Every one of the 9 linked `Connect` strings has the shape
   `MS Access;PWD=<redacted>;DATABASE=<absolute-local-path>\StudentICTDatabase_BE.accdb`
   — a **plaintext password** plus an **absolute local filesystem path** to the BE.
   (The literal password value and machine path are deliberately **not** reproduced
   in this committed doc — that is exactly the leak DW-24 is about.) Committing
   harvested output would leak a live credential and a machine path. → **DW-24**.
3. **Second independent custom ribbon — CONFIRMED exactly.**
   `NavigationGraph.Ribbon.Name` = `"Help"`; `NavigationGraph.RibbonEntryPoints`
   = **5** (4 `OnAction` + 1 `OnLoad`), **all `Resolved`** to module
   `modRibbonCallbacks` (the four buttons share one callback — common dispatch
   pattern). Independently re-validates the 016 / DW-13 ribbon producer + S2
   bindable resolution on a different ribbon than Northwind (`modRibbonCallback`,
   singular).
4. **Large real DW-18 signal — CONFIRMED exactly.** `UnresolvedItems` = **3380**,
   of which **3315 are `FunctionClassification`** (and 65 `NavigationTarget`) —
   dominated by VBA intrinsic constants surfacing as false-positive unresolved refs.
   This corpus is the empirical demonstration of DW-18's scale.

## New finding surfaced by re-grounding (candidate DW)

**`TableMetadata.IsLinked` (and the raw `Attributes` bitmask) are absent from the
`run`-pipeline manifest table records.** `tblAdministrators` carries raw
`Attributes = 0x40020000` (the `dbAttachedTable` bit `0x40000000` **is** set), yet
its manifest/IR record has **no `IsLinked` key** and no `Attributes`. Root cause
(proven, not inferred): the `run` pipeline builds manifest `Tables` by
deserializing `ir/Tables/*.ir.json` into `TableMetadata`
(`Pipeline.BuildTableExports`, `AccessHarvester.Core/Pipeline.cs:1154-1190`). The
raw `TableExporter` writes the DAO dump with top-level `Attributes` / `Connect` /
`SourceTableName` but **never a derived `IsLinked`**, and the unmapped top-level
`Attributes` key is dropped on deserialization (it is not under a `Properties`
object). `DeriveIsLinked` exists only in `TableMetadataExtractor`
(`AccessHarvester.Access`, line 67 — the classify-path COM extractor, pinned by
`TableMetadataExtractorTests`), which is **not** on the `run` → manifest path. So
in `run` output `IsLinked` is **always null**, contradicting the `TableMetadata`
doc-comment ("consumers that need a 'is this linked' boolean should use
`IsLinked`"). The HHC committed golden confirms the omission is consistent
(no `IsLinked` key on any of its 13 tables — but HHC has no linked tables, so it
was invisible). **StudentICTDatabase is the first corpus with linked tables, so it
is the first to surface this gap.** The robust linked signal in `run` output is a
non-empty `Connect` + `SourceTableName` — which is exactly what this set's
invariant test asserts. → recorded as **DW-25** (do not fix here; corpus-onboarding
scope only).
