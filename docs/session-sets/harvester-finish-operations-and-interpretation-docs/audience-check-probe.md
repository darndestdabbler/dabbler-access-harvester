# Audience-Check Probe — Session 1

> **Purpose:** Two cross-provider downstream-AI consumers were asked
> whether the [`interpretation-guide outline`](../../harvester/interpretation-guide-outline.md)
> would give them enough to interpret a harvester run output. The
> probe satisfies the spec's *Audience-check probe* step plus the
> *Probe Substrate Value Earlier* convention in
> [`docs/planning/project-guidance.md`](../../planning/project-guidance.md).

## Cross-provider rigor — caveat

The intended probe pair was **gemini-pro (Google) + gpt-5-4-mini
(OpenAI)** — both non-Anthropic relative to the Opus orchestrator,
satisfying strict cross-provider rigor. OpenAI returned **sustained
HTTP 429** across both `gpt-5-4` and `gpt-5-4-mini` for the duration
of this session (multiple retries, ~2 minutes total wait, then a
retry against the full `gpt-5-4` model also 429ed). The second probe
data point was taken against **sonnet** (Anthropic) as fallback.

The fallback degrades the probe from "two cross-provider verifiers"
to "one cross-provider verifier (gemini-pro) + one cross-model-family
verifier (sonnet)." Both data points still flag the **same three
must-have gaps** independently, so the convergence is meaningful
despite the degradation — and the convergence is the signal the probe
was designed to extract. The OpenAI outage is surfaced separately
under *Operational notes* below; it is **not** treated as a probe
failure here.

## Synthesized gap-list

Both probes independently flagged the same three must-have gaps —
they are real, and they will be folded into the Session 2 writing
scope by an explicit outline update (below). The nice-to-haves are
each flagged by exactly one probe; folded in as optional polish.

### Must-have gaps (both probes, fold into Session 2 scope)

1. **`RowSourceSpecs` record shape (outline §5.3 / §4).** The outline
   tells the consumer to consult the manifest's `RowSourceSpecs` for
   query dependency facts but never sketches what an entry looks
   like. Without the record shape (keys, typed fields, join key back
   to the named query or form control), the form → query → table
   traversal that §3 says is the core read path is blocked.
   **Resolution:** outline §4 (envelope) and §5.3 (Queries) must each
   include a small JSON snippet showing one `RowSourceSpec` record
   with field-by-field explanation.

2. **`FunctionClassifications` and `EventHandlerClassifications`
   record shapes (outline §5.4).** The outline says these arrays
   classify procedures and surface caller → callee edges, but gives
   no sketch of the actual record shape or the join key back to a
   per-module `ir/` file. Without this the module call-graph
   reconstruction the outline implies is blocked. **Resolution:**
   outline §5.4 must include a small JSON snippet of one classification
   record per array, naming the join key explicitly.

3. **Embedded-macro XML schema after `_AXL:` reassembly (outline §5.1
   / §5.6).** The outline says to concatenate `Comment` strings across
   `Begin` nodes to reassemble the embedded UI macro XML, but does not
   say what the resulting XML's structure looks like or where its
   schema is documented. A consumer can produce the blob and not know
   how to read it. **Resolution:** outline §5.1 and §5.6 must include
   either a brief sketch of the `<UserInterfaceMacro>` XML shape
   (Statements, ConditionalBlock, Action elements) or a pointer to
   the Microsoft `accessservices/2009/11/application` schema URL the
   payloads carry, plus a note on how Access macro actions map to
   their XML element names.

### Nice-to-haves (single-probe; fold in if budget allows)

4. **Default-value lookup for omitted properties (outline §7,
   gemini-pro).** §7 distinguishes omitted-because-default from
   null-because-explicit, but doesn't say where to find the default
   values. **Resolution:** add a one-line pointer to
   `samples/GoldenOutput/HomeHealthCare/kb/Defaults.json` (or whatever
   the canonical defaults source ends up being) so a consumer can
   reconstruct complete property state when needed.

5. **Relationship cardinality (outline §5.5, gemini-pro).** §5.5
   mentions join keys and cascade rules but not one-to-one /
   one-to-many cardinality. **Resolution:** add one sentence noting
   how cardinality is encoded (likely via the relationship's index
   attributes).

6. **`Diagnostics` field semantics (outline §4 + §reading-order recap,
   sonnet).** The envelope bullet lists `Diagnostics` and the reading-
   order recap suggests cross-referencing against it, but no section
   describes what an entry contains or what action to take on one.
   **Resolution:** add one bullet under §4 sketching the
   `Diagnostics` record shape and the recommended downstream-AI
   action class.

7. **`kb/` tier file layout when consulting for provenance (outline
   §3, sonnet).** §3 says to consult `kb/` when an `ir/` field's
   provenance is in question but doesn't describe what `kb/` files
   look like or which fields differ. **Resolution:** add one
   paragraph to §2 (output map) noting that `kb/` mirrors `ir/`'s
   per-category subdirectory structure but omits the enrichment
   fields, with a one-line "compare X to Y" example.

### Outline action — folded in this session

The outline at
[`docs/harvester/interpretation-guide-outline.md`](../../harvester/interpretation-guide-outline.md)
will be amended in the same Session 1 commit to:

- Add a *Required JSON snippets* sub-bullet to §4, §5.3, §5.4
  identifying the three must-have snippets and their canonical
  source (the C# record definitions in
  `AccessHarvester.Pipeline/Manifest/` and `Enrichment/RowSourceSpec.cs`,
  or a populated example if one exists).
- Add the embedded-macro XML guidance to §5.1 and §5.6 (schema URL
  pointer + one-paragraph shape sketch).
- Add the nice-to-have polish items to their respective sections as
  one-sentence asks for Session 2.

This converts the audience-check probe's findings from "Session 2
must read this probe file" into "Session 2 sees the gaps as outline
content," which is the stronger form of forwarding the feedback.

## Operational notes

- **OpenAI 429 during this session.** Both `gpt-5-4-mini` and
  `gpt-5-4` returned sustained 429s. This will affect Step 6
  session-verification, which the verifier-pairing rules default to
  GPT-5.4 for Opus output. Per the *Verifier-failure escalation
  ladder* convention in
  [`docs/planning/project-guidance.md`](../../planning/project-guidance.md),
  if Step 6's first GPT-5.4 attempt also 429s, the escalation path
  is to fall back to `gemini-pro` for verification. This will be
  recorded explicitly in the session review.
- **Raw probe outputs preserved verbatim** in
  [`./probe-raw/`](./probe-raw/). The two `.txt` files there are the
  source of truth for the synthesis above and must not be edited.

---

## Raw output — `gemini-pro` (Google)

- Cost: $0.0091
- Tokens: in=4460, out=351
- File: [`./probe-raw/gemini-pro.txt`](./probe-raw/gemini-pro.txt)

```
1.  [must-have] The outline does not specify the linkage mechanism for connecting records in the manifest's `FunctionClassifications` and `EventHandlerClassifications` arrays (§5.4) back to specific procedures in specific `ir/` files; I would get stuck trying to apply these critical classifications without knowing the join key.
2.  [must-have] The outline states that query dependencies are in the manifest's `RowSourceSpecs` (§5.3), but does not describe the shape of a `RowSourceSpecs` entry; I would not know how to parse the list of referenced tables and queries without this structural information.
3.  [must-have] The outline explains how to reassemble embedded macro XML from `_AXL:` comments (§5.1, §5.6), but does not specify how to interpret the reassembled XML itself; I would get stuck trying to understand the macro's actions without a primer on its structure or a pointer to a schema.
4.  [nice-to-have] Section 7 correctly distinguishes omitted properties (use Access default) from `null` properties (explicitly no value), but does not state where to find the default values; I could infer a property is "not interesting" if omitted, but could not reconstruct its complete state with certainty.
5.  [nice-to-have] The section on Relationships (§5.5) mentions join keys and cascade rules but not cardinality (one-to-one, one-to-many); I would have to infer cardinality from index properties instead of reading it directly from the relationship definition.

COVERED_WITH_CAVEATS
```

## Raw output — `sonnet` (Anthropic, fallback for OpenAI 429)

- Cost: $0.0372
- Tokens: in=4612, out=1555
- File: [`./probe-raw/sonnet.txt`](./probe-raw/sonnet.txt)

```
1. **Embedded-macro XML schema after reassembly (§5.1, §5.6)** [must-have] — The outline tells me to concatenate `_AXL:` `Comment` strings across `Begin` nodes but never describes the resulting XML schema; once I have the blob I'd have no basis for interpreting what the embedded macro actually does or how its action nodes map to Access macro actions.

2. **`RowSourceSpecs` record shape (§5.3, §4)** [must-have] — §5.3 says dependency facts live in the manifest's `RowSourceSpecs` and promises a "cross-link explained here," but the outline never sketches the record shape (keys, typed fields, how a spec is matched back to a named query or form control); without this I cannot traverse form → query → table even though the outline implies that traversal is the core workflow.

3. **`FunctionClassifications` and `EventHandlerClassifications` record shapes (§5.4)** [must-have] — The outline says these manifest arrays surface caller→callee edges and classify procedures as event handlers, helper subs, etc., but gives no sketch of the actual record shape; I would be able to find the arrays but not read the edges, leaving module call-graph reconstruction blocked.

4. **`Diagnostics` field semantics (§4)** [nice-to-have] — It appears in the manifest envelope bullet list and the reading-order recap instructs me to "cross-reference findings against `Diagnostics`" for repo-wide synthesis, but no section describes what a Diagnostics entry contains or what action I should take on one; I'd skip it rather than risk misreading it.

5. **`kb/` tier file structure for provenance checking (§3)** [nice-to-have] — §3 says to open `kb/` when an `ir/` field's provenance is in question, but never describes what `kb/` files look like or which fields to compare against their `ir/` counterparts; the fallback instruction is present but not actionable.

Overall verdict: COVERED_WITH_CAVEATS — The outline covers the primary read path (manifest → ir/ → unresolved-items) adequately, but the three must-have gaps (embedded-macro XML schema, `RowSourceSpecs` shape, classification record shapes) would leave me unable to interpret significant structural content the guide itself directs me to read.
```

## Raw output — `gpt-5-4-mini` (OpenAI) — FAILED

- Status: 3 attempts, all returned HTTP 429
- File: [`./probe-raw/gpt-5-4-mini.txt`](./probe-raw/gpt-5-4-mini.txt)

## Raw output — `gpt-5-4` (OpenAI) — FAILED

- Status: 3 attempts with 30s/60s/90s backoffs, all returned HTTP 429
- File: [`./probe-raw/gpt-5-4.txt`](./probe-raw/gpt-5-4.txt)
