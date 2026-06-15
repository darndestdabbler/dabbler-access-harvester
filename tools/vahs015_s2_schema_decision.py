"""015 S2 — route an architecture critique of the Layer B schema-home decision.

Asks a cross-provider model to challenge the decision to surface module-level
unresolved refs into unresolved-items.json under a new UnresolvedSource enum
value (no envelope bump, no ParserOutput change) BEFORE we commit the fix +
golden regen. Per Delegation Discipline (architecture decisions route).
"""
from pathlib import Path
from ai_router import route

SESSION_SET = Path("docs/session-sets/015-unresolved-items-completeness")
note = (SESSION_SET / "s2-design-note.md").read_text(encoding="utf-8")

prompt = f"""You are reviewing an ARCHITECTURE/SCHEMA decision for a C#/.NET metadata
harvester before it is implemented. Be adversarial: your job is to find the
flaw, not to rubber-stamp.

CONTEXT
- `unresolved-items.json` is a run-wide index of every VBA reference the
  resolver could NOT bind. Each entry is an `UnresolvedItem` with a `Source`
  enum discriminator and a `Location` (ModuleName / ProcedureName / Line /
  Column, all optional).
- DW-11 Layer B: a MODULE-LEVEL unresolved reference (e.g. a `Public Const X =
  MissingThing` initializer) lives on `resolvedModule.References` with NO
  enclosing ProcedureScope, so the projector drops it from both the per-module
  `ParserOutput.Body.References` AND the `unresolved-items.json` index. It has
  no manifest signal at all today.
- Layer A (already shipped, prior session) added procedure-HEADER refs to the
  index via an additive aggregator feed + a new projector enumerator,
  deduped against the body-only feed, reusing `Source=FunctionClassification`.
- The `UnresolvedItem` JSON `$def` has NO `additionalProperties:false`; the
  only strict element is the `Source` enum. The repo treats
  `harvester-schema.v1.1.json` as the LIVING schema for the v1.1 envelope.
- A documented repo lesson: "Additive field on an additionalProperties-tolerant
  shared $def → bump the sub-document version, not the envelope file. Only do a
  full envelope bump when the envelope's required/structural shape changes or a
  strict (additionalProperties:false) def must admit a new property."

THE DECISION TO CRITIQUE (full design note follows):

{note}

Answer these specific questions, citing concrete reasoning:
1. Is "widen the `Source` enum to add `ModuleLevelReference`, no envelope
   version bump" contract-correct given the living-schema treatment? Or does
   adding an enum value to a closed set constitute a breaking/structural change
   that warrants an envelope bump? Argue both sides, then commit to one.
2. Is the no-double-count claim ("a module-level ref has no ProcedureScope so it
   is disjoint from the procedure-scoped feeds, no dedup needed") actually
   airtight, or is there a construct where the SAME reference could appear in
   BOTH a procedure-scoped feed and the module-level feed? Consider: nested
   procedures? module-level `Dim`/`Const` whose initializer calls a function?
   `Implements`? property/field initializers? With-block edge cases?
3. Is surfacing module-level refs into the INDEX ONLY (not also into a
   ParserOutput inline surface) the right completeness boundary, given DW-11
   says "no complete manifest signal"? Would a consumer reasonably expect an
   inline per-module signal too?
4. Any other flaw, omission, or smaller-correct alternative.

Return prose. End with a one-line verdict: SOUND, SOUND-WITH-CAVEATS, or
RECONSIDER.
"""

result = route(
    content=prompt,
    task_type="architecture",
    context="015 S2 Layer B schema-home decision critique (pre-implementation)",
    session_set=str(SESSION_SET),
    session_number=2,
)
out = SESSION_SET / "s2-schema-decision-critique.md"
out.write_text(
    f"# 015 S2 — routed schema-decision critique ({result.model_name})\n\n"
    f"> task_type=architecture; cost ${result.cost_usd:.4f}. Raw routed output, unedited.\n\n"
    + result.content,
    encoding="utf-8",
)
print("MODEL:", result.model_name)
print("COST_USD:", result.cost_usd)
print("WROTE:", out)
