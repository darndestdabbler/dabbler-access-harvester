VERIFIED

1. The four new absent-case tests are present in the excerpts and correctly written:
   - `FormMetadataExtractorTests`: `Extract_FilterAbsent_IsNull`, `Extract_OrderByAbsent_IsNull`
   - `ReportMetadataExtractorTests`: `Extract_FilterAbsent_IsNull`, `Extract_OrderByAbsent_IsNull`
   Each uses IR JSON where the target key is genuinely absent from `Properties` and asserts the extracted field is `null`, which is the right regression coverage.

2. The `Type` doc fix is accurate and no longer misleading:
   - `AccessControl.Type` is documented as pass-through from IR JSON.
   - The form detail/body band is now correctly called out as `Type = "Section"` with `Name = "Detail"`.
   - The worked example now matches that rule.

3. No new issues are evident in the provided test/doc changes.