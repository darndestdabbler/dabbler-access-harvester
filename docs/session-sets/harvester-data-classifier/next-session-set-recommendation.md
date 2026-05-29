### Next session-set recommendation (after harvester-data-classifier): `harvester-cli-distribution`

**Orchestrator:** `claude-sonnet-4-6` @ effort=medium

> Mechanical normalization: the analysis call named the model in marketing form
> (`Claude 3.5 Sonnet @ effort=moderate`). Anthropic's current Sonnet is
> `claude-sonnet-4-6` (`claude-sonnet-4-6` ID per CLAUDE.md), and the supported
> effort levels are `low|medium|high|max` (per `lessons-learned.md` →
> "`effort:xhigh` Is Not A Valid Anthropic API Effort Level"). `medium` is the
> nearest valid mapping of the analysis's intent.

**Rationale:** With the core harvester tool and its sanitization extension now complete and packaged, the most logical next step is to establish a public distribution channel. The `harvester-cli-distribution` set directly addresses this by creating the release workflow and user-facing installation documentation, closing the loop on the original "finish line" work. This task consists of well-defined implementation—crafting a GitHub Actions workflow and writing technical documentation—which is an ideal fit for a Tier-2 model's capabilities, balancing cost and performance without requiring the deep synthesis of a Tier-3 orchestrator.

**Estimated routed cost band:** Moderate ($0.50-$2.00)