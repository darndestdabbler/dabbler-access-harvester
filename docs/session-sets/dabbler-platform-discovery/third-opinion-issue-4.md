# Third opinion on Issue 4 (sample .rdf.json copying)

Model: opus
Cost: $0.2527

---

## Analysis

The critical boundary sentence is: **"Cite paths and commit SHAs; do not fork content."** The word "content" is intentionally broader than the preceding phrase "source code." The spec first names the specific concern ("No copying of platform source code"), then restates the principle more expansively: the output is a *map*, not a mirror, and the approved referencing mechanism is citation, not reproduction. The 7 snippets in the doc are verbatim fragments from named `.rdf.json` files in the sibling repo, attributed by exact path. That is forking content by any plain reading, regardless of whether JSON data qualifies as "source code" in the narrower first sentence.

The orchestrator's strongest argument is that Feature 2 explicitly requires "example documents" at "sufficient fidelity that an interpretation agent … could *generate* a valid `report.rdf.json`." That is a real deliverable requirement, and it does create genuine tension with the boundary. But in spec interpretation, boundaries constrain deliverables — they are not overridden by them. The way to reconcile the two is not to ignore the boundary, but to fulfill the deliverable *within* it. The Feature 2 language says "example documents"; it does not say "example documents copied from the sibling repo." Synthetic examples — authored fresh in this repo, grounded in the schema tables that already exist in the doc — satisfy Feature 2's fidelity requirement without violating "do not fork content." The orchestrator's worry that a model "struggles to interpret the schema" is a practical concern, but the doc already contains comprehensive per-field tables derived from the C# models; a synthetic example constructed to match those tables is no less authoritative than a copied one, and avoids the boundary violation.

The verifier is therefore substantively correct that the current snippets violate the boundary, but the proposed fix ("or with prose descriptions that cite the source paths without reproducing their content") undershoots Feature 2's explicit requirement for example documents. The right fix is: **replace each verbatim sibling-repo snippet with a synthetic minimal example authored in this repo that exercises the documented fields, and append a "See also" citation pointing to the canonical sibling-repo path for real-world reference.** This satisfies "do not fork content," honors Feature 2's "example documents" requirement, and preserves the traceability the orchestrator correctly values.

VERDICT: C
