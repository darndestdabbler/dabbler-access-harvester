"""Decision-time cross-provider consensus for F-3-4 item 3 (DefaultMemberRegistry).

Routes the design question to GPT-5.4 and Gemini Pro (different providers than
the Claude orchestrator). Persists each raw response to disk BEFORE printing
(per the persist-before-display lesson).
"""
import os

from ai_router import query

SESSION_SET = "docs/session-sets/011-resolver-and-navigation-wiring"
OUT = os.path.join(SESSION_SET, "s1-item3-design-consensus.md")

PROMPT = r"""
You are advising on a C#/.NET resolver-wiring decision in a Microsoft Access
metadata harvester. Be decisive: recommend ONE option and give the reasoning.

## Context

A VBA reference resolver emits, for each name-use site, a `ResolutionOutcome`.
`ResolutionOutcome` is a CLOSED discriminated-union record (private constructor;
the doc comment states: "Adding a variant later is a deliberate breaking change
that must coordinate with all consumers"). Existing variants: Resolved,
ResolvedToObjectModel, ResolvedToBuiltIn, ResolvedToMe, ResolvedViaWithFlattening,
Unresolved(reason). Consumers that switch over the closed set include the
manifest JSON projector (ParserOutputProjector.MapOutcome), a SymbolDb
references extractor, and a cross-reference index back-population.

`DecideMemberOutcome(target, memberName, isBang)` decides member-access outcomes
(`X.member` and `X!member`). Today its default arm hardcodes
`Unresolved("default-member-not-yet-resolved")` for a member access on a
*resolved-but-source-typed* target (e.g. `Dim rs As DAO.Recordset` then
`rs!Notes`). It never consults the curated `DefaultMemberRegistry`
(TypeName -> DefaultMember{MemberName, ReturnType, IsParameterized}), which knows
e.g. Recordset's default member is `Fields` (parameterized, returns `Field`),
Collection -> Item, TextBox -> Value, etc.

The task (006 review finding F-3-4): "consult DefaultMemberRegistry in
DecideMemberOutcome (retire the hardcoded default-member-not-yet-resolved default
arm WHERE THE REGISTRY RESOLVES)."

## Corpus evidence (HomeHealthCare golden, the committed fixture)

~129 bang-access references like `rsTarget!Notes`, `rsSource!ServiceID` on
`Dim ... As DAO.Recordset` / `As ADODB.Recordset` variables currently emit
`Unresolved("default-member-not-yet-resolved")`. These are genuine Recordset
field accesses (`rs!Notes` ≡ `rs.Fields("Notes")`). Resolving them flips ~129
golden references from Unresolved to Resolved — a real, correct semantic gain
for the downstream AI consumer, but also a large positive claim that must be
right.

Note: control default-members (`Me.txtName.Value`) ALREADY resolve via a
separate Access-object-model registry + type library — that path is unaffected.
The DefaultMemberRegistry's unique contribution is DAO/ADO Recordset/Field/
Collection/Dictionary default members on SOURCE-typed variables, primarily the
bang-on-Recordset case.

## The options

**Option A — New closed-type variant `ResolvedToDefaultMember(DefaultMember,
accessedName)`.** Most explicit/self-describing. Cost: a breaking change to the
closed ResolutionOutcome (update every consumer switch: projector, SymbolDb
extractor, cross-ref back-population) + a new ReferenceKind in the projector/JSON
schema. Highest fidelity, highest blast radius.

**Option B — Synthetic `Declaration` wrapping the default member, emitted via the
EXISTING `ResolvedToObjectModel` variant.** Reuses the existing outcome and the
existing projector path (ReferenceKind="ObjectModel"). Add a small
`SyntheticDefaultMemberDeclaration : Declaration`; the projector's
DeclarationKindString gets one case (or returns a chosen kind). No closed-type
breaking change. Slightly less self-describing (a consumer sees "ObjectModel",
not "DefaultMember"), but the back-population/cross-ref machinery already handles
ResolvedToObjectModel uniformly.

**Option C — Defer item 3 to a deferred-work (DW) entry.** Implement only the
shared-ProjectScope + Access-registry + owningClass wiring (already done, which
resolves the control/Me cases). Leave the ~129 Recordset-bang refs as
`default-member-not-yet-resolved`. Contradicts the spec step's explicit ask, but
honours lean/scope-discipline if the new outcome surface is judged too risky for
one session.

## Decision criteria (project principles)

- "Unresolvable constructs are explicit, not silent" — resolved should mean
  genuinely resolved; do not over-claim.
- Lean by default — don't add abstraction/variants beyond what the task needs.
- Output is consumed by a downstream AI, not a human; machine-readability +
  honest provenance matter; layout does not.
- The change must be deterministic and every golden delta explainable by F-3-4.

## What I need

1. Pick ONE option (A, B, or C) and justify in 3-6 sentences.
2. If A or B: what should the emitted JSON convey for `rsTarget!Notes` so the
   downstream AI reads it correctly without over-claiming the Field's own type
   (which we cannot know without the recordset's source schema)? Propose the
   ResolutionStatus / ReferenceKind / target shape and a Confidence if any.
3. Name the single biggest correctness risk of your chosen option and how to
   guard it with a hermetic test.
4. Should `isBang` gate the consultation (only bang applies the default member),
   or do dot-accesses also? Be specific about `rs.Fields` vs `rs!Notes`.
"""


def main() -> None:
    results = {}
    for model in ("gpt-5-4", "gemini-pro"):
        try:
            r = query(model, content=PROMPT, task_type="analysis",
                      session_set=SESSION_SET, session_number=1)
            results[model] = (r.content, r.model_name, r.cost_usd)
        except Exception as exc:  # noqa: BLE001
            results[model] = (f"ERROR: {exc}", model, 0.0)

    lines = ["# F-3-4 Item 3 (DefaultMemberRegistry) — design consensus\n",
             "Cross-provider decision input (GPT-5.4 + Gemini Pro). Raw, unedited.\n"]
    total = 0.0
    for model, (content, model_name, cost) in results.items():
        total += cost
        lines.append(f"\n---\n\n## {model} ({model_name}) — ${cost:.4f}\n\n{content}\n")
    lines.append(f"\n---\n\nTotal routed cost: ${total:.4f}\n")

    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    # Persisted to disk first; now safe to print a short summary.
    print(f"Wrote {OUT} (total ${total:.4f})")


if __name__ == "__main__":
    main()
