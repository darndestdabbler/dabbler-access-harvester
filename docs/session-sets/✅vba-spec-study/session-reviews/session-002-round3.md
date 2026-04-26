1. **Yes.** The README now explicitly states that **both** `In_Corpus` and `Corpus_Fixtures` are derived from the **12 hand-curated text fixtures under `docs/test-cases/` only**.

2. **Yes.** The Northwind disclosure is sufficient for a documents-only session:
   - it states `NorthwindDevEdition.accdb` was **not inspected**
   - explains **why**: binary `.accdb`, requires **Access COM** to extract VBA
   - states the platform constraint: **Windows-only**
   - states the session boundary: **out of scope**
   - identifies the deferred owner: **child set `#10 access-metadata-extraction`**

3. **VERIFIED**