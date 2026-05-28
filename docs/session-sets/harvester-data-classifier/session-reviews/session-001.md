# Verification Round 1

1. **Category coverage.**  
The schema does enumerate all 10 plan § 3.5 column categories, and it does so with stable code identifiers suitable for a long-lived contract. `definitions.ColumnCategory.enum` contains exactly:

- `direct-identifier-pii`
- `quasi-identifier`
- `sensitive-non-pii`
- `free-text-memo`
- `lookup-reference`
- `system-audit`
- `foreign-key`
- `surrogate-autonumber-pk`
- `business-natural-key`
- `generic-non-sensitive`

Those are stable slugs, not UI labels, which is the right choice for downstream compatibility. The default masking strategies are documented per category in the `ColumnCategory.description`, and the available strategies themselves are stably enumerated in `definitions.MaskingStrategy.enum`. The human-override path is also present and clear: per-column override of category via `ColumnHumanOverride.category`, per-column override of masking via `ColumnHumanOverride.maskingStrategy`, and per-table lookup override via `TableHumanOverride.isLookup`. So on raw coverage, this passes.

The only caveat is that the category→default-masking mapping is documented in schema prose, not encoded as machine-readable data in the instance. That is workable for versioned consumers, but it means Session 3 and Session 4 must carry contract knowledge in code rather than deriving it from the saved JSON alone.

2. **Privacy invariant (plan § 4).**  
The fixture itself is clean: it contains no raw sample values, and every `valueFormatPattern` shown is pattern-like or null. The schema also does several good things structurally: root-level `additionalProperties: false` is present, and `additionalProperties: false` is also applied to `ScanConfig`, `Table`, `TableHumanOverride`, `Column`, `ColumnHumanOverride`, and `FkTarget`. That prevents ad hoc fields like `sampleValues`, `rawValues`, `exampleValues`, or `rawSample` from quietly appearing and still validating.

However, the schema does **not** fully enforce the privacy invariant. The weak point is `Column.valueFormatPattern`: it is typed as `["string","null"]` with no pattern restriction. A raw sampled value like `"123-45-6789"` would validate just as easily as a safe pattern like `"###-##-####"`. So `additionalProperties: false` plus the current `valueFormatPattern` definition does **not** structurally guarantee that raw data cannot leak.

There is a second leakage path: `TableHumanOverride.notes` and `ColumnHumanOverride.notes` are unconstrained free-form strings. The plan requires notes, so the field is understandable, but from a schema-enforcement perspective a reviewer could paste a live SSN, phone number, or full name into notes and the document would still validate. That means the privacy rule is presently enforced by discipline and downstream convention, not by the contract itself.

So this concern does **not** pass as a strict closeout-grade invariant. The fixture is good; the schema is not sufficiently airtight.

3. **FK and autonumber preservation (plan § 5).**  
For the specific single-column relationships shown in the fixture, the contract is mostly usable by Session 4. The sanitizer can identify:

- surrogate autonumber PKs via `isAutoNumber: true` + `isPrimaryKey: true` + category `surrogate-autonumber-pk`
- human filing-significance via `humanOverride.preserveAutonumber`
- FK edges via `isForeignKey: true` + `fkTarget.table` + `fkTarget.column`

Using that, it can see that `Visit.PersonID` points to `Person.PersonID` and `Visit.StatusID` points to `VisitStatus.StatusID`, and it can therefore preserve or remap those FK values in lockstep with the referenced PK.

That said, there are two contract problems here.

First, `FkTarget` is only `{ table, column }`. That is enough for the fixture’s simple one-column FKs, but it is not complete for composite relationships. There is no relationship ID, no ordinal, and no grouping construct for “these two source columns together reference these two target columns.” For a long-lived Access contract, that is under-specified.

Second, the schema text creates an authority ambiguity. `ColumnHumanOverride` says the sanitizer reads effective values as `(humanOverride.X ?? AI X)` for category and masking strategy, and `Table.tableHint` says per-column category is authoritative. But plan § 5 says a foreign key must never be faked independently. Right now a human could set an FK column’s override category to `direct-identifier-pii` with masking `mock-replace`, and the JSON would still validate. The structural FK metadata exists, but the contract does not explicitly say that `isForeignKey`/`fkTarget` override any incompatible category or masking choice. For the fixture as written, no conflict occurs; for the contract, this is a real gap.

On autonumber significance specifically, the schema does expose the manual flag cleanly via `humanOverride.preserveAutonumber`, so that part is covered.

4. **HumanOverride block coverage.**  
This passes. The schema exposes every editor control called for by the spec:

- per-column 10-category selector: `ColumnHumanOverride.category`
- per-column preserve-autonumber checkbox: `ColumnHumanOverride.preserveAutonumber`
- per-column masking-strategy selector: `ColumnHumanOverride.maskingStrategy`
- per-table lookup override: `TableHumanOverride.isLookup`
- reviewer name: `reviewedBy` on both table and column overrides
- free-form notes: `notes` on both table and column overrides

The table override is tri-state (`boolean | null`), which is actually better than a plain checkbox because it distinguishes “no override” from an explicit yes/no override. The only practical caution is UI design: a literal checkbox alone cannot represent null without extra UX treatment, but that is an editor implementation issue, not a schema coverage gap.

5. **Editor-on-paper validation.**  
For the `Person` table, the editor has enough data to render what the spec explicitly requires.

At the **table level**, it has:
- `name`: `Person`
- AI lookup guess: `lookupGuess: false`
- table shape hint: `tableHint: "entity-pii-bearing"`
- confidence: `0.92`
- fired heuristics: `["pii-name-match", "pii-veto", "uniqueness-high-on-surrogate-pk"]`
- informational `referencedByFkCount: 2`
- table override controls via `humanOverride.isLookup`, `reviewedBy`, `notes`

At the **column row** level:

- **PersonID**: shows AI guess `surrogate-autonumber-pk`, confidence `0.98`, heuristics `autonumber-pk` and `uniqueness-high`, plus override controls with `preserveAutonumber: true` and reviewer notes.
- **FirstName**: shows direct-identifier PII guess, confidence `0.95`, heuristic `pii-name-match`, and blank override controls.
- **LastName**: same shape as `FirstName`.
- **DOB**: shows quasi-identifier guess, confidence `0.88`, heuristics, and explicit masking override `generalize` with reviewer note.
- **SSN**: shows direct-identifier PII guess, confidence `0.99`, both name and value evidence, and safe `valueFormatPattern: "###-##-####"`.
- **MedRecordNo**: shows AI guess `sensitive-non-pii`, confidence `0.78`, then human override to `business-natural-key` with masking `preserve`.
- **Notes**: shows `free-text-memo`, confidence `0.9`, heuristic `memo-free-text`.

So for the required display items—fired heuristics, confidence, AI guess, referenced-by count, and override controls—the contract is sufficient.

What the editor would **not** get from the instance alone is a machine-readable default masking strategy for a category when `humanOverride.maskingStrategy` is null. It can still work by hard-coding the versioned contract mapping, but that mapping is not carried in the saved JSON document.

6. **Sanitizer-on-paper validation.**  
Using the fixture alone plus knowledge of schema version 1.0 semantics, Session 4 can process the concrete tables as follows.

**Person table**
- `PersonID`: effective category is `surrogate-autonumber-pk`; `preserveAutonumber` is `true`, so preserve the value. Because `Visit.PersonID` references it, the FK side must remain aligned; since the PK is preserved here, the FK values remain unchanged.
- `FirstName`: effective category `direct-identifier-pii`; no masking override, so use category default mock replacement.
- `LastName`: same as `FirstName`.
- `DOB`: effective category `quasi-identifier`; explicit masking override `generalize`, so generalize to birth year or similar.
- `SSN`: effective category `direct-identifier-pii`; no masking override, so mock replace.
- `MedRecordNo`: human override changes effective category to `business-natural-key` and masking to `preserve`; preserve as instructed.
- `Notes`: effective category `free-text-memo`; no masking override, so use scrub-free-text.

**VisitStatus table**
- `StatusID`: effective category `surrogate-autonumber-pk`; `preserveAutonumber` is `true`, so preserve. Since `Visit.StatusID` references it, FK values must also remain unchanged.
- `Description`: effective category `lookup-reference`; preserve.

**Visit table**
- `VisitID`: effective category `surrogate-autonumber-pk`; `preserveAutonumber` is `false`, so default behavior is remap-id.
- `PersonID`: effective category `foreign-key`; it targets `Person.PersonID`. Because `PersonID` in `Person` is preserved in this fixture, `Visit.PersonID` must also be preserved exactly. If the parent had been remapped instead, this FK would have to follow that remap in lockstep.
- `StatusID`: effective category `foreign-key`; it targets `VisitStatus.StatusID`. Since the target PK is preserved here, preserve the FK as-is.
- `MoneyDue`: effective category `generic-non-sensitive`; preserve.
- `LastModifiedBy`: effective category `system-audit`; no override, so use the category default, which the schema documentation says is `regenerate`.
- `LastModifiedOn`: same as `LastModifiedBy`.

For the fixture’s concrete FK pairs—`Visit.PersonID -> Person.PersonID` and `Visit.StatusID -> VisitStatus.StatusID`—the lockstep invariant is achievable from the JSON.

But I would not call this fully closeout-safe because of the same two problems noted above:
- the contract does not fully model composite relationships, and
- the contract text is ambiguous about whether a human override can supersede FK structural truth.

Also, strictly speaking, effective masking strategy is not derivable from the instance alone whenever `humanOverride.maskingStrategy` is null; it depends on the versioned category default mapping defined by the contract, not stored in the artifact.

7. **Access-specific gotchas (plan § 8).**  
These are structurally covered.

- Memo / Long Text: `isMemo`
- OLE Object: `isOleObject`
- multi-value field: `isMultiValue`
- linked-table flag: `Table.isLinkedTable`
- system-table exclusion (`MSys*`): expressed in the root/table description as exclusion by omission
- DAO AutoNumber attribute: `isAutoNumber` with description explicitly referencing DAO `dbAutoIncrField`

The schema also acknowledges Access-specific FK recovery from combo-box `RowSource` in the `isForeignKey` description, and it records the cardinality-query workaround in `cardinality.description`. So this concern passes.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "AccessHarvester.Pipeline/Classification/classification-schema.json",
      "summary": "The privacy invariant is not structurally enforced because `valueFormatPattern` and free-form `notes` fields accept arbitrary strings, including raw sampled values.",
      "fix": "Constrain `valueFormatPattern` to a tokenized pattern alphabet or a structured pattern object, and add schema/lint rules or a separate non-exported channel for reviewer notes so raw values cannot validate in the shared artifact."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Classification/classification-schema.json",
      "summary": "Foreign-key modeling is only complete for single-column relationships because `fkTarget` lacks relationship identity and ordered multi-column grouping.",
      "fix": "Add an explicit relationship model with stable relationship IDs and ordered source/target column arrays, or formally declare single-column FK-only support and make Session 2 reject composite PK/FK schemas."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Classification/classification-schema.json",
      "summary": "The contract is ambiguous about FK authority because human category/masking overrides can contradict `isForeignKey`/`fkTarget`, risking independent masking of foreign keys.",
      "fix": "Specify and enforce that structural FK metadata overrides incompatible category or masking choices, or restrict FK-column overrides with Draft-07 `if`/`then` rules so FK columns cannot validate with non-FK masking semantics."
    }
  ]
}
```

---

# Verification Round 2

**R1.1 (Critical) — privacy invariant not structurally enforced**

The `valueFormatPattern` change is a real improvement, but it does **not** fully close the structural leak path identified in R1. It now correctly rejects the most obvious raw-value cases the original finding called out: digit-bearing literals like `"123-45-6789"` and lowercase text like `"John Smith"` no longer validate. That is a meaningful tightening.

However, the new regex still accepts a broad class of plausible **raw** values: any all-uppercase string composed only of letters and the permitted punctuation. Examples like `"JOHN SMITH"`, `"BOSTON"`, `"PO BOX"`, `"MRN-ABC"`, `"CA"`, or `"ER"` would validate. Those are not theoretical edge cases; uppercase-only names, addresses, codes, initials, and abbreviations are common in Access data. Structurally, the schema still cannot distinguish “canonical pattern” from “raw uppercase literal” because the allowed alphabet intentionally includes unrestricted uppercase letters for literal labels. So the fix closes the digit/lowercase path, but not the broader raw-literal path.

I also do **not** agree that leaving `notes` free-form is acceptable for a closeout-grade artifact **while the schema description still claims a file-level privacy invariant**. The orchestrator’s rationale is operationally understandable: no schema rule can prevent a determined reviewer from paraphrasing or pasting sensitive content. But that argument cuts the other way for contract closeout: if the artifact intentionally includes unconstrained free text, then the schema should not claim the artifact “never contains raw sampled values” as a structural invariant. Right now the contract still has an explicit escape hatch for arbitrary content in `TableHumanOverride.notes` and `ColumnHumanOverride.notes`.

So my conclusion on R1.1 is: **partially fixed, not fully closed**. The regex is directionally good, but it is not strong enough to make `valueFormatPattern` unambiguously non-literal, and the decision to keep `notes` free-form means the file-level privacy invariant remains unenforced at the schema level. For closeout, either:
1. the pattern encoding needs to become unambiguous and non-literal, and notes need to move out of the shared artifact, **or**
2. the invariant needs to be narrowed explicitly to scanner-generated fields only, with notes governed by policy/lint rather than by claiming a whole-file structural guarantee.

---

**R1.2 (Major) — FK modeling only complete for single-column relationships**

This fix is structurally adequate for a v1.0 contract.

The original problem was not merely lack of composite support; it was that the schema looked more complete than it really was. The updated schema now makes the scope explicit in two places: the root description and `definitions.FkTarget.description` both state that **v1.0 supports single-column foreign keys only**. That is the key contract correction. It turns an implicit limitation into an explicit versioned boundary.

Given the stated constraint that the planning document does **not** require composite-FK support in v1.0, “declare scope clearly and defer composite modeling to v1.1” is a reasonable resolution. This is especially true for a keystone contract: explicit incompleteness is better than a misleading pseudo-general shape.

The guidance to Session 2 is also specific enough for v1.0 purposes. The schema now says composite FKs are not representable and that the scanner **SHOULD** emit a degradation note when one is encountered. That tells implementers and consumers the correct behavior: do not try to pretend composite relationships are faithfully encoded in this version. I would have preferred a persisted diagnostics channel if the product needs machine-visible degradation reporting, but that is an enhancement, not a blocker for this specific closeout. For the scope stated, this is adequate.

So R1.2 is **verified**: the contract no longer overpromises composite-FK support, and the v1.0 limitation is documented clearly enough for Session 2 and downstream readers.

---

**R1.3 (Major) — FK authority ambiguity (override could contradict FK)**

This fix is structurally adequate.

The `Column` schema now has an `allOf` with an `if`/`then` branch keyed on `isForeignKey: true`. Because `Column.required` already includes both `isForeignKey` and `humanOverride`, that conditional will evaluate on every column instance, and it will fire on every FK column. In draft-07, the added `then` constraints compose cleanly with the base `humanOverride` `$ref`, so the override object is still the normal `ColumnHumanOverride` shape, just further restricted in the FK case.

The constrained values are also the right ones:
- `humanOverride.category`: `null` or `"foreign-key"`
- `humanOverride.maskingStrategy`: `null`, `"preserve"`, or `"remap-id"`

That is the correct set for plan § 5 semantics. I do **not** think `regenerate` should be allowed on FK columns. If a referenced key is rewritten in a relationship-safe way, that is exactly the `remap-id` case. Allowing `regenerate` on an FK would reintroduce ambiguity about whether the FK may be rewritten independently, which is what this fix is meant to prevent.

I do not see a regression in the non-FK case. When `isForeignKey` is `false`, the `if` condition does not match, so the `then` restrictions do not apply. Non-FK columns retain the full `ColumnHumanOverride` freedom they had before.

So R1.3 is **verified**: the authority ambiguity is now structurally blocked for FK columns, and the fix does not overconstrain non-FK columns.

---

**Additional audit across the R1 concern set**

1. **Category coverage**  
No regression. `ColumnCategory.enum` still contains all 10 plan categories, unchanged and stable.

2. **Privacy invariant**  
Improved but still not closeout-grade for the reasons above. This is the only remaining substantive blocker I found.

3. **FK and autonumber preservation**  
No regression. For the fixture’s single-column FKs, Session 4 can still preserve/remap in lockstep using `isForeignKey` + `fkTarget`, and the FK override restriction now makes that flow safer. The explicit v1.0 single-column scope resolves the earlier overclaim.

4. **HumanOverride coverage**  
No regression. All required editor controls remain present. The FK-specific override restriction is targeted and does not reduce needed coverage outside FK columns.

5. **Editor-on-paper**  
No regression. The fixture still carries enough information for the editor to render AI guess, confidence, heuristics, lookup context, and override controls. Nothing in the fixes removes needed editor data.

6. **Sanitizer-on-paper**  
No regression. The fixture still supports the expected sanitize decisions, and the FK authority fix materially improves safety by preventing contradictory FK overrides.

7. **Access-specific gotchas**  
No regression. `isMemo`, `isOleObject`, `isMultiValue`, `isLinkedTable`, and DAO-backed `isAutoNumber` handling are all still represented.

Overall: **R1.2 and R1.3 are structurally adequate; R1.1 is not fully closed.** I did not find any new regressions introduced by the FK-related fixes. The remaining problem is the still-incomplete structural privacy guarantee.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "AccessHarvester.Pipeline/Classification/classification-schema.json",
      "summary": "The privacy invariant is still not structurally enforced because `valueFormatPattern` accepts plausible raw all-uppercase literals and reviewer `notes` remain unrestricted free text in the same artifact.",
      "fix": "Use an unambiguous non-literal pattern encoding for `valueFormatPattern` (and require a real pattern, not an arbitrary uppercase string), and either move reviewer notes out of the shared classification artifact or narrow the privacy invariant to scanner-generated fields only and enforce notes via explicit lint/policy."
    }
  ]
}
```

---

# Verification Round 3

R3 is **not fully verified yet**. The privacy-scope clarification is good, but the `valueFormatPattern` structural defense is still not airtight for the scanner emit path.

On audit item 1: the new two-rule approach closes several important leak paths, but it still has a fundamental ambiguity. Rule 1 allows uppercase letters as literals. Rule 2 requires at least one placeholder token via `[ # X A ? ]`. The problem is that `A` and `X` are doing double duty: they are both allowed literal letters and counted as “placeholder tokens.” That means a raw all-uppercase value containing `A` or `X` will still validate even when it is not a pattern.

A realistic example is a raw name such as **`"JANE DOE"`**:

- Rule 1: passes — only uppercase letters and a space
- Rule 2: passes — contains `A`

Likewise **`"ALEX SMITH"`**, **`"ADAMS"`**, **`"MAX"`**, etc. These are realistic Access raw values, especially in databases that store names in uppercase. So the scanner could still accidentally persist a verbatim sampled name and have it validate as a supposed “format pattern.” That means the scanner-path privacy guarantee is still not structurally complete.

For the other examples you asked about:
- real SSN: blocked, because digits are not allowed
- real ZIP: blocked, because digits are not allowed
- real phone: blocked, because digits are not allowed
- real date in normal numeric form: blocked, because digits are not allowed
- real name in uppercase with `A` or `X`: **still slips through**

That is enough to keep this as a **Critical** issue, because the unresolved leak is in the exact scanner-emitted field that is meant to carry only derived format information.

On audit item 2: the narrowed privacy-invariant scope is now honestly framed and consistent with plan §4. The plan language is about what the scanner stores: derived signals only, never live sampled values. It does not discuss reviewer-authored notes. The revised root description now correctly says the invariant applies to scanner-emitted fields, and the `notes` descriptions explicitly say notes are human-authored, free-form, and only governed by reviewer discipline/documentation. That is a truthful scope boundary. I do **not** see a remaining issue on the notes framing itself.

On audit item 3: I do not see any regression in the prior R1 areas. The changes are limited to prose plus the `valueFormatPattern` branch. They do not alter:
- the 10-category enum coverage
- FK/autonumber preservation semantics
- humanOverride field coverage
- editor-on-paper sufficiency
- sanitizer-on-paper sufficiency
- Access-specific flags and gotchas

So the only remaining blocker is the unresolved `valueFormatPattern` ambiguity. To actually close this, the pattern encoding needs to become unambiguous — specifically, bare letters like `A` and `X` cannot safely serve as placeholder tokens in the same flat string format that also permits arbitrary uppercase literals. Use non-letter placeholders only, or a delimited/structured token syntax, and add regression tests with uppercase literals like `JANE DOE` and `ALEX SMITH`.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "AccessHarvester.Pipeline/Classification/classification-schema.json",
      "summary": "`Column.valueFormatPattern` still accepts realistic raw uppercase literals because bare `A`/`X` satisfy the placeholder-token rule, so the scanner emit path is not yet structurally protected against verbatim name leakage.",
      "fix": "Make placeholder syntax unambiguous by removing bare alphabetic placeholder tokens from the flat string encoding (for example, use only non-letter placeholders or a delimited/structured token format), then add regression tests that must reject uppercase literals such as `JANE DOE`, `ALEX SMITH`, and similar raw values."
    }
  ]
}
```

---

# Verification Round 4

Analysis: the R4 change does remove the exact R3 hole. Bare alphabetic placeholders are gone, so raw uppercase values like `JANE DOE`, `ALPHA`, `MRN`, and similar no longer validate just because they contain `A` or `X`. That part is fixed.

But I cannot fully verify the scanner-path privacy guarantee yet, because the flat-string grammar still overlaps with some realistic raw values. Under the current rules, a string validates if it:
1. uses only `A-Z`, `#`, `?`, space, and the listed punctuation, and
2. contains at least one `#` or `?`.

That means a raw value with a literal `#` or `?` can still pass. A concrete realistic example is `APT #B` from an address/unit field. It matches `^[A-Z#? .,()+:/-]*$` and satisfies `[#?]`, so it would validate verbatim even though it is raw data, not a derived format pattern. Same class of problem: `UNIT #C`, `RM #A`, and dirty-data literals like `SMITH?`. So the answer to audit item 1 is **yes**: there is still at least one realistic raw value that can validate.

On audit item 2: `A-1` is correctly rejected because raw digits are still excluded, and the accepted `X-####` example is fine as an actual derived pattern. The remaining concerning edge cases are not numeric; they are raw uppercase literals that contain a literal `#` or `?`. That is the residual ambiguity now. In other words, collapsing the placeholder alphabet to non-letters fixed the `A`/`X` collision, but `#` and `?` are still characters that can appear in real stored values, especially address/unit fragments and uncertain/dirty text.

On audit item 3: the prior fixes do remain in place and unchanged:
- The **10-category enum** is still present in `definitions.ColumnCategory.enum`.
- The **FK `if`/`then` override constraint** is still present in `definitions.Column.allOf`, restricting FK-column overrides to `category = null|"foreign-key"` and `maskingStrategy = null|"preserve"|"remap-id"`.
- The **FkTarget single-column scope note** is still present in `definitions.FkTarget.description` and echoed in the root description.
- The **notes-field scope clarification** is still present: the root description explicitly narrows the privacy invariant to scanner-emitted fields, and both `TableHumanOverride.notes` and `ColumnHumanOverride.notes` still document that notes are free-form human input and the scanner never writes them.

So there is no regression on R1/R2/R3. The only remaining blocker is that `valueFormatPattern` is still a flat string whose token alphabet overlaps with plausible raw literals.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Critical",
      "location": "AccessHarvester.Pipeline/Classification/classification-schema.json",
      "summary": "`Column.valueFormatPattern` still accepts some realistic raw literals containing literal `#` or `?` (for example `APT #B`), so scanner-emitted verbatim values are not structurally excluded.",
      "fix": "Replace the flat string grammar with an unambiguous pattern representation that separates literals from placeholders, preferably a structured token array/object rather than a single string, and add regression tests that reject raw values such as `APT #B`, `UNIT #C`, and `SMITH?`."
    }
  ]
}
```
