namespace AccessHarvester.Vba.Resolver.DefaultMembers;

// Whether the default member is a Property or a Method. The
// distinction matters for downstream translation when picking the
// right callable kind. The currently seeded set is all `Property` —
// `Item` on Collection / Dictionary / Controls is exposed as
// PROPERTYGET in the COM type info even though `coll(i)` looks
// method-shaped at the use site. The `Method` value remains in the
// public surface for any future seed (or consumer-extension layer)
// whose default is a genuinely side-effecting method call.
public enum DefaultMemberKind
{
    Property,
    Method,
}
