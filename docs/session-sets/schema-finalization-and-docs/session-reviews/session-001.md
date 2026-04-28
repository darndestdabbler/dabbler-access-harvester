### 1. JSON Schema Draft 2020-12 validity

`harvester-schema.v1.0.json` is structurally consistent with Draft 2020-12 in the material shown. It declares the correct meta-schema URI, uses `$id`, `$defs`, and `$ref` in a valid 2020-12 style, and the root `$ref` to `#/$defs/DatabaseManifest` is allowed.

The visible problem is not dialect validity, but contract accuracy. `SchemaVersion` is documented as “Must be '1.0'” yet the schema only enforces a generic semantic-version-like regex. That means the schema is valid JSON Schema, but it does **not** enforce the versioning rule the ADR and inline description claim. Separately, `ParserOutput` and `NavigationGraph` are described as nullable in some cases, but the shown schema only allows the referenced object shapes, not `null`. That is also not a Draft 2020-12 violation, but it is a validation mismatch.

Conclusion: **Draft 2020-12 usage appears valid, but there are important schema-contract mismatches.**

### 2. D3/D4/D8/D9/D10/D13/D16/D20 gap-fix application

From the provided schema excerpt:

- **D20 (PascalCase normalization):** confirmed. The visible root and definitions use PascalCase consistently: `SchemaVersion`, `HarvesterVersion`, `Database`, `Summary`, `Forms`, `Reports`, `ModuleName`, `ModuleKind`, `TableSchema`, `TableField`, etc.
- **D16 (table/relationship types):** confirmed in the shown material. `TableSchema`, `TableField`, `TableIndex`, and `RelationshipDefinition` are referenced or defined, and `Relationships` is present on `DatabaseManifest`.

From the provided session summary and ADR text, the remaining fixes are stated as having been applied:

- **D3:** `DeclareFunction` / `DeclareSub`
- **D4:** `visibility` → `Visibility`
- **D8:** flat `StartLine`
- **D9:** `Lib` / `Alias`
- **D10:** `PassingMechanism`
- **D13:** `QueryTypeCode`, `Connect`, `ReturnsRecords`, `OdbcTimeout`

However, those specific definitions are not visible in the supplied schema excerpt, so they are **not independently verifiable from the pasted content alone**. The documentation is internally consistent with the claimed changes, but the excerpt does not provide direct proof for those six deltas.

Conclusion: **D20 and D16 are evidenced; D3/D4/D8/D9/D10/D13 are plausible and documented, but not fully verifiable from the provided snippet.**

### 3. `DatabaseManifest` coverage of full pipeline output

The manifest shape shown does cover the major pipeline domains expected for a full database export:

- **Forms:** `Forms`
- **Reports:** `Reports`
- **Queries:** `Queries`
- **Modules:** `Modules`
- **Tables:** `Tables`
- **Relationships:** `Relationships`
- **Validation rules:** `ValidationRules`
- **Row sources:** `RowSourceSpecs`
- **Function classifications:** `FunctionClassifications`
- **Event-handler classifications:** `EventHandlerClassifications`
- **Navigation graph:** `NavigationGraph`
- **Unresolved/manual-review queue:** `UnresolvedItems`
- **Diagnostics:** `Diagnostics`

It also includes database metadata (`Database`), versioning (`SchemaVersion`, `HarvesterVersion`), and summary metadata (`Summary`), which is appropriate for the Phase 7.1 manifest role.

The main defect here is not missing categories, but representation accuracy: `NavigationGraph` is documented as nullable when unavailable, and `FormExport.ParserOutput` is documented as nullable when a form/report has no code-behind, but the shown schema does not allow `null` for either property. That means the manifest shape is conceptually comprehensive, but some legitimate full-pipeline states would not validate.

Conclusion: **Coverage is broadly correct, but nullability for optional pipeline outputs is not properly encoded.**

### 4. ADR accuracy and `PredicateNode` known issue

The ADR excerpt aligns with the session summary on the major points:

- v0 → v1.0 promotion
- PascalCase normalization
- `DatabaseManifest` introduction
- incorporation of enrichment outputs
- deferred fixture migration
- continued v0 validity during transition

Those points are accurately reflected.

On the `PredicateNode` polymorphism issue specifically, the prompt states that the ADR flags it as a known issue. That claim is consistent with the session description, but the provided ADR excerpt is truncated before the known-issues section, so the exact wording cannot be independently checked from the pasted text.

Conclusion: **The ADR appears accurate on the visible changes; the `PredicateNode` known-issue callout is asserted in the session description but not directly confirmable from the excerpt shown.**

### 5. Back-compatibility plan reasonableness

The migration strategy is reasonable in principle:

- keep **v0** valid for current fixtures/consumers,
- introduce **v1.0** as a stable manifest schema,
- defer fixture migration and validator wiring to Session 2,
- use a schema-version discriminator to separate formats.

That is a standard staged rollout and avoids breaking existing test assets immediately.

The weakness is implementation detail: the current `SchemaVersion` definition does **not** act as a strong v1.0 discriminator because it accepts any version string matching the regex. If consumers are meant to route by schema version, the schema should enforce `1.0` exactly. Without that, the plan is conceptually sound but only partially encoded.

Conclusion: **The back-compatibility plan is sound, but the schema should make `SchemaVersion` an exact discriminator to support it cleanly.**

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/test-cases/harvester-schema.v1.0.json -> $defs.DatabaseManifest.properties.SchemaVersion",
      "summary": "SchemaVersion is documented as an exact v1.0 discriminator, but the schema accepts any semantic-version-like string.",
      "fix": "Replace the regex-only constraint with an exact discriminator such as `\"const\": \"1.0\"` or `\"enum\": [\"1.0\"]`."
    },
    {
      "severity": "Major",
      "location": "docs/test-cases/harvester-schema.v1.0.json -> $defs.FormExport.properties.ParserOutput; $defs.DatabaseManifest.properties.NavigationGraph",
      "summary": "Descriptions say these fields may be null, but the schema only allows the referenced object type, so valid no-code-behind or no-navigation cases would fail validation.",
      "fix": "Allow null explicitly, e.g. use `anyOf` with the referenced schema and `{ \"type\": \"null\" }` for both properties."
    }
  ]
}
```