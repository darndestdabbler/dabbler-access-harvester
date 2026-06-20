# AI Assignment — Set 019 Session 1

## Orchestrator Recommendation

The orchestrator for Session 1 should be Claude Opus 4.8. This session is highly mechanical, focusing on precise `csproj` modifications for `dotnet tool` packaging, followed by a local `pack`/`install`/smoke-run verification. The documentation update is also a straightforward addition of a new installation method. Claude Opus 4.8 is well-suited for these types of precise code and documentation edits, especially when the task involves applying specific XML properties and verifying command-line output.

## Routing Decision Table

| Step | Decision | Task Type | Target Tier |
|---|---|---|---|
| 1. Add tool packaging to AccessHarvester.Cli.csproj | Handle Directly | mechanical_code_edit | claude-code |
| 2. `dotnet pack` the CLI; install the .nupkg from a local folder feed. | Route | verification | claude-code |
| 3. Smoke-run installed `access-harvester` | Route | verification | claude-code |
| 4. Extend docs/harvester/install.md | Handle Directly | doc_edit | claude-code |
| Cross-provider session verification | Route | verification | gpt-5-4 / gemini-pro |

## Rough Cost Estimate

Approximately $1.50 - $2.50. This estimate accounts for the mechanical `csproj` and documentation edits, which are relatively low token cost, and the routed verification steps including a mandatory cross-provider check, which will incur additional processing but are still focused tasks.

## Session 2 Orchestrator Recommendation

Session 2 should be orchestrated by Claude Opus 4.8.