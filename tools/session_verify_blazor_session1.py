"""
Session 1 cross-provider verification for blazor-add-an-order.

Per the human's session-start directive, all routed work for this set is
in-conversation EXCEPT cross-provider session-verification, which routes
to Gemini Pro (overriding the router's default `task_type_overrides`
mapping for `session-verification` → gpt-5-4 — the human chose Gemini Pro
for this set).

The script writes the verifier's raw response to disk BEFORE any print
side effect (per `project-guidance.md` → "Persist Routed Output To Disk
Before Any Display Or Logging Side Effects").
"""

import importlib.util
import os
import sys
from pathlib import Path


def load_ai_router():
    spec = importlib.util.spec_from_file_location(
        "ai_router", "ai-router/__init__.py", submodule_search_locations=["ai-router"]
    )
    mod = importlib.util.module_from_spec(spec)
    sys.modules["ai_router"] = mod
    spec.loader.exec_module(mod)
    return mod


SESSION_SET = Path("docs/session-sets/blazor-add-an-order")
SESSION_NUMBER = 1

DELIVERABLES = [
    "output-tests/blazor-add-an-order/BlazorAddAnOrder.csproj",
    "output-tests/blazor-add-an-order/Program.cs",
    "output-tests/blazor-add-an-order/appsettings.json",
    "output-tests/blazor-add-an-order/appsettings.Development.json",
    "output-tests/blazor-add-an-order/Properties/launchSettings.json",
    "output-tests/blazor-add-an-order/Contracts/HealthResponse.cs",
    "output-tests/blazor-add-an-order/Services/HealthController.cs",
    "output-tests/blazor-add-an-order/Ui/_Imports.razor",
    "output-tests/blazor-add-an-order/Ui/App.razor",
    "output-tests/blazor-add-an-order/Ui/Routes.razor",
    "output-tests/blazor-add-an-order/Ui/MainLayout.razor",
    "output-tests/blazor-add-an-order/Ui/Pages/Home.razor",
    "docs/session-sets/blazor-add-an-order/ai-assignment.md",
]

SPEC_PATH = "docs/session-sets/blazor-add-an-order/spec.md"


def read(path: str) -> str:
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


def build_prompt() -> str:
    spec = read(SPEC_PATH)
    spec_session1_marker = "### Session 1 of 3"
    spec_session2_marker = "### Session 2 of 3"
    if spec_session1_marker in spec and spec_session2_marker in spec:
        i = spec.index(spec_session1_marker)
        j = spec.index(spec_session2_marker)
        spec_excerpt = spec[i:j].strip()
    else:
        spec_excerpt = spec

    parts = [
        "You are an independent cross-provider session verifier reviewing"
        " Session 1 of the `blazor-add-an-order` session set in the"
        " `dabbler-access-harvester` repository.",
        "",
        "## Orchestrator Disclosure",
        "",
        "- **Orchestrator:** Claude Code (Claude Opus 4.7).",
        "- **Routing directive (human, at session start):** No routed calls"
        " this set EXCEPT cross-provider session-verification, which the"
        " human directed to Gemini Pro (you).",
        "  This means Step 3.5's `ai-assignment.md` was authored"
        " in-conversation rather than via `route(task_type=\"analysis\")`,"
        " and the layer-separation code review was performed in-conversation"
        " rather than via `route(task_type=\"code-review\")`. The trade-off"
        " is acknowledged: please weigh the orchestrator's"
        " self-opinion-bias risk on the `ai-assignment.md` content"
        " specifically. The work product (csproj/Program.cs/Razor/Controller/"
        "DTO files) is independent of that bias.",
        "",
        "## Session 1 Goal (from spec)",
        "",
        "Two-layer scaffold + smoke endpoint for an in-process Blazor Server"
        " (UI) + Web API (Services) ASP.NET Core 10.0 host at"
        " `output-tests/blazor-add-an-order/`. UI must reach the service"
        " layer **only** via HttpClient against in-process REST endpoints —"
        " no direct service references from Ui. Three namespaces:"
        " `BlazorAddAnOrder.Ui`, `BlazorAddAnOrder.Services`,"
        " `BlazorAddAnOrder.Contracts` (the only allowed cross-layer"
        " reference).",
        "",
        "## Verification Focus (per spec Step 9)",
        "",
        "1. Layer separation discipline. `BlazorAddAnOrder.Ui` files must"
        " not reference any `BlazorAddAnOrder.Services` types. The"
        " composition root in `Program.cs` MAY reference both — that's the"
        " wiring root, not cross-layer leakage.",
        "2. `HttpClient`-only cross-layer communication. The UI's smoke call"
        " must go through `IHttpClientFactory.CreateClient(...)` against"
        " the configured base address, not a hard-coded URL or an"
        " `IServiceProvider`/MediatR shortcut.",
        "3. MudBlazor integration sanity. `MudThemeProvider`,"
        " `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`"
        " at the layout level. `AddMudServices()` registered. CSS and JS"
        " references present in the host page.",
        "4. `AccessHarvester.sln` builds with the new project added; tests"
        " still pass. Build result reported below.",
        "",
        "## Build & Test Results (this session)",
        "",
        "```",
        "dotnet build AccessHarvester.sln  -> 0 errors, 4 NU1903 warnings"
        " (pre-existing transitive Microsoft.Build.Utilities.Core advisory,"
        " not introduced by this session)",
        "",
        "dotnet test AccessHarvester.sln --no-build:",
        "  10 test assemblies passed",
        "  Aggregate ~2300 tests pass; 2 SkippableFact integration tests"
        " skipped (Access COM unavailable in this run path; expected)",
        "  No failures",
        "```",
        "",
        "## Files Out Of Scope (present in repo but not changed this session)",
        "",
        "- `tests/AccessHarvester.CharacterizationTests/AddAnOrder/*` —"
        " consumed by Session 2/3, not wired this session.",
        "- All other `AccessHarvester.*` projects — pre-existing, untouched.",
        "- `docs/session-sets/blazor-add-an-order/spec.md` — the input"
        " spec, not changed.",
        "",
        "## Spec Excerpt (Session 1 plan, verbatim)",
        "",
        "```markdown",
        spec_excerpt,
        "```",
        "",
        "## Files Created This Session",
        "",
    ]
    for path in DELIVERABLES:
        try:
            content = read(path)
        except FileNotFoundError:
            content = f"<file missing: {path}>"
        parts.extend([
            f"### `{path}`",
            "",
            "```",
            content,
            "```",
            "",
        ])

    parts.extend([
        "## Required JSON Response Block",
        "",
        "Your response MUST begin with detailed prose analysis covering each"
        " of the four verification-focus items above (layer separation,"
        " HttpClient-only boundary, MudBlazor sanity, build/test status)"
        " BEFORE the JSON block. Bare-paragraph or JSON-only verdicts have"
        " caused parser regressions; the prose-then-JSON ordering is"
        " required. Prefer false-positive ISSUES_FOUND over false-negative"
        " VERIFIED.",
        "",
        "After your prose analysis, output a JSON block in this EXACT format:",
        "",
        "```json",
        "{",
        "  \"verdict\": \"VERIFIED\" | \"ISSUES_FOUND\",",
        "  \"issues\": [",
        "    {",
        "      \"severity\": \"Critical\" | \"Major\" | \"Minor\",",
        "      \"location\": \"<file or check name>\",",
        "      \"summary\": \"<one-sentence description>\",",
        "      \"fix\": \"<recommended fix>\"",
        "    }",
        "  ]",
        "}",
        "```",
        "",
        "If verdict is VERIFIED, `issues` must be an empty array `[]`. Do"
        " not omit the JSON block.",
    ])
    return "\n".join(parts)


def main() -> None:
    ar = load_ai_router()

    prompt = build_prompt()
    raw_path = Path("tmp") / "blazor-add-an-order-session1-verifier-prompt.txt"
    raw_path.parent.mkdir(parents=True, exist_ok=True)
    with open(raw_path, "w", encoding="utf-8") as f:
        f.write(prompt)
    print(f"Verifier prompt written to {raw_path} ({len(prompt)} chars)")

    print("Routing to gemini-pro via query() to bypass session-verification"
          " task_type override (the human chose Gemini Pro for this set).")
    result = ar.query(
        model="gemini-pro",
        content=prompt,
        task_type="session-verification",
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )

    review_dir = SESSION_SET / "session-reviews"
    review_dir.mkdir(parents=True, exist_ok=True)
    review_path = review_dir / f"session-00{SESSION_NUMBER}.md"
    with open(review_path, "w", encoding="utf-8") as f:
        f.write(result.content)
    print(f"Verifier response written to {review_path}")

    print("---")
    print(f"Model: {result.model_name}")
    print(f"Input tokens: {result.input_tokens}")
    print(f"Output tokens: {result.output_tokens}")
    print(f"Cost: ${result.cost_usd:.4f}")
    print("---")
    print("First 80 chars of response:")
    print(result.content[:80].replace("\n", " "))


if __name__ == "__main__":
    main()
