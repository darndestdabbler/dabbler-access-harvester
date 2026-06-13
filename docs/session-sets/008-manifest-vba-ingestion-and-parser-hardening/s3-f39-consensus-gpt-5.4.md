Recommendation: **Option B**.

The current docs describe a contract that does not exist. The right close is to make the contract truthful, not to build a costly adapter around hand-authored fixtures that were never aligned to the operative output.

### Why B is the better decision

1. **Field-mask size makes A weak**
   The proposed diff would have to ignore most of each `expected.json` because those files contain large surfaces the harvester does not emit and may never emit: confidence scores, template candidates, narrative next steps, invented diagnostic codes, etc.

   Once you mask most fields, the “conformance” test stops being a real corpus contract. It becomes a narrow subset comparison with higher maintenance cost than the targeted assertions you already have. That is bad leverage.

2. **The shapes do not actually match**
   This is the decisive technical problem. The fixture files are in a manifest envelope, but the inner content reflects pre-implementation parser intent, not the actual `DatabaseManifest` v1.1 output shape.

   So Option A is not “just add a runner.” It requires one of:
   - a translation layer from real output to intent-shape,
   - a translation layer from intent-shape to real output,
   - or re-authoring all 14 expected files into the operative shape.

   All three are substantial. The third is especially suspect because it turns the fixtures from forward-looking design intent into current-behavior snapshots, which is not clearly an upgrade.

3. **B is more honest for the consumer**
   The downstream consumer is an AI agent reading the manifest. What it needs is a truthful statement of what is guaranteed today.

   Today, the guarantee is:
   - fixture JSON files validate against the pinned schema shape,
   - specific parser/resolver behaviors are enforced by named tests on named cases,
   - not “every fixture is re-run and diffed against expected output.”

   Stating that plainly is better than giving the consumer a stronger-sounding but mostly-masked pseudo-contract.

4. **A is not the right regression mechanism for the bugs you just fixed**
   The recent escaped defects were caught by adding/strengthening **targeted semantic assertions**. That is the pattern that actually closes parser bugs.

   A broad corpus diff only helps when:
   - the expected outputs are aligned to the emitted outputs, and
   - the affected semantics are represented in the compared fields.

   Here, neither is reliably true. So A likely would not have been the thing that saved you; the new behavioral pins are.

5. **Lean + consumer-driven says don’t build this harness now**
   This repo is explicitly lean and consumer-driven. A new conformance harness is justified only if it buys unique, material coverage. In this case it mostly buys:
   - translation/masking complexity,
   - more maintenance,
   - and a risk of overstating certainty.

   That is not proportionate.

### What B should include

Amend the docs so they say exactly what is enforced:

- `project-guidance.md`:
  - remove “source of truth” / enforced diff language,
  - state that `docs/test-cases/*.expected.json` are **design-intent / onboarding artifacts**, not executable output snapshots,
  - describe the real enforcement layers: schema-shape validation plus targeted behavioral tests.

- `docs/test-cases/README`:
  - correct the case count from 12 to 14,
  - correct schema version language from v0 to the actual pinned v1.1 shape,
  - note any exclusions like the stop-rule case,
  - explicitly label expected files as illustrative design intent unless/until a real golden-output suite exists.

### Important nuance

If you ever do need stronger corpus-level pinning later, do **not** retrofit these current `expected.json` files into that role. Create a separate, smaller golden suite in the **actual emitted `DatabaseManifest` shape**, scoped to fields the consumer truly relies on. That would be a legitimate future consumer-driven ask. It is not the right answer to this finding.

```json
{
  "recommendation": "B",
  "confidence": "high",
  "one_line": "Amend the docs to match the real contract; do not build a masked corpus-diff harness around design-intent fixtures that do not match the emitted manifest.",
  "key_reasons": [
    "Option A would need to mask most fixture fields, so the diff would provide weak signal at high maintenance cost.",
    "The expected.json files do not share a clean, direct shape with the operative DatabaseManifest v1.1 output, so a true diff is not well-defined without substantial translation or re-authoring.",
    "The downstream consumer is better served by an honest statement of today's guarantees than by an aspirational contract backed by a heavily masked comparison.",
    "The recent escaped defects were better addressed by targeted behavioral assertions, which are the regression mechanism that actually matches parser semantics.",
    "Lean-by-default and consumer-driven posture do not justify a net-new harness that mostly re-encodes existing selective checks."
  ],
  "if_hybrid_what_minimal_A": "",
  "risks_of_your_choice": [
    "There will still be no full end-to-end golden-output regression over the entire corpus, so some unpinned behavior can drift unnoticed.",
    "If the docs are not explicit enough, contributors may continue to mistake expected.json for an executable contract.",
    "Targeted behavioral coverage requires discipline to add new assertions when consumer-relevant semantics change or bugs are fixed."
  ]
}
```