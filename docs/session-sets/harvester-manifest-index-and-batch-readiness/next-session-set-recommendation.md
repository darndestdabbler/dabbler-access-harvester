**Slug:** `harvester-parallel-com-test-cleanup`

**Orchestrator:** `claude sonnet-4.6 @ effort=medium`

**Rationale:** The repository is in maintenance, prioritizing stability and bug fixes. Addressing the known test suite flakiness caused by parallel COM contention is the highest-leverage maintenance task available. Resolving this technical debt ensures the CI pipeline remains a reliable gate for future schema adjustments and bug fixes, which is the repository's core mandate post-v1.0.0. Deferring this cleanup risks complicating future maintenance, while the alternative (`harvester-sql-migration-generator`) constitutes a new feature, violating the current maintenance-mode strategy.

**Estimated routed cost:** Low (<$0.50)