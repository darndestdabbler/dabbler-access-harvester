# Recommendation: Set 015

## Recommendation: `015-unresolved-items-completeness`

Proceed with a proactive set targeting **DW-11 (`unresolved-items-completeness`)**.

### Justification

While the project is in a consumer-driven state, DW-11 represents a known, latent correctness defect, not a request for new functionality. The existence of "proven reproducers" elevates this from "unproven need" to a demonstrable flaw in the harvester's aggregation logic.

Addressing this undercount proactively hardens the tool's core reliability. It is the most valuable candidate because it fixes a known bug, preventing future invalid-data incidents for any consumer whose database exercises this specific code path. This aligns with engineering best practice to prioritize correctness bugs over feature completeness. The other DW bundles address syntax coverage or new wiring, which are lower-priority concerns than a proven counting error.

## Runner-Up: `STOP/hold`

Holding for a consumer report is the most disciplined application of the "consumer-driven-only" principle. With the major 006-arc concluded, a pause is a valid and low-risk alternative. This remains the second-best option if maximum principle adherence is desired over proactive hardening.

## Orchestrator Tier

**Mechanical.** The work involves fixing a specific undercount in existing aggregation logic, which is a detail-oriented task within the established architecture, not high-level synthesis.