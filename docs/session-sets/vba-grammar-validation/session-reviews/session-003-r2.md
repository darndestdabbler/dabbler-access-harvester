**ISSUES_FOUND**

- **Minor Issue** → `Harness run: Session 2 of vba-grammar-validation (#5)` → `Open items for #6`, item 2  
  **Fix:** Remove or rewrite the note _“The `module` rule should end with `EOF`...”_. The grammar already has `module : NEWLINE* moduleItem* EOF ;`, so full-input consumption for the `module` entry point is already enforced. Round-1 Major Issue 2 is resolved; this leftover note is stale.

- **Minor Issue** → `Harness run: Session 3 of vba-lexical-and-expression-grammar` → `Corpus` paragraph  
  **Fix:** Change _“All 12 fixtures under `docs/test-cases/` plus the 5 target fixtures...”_ to _“All 12 fixtures under `docs/test-cases/`, including the 5 target fixtures...”_. Current wording implies double-counting beyond the 12 listed results.

Round-1 findings are otherwise addressed. The `module : NEWLINE* moduleItem* EOF ;` rule fully resolves the full-input-consumption concern for parses started at `module`.