# Verification Round 1

ISSUES_FOUND

- `tools/validate_against_yaml.py` retains stale sibling-repo defaults, so its documented/default invocation is not repo-valid here
  - category: Correctness
  - severity: Major
  - specific evidence: The script header documents no-arg usage with defaults under `../sample-output/AccessMetaData/NorthwindDevEdition/ir` and `../AccessExporter.Core/Resources/property-defaults.json`, and still refers to `AccessExporter` rather than `AccessHarvester`. The repo’s project path is `AccessHarvester.Core/`, and `sample-output/` was explicitly kept out-of-scope/not imported. The known disclosure also confirms the argparse defaults still reference the sibling layout.
  - recommended fix: Update `validate_against_yaml.py` help text and argparse defaults to repo-valid paths (`AccessHarvester.Core/...`), and either require `--ir-root` explicitly or default it to a location that exists in this repo’s documented workflow; also rename stale `AccessExporter` references to `AccessHarvester` for consistency.

---

# Verification Round 2

VERIFIED
