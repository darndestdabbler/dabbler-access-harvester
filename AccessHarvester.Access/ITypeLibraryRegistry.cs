namespace AccessHarvester.Access;

/// <summary>
/// Lookup contract for hand-curated DAO, ADO, Office, and Access object-model
/// type-member surfaces. The future VBA resolver
/// (<c>AccessHarvester.Vba.Resolver</c>, landed by session set
/// <c>access-resolver-symbol-table-integration</c>) calls into this contract
/// to resolve member references on declarations whose types come from a
/// type library (e.g., <c>Dim rs As DAO.Recordset</c> → the resolver looks
/// up <c>rs.MoveNext</c> here rather than in VBA source).
/// </summary>
/// <remarks>
/// <para>
/// Per Phase 4.5 of the parser plan: pragmatic, hand-curated, not introspected.
/// Unknown types and unknown members return a "not found" signal; the caller
/// translates that into <c>{ source: "external-unknown", typeLibrary: "&lt;name&gt;" }</c>
/// provenance. The registry itself never throws and never logs.
/// </para>
/// <para>
/// All lookups are case-insensitive (VBA itself is case-insensitive). Type
/// names are canonical-case (e.g., <c>"DAO.Recordset"</c>); a lookup for
/// <c>"dao.recordset"</c> succeeds and the returned <see cref="TypeInfo.Name"/>
/// carries the canonical casing.
/// </para>
/// </remarks>
public interface ITypeLibraryRegistry
{
    /// <summary>
    /// Look up a registered type by qualified name (e.g.,
    /// <c>"DAO.Recordset"</c>, <c>"Access.Form"</c>, <c>"Office.FileDialog"</c>).
    /// </summary>
    /// <param name="typeName">Qualified type name. Case-insensitive.</param>
    /// <param name="info">The matching <see cref="TypeInfo"/>, or <c>null</c> if not registered.</param>
    /// <returns><c>true</c> if the type is registered; <c>false</c> otherwise.</returns>
    bool TryGetType(string typeName, out TypeInfo? info);

    /// <summary>
    /// Look up a member on a registered type, walking the
    /// <see cref="TypeInfo.BaseTypeName"/> inheritance chain when the member
    /// is not declared directly on the named type (e.g.,
    /// <c>Access.TextBox.Name</c> resolves via <c>Access.Control.Name</c>).
    /// Returns <c>false</c> if either the type is not registered OR the
    /// member is not declared anywhere in the chain — the caller cannot
    /// distinguish the two cases from this method alone (call
    /// <see cref="TryGetType"/> first if the distinction matters).
    /// </summary>
    /// <param name="typeName">Qualified type name. Case-insensitive.</param>
    /// <param name="memberName">Member name. Case-insensitive.</param>
    /// <param name="info">The matching <see cref="MemberInfo"/>, or <c>null</c> if not found.</param>
    /// <returns><c>true</c> if the member is registered (directly or via a base type); <c>false</c> otherwise.</returns>
    bool TryGetMember(string typeName, string memberName, out MemberInfo? info);

    /// <summary>
    /// Resolve the default member of a registered type, if it has one. Used by
    /// the resolver to translate constructs like <c>rs!FieldName</c> (which
    /// dispatches through <c>Recordset.Fields</c>'s default member) and
    /// <c>txtFirstName.Value</c> being writable as <c>txtFirstName = "..."</c>.
    /// Walks <see cref="TypeInfo.BaseTypeName"/> when the type does not
    /// declare a default at its own level — a subtype's default takes
    /// precedence over its base's.
    /// </summary>
    /// <param name="typeName">Qualified type name. Case-insensitive.</param>
    /// <param name="info">The default <see cref="MemberInfo"/>, or <c>null</c> if the type (and its bases) have no default member.</param>
    /// <returns><c>true</c> if the type (or any base) has a registered default member; <c>false</c> otherwise.</returns>
    bool TryGetDefaultMember(string typeName, out MemberInfo? info);
}

/// <summary>The kind of a registered member.</summary>
public enum MemberKind
{
    /// <summary>A property (read or read/write).</summary>
    Property,

    /// <summary>A method (Sub or Function in VBA terms).</summary>
    Method,

    /// <summary>A field (rare in COM — most "fields" surface as properties).</summary>
    Field,

    /// <summary>An event (e.g., form events declared in form modules).</summary>
    Event,
}

/// <summary>
/// One entry in the registered type's member surface.
/// </summary>
public sealed record MemberInfo
{
    /// <summary>Canonical-case member name as the resolver should report it.</summary>
    public required string Name { get; init; }

    /// <summary>The member's role on the type.</summary>
    public required MemberKind Kind { get; init; }

    /// <summary>
    /// Qualified return type (e.g., <c>"DAO.Field"</c>, <c>"Long"</c>,
    /// <c>"Boolean"</c>, <c>"String"</c>). <c>null</c> for void methods,
    /// events, and members whose return type the curator has not pinned
    /// (Phase 4.5 is "pragmatic" — leave null rather than guess).
    /// </summary>
    public string? ReturnType { get; init; }

    /// <summary>
    /// <c>true</c> if this member is the type's default member (the one
    /// invoked by <c>obj(arg)</c> or <c>obj!member</c> shorthand). At most
    /// one member per type carries this flag.
    /// </summary>
    public bool IsDefault { get; init; }
}

/// <summary>
/// One registered type entry: the canonical name, the type library it lives
/// in, an optional base-type pointer for inheritance, an optional default-member
/// pointer, and the member surface.
/// </summary>
public sealed record TypeInfo
{
    /// <summary>
    /// Qualified canonical name (e.g., <c>"DAO.Recordset"</c>). The
    /// case here is what callers should display; lookups are case-insensitive.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// One of <c>"DAO"</c>, <c>"ADO"</c>, <c>"Office"</c>, <c>"Access"</c>.
    /// Used by the caller to populate the
    /// <c>{ source: "external-unknown", typeLibrary: "&lt;name&gt;" }</c>
    /// provenance shape when a member on a registered type is itself
    /// unknown.
    /// </summary>
    public required string TypeLibrary { get; init; }

    /// <summary>
    /// Qualified name of the base type this type inherits from, or
    /// <c>null</c> for root types. The registry's <c>TryGetMember</c>
    /// walks this chain when a member is not found directly on the
    /// current type — e.g., <c>Access.TextBox.Name</c> resolves via
    /// <c>Access.Control.Name</c>. The <see cref="DefaultMemberName"/>
    /// declared here takes precedence over the base type's default.
    /// The <c>TypeLibraryRegistry</c> implementation validates this
    /// chain at construction time and throws
    /// <see cref="InvalidOperationException"/> on missing bases or
    /// cycles, so runtime walks are guaranteed to be finite.
    /// </summary>
    public string? BaseTypeName { get; init; }

    /// <summary>
    /// Canonical-case name of the default member, or <c>null</c> if the
    /// type has no default member declared at this level. The named
    /// member may live in <see cref="Members"/> directly OR be inherited
    /// from a base type via <see cref="BaseTypeName"/> — for example
    /// <c>Access.TextBox</c> declares <c>DefaultMemberName="Value"</c>
    /// while the actual <c>Value</c> property record lives on
    /// <c>Access.DataBoundControl</c>. The registry validates at
    /// construction time that every declared default-member name is
    /// reachable in the inheritance chain.
    /// </summary>
    public string? DefaultMemberName { get; init; }

    /// <summary>
    /// Member surface keyed by member name. Use a case-insensitive
    /// comparer (<see cref="StringComparer.OrdinalIgnoreCase"/>) when
    /// constructing the underlying dictionary — VBA is case-insensitive
    /// and callers may not normalize. Only members declared directly on
    /// this type appear here; inherited members are accessed via the
    /// registry's <c>TryGetMember</c>, which walks
    /// <see cref="BaseTypeName"/>.
    /// </summary>
    public required IReadOnlyDictionary<string, MemberInfo> Members { get; init; }
}
