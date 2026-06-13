"""Round-2 cross-provider verification for 011 Session 1 — responds to R1
(ISSUES_FOUND: 1 Major A, 1 Minor B) with the disposition + evidence.
"""
import os

from ai_router import route

SESSION_SET = "docs/session-sets/011-resolver-and-navigation-wiring"
OUT = os.path.join(SESSION_SET, "s1-verification-round-2.md")
A_COMMENT = open(r"C:/temp/vahs011_A_comment.txt", encoding="utf-8").read()

PROMPT = r"""
You are the cross-provider verifier (different provider than the Claude
orchestrator). This is Round 2 for 011 Session 1 (resolver wiring, F-3-4). Round 1
returned ISSUES_FOUND with one Major (A) and one Minor (B). No new production
BEHAVIOR changed since R1 — only a clarifying code comment plus a new hermetic
pin were added. Re-assess A and B against the evidence below. Begin with prose
analysis, then the JSON block. Prefer false-positive ISSUES_FOUND over a
false-negative VERIFIED, but do not re-raise a finding the evidence refutes.

# R1 Major A — "degraded modules can contaminate the shared ProjectScope"
R1 concern: with one shared ProjectScope, a module that fails scope-build or
pass-2 resolution stays registered in `project.Children`, so siblings could
resolve cross-module references against a degraded/omitted module.

## Disposition: rebutted with reachability evidence + documented; no code change.

**Two failure cases, analyzed separately:**

**(A1) Scope-build failure → partial-scope leak. UNREACHABLE from real input.**
`ScopeBuildingVisitor.BuildModuleScope` creates `new ModuleScope(parent: project)`
(which auto-attaches to project.Children via the Scope base ctor) and THEN walks
the AST. The ONLY exceptions the visitor can throw AFTER that attach are the
defensive `CurrentProcedureScope ?? throw InvalidOperationException(...)` guards in
`VisitLocalDeclarationStatement` / `VisitLocalConstantStatement`. Those guards are
unreachable from a parser-produced module AST, because:
  - `LocalDeclarationStatement` / `LocalConstantStatement` derive from `Statement`,
    and `Statement` is NOT a `ModuleItem` (verified: `Statement : VbaSyntaxNode`;
    `Module.Items` is `IReadOnlyList<ModuleItem>`). A local declaration therefore
    cannot appear at module level.
  - The visitor only walks statements inside a procedure body (via EnterProcedure,
    which pushes a ProcedureScope first), so when those visits run,
    `CurrentProcedureScope` is always non-null. The guard never fires.
Also note the pipeline's `VBA_PARSE_FAILURE` catch is dominated by PARSE failures
(the F-3-2 AstBuilder NRE class), which throw in `LoadAndParse` — BEFORE
`new ModuleScope` exists — so they create no scope and attach nothing.
Per the project's Lean-by-default principle ("do not add a defensive try/catch
around code with no realistic failure path") and Evidence-over-inference, adding
a transactional detach for an unreachable throw would be untestable dead code, so
the code change was declined. The degradation contract is now documented in
Pipeline.cs:

```csharp
%s
```

**(A2) Resolve failure → complete scope visible to siblings. REACHABLE but
CORRECT, not a leak.** A pass-2 resolution failure happens only AFTER scope-build
succeeded, so the module's scope is COMPLETE and its Public declarations are
accurate (they genuinely exist in source). A sibling's bare call resolving to that
module is therefore a CORRECT resolution, even though the failed module's own
ParserOutput is a kind-correct stub. Leaving it visible is the right behavior, not
contamination. (The HomeHealthCare golden has zero pass-2 resolve failures, so this
path is also empirically silent there.)

Question for you: do you accept that A1 is unreachable from parser output and A2 is
correct-as-is? If you can name a concrete parser-producible input that makes
BuildModuleScope throw AFTER attaching the scope, that would refute A1 — please
provide it; otherwise A1 should be closed.

# R1 Minor B — "owningClass name alignment is brittle when Name/ModuleName/filename diverge"
## Disposition: discharged with a hermetic pin.
The alignment is structural: the parse-loop module name = the kb/ir file-name stem
= `FormMetadata.Name` (FormMetadataExtractor.Extract is called with that same
file-name string as `name`), and `owningClass.Name` is set to that same module
name. The registry indexes forms by BOTH `Name` and `ModuleName`, and
`TryResolveControlOnForm` falls back from the by-ModuleName index to the by-Name
index. So `owningClass.Name == FormMetadata.Name` always, and resolution rides the
by-Name index regardless of ModuleName.

New pin proving the divergence case (ModuleName != form/file name):
```csharp
[Fact]
public void MeControl_ResolvesViaByNameIndex_WhenModuleNameDivergesFromFormName()
{
    using var tree = StagedTree.Create();
    tree.AddKbForm("Forms", "frmDiverge",
        "CodeBehindForm\r\nPrivate Sub Form_Load()\r\n    Me.txtName = \"\"\r\nEnd Sub\r\n");
    // ir VbaCode carries Attribute VB_Name = "Form_TotallyDifferent" → the form's
    // ModuleName diverges from its Name ("frmDiverge"); the by-ModuleName index
    // misses and the by-Name index (= file name = owningClass.Name) resolves it.
    tree.AddIrForm("Forms", "frmDiverge",
        controls: new[] { ("TextBox", "txtName") }, moduleName: "Form_TotallyDifferent");
    var manifest = tree.EmitAndReadManifest();
    var reference = FindReference(manifest, "frmDiverge", "Me.txtName");
    Assert.Equal("Resolved", reference.GetProperty("ResolutionStatus").GetString());
    Assert.Equal("ObjectModel", reference.GetProperty("ReferenceKind").GetString());
}
```
This pin is GREEN (5/5 ManifestResolverWiringTests pass). All other R1 facts are
unchanged: hermetic suites green (Resolver 410, Pipeline 835, SymbolDb 71,
AccessHarvester F-3-4 pins + ingestion), golden regen byte-consistent + gate green,
every golden delta mapped to F-3-4, items 1-4 unchanged.

Output a JSON block in this exact format after your prose:
```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    { "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>" }
  ]
}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit the block.
""" % A_COMMENT


def main() -> None:
    result = route(content=PROMPT, task_type="session-verification",
                   complexity_hint=70, session_set=SESSION_SET, session_number=1)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(result.content)
    print(f"Wrote {OUT} | model={result.model_name} | cost=${result.cost_usd:.4f}")


if __name__ == "__main__":
    main()
