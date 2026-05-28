1. **Two-level cache vs. JsonSchema.Net duplicate registration**

   The new cache shape is directionally correct for the failure mode you described. Both construction paths now converge on `GetOrCreate(schemaJson)`, which means path-based loads and text-based loads no longer independently call `JsonSchema.FromText` for the same schema. The `_pathCache` only deduplicates file reads; the real fix is `_schemaCache`, keyed by extracted `$id` or, if no `$id` exists, by a content hash. Using `Lazy<JsonSchema>` inside `ConcurrentDictionary` is also the right pattern here: even if `GetOrAdd` races and allocates more than one `Lazy`, only the retained `Lazy` instance has its `.Value` forced, so `JsonSchema.FromText` should execute once per retained key.

   That said, there is one concrete correctness issue in the implementation: `_schemaCache` uses `StringComparer.OrdinalIgnoreCase`. That is appropriate for Windows file paths, but not for JSON Schema `$id` values in general. `$id` is a URI, and URI comparison is not universally case-insensitive across the whole string. This means two distinct schemas whose `$id`s differ only by case in a path/query/fragment component could be incorrectly collapsed to one cache entry. For the current one-schema v1.0 use case, this is unlikely to bite immediately, but it is still a real bug in the general validator.

   There is also a secondary behavioral caveat: if two different schema bodies share the same `$id`, the first one silently wins and the second is ignored. That does avoid duplicate registration, but it also masks schema drift instead of surfacing it. Again, probably acceptable for the current embedded-schema scenario, but worth noting.

   **Bottom line:** the duplicate-registration problem is effectively addressed for the intended use case, but the `$id` cache comparer should not be case-insensitive.

2. **`RunValidate` exit codes 0 / 1 / 2 / 3**

   I cannot fully verify this concern from the supplied material.

   The provided tests clearly cover:
   - `0` for valid manifest input,
   - `1` for usage errors, missing files/directories, and non-JSON input,
   - `3` for schema-validation failures.

   What is missing is any evidence for `2` (“unexpected error”). The `Program.cs` implementation of `RunValidate` was not included, and none of the tests intentionally force an unexpected exception path such as:
   - embedded schema resource missing,
   - resource read failure,
   - permission/IO failure during recursive enumeration,
   - unexpected exception during validation.

   So while the contract is documented, and the common paths are covered, exit code `2` is not actually verified by the snippets and tests shown here.

   **Bottom line:** 0/1/3 are supported by evidence; 2 is not verified.

3. **Integration-test coverage for all in-scope scenarios**

   The added test set is solid, but it does not cover *all* in-scope behavior.

   What is covered well:
   - `--manifest` valid case,
   - `--manifest` invalid-schema case,
   - `--manifest` missing file,
   - `--manifest` non-JSON file,
   - `--in` valid directory case,
   - `--in` empty directory,
   - `--in` missing directory,
   - usage errors,
   - `--json-status` valid/invalid envelope basics,
   - at least one packaged smoke path.

   What is still missing relative to the described behavior:
   - **Recursive discovery under a nested run-style output tree.** The `--in` test writes `manifest.json` directly in the temp root. That does not prove recursive search works.
   - **Multiple `manifest.json` files under one input root.** Since `--in` “searches recursively,” the aggregate behavior when multiple manifests are found is important and currently untested.
   - **Mixed valid/invalid manifests under `--in`.** Needed to prove whether the command returns `3` if any manifest fails and whether reporting is complete.
   - **Unexpected-error path / exit code `2`.**
   - Potentially **JSON envelope behavior on usage/file-error cases**, if that is part of the CLI-wide contract.

   **Bottom line:** good baseline coverage, but not complete coverage for all in-scope scenarios.

4. **Mutual exclusivity of `--in` and `--manifest`, and exit-1 on file-not-found**

   On the evidence provided, this concern is satisfied.

   There is an explicit test for:
   - no options supplied → exit `1`,
   - both `--in` and `--manifest` supplied → exit `1`,
   - missing manifest file → exit `1`,
   - missing input directory → exit `1`.

   That is the contract you asked about, and it is directly exercised. I do not see tests for wrong-kind path inputs such as “directory passed to `--manifest`” or “file passed to `--in`,” but that is beyond the narrower concern stated here.

   **Bottom line:** yes, this is covered by the current tests.

5. **EmbeddedResource availability in the self-contained published binary**

   The project-file change is correct for this purpose.

   This line:

   ```xml
   <EmbeddedResource Include="..\docs\test-cases\harvester-schema.v1.0.json">
     <LogicalName>AccessHarvester.Cli.harvester-schema.v1.0.json</LogicalName>
   </EmbeddedResource>
   ```

   embeds the schema into the CLI assembly at build time. In .NET single-file self-contained publish mode, embedded managed resources remain part of the bundled assembly payload and are available via `GetManifestResourceStream`. So from a packaging standpoint, this is the right mechanism, and the single-file settings shown do not undermine it.

   Two additional signals support this:
   - the ordinary CLI validate tests would fail immediately if the embedded resource were not available in the built CLI,
   - a packaged smoke test exists specifically to exercise the published artifact.

   The only missing piece in the supplied snippets is the actual `LoadEmbeddedSchema()` implementation, so I cannot inspect whether it uses the exact logical resource name. But because validate tests passed, the lookup is almost certainly correct in normal execution. Static review says the publish story is sound.

   **Bottom line:** yes, the `EmbeddedResource` setup is correct for self-contained publish.

6. **`install.md` accuracy**

   The validate-related documentation looks broadly accurate and internally consistent with the implementation you described.

   In particular, these parts line up:
   - new `validate` subcommand syntax,
   - `--in` and `--manifest` modes,
   - `--json-status`,
   - exit-code meanings,
   - schema embedded in the binary,
   - no schema file needed on disk at runtime,
   - smoke-check example.

   I do not see a clear validate-specific factual mismatch in the document.

   The one caveat is evidentiary rather than textual: `install.md` states that `--in` locates `manifest.json` recursively under the run output root, but the provided tests do not actually prove recursive discovery against a nested output structure. That does not make the doc wrong; it just means the docs are slightly ahead of the demonstrated test coverage.

   **Bottom line:** documentation appears accurate for the validate feature, with the recursive-discovery claim needing stronger test backing.

```json
{
  "verdict": "NOT VERIFIED",
  "issues": [
    "RunValidate exit code 2 is not verified. The provided evidence covers 0, 1, and 3 only; Program.cs was not included and no test forces an unexpected-error path.",
    "Integration coverage is incomplete for --in recursion and aggregation. There is no test for nested run-style output discovery, multiple manifest.json files, or mixed valid/invalid manifests under one input root.",
    "SchemaValidator._schemaCache uses StringComparer.OrdinalIgnoreCase for schema $id keys. JSON Schema $id values are URIs and should not be treated as universally case-insensitive; this can incorrectly alias distinct schemas."
  ]
}
```