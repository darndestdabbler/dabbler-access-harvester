# F-3-4 Item 3 (DefaultMemberRegistry) — design consensus

Cross-provider decision input (GPT-5.4 + Gemini Pro). Raw, unedited.


---

## gpt-5-4 (gpt-5-4) — $0.0749

**1. Pick:** **Option B**

Option B is the right trade-off: it fully implements F-3-4 and fixes the real semantic gap in the ~129 `Recordset!FieldName` cases, without breaking the deliberately closed `ResolutionOutcome` surface or forcing churn through every consumer. The fact we can honestly resolve here is “this access binds through a curated default member on a known source type,” and a synthetic declaration carried by the existing `ResolvedToObjectModel` variant expresses that well enough. That keeps the golden deltas deterministic and explainable, while avoiding the unnecessary blast radius of a new DU case and schema kind. Option C leaves a spec-called-for, high-value correctness improvement on the table.

**2. JSON for `rsTarget!Notes`**

Emit it as resolved, but target the **default member**, not a fictitious statically-known field named `Notes`.

- `ResolutionStatus`: `Resolved`
- `ReferenceKind`: `ObjectModel`
- `Confidence`: `High` or `Deterministic` **if** the schema already has confidence; otherwise don’t add one just for this
- `Target`: synthetic declaration for `DAO.Recordset.Fields`, with explicit provenance and the bang name carried separately

Shape should be conceptually like:

```json
{
  "resolutionStatus": "Resolved",
  "referenceKind": "ObjectModel",
  "confidence": "High",
  "target": {
    "declarationKind": "SyntheticDefaultMember",
    "provenance": "DefaultMemberRegistry",
    "containingType": "DAO.Recordset",
    "memberName": "Fields",
    "isDefaultMember": true,
    "isParameterized": true,
    "returnType": "DAO.Field",
    "accessedName": "Notes"
  }
}
```

Key point: **do not** emit a target like `DAO.Field.Notes`, and **do not** claim the field’s value type. The only honest static claim is that `rsTarget!Notes` binds via `DAO.Recordset`’s default member `Fields("Notes")`, returning a `DAO.Field`.

**3. Biggest correctness risk + hermetic test**

Biggest risk: **over-claiming `Notes` as a real resolved symbol/member**, which would create fake field declarations/references in SymbolDb and cross-ref back-population.

Guard it with one hermetic end-to-end test using an arbitrary field name that cannot be validated statically, e.g.:

```vb
Dim rs As DAO.Recordset
x = rs!TypoedName
```

Assert all of these:

- outcome is `ResolvedToObjectModel`
- target is the synthetic default-member declaration for `DAO.Recordset.Fields`
- `accessedName == "TypoedName"`
- no declaration/symbol/xref target named `TypoedName` is created
- no claim is made about the field value type

That proves the resolver is binding the **indirection** correctly without inventing schema facts.

**4. Should `isBang` gate consultation?**

**Yes — gate this fallback on `isBang` only.**

- `rs!Notes` → consult `DefaultMemberRegistry`; for `DAO.Recordset`, rewrite semantically to default-member access `Fields("Notes")`
- `rs.Fields` → **do not** use the default-member fallback; resolve `Fields` through normal member lookup on `DAO.Recordset`
- `rs.Notes` → stays unresolved unless `Notes` is an actual member on the type

So: **bang gets default-member fallback; dot does not.** `rs.Fields` is a direct named member access, while `rs!Notes` is the default-member/keyed-access case this registry is meant to cover.


---

## gemini-pro (gemini-pro) — $0.0092

**1. Recommendation**

I recommend **Option B**. It delivers the required semantic resolution by creating a synthetic `Declaration` for the default member and emitting it via the existing `ResolvedToObjectModel` variant. This approach is lean, avoids a disruptive breaking change to the closed `ResolutionOutcome` type, and correctly reuses existing consumer machinery that already handles object-model resolutions. It treats the default member as part of the object's contract, which is a semantically honest and pragmatic classification that respects the project's established architecture.

**2. JSON Projection for `rsTarget!Notes`**

The emitted JSON should convey that the resolution mechanism was via a default member without over-claiming knowledge of the database schema.

*   **ResolutionStatus:** `"Resolved"`
*   **ReferenceKind:** `"ObjectModel"`
*   **Target:** A synthetic object representing the default member itself (not the accessed field).
    ```json
    "Target": {
      "Name": "Fields",
      "Kind": "Property",
      "ContainingType": "DAO.Recordset",
      "ReturnType": "DAO.Field",
      "IsSynthetic": true,
      "IsParameterized": true
    }
    ```
*   **ResolutionDetail:** An additional property should be added to the reference object to capture the specifics of this resolution path.
    ```json
    "ResolutionDetail": {
      "Mechanism": "DefaultMemberAccess",
      "AccessedAs": "!Notes"
    }
    ```
*   **Confidence:** High. The confidence is in the resolution *mechanism* (that `rs!Notes` resolves through `rs.Fields`), not in whether a field named "Notes" exists at runtime. This distinction is crucial for the AI consumer.

**3. Biggest Correctness Risk and Test**

The single biggest risk is that downstream consumers of `ResolutionOutcome`—specifically the cross-reference back-population—might make incorrect assumptions about the `Declaration` payload inside the `ResolvedToObjectModel` variant. They might not expect a synthetic, on-the-fly declaration and could fail if they assume, for example, that every declaration has a source location or a complete type-library-backed definition.

To guard against this, create a hermetic test that:
1.  Parses a minimal VBA module containing only `Sub Test(): Dim rs As DAO.Recordset: Debug.Print rs!SomeField: End Sub`.
2.  Asserts that the resolver produces a `ResolvedToObjectModel` outcome whose payload is the synthetic `Declaration` for the `Fields` property.
3.  Runs the full pipeline, including the SymbolDb extractor and cross-reference back-population, on this single resolution.
4.  Asserts that the final cross-reference index contains a valid entry linking the use of `!SomeField` to the synthetic `DAO.Recordset.Fields` declaration without exceptions or data corruption.

**4. `isBang` vs. Dot Access**

Consultation of the `DefaultMemberRegistry` **must be gated by `isBang`**.

In VBA, the bang (`!`) operator is the specific syntax for invoking a type's default member with a parameter. The expression `rs!Notes` is syntactic sugar for `rs.Fields("Notes")`. In contrast, a dot (`.`) is for direct member access. `rs.Fields` is a valid access to the `Fields` collection property itself, whereas `rs.Notes` is a compile-time error on a `DAO.Recordset` object. Applying default-member logic to dot-accesses would be semantically incorrect and would misinterpret valid code.


---

Total routed cost: $0.0841
