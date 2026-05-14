## Review

### Overall assessment

The changes are correct, well-scoped, and follow existing conventions. Three issues worth calling out—two minor, one worth fixing before merge.

---

### TableExporter.cs

**DAO access paths** — all correct:

- `td.ValidationRule` / `td.ValidationText` are direct `TableDef` properties; wrapping with `SafeString` is right because Access returns an empty string rather than null for unset rules, and a missing-property COM exception is possible on older JET versions.
- `f.Properties("InputMask").Value` is the standard DAO `Properties` collection access. Exceptions when a property is absent are correctly swallowed by `SafeString` / `SafeInt`. This is the established project pattern.
- `DisplayControl` cast to `int` via `SafeInt` is correct (returns control type constants: 109, 106, 111, etc.).

---

### RelationshipExporter.cs

**Bitmask logic** — correct:

```csharp
EnforceReferentialIntegrity = (attributes & 0x00000002) == 0,   // dbRelationDontEnforce; absent → enforced ✅
CascadeUpdate               = (attributes & 0x00000100) != 0,   // dbRelationUpdateCascade ✅
CascadeDelete               = (attributes & 0x00001000) != 0,   // dbRelationDeleteCascade ✅
```

The masks are consistent with those already used in `DecodeAttributes`. No contradictions.

---

### TableMetadata.cs / TableMetadataExtractor.cs

Nullable types, `ReadString`/`ReadInt` helpers, and `PromotedKeys` inclusions are all consistent with the existing schema contract.

---

### Issues

**Issue 1 — Stale doc comment (fix before merge):**

`TableMetadata.ValidationRule` and `ValidationText` still carry this text:

> **Raw-IR coverage gap (acknowledged):** the current `TableExporter` … does not yet emit table-level `ValidationRule`/`ValidationText` … so `TableMetadataExtractor` currently always reads `null` here.

This PR fills that gap. Shipping this comment as-is will mislead future readers. The comment should be updated to indicate the gap is closed.

**Issue 2 — `Extract_RemovesPromotedKeysFromPropertiesBag` not updated (minor):**

The test confirms `Connect`, `SourceTableName`, `Fields`, `Indexes` are stripped from the bag but doesn't assert the same for `ValidationRule`/`ValidationText`, even though they are in `PromotedKeys`. Not a failure, but inconsistent coverage. Add:

```csharp
Assert.False(meta.Properties.ContainsKey("ValidationRule"));
Assert.False(meta.Properties.ContainsKey("ValidationText"));
```

**Issue 3 — Four new tests are sufficient but one edge case is untested (minor):**

`Extract_FieldWithNoDisplayMetadata_PropertiesAreNull` correctly covers the absent-property path via JSON keys simply being missing. However, the DAO exporter emits `null` JSON for absent properties (e.g. `"InputMask": null`) *and* omits the key entirely depending on the field. The `ReadString` / `ReadInt` helpers already handle both (`TryGetProperty` + `JsonValueKind.Null` check), so this is fine—just worth noting it's tested via the absent-key path, not the explicit-null path.

---

### Verdict

**Approve with fix for Issue 1.** The stale coverage-gap doc comment is the only item that should not ship as-is. Issue 2 is a two-line addition worth doing now. Issue 3 is informational.