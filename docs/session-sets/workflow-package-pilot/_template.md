# Workflow Package: <Workflow Title>

> **Slug:** `<workflow-slug>` (filename: `packages/<workflow-slug>.md`)
> **Pilot session:** <Session N of 7>
> **Author orchestrator:** <Codex GPT-5.4 | Claude Opus 4.7>
> **Reviewer (when applicable):** <other orchestrator>
> **Source DB:** `samples/NorthwindDevEdition.accdb`
> **Source IR:** `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/`
>
> Open Decisions in effect for this set
> (resolved Session 1, 2026-04-30):
>
> - **Source operationalization format:** structured prose
>   (numbered click-sequence in markdown). See Section 5.
> - **Component / role mapping format:** generic-now
>   (implementation-agnostic hints). See Section 6.

---

## 1. Workflow summary

<!--
Two to four sentences. Implementation-agnostic. Who does this, why,
what business outcome it accomplishes. Do NOT describe how Access
implements it; that goes in Section 5.
-->

## 2. Actors and entities

- **Actors:** <list, mapped to role taxonomy where the Access roles map cleanly>
- **Primary entity:** <Order | Product | Employee | …>
- **Related entities:** <Customer, OrderDetails, Inventory, …>

## 3. Business rules with evidence + confidence tags

<!--
Bulleted list. Each rule:
  - Rule statement (what must be true)
  - Evidence: harvester citation (proc name, query name, table
    constraint, validation rule). Include path under
    `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/ir/`
    where helpful, or just the artifact name (e.g.
    `frmOrderDetails::Form_BeforeUpdate`,
    `modInventory::AllocateInventory`,
    `Tables/Orders.OrderStatusID`).
  - Confidence: confirmed | inferred | unknown
    - confirmed = rule is explicit in the cited code/constraint
    - inferred = behavior implies it but not spelled out
    - unknown = looks like there might be a rule here but author
      cannot confirm — push to Section 7 instead

Forbidden: rules without evidence. If you can't cite, the entry
goes in Section 7 (Open questions / ambiguity log) instead, NOT in
Section 3.
-->

- **Rule:** <statement>
  - **Evidence:** <citation>
  - **Confidence:** <confirmed | inferred | unknown>

## 4. Behavioral invariants -> characterization tests

<!--
For each behavior-bearing procedure identified in this workflow.
"Behavior-bearing" per spec.md: validators, calculations,
state-change logic, save/delete gating, business-rule-heavy query
shaping. NOT behavior-bearing: pure UI choreography, focus events,
control visibility, message-box plumbing, navigation handlers,
trivial getters/setters.

Each entry:
  - Invariant statement (what behavior the test pins)
  - Source procedure (citation)
  - Test file path:
      tests/AccessHarvester.CharacterizationTests/<WorkflowSlug>/<TestClassName>.cs
  - Anti-coupling check: invariant must be implementation-
    independent. Tests that assert against Recordset.Edit,
    Forms!MyForm!MyControl.Value, focus events, or message-box
    displays are operationalization-coupled and forbidden — push
    them to Section 7 instead, OR reformulate as invariant tests
    against pure functions.
-->

- **Invariant:** <statement>
  - **Source procedure:** <e.g. `modInventory::ProductAvailable`>
  - **Test file:** `tests/AccessHarvester.CharacterizationTests/<WorkflowSlug>/<TestClassName>.cs`
  - **Anti-coupling check:** <one line stating why this is
    implementation-independent>

## 5. Source operationalization reference

<!--
The Access click-sequence that operationalizes this workflow in
the existing app. Format per Decision 1 (structured prose,
numbered click-sequence):

  1. User opens <FormName> (e.g. via the navigation pane or
     `OpenOrderDetailsForm` from `frmOrderList::cmdAddOrder_Click`).
  2. User selects <something> from <ControlName>.
  3. User enters <something> into <ControlName>.
  4. <ControlName>_AfterUpdate fires; <observable consequence>.
  5. ...

Used for ambiguity resolution, not as a blueprint for the new
system. Cite the procedure or control name when helpful so the
reader can cross-reference the harvester output.
-->

1. <step>
2. <step>

## 6. Component / role mapping hints

<!--
Advisory-only hints for the AI builder. Format per Decision 2
(generic-now, implementation-agnostic):

- UI pattern: e.g. "fits master-detail with filter+grid+details",
  "needs separate create-form vs. edit-form", "is a wizard with
  N steps", "list-and-edit (no separate detail form)".
- Role gating: "edit operations require <role>" / "delete
  operations require <higher role>" / "no role gating observed".
- Component library mapping (optional): include only when
  Session 6 surfaces a clean mapping (e.g. role taxonomy from
  `modSecurity::HasPrivilege` call sites).

Do not pin specific Blazor component names unless the library
docs already cover them and the mapping is obvious. The downstream
implementation pilot is where this gets concrete.
-->

- **UI pattern:** <hint>
- **Role gating:** <hint>
- **Component library mapping (optional):** <hint or "deferred to library-readiness pass">

## 7. Open questions / ambiguity log

<!--
Required: at least one entry, even if just "no ambiguities found
in the source body." The act of looking is the checkpoint.

Each entry:
  - Question: what couldn't be determined from harvester output
  - Source-of-uncertainty: what evidence is missing
  - Recommended resolution path: human review of running app /
    domain expert / specific harvester gap to address
-->

- **Question:** <unknown>
  - **Source-of-uncertainty:** <what evidence is missing>
  - **Recommended resolution path:** <human review | domain expert | harvester gap | …>

---

## Author checklist (delete before merge)

- [ ] Section 1 written in 2-4 sentences, implementation-agnostic.
- [ ] Section 2 names actors, primary entity, related entities.
- [ ] Every Section-3 rule has both evidence and a confidence tag.
- [ ] No rule lacks a citation; all uncited claims moved to Section 7.
- [ ] Every Section-4 invariant cites its source procedure and test
      file path.
- [ ] Every Section-4 entry passes the anti-coupling check; UI-coupled
      invariants are reformulated or moved to Section 7.
- [ ] Section 5 follows the structured-prose format (Decision 1 = b).
- [ ] Section 6 follows the generic-now format (Decision 2 = a).
- [ ] Section 7 has at least one entry.
- [ ] Characterization tests for this workflow live under
      `tests/AccessHarvester.CharacterizationTests/<WorkflowSlug>/`
      and pass `dotnet test`.
