# Plan: Access/JET Sensitive-Data Classifier & Migration Toolkit

## 1. Goal

Build a lightweight, transparent pipeline that produces a **preliminary** classification of
the tables and columns in a Microsoft Access (JET/ACE) database, then routes that classification
through a human-confirmable JSON artifact. IT staff (optionally with business users) confirm or
override the guesses in an HTML editor. The corrected JSON then drives two generators:

1. **Sanitized test records** — replace sensitive values with mock data for testing.
2. **Migration code** — move the data to SQL Server, preserving what must be preserved.

The algorithm does **not** need to be perfect. It needs to be cheap, explainable, and easy for a
human to correct. Every automated decision is a *suggestion*, never the final word.

## 2. End-to-end flow

```
[Access .mdb/.accdb]
        |
        v
[Scanner] --reads schema + samples values--> writes best-guess JSON
        |
        v
[classification.json]  <-- the single contract for everything downstream
        |
        v
[HTML Editor] --human confirms/overrides categories, flags preserve-IDs--> writes JSON back
        |
        v
[classification.json (reviewed)]
        |
        +--> [Test-data generator]  -> sanitized records
        |
        +--> [Migration generator]  -> SQL Server schema + data-load scripts
```

The JSON schema is the keystone. It is drafted and pressure-tested against both downstream uses
*before* any scanner code is written.

## 3. Classification approach

### 3.1 Lookup / reference table detection

**Primary signal — non-key, non-system column count.**
For each table, count its columns *minus* foreign-key columns *minus* system/audit columns:

- **1 remaining column** → very likely a lookup table (the classic ID + Description shape).
- **2–3 remaining columns** → still quite possibly a lookup table, *unless* column names or
  values suggest otherwise.
- **More than that** → probably not a lookup table.

**Supporting signals (recorded, lightly weighted):**

- **Relative cardinality** — a column's distinct-value count relative to the largest cardinality
  observed across all columns in the database.
- **Uniqueness ratio** — distinct values ÷ row count, computed *within the column's own table*.
  Low ratio → categorical/code (leans lookup/low-risk); near 1.0 → identifier-like (leans PII or
  natural key). Better per-table discriminator than the global relative cardinality.
- **Row count** — lookup tables tend to be small in absolute terms.

**Recorded but NOT weighted — "referenced-by" count.**
The number of foreign keys across the database that target this table's primary key is captured as
*informational context for the human reviewer only*. It is deliberately excluded from the automated
guess because it fails in both directions:

- *Not necessary:* legitimate lookup tables frequently serve only one parent table (common in
  government schemas).
- *Not sufficient:* high-fan-in entity tables such as `Person` (referenced by `Client` and
  `Practitioner`) or `Address` (referenced by many tables) are PII-dense, not reference data.
  Weighting referenced-by would misclassify the most sensitive tables as "safe to preserve" — the
  worst-direction error for this use case.

### 3.2 System / audit column detection

A column is treated as a system/audit column when **both** are true:

1. Its name matches a configurable list/regex (e.g., `LastModifiedBy`, `LastModifiedOn`,
   `CreatedBy`, `CreatedOn`, `UserId`, `Timestamp`, `RowVersion`, ...), **and**
2. It appears across multiple tables.

Requiring both conditions avoids stripping a genuinely meaningful column (e.g., `UserId` in a table
that is actually *about* users). The list is configurable.

### 3.3 PII detection (name + value)

Two layers, both seeded from an adapted dictionary (see 3.4):

- **Name match (primary preliminary signal):** match column names against PII vocabulary
  (`Last`, `First`, `SSN`, `SS`, `SS#`, `Soc`, `MedId`, `DLNo`, `LicNo`, `DOB`, `Email`, `Phone`,
  `Addr`, ...).
- **Value-sampling regex (secondary signal):** sample column values and test them against PII
  patterns. Catches cryptically named or mislabeled columns — important in Access, where legacy
  column names are often terse or misleading.

**PII veto rule (important).**
The PII signal **overrides** the lookup classification. A thin table whose columns match PII name or
value patterns (e.g., `Person` = FirstName, LastName, DOB, SSN) is an **entity table, not reference
data**, no matter how few columns it has. This protects against thin PII tables being swept up by the
"2–3 columns = maybe a lookup" rule.

### 3.4 Adapting an existing PII dictionary

Seed the name/value dictionary from **Microsoft Presidio's predefined recognizers** (open source).
Each recognizer bundles a regex pattern *and* a set of context words (e.g., the SSN recognizer
carries "ssn", "social security"). Both the patterns and the context-word lists translate almost
directly into the JSON config and already cover SSN, US driver's license, credit cards, phone, and
email. We do **not** run Presidio itself — we lift its patterns/vocabularies as a seed and extend
with domain terms (MedId, DLNo, LicNo, Soc, SS#, etc.).

### 3.5 Category taxonomy

The category a human selects maps directly to a masking strategy *and* a migration behavior, so it
is an instruction, not just a label:

| Category                 | Test-data behavior            | Migration behavior                       |
|--------------------------|-------------------------------|------------------------------------------|
| Direct identifier / PII  | Replace with mock data        | Migrate masked value                     |
| Quasi-identifier         | Generalize (e.g., birth year) | Migrate generalized value                |
| Sensitive non-PII        | Replace / redact              | Migrate masked value                     |
| Free-text / memo         | Scrub or regenerate (high risk of embedded PII) | Migrate scrubbed value |
| Lookup / reference        | Preserve                      | Preserve                                 |
| System / audit            | Preserve or set deterministically | Preserve / regenerate               |
| Foreign key               | Preserve or remap in lockstep | Preserve / remap in lockstep             |
| Surrogate autonumber PK   | Preserve-if-flagged or renumber | Preserve-if-flagged or renumber        |
| Business / natural key    | Usually preserve              | Preserve (often the "filing" identifier) |
| Generic / non-sensitive   | Keep                          | Keep                                     |

## 4. Privacy rule for the JSON artifact

The JSON is passed around to developers and business users, so it must **never contain raw sampled
values**. Store only derived signals: counts, regex-match booleans, and at most a *format pattern*
(e.g., `###-##-####`) or a hashed exemplar — never a live value. Otherwise the artifact recreates the
exact exposure the project exists to remove.

## 5. ID and referential-integrity handling

- An ID is either **preserved** or **remapped consistently everywhere it appears** (PK + every FK
  that references it, in lockstep). A foreign-key column is **never** independently faked — that
  orphans rows.
- **Two independent reasons to preserve an autonumber:**
  1. *Referential integrity (mandatory)* — auto-detected whenever a PK is FK-referenced.
  2. *Human-facing significance (manual flag)* — autonumber IDs used for filing or for
     communicating about a case. The editor lets a reviewer set this flag explicitly.
- On SQL Server, preserving autonumber values means `SET IDENTITY_INSERT ON` during load, or
  declaring the column as a plain `INT` when it is purely historical.

## 6. JSON schema (the contract) — draft shape

```jsonc
{
  "database": "CaseManagement.accdb",
  "scannedUtc": "2026-05-28T00:00:00Z",
  "config": { "systemColumnPatterns": ["LastModified*", "CreatedBy", "UserId", "Timestamp"] },
  "tables": [
    {
      "name": "Person",
      "rowCount": 12450,
      "lookupGuess": false,
      "nonKeyNonSystemColumnCount": 4,
      "referencedByFkCount": 2,            // informational only
      "guessedCategoryHint": "Entity (PII-bearing)",
      "confidence": 0.82,
      "firedHeuristics": ["pii-name-veto", "uniqueness-high"],
      "humanOverride": { "isLookup": false, "reviewedBy": null, "notes": "" },
      "columns": [
        {
          "name": "PersonID",
          "dataType": "Long",
          "isAutoNumber": true,
          "isPrimaryKey": true,
          "isForeignKey": false,
          "fkTarget": null,
          "cardinality": 12450,
          "uniquenessRatio": 1.0,
          "relativeCardinality": 1.0,
          "nullCount": 0,
          "piiNameMatch": false,
          "piiValueMatch": false,
          "valueFormatPattern": null,       // never raw values
          "guessedCategory": "Surrogate autonumber PK",
          "confidence": 0.95,
          "firedHeuristics": ["autonumber-pk"],
          "humanOverride": {
            "category": null,
            "preserveAutonumber": false,    // human sets true if filing-significant
            "maskingStrategy": null,
            "notes": ""
          }
        }
        // ... more columns
      ]
    }
    // ... more tables
  ]
}
```

The schema must be validated against both generators before scanner work begins.

## 7. HTML editor

Modeled on the existing JSON-driven UAT Checklist Editor:

- Loads `classification.json` from the file system.
- Presents a hierarchical tree of tables → columns.
- Option buttons for the category taxonomy per column.
- A checkbox to flag **preserve autonumber ID** (filing/case significance).
- Shows the fired heuristics, confidence, and informational referenced-by count next to each guess
  so reviewers understand *why* something was flagged.
- Saves the modified JSON back to the file system.

## 8. Access-specific gotchas

- **No `COUNT(DISTINCT col)` in Jet/ACE SQL.** Cardinality must use a subquery:
  `SELECT COUNT(*) FROM (SELECT DISTINCT col FROM tbl)`.
- **ACE OLE DB provider is bitness-locked** — provider bitness must match the process (32 vs 64).
- **Use DAO (interop) for two things OLE DB handles poorly:** the AutoNumber attribute
  (`dbAutoIncrField`) and relationships. Use `GetOleDbSchemaTable` for TABLES, COLUMNS,
  PRIMARY_KEYS, FOREIGN_KEYS.
- **Access "Lookup" column property often encodes an implicit FK** (a combo-box whose row source is
  another table) that was never declared as a formal relationship. Reading these recovers FK
  structure in messy databases and improves both FK detection and the column-count heuristic.
- **Memo (long text), OLE Object, and multi-value fields need special handling.** Memo = highest
  free-text PII risk; OLE Object and multi-value fields do not migrate cleanly to SQL Server — flag
  for human attention.
- **Exclude system tables** (`MSys*`); consider flagging linked tables.
- **Migration mechanics are partly solved** by Microsoft's SQL Server Migration Assistant (SSMA)
  for Access. Use it as a reference for type mapping (Text→nvarchar, Memo→nvarchar(max),
  Number/Long→int, Double→float, Currency→money/decimal, Date/Time→datetime2, Yes/No→bit,
  AutoNumber→int IDENTITY, OLE Object→varbinary(max)). Our value-add is the classification +
  sanitization layer SSMA lacks. (Verify SSMA's current version.)

## 9. Open questions

1. **Sanitized test records:** mask the existing rows in place (simpler, preserves the relational
   graph) or generate fresh synthetic rows (needs full FK-graph construction)?
2. **Target files:** `.mdb` (Jet 4.0) or `.accdb` (ACE), or both?
3. Confirm the exact category taxonomy and the editor's option-button labels with stakeholders.

## 10. Phased build plan

1. **Phase 0 — Contract.** Finalize the JSON schema; validate it against both generators on paper.
2. **Phase 1 — Scanner.** Read schema (OLE DB + DAO), compute structural signals, apply lookup +
   PII heuristics with the veto rule, sample values for patterns (never store raw values), emit JSON.
3. **Phase 2 — Editor.** Build the HTML reader/writer with category option buttons and the
   preserve-autonumber flag.
4. **Phase 3 — Test-data generator.** Consume reviewed JSON; mask/preserve per category and ID rules.
5. **Phase 4 — Migration generator.** Emit SQL Server schema + data-load scripts honoring
   preserve/remap and `IDENTITY_INSERT` handling.
