R1 finding 1 — resolved. The updated test file no longer calls `CodePagesEncodingProvider.RegisterProvider(...)` and no longer constructs a cp1252 `Encoding` instance in test code. The non-ASCII fixtures are now built from literal byte arrays plus ASCII-only segments, so the test inputs themselves do not depend on prior provider registration. That removes the original masking condition. Access through `SaveAsTextTranslator.Forms` / `.Modules` / `.Macros` is now the relevant trigger, so if the translator stopped self-registering code pages, these cp1252 tests would surface it. The acknowledged process-global caveat is real, but it does not reintroduce the specific defect from R1.

R1 finding 2 — resolved. `Cp1252Module_ReadAllTextPath_DecodesAccentedLiteral_NoReplacementChar` directly exercises the module branch that returns `VbaCode = text`, using a raw cp1252 `0xE9` byte in `Café`. The assertions check both positive decoding (`"Café"`) and the absence of U+FFFD. That is the missing coverage from R1: a regression that reverted only the `ReadAllText` path to UTF-8 would now fail.

R1 finding 3 — resolved. `Utf8Bom_HonouredOverCp1252Fallback` adds the previously missing BOM-based fixture and uses a payload that would visibly misdecode under blind cp1252 (`é` as UTF-8 bytes becoming `Ã©`). Asserting `"Café"` and rejecting `"Ã"` is an effective pin that BOM-indicated UTF-8 still wins over the cp1252 fallback. Within the scope of the R1 concern, this closes the BOM-precedence gap.

New issues — none identified. The strengthened tests address the three prior posture gaps without introducing a new substantive weakness in the stated scope.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```