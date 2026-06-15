# Session 1 — Cross-provider verification (routed)

**Verifier:** gpt-5-4 (`route(task_type="session-verification")`), cross-provider to the
Opus orchestrator. Prose-first + trailing JSON. **Final verdict: VERIFIED (R3).**
In-IDE devil's-advocate pair prompt (user-run, spec S1 step 7) is at
`s1-in-ide-devils-advocate-prompt.md`. Model-design consensus is at
`s1-ribbon-model-consensus.md`. Raw round outputs are reproduced verbatim below
(never edited).

## Round 1 — ISSUES_FOUND (4)
gpt-5-4 raised: (1 Major) empty->null attr contract; (2 Major) structured view drops
non-tabs customUI content; (3 Major) tie-break determinism on (RibbonName,Table);
(4 Minor) signature-only detection collision.

```
1. **Parser correctness & faithfulness.**  
For the subset it explicitly models, the parser is solid: element matching by `LocalName` gives the intended 2006/01 vs 2009/07 namespace tolerance, recursion under `group`/nested controls is faithful, source order is preserved for controls and unslotted attributes, namespace declarations are excluded, and malformed XML or missing `<customUI>` / `<ribbon>` correctly become `FormatException`. The committed tests also cover the important positive cases: built-in `idMso` controls, deep nesting, contextual tabs, prefixed XML, and determinism.

I do see two real fidelity gaps. First, `AttrValue` returns `""` for empty authored attributes, not `null`, so the stated `empty->null` contract is not actually implemented. Second, the parser is only faithful for the modeled branch shape; it silently drops other authored content: root-level customUI attributes other than `onLoad`, non-`ribbon` children of `<customUI>`, non-`tabs` / non-`contextualTabs` children of `<ribbon>`, non-`tab` children of `<tabs>`, non-`group` children of `<tab>`, and non-`tabSet` children of `<contextualTabs>`. Because the stated principle is “unknown/unsupported constructs are surfaced, never silently dropped,” that is a substantive mismatch. Raw XML preservation in `ribbon-xml.json` mitigates recoverability, but it does not make the surfaced `RibbonStructure` fully faithful.

2. **Shape-gate seam (golden neutrality).**  
For the canonical Access case, the seam looks right. `USysRibbons` is not filtered out by `SystemObjectNames.IsSystemObjectName`, field-name detection is case-insensitive, rows are read only from matching tables, and no `ribbon-xml.json` is written when `detected.Count == 0`. That is consistent with the disclosed HHC byte-neutral validation and with `NavigationGraph.Ribbon` remaining null when no artifact exists.

It is not airtight in the strict sense, though. Detection is signature-only: any unrelated user table with both `RibbonName` and `RibbonXML` columns will be treated as ribbon-shaped and will cause `ribbon-xml.json` to be emitted. That means “no file on a non-ribbon DB” is only true for corpora that do not contain an accidental shape match. Also, table field introspection failures are silently skipped, so a real `USysRibbons` could be missed under unusual permission/introspection failures. I would call the current gate practical and corpus-proven, but not logically airtight.

3. **Builder selection.**  
The builder does the right high-level thing: it is pure, deterministic for distinct sort keys, continues past malformed rows, records them in `MalformedRows`, and only counts additional *parseable* ribbons in `AdditionalRibbonCount`. The “non-drop” story is sound at the raw tier because all rows remain in `ribbon-xml.json` regardless of which one is surfaced into `NavigationGraph.Ribbon`.

There is one determinism hole: ties on `(RibbonName, Table)`. `Build` sorts only by `RibbonName` then `Table`. If multiple rows share the same values for both, their relative order falls back to source order. But exporter-side row order within a table is only `ORDER BY [RibbonName]`, so duplicate names in the same table have no stable tiebreaker. In that case, the surfaced singular ribbon is not guaranteed to be deterministic across DAO/Jet behaviors. If determinism is a hard principle, this needs a stable row ordinal or key.

4. **Wiring & diagnostics.**  
From the excerpted block, the wiring is conceptually correct: it only loads the artifact if `ribbon-xml.json` exists, deserializes it, builds the singular structure, assigns `ribbon`, emits `RIBBON_MULTIPLE_DEFINITIONS` when appropriate, and emits `RIBBON_XML_PARSE_FAILURE` for each malformed row. That matches the intended null-when-no-artifact behavior and preserves the existing “no file means no ribbon” short-circuit.

I do not see a separate logic defect in the shown wiring itself. The only caveat is that the `catch` body is truncated in the provided snippet, so I cannot independently inspect the exact `RIBBON_LOAD_FAILURE` implementation from source here. I am not flagging that solely because the excerpt is incomplete; the material risks I see are upstream: parser fidelity and builder tie-breaking.

5. **Model & schema contract.**  
The model extension itself is sensible. Nullable `Id`/`Label`, `IdMso`, open-string `ControlType`, recursive `Children`, `ContextualTabSets`, `OnLoad`, `Name`, and an `Attributes` bag are all appropriate for the real Access ribbon shape you described. Given the disclosed schema policy—additive change on an additionalProperties-tolerant `$def` with relaxed `required`—the backward-compatibility claim is credible, and the local Northwind schema pass is the right validation.

I do not see a schema-shape contradiction from the material shown. The main reservation is not with the contract design, but with the implementation living up to the contract’s “faithful recursive tree” claim. Because some authored content can still be dropped before reaching the model, the contract is sound for the modeled subset, but the implementation does not yet fully satisfy the stronger fidelity wording.

6. **Scope discipline.**  
Scope control is mostly good. The code does not attempt to resolve dynamic callbacks, does not interpret `imageMso`, `visible`, `size`, etc., and only promotes static `onAction` into a typed property. That is aligned with “surface, don’t interpret.” The builder also correctly keeps “choose active ribbon via `CustomRibbonID`” out of scope for this session.

The inconsistency is at the root/customUI level. `onLoad` is promoted into `RibbonStructure.OnLoad`, which is acceptable if documented as “surfaced but not resolved,” but other root-level callbacks/attributes with no slot—most obviously `loadImage`—are dropped entirely, not preserved verbatim anywhere in the surfaced model. So I would not call this a scope overreach; I would call it an in-scope omission relative to the stated preservation rule.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "RibbonXmlParser.cs",
      "summary": "Promoted attribute extraction returns empty-string values as \"\" instead of normalizing empty authored values to null, so the stated empty->null contract is not implemented.",
      "fix": "Normalize zero-length attribute values to null in AttrValue (and document whether unslotted attributes should also normalize or remain verbatim)."
    },
    {
      "severity": "Major",
      "location": "RibbonXmlParser.cs",
      "summary": "The parser silently drops authored customUI content outside the modeled ribbon/tab/group/control path, including root-level unslotted attributes and unexpected child elements, which breaks the stated 'surface, never silently drop' rule.",
      "fix": "Extend the model/parser to preserve root-level attributes and unmodeled child nodes, or add an explicit raw/unknown-node capture so unsupported constructs are surfaced instead of filtered out."
    },
    {
      "severity": "Major",
      "location": "RibbonStructureBuilder.cs; RibbonExporter.cs",
      "summary": "Singular ribbon selection is not fully deterministic when multiple rows share the same RibbonName and Table because there is no stable tie-breaker beyond values that can collide.",
      "fix": "Carry a stable row ordinal or primary key in RibbonXmlRow during export and include it as the final sort key when selecting the surfaced ribbon."
    },
    {
      "severity": "Minor",
      "location": "RibbonExporter.cs; RibbonSignatureDetector.cs",
      "summary": "Signature-only table detection can emit ribbon-xml.json for unrelated user tables that happen to contain RibbonName and RibbonXML columns, so the no-file gate is not airtight on arbitrary corpora.",
      "fix": "Tighten detection by preferring the canonical USysRibbons table and/or add stronger shape checks such as field-type validation before treating a table as ribbon-defined."
    }
  ]
}
``````

## Round 2 — dispositions confirmed (Issue 2 doc-slice false positive)
Accepted disposition on 1/3/4; Issue 2's residual was traced to the R2 packet showing the
wrong doc paragraph (the boundary paragraph was already in the code).

```
Issue 3: I accept this disposition. In the shown `RibbonStructureBuilder.Build`, adding `ThenBy(r => r.RibbonXml ?? string.Empty, StringComparer.Ordinal)` after `(RibbonName, Table)` removes DAO-order dependence for rows that previously tied on the first two keys. That gives a deterministic selection order over all meaningfully distinct row values used by the builder. If two rows are identical on `RibbonName`, `Table`, and `RibbonXml`, they produce the same parsed result, so their relative order is irrelevant to the surfaced ribbon. The behavior is now deterministic.

Issue 1: I accept this rejection. The original `empty -> null` contract applies to whole-document parse input, not to per-attribute normalization. For a faithful mechanical harvester, preserving `label=""` as `""` while treating an absent `label` as `null` is the correct behavior. Collapsing those cases would erase authored signal. The new test described matches the right contract.

Issue 2: I do not fully accept this disposition as currently evidenced. I agree that, for a navigation-focused producer, lossless raw preservation plus deferred work can justify not expanding the structured model right now. However, the provided documentation paragraph does not actually make the exclusion boundary explicit. It says “Every authored tab/group/control is surfaced” and that untyped attributes are preserved on the node’s `Attributes` bag, but it does not state that `<qat>`, `<officeMenu>`, `<backstage>`, and root `<customUI>` attributes beyond `onLoad` are not projected into the structured model, nor does it name the raw fallback location. As written, the docs still overstate coverage, so the “no longer silent” part is not verified.

Issue 4: I accept this rejection. If the repository’s established principle is signature-based detection rather than literal table-name matching, then treating ribbon detection the same way as switchboard detection is consistent. The `RibbonName` + `RibbonXML` signature is reasonably specific, and preferring the literal `USysRibbons` name would indeed conflict with that precedent.

```json
{"verdict":"ISSUES_FOUND","issues":[{"severity":"Major","location":"RibbonXmlParser documentation / Issue 2 disposition","summary":"The claimed projection boundary is not actually documented by the provided paragraph; it overstates coverage and does not explicitly say that <qat>, <officeMenu>, <backstage>, and root <customUI> attributes beyond onLoad are omitted from the structured model.","fix":"Amend the parser docs to explicitly scope the structured projection to tabs/contextualTabs, name the excluded non-tab sections and root attributes, and point consumers to raw/Navigation/ribbon-xml.json as the lossless fallback. If that explicit boundary is not acceptable, expand the model instead."}]}
``````

## Round 3 — VERIFIED
Shown the full actual class doc comment; confirmed the structured-view boundary is explicit.

```
Yes. The actual class doc comment now explicitly covers all four requested points:

- (a) **Scope to `<tabs>` / `<contextualTabs>`**: explicit. It says the structured tree models the ribbon’s `<tabs>` and `<contextualTabs>`.
- (b) **Names excluded sections / root attrs not projected**: explicit. It specifically lists `<qat>`, `<officeMenu>`, `<backstage>`, and says root-level `<customUI>` attributes beyond `onLoad` are **NOT projected**, with `loadImage` as an example.
- (c) **Names the lossless fallback**: explicit. It says the raw, byte-verbatim customUI XML is preserved in `raw/Navigation/ribbon-xml.json` and is the “lossless source of record.”
- (d) **References deferred work**: explicit. It says surfacing `onAction` entry points from those other sections is tracked as deferred work and cites **DW-21**.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
``````
