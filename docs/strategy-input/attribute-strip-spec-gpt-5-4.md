## Q1 — Audit scope of call sites

**Recommendation:** take **Q1.a**, but define it precisely: **audit and route every production/runtime call site that parses Access-exported module text through the shared loader; leave `VbaParserPipeline.Parse(string)` as the raw low-level parser API for grammar-focused/unit tests and explicitly pre-normalized inputs.**

Why:
- The current failure is systemic, not consumer-specific.
- A two-path production world will rot quickly; someone will forget the loader and reintroduce parse failures.
- Keeping the parser API raw is still useful for grammar tests and hand-built fixtures, so you don’t need to contort tests that never had headers.

**How to handle tests that don’t need strip**
- Parser/grammar tests may continue calling `Parse(rawSource)` directly.
- Any test/harness that reads real SaveAsText/YAML-derived module bodies should use the loader path.
- Document this split explicitly: **raw parser = normalized source only; loader = Access-exported source.**

**What to put in the spec**
1. **Enumerate audited call sites** and require each production/runtime consumer to use the loader before parse:
   - Resolver
   - FunctionClassifier
   - NavigationGraphBuilder
   - DynamicSqlAnalyzer (where fed module bodies)
   - ParseSuccessReportGenerator
   - CLI `parse` subcommand
2. **State that `VbaParserPipeline.Parse` remains raw** and is not responsible for stripping.
3. Add loader tests:
   - canonical form/report header is stripped
   - `.bas` body with no header is unchanged
   - stripping is deterministic / idempotent
   - CRLF/LF and optional BOM are tolerated
4. Add at least one end-to-end regression proving a previously failing code-behind module now parses via the normal consumer path.

**Edge case to pin**
- **Only strip a canonical leading header at start-of-file** (allow BOM/leading blank lines if you want, but say so). Do **not** remove `Attribute`-looking lines elsewhere in the module.
- If the prefix is malformed/partial, **do not heuristic-strip**; leave source untouched and let parse fail visibly.

---

## Q2 — What “strip” means structurally

**Recommendation:** choose **Q2.b: preserve header as metadata**, because `VB_PredeclaredId` is migration-relevant and stripping without preservation creates silent semantic loss. Do it in a **typed metadata record**, with an **additional-attributes map** only for unrecognized `Attribute VB_*` lines.

Why:
- `VB_PredeclaredId` matters for understanding default-instance usage (`Form_frmFoo.Visible = True`).
- `VB_Creatable` / `VB_Exposed` matter for class-module semantics, even if form/report modules often hardcode them.
- `VB_Name` is redundant with file/object identity but still cheap and useful for consistency checks.
- `VB_GlobalNameSpace`, `MultiUse`, `Version` are lower-value, but if you already parse them, preserving them avoids arbitrary loss.

**Recommended shape**
- `ModuleHeaderMetadata` typed record:
  - `VersionMajor`, `VersionMinor`
  - `MultiUse`
  - `VbName`
  - `VbGlobalNameSpace`
  - `VbCreatable`
  - `VbPredeclaredId`
  - `VbExposed`
  - `AdditionalAttributes: IReadOnlyDictionary<string,string>`
- Attach it to the parsed module result (`Module.Header` or equivalent).  
- Do **not** use a flat map alone; known semantics deserve typed fields.

**What to put in the spec**
1. Define the exact recognized fields above.
2. Define parsing/normalization rules for boolean-like values (`True`/`False`, `-1`/`0` if applicable).
3. Require tests showing:
   - canonical header -> parsed metadata populated correctly
   - no header -> `Header == null`
   - unknown `Attribute VB_*` lines are preserved in `AdditionalAttributes`
4. Require that stripped body parsing and header metadata extraction are deterministic and independent of newline style.

**Edge case to pin**
- If `VbName` disagrees with external artifact identity/path, **do not let header metadata redefine module identity**; preserve it as metadata only.
- Preserve only the **leading header block**; later attribute lines are not reclassified as header metadata.