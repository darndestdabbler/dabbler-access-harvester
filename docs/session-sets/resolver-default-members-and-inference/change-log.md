```markdown
### Resolver: Default Members and Inference

Implemented Phases 3.5-3.7 of the VBA Parser Migration Plan.

- **DefaultMemberRegistry**: Landed registry to resolve default members for common VBA types (e.g., `TextBox.Value`, `Collection.Item`).
- **TypeInferencePass**: Introduced lightweight type inference for `Variant` declarations based on literal, new-expression, return-type, and default-member evidence, with confidence scoring.
- **CrossReferenceIndex**: Implemented single-pass cross-referencing to efficiently find all references to a given declaration.
- Resolver test count increased from 247 to 319.
- Deferred JSON serialization and `fullyQualifiedName` for the resolver type model to issue #11 (AccessObjectModelRegistry).
```