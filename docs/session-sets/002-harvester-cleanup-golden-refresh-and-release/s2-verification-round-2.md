1. **Issue 1 — Resolved.**  
   The `install.md` updates are accurate and internally consistent with the stated ground truth:
   - `validate` now correctly states it uses the manifest’s declared `schemaVersion` and supports embedded schemas **v1.0 and v1.1**.
   - The `validate` exit-code description is now correct for either declared schema version.
   - The smoke-check example showing validation against **schema v1.1** is consistent with both a fresh `run` output and `examples/northwind-output.json`.
   - The note about embedded schemas now correctly references both `harvester-schema.v1.0.json` and `harvester-schema.v1.1.json`, and correctly says `examples/northwind-output.json` declares **1.1**.  
   I do not see a new error introduced by these doc changes.

2. **Issue 2 — Resolved.**  
   This is the correct release discipline:
   - get explicit maintainer approval **before** pushing the `v1.2.0` tag, since the tag push creates the public tag and draft release;
   - treat the **CI-built artifact** as authoritative;
   - replace `<FILLED_AT_PUBLISH>` with the real SHA-256 from that authoritative CI artifact **before publishing** the release.  
   No new error introduced.

3. **Issue 3 — Resolved.**  
   The revised schema wording removes the ambiguity for users upgrading from **published v1.0.0** while remaining accurate:
   - it clearly distinguishes “unchanged from unpublished v1.1.0 draft” from the published upgrade path;
   - it correctly states that published-channel consumers are moving **v1.0 → v1.1**;
   - it accurately describes the bump as **additive/backward-compatible** and notes that `validate` accepts both versions.  
   No new error introduced.

{"verdict":"VERIFIED","issues":[]}