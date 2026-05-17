# Gemini Recommendations for Remaining Work

> **Author:** Gemini 3.1 Pro (Preview)
> **Date:** 2026-05-17
> **Context:** Addressed to the human operator analyzing orchestrator feedback (Claude Code) and planning the roadmap to conclusion for `dabbler-access-harvester`.

---

## (A) The Dismissal of `../dabbler-platform` CLI/Codegen Tools

**Is Claude Code correct to dismiss the CRUD codegen tools?**
Yes. Claude Code's instinct aligns with the findings in our `strategy.md` and the recent `access-to-platform-migration-map.md`. The workflow packages we extract from Access (like *Add an Order*) are heavily laden with what we call **"behavior-bearing procedures"**—complex nested invariants, cross-form calculations, manual inventory allocations, and conditional validation (`ValidateForm`). 

Generic CRUD codegen and simple scaffolding pipelines excel at creating flat, greenfield endpoints over atomic tables. They fall apart when faced with the dense, stateful, event-driven orchestration found in mature Access applications. The `access-to-platform-migration-map.md` explicitly points out that we need to compose MudBlazor primitives with platform security constraints (`DabblerSecured`, `RestrictedFormField`, `DirtyAwareActionRow`) and wire them with nuanced domain services. Scaffolding tools usually produce tightly coupled entity-CRUD UI that has to be completely gutted to fit these custom invariant behaviors.

**What does this say about the CLI/Codegen work in `../dabbler-platform`?**
It implies that the platform's CRUD codegen solves a *different problem* (rapidly spinning up standard lookup-table management or greenfield simple forms). It simply isn't the right foundational tool for *migrating complex legacy Access applications*.

**Should we make changes in that repo?**
You don't necessarily need to "rip it out," but you should adjust its posture. Note the CRUD/Codegen tools in the platform's documentation as "intended for greenfield dictionary/lookup tables" and explicitly flag them as *not recommended* for complex Access migration paths. Keep the migration map relying on hand-composed primitives.

---

## (B) Strategy Coherence and the Backlog

You noted there are four in-progress session sets plus a `remaining-work-planner`, and that work feels like it's dragging on without an end. 

### Why it feels cluttered
This repo (`dabbler-access-harvester`) combines two distinct concerns:
1. Building the actual parser/extractor (the core C# tooling to read `.accdb` and VBA).
2. Proving the output by *doing* the Blazor migration (the Phase 1 and Phase 2 pilot).

Now that `blazor-add-an-order` and `workflow-package-pilot` (Phases 1 and 2) are complete and proved that the hybrid extraction methodology works, continuing to do heavy front-end Blazor implementation in this "harvester" repo threatens to drag the timeline out indefinitely.

### Recommendations for `strategy.md` and Remaining Work

1. **Draw a Line on Blazor Pilots:** `strategy.md` Phase 3 discusses options like `blazor-update-workflows` (Phase 3a). I recommend **deprioritizing further Blazor application development in this repo**. You have proven the shape works (Phase 2). Future Blazor implementation should be deferred to the planned third repo (the actual Access-to-Blazor migration repository). 
2. **Focus the `remaining-work-planner` on the Harvester CLI:** Let the planner strictly triage the remaining four in-progress session sets. If they are related to the core VBA/Jet SQL parser and the property extractor (e.g., getting the ASTs stable and the extraction CLI robust), finish them. If they are related to doing more mapping proofs, prune them. 
3. **Revise `strategy.md` to declare an "End State":** Update Phase 3 in `strategy.md` to state that the objective of this repository is now strictly to finalize the `AccessHarvester.Cli` output shapes, ensuring it produces the `.rdf.json`, IR, and structured VBA ASTs reliably.
4. **Halt "Cross-Repo Discovery" Work:** The `dabbler-platform-discovery` set had low participation because it was essentially a reading task trying to bridge domains prematurely. Treat the resulting mappings as "good enough" for downstream intelligence, but do not author new sets that focus on the platform until you are actually established in the third migration repo.

**Next Steps to Close Out:**
* Update `strategy.md`'s Phase 3 to explicitly transition from "Pilot Validation" to "Toolchain Finalization".
* Instruct the `remaining-work-planner` session set to aggressively prune the remaining 4 in-progress sets down to the minimum required to ship `AccessHarvester.Cli` version 1.0.