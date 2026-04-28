## Schema Conformance Analysis

### 1. Envelope Required Fields

| Field | Emitted Value | Schema Requirement | Status |
|---|---|---|---|
| `schemaVersion` | `"0.1"` | `^\d+\.\d+(\.\d+)?$` | ✅ Matches |
| `source.kind` | `"procedure"` | enum `[procedure, query, expression, form, report, database]` | ✅ Valid |
| `source.location.startLine` | `1` | integer, minimum 1 | ✅ Valid |
| `source.location.startColumn` | `1` | integer, minimum 1 | ✅ Valid |
| `items` | array | required array | ✅ Present |

---

### 2. procedureItem Required Fields

`BuildRunOutputEnvelope` emits all five required fields per item:

| Field | Source in Code | Status |
|---|---|---|
| `itemKind` | literal `"procedure"` | ✅ |
| `name` | `c.ProcedureName` | ✅ |
| `procedureKind` | `c.Kind.ToString()` | ⚠️ **See issue below** |
| `signature` | `new { parameters, returnType }` | ✅ |
| `body` | `new { }` | ✅ (empty but present, schema treats `body` properties as optional) |

---

### 3. `procedureKind` Enum Mismatch — **CRITICAL**

**Schema's closed enum:**
```
"Sub", "Function", "PropertyGet", "PropertyLet", "PropertySet", "EventHandler"
```

**Pipeline `ProcedureKind` enum (from `FunctionClassification.cs`):**
```
Sub, Function, PropertyGet, PropertyLet, PropertySet, Declare
```

The code calls `c.Kind.ToString()` which serialises `ProcedureKind.Declare` as `"Declare"`. The schema's closed enum does **not** contain `"Declare"`. Conversely, `"EventHandler"` is in the schema but absent from `ProcedureKind`.

The in-process test assertion (`RunOutputEnvelope_HasSchemaRequiredFields`) accepts:
```csharp
new[] { "Sub", "Function", "PropertyGet", "PropertyLet", "PropertySet", "Declare" }
```
This list mirrors the Pipeline enum rather than the schema enum, so the test will pass even when emitting schema-invalid JSON. Any fixture containing a `Declare` statement (common for Windows API declarations in legacy VBA modules such as fixture 07) would produce a non-conforming item.

---

### 4. `startColumn` Minimum-1 Violation — **MAJOR**

The `UnresolvedLocation.Column` documentation explicitly states:

> *"resolver diagnostics and SQL form-reference annotations use the parser layer's 0-based ANTLR convention"*

The envelope builder handles null columns with `?? 1` but **not zero columns**:

```csharp
startColumn = u.Location.Column ?? 1,   // Column = 0 passes through as 0
```

The schema requires `startColumn: integer, minimum: 1`. A resolver diagnostic or SQL form-reference unresolved item with `Column = 0` (ANTLR position) will emit:
```json
{ "startLine": 5, "startColumn": 0 }
```
…which fails the `minimum: 1` constraint. Sources affected: `UnresolvedSource.Resolver` and `UnresolvedSource.SqlFormReference`.

---

### 5. `MapSchemaReason` Reason-Code Coverage

All `MapSchemaReason` outputs are within the schema's closed enum. Every `UnresolvedSource` branch has a default/catch-all arm that emits a valid schema value, so no unmapped code can escape to an invalid string.

| Source | Reason codes handled | Schema values produced | Status |
|---|---|---|---|
| `Resolver` | 4 explicit + `_ => "unresolvedReference"` | valid | ✅ |
| `SqlFormReference` | 5 explicit + prefix match + `_ =>` fallback | valid | ✅ |
| `DynamicSqlSite` | 3 explicit + `_ =>` fallback | valid | ✅ |
| `NavigationTarget` | `non-literal-target`, `missing-target` + `_ =>` | valid | ✅ |
| `FunctionClassification` | 4 explicit + `_ =>` fallback | valid | ✅ |
| Default `_ =>` arm | catches any future `UnresolvedSource` | `"unsupportedConstruct"` | ✅ |

Schema values `"parseFailure"` and `"circularReference"` are never emitted by the current aggregator sources — this is a coverage gap in the *inverse* direction but not a conformance violation.

---

### 6. `purity` and `translatability` Enum Values

**`MapPurity` outputs:** `"mixed"`, `"pure"`, `"dataAccess"`, `"uiBound"`, `"externalIO"`, `"unknown"`
**Schema enum:** `pure | dataAccess | uiBound | externalIO | mixed | unknown`
→ ✅ Exact match.

**`MapTranslatability` outputs:** `"manualReview"`, `"mechanical"`, `"structured"`, `"ambiguous"`
**Schema enum:** `mechanical | structured | ambiguous | manualReview`
→ ✅ Exact match.

---

### 7. Minor: Test procedureKind Assertion Set Diverges from Schema

The assertion set in `RunOutputEnvelope_HasSchemaRequiredFields`:
```csharp
Assert.Contains(pk, new[] {
    "Sub", "Function", "PropertyGet", "PropertyLet", "PropertySet", "Declare",
});
```
This silently accepts `"Declare"` (not in schema) and would not detect `"EventHandler"` as required/expected. The test validates against the Pipeline enum, not the schema enum, so a future fixture emitting `Declare` would pass this test while failing JSON Schema validation.

---

## Summary Table

| # | Severity | Finding |
|---|---|---|
| 1 | Critical | `ProcedureKind.Declare` serialises as `"Declare"`, which is absent from schema's closed `procedureKind` enum |
| 2 | Major | ANTLR 0-based `Column` from Resolver/SqlFormReference sources passes through as `startColumn: 0`, violating `minimum: 1` |
| 3 | Minor | Test assertion's accepted `procedureKind` set (`"Declare"`) diverges from schema enum (`"EventHandler"`), masking issue #1 |

---

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "EndToEndFixtureTests.cs – BuildRunOutputEnvelope / RunOutputEnvelope_HasSchemaRequiredFields",
      "summary": "ProcedureKind.Declare serialises as \"Declare\" via ToString(), but the schema's closed procedureKind enum contains \"EventHandler\" instead of \"Declare\", making any Declare procedure produce a non-conforming itemKind entry.",
      "fix": "Add a MapProcedureKind helper that translates ProcedureKind.Declare to a schema-valid value (either omit Declare items, emit them as \"Sub\" with an isExtern flag, or negotiate adding \"Declare\" to the schema); update the test assertion set to match the schema enum (replace \"Declare\" with \"EventHandler\")."
    },
    {
      "severity": "Major",
      "location": "EndToEndFixtureTests.cs – BuildRunOutputEnvelope (unresolvedItems location projection)",
      "summary": "UnresolvedLocation.Column is documented as 0-based for Resolver and SqlFormReference sources; the null-coalescing guard (Column ?? 1) does not catch the value 0, so those sources can emit startColumn: 0, violating the schema's minimum: 1 constraint on sourceLocation.startColumn.",
      "fix": "Replace `u.Location.Column ?? 1` with `(u.Location.Column is null or <= 0) ? 1 : u.Location.Column.Value` (or normalise to 1-based at the UnresolvedItemAggregator boundary before the envelope is built)."
    },
    {
      "severity": "Minor",
      "location": "EndToEndFixtureTests.cs – RunOutputEnvelope_HasSchemaRequiredFields assertion",
      "summary": "The in-process procedureKind assertion set mirrors the Pipeline enum (includes \"Declare\", excludes \"EventHandler\") rather than the schema enum, so the test passes even when emitting schema-invalid \"Declare\" values.",
      "fix": "Update the assertion set to exactly match the schema enum: remove \"Declare\", add \"EventHandler\" — or drive the accepted set directly from the schema document to prevent future drift."
    }
  ]
}
```