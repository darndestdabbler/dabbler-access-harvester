Re-verified the round-2 fix. The added early return in `IsInvalidDataToken`:

```csharp
if (IsBinary(typeCode)) return false;
```

does resolve the reported issue. For `DbBinary` (9), `DbLongBinary` (11), and `DbVarBinary` (17), binary/hex payloads no longer fall through to the numeric-token validation path, so direct callers cannot get a false `InvalidDataToken` result.

This is also regression-safe:

- It does not affect the actual generator flow, where binary columns are already excluded upstream via `IsBinary(...)` before row processing.
- It preserves the existing omission behavior for binary families; `Format(...)` still has no binary formatting branch, which remains consistent with that upstream contract.
- The added theory explicitly covers all three binary families with representative hex tokens.
- Reported build/tests are clean (`42/42`), and the change is golden-neutral.

```json
{"verdict":"VERIFIED","issues":[]}
```