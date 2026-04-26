using System.Linq;

namespace AccessHarvester.Access;

/// <summary>
/// Hand-curated registry of common DAO, ADO, Office, and Access object-model
/// type-member surfaces. The seed targets the members the VBA resolver will
/// most often encounter in test fixtures <c>01</c>-<c>12</c> plus the
/// <c>NorthwindDevEdition.accdb</c> sample. Adding a new type or new
/// members on an existing type is a single-file edit — append a
/// <c>Build*()</c> entry and register it in the constructor.
/// </summary>
/// <remarks>
/// <para>
/// Per Phase 4.5 of the parser plan: pragmatic, hand-curated, not introspected.
/// "Most common members" — not exhaustive. Members beyond this seed surface
/// resolve as <c>{ source: "external-unknown", typeLibrary: "&lt;name&gt;" }</c>
/// at the caller level, and the registry never throws.
/// </para>
/// <para>
/// <b>Inheritance is modeled via <see cref="TypeInfo.BaseTypeName"/>.</b>
/// Common Access control subtypes (<c>Access.TextBox</c>,
/// <c>Access.ComboBox</c>, <c>Access.ListBox</c>, <c>Access.CommandButton</c>,
/// <c>Access.Label</c>) are registered with their distinguishing members and
/// default members; the universal control surface lives on
/// <c>Access.Control</c>. <see cref="TryGetMember"/> walks the
/// <see cref="TypeInfo.BaseTypeName"/> chain, so a lookup for
/// <c>Access.TextBox.Name</c> resolves via <c>Access.Control.Name</c>.
/// </para>
/// <para>
/// All lookups are case-insensitive. The registry is immutable after
/// construction.
/// </para>
/// <para>
/// <b>Graceful degradation contract:</b> the registry never throws — null
/// or empty type/member names return <c>false</c> rather than raising
/// <see cref="ArgumentNullException"/>. Unknown types and unknown members
/// likewise return <c>false</c>; the caller emits the
/// <c>{ source: "external-unknown", typeLibrary: "&lt;name&gt;" }</c>
/// provenance.
/// </para>
/// </remarks>
public sealed class TypeLibraryRegistry : ITypeLibraryRegistry
{
    private readonly IReadOnlyDictionary<string, TypeInfo> _types;

    /// <summary>
    /// Canonical-case names of every registered type. Exposed on the
    /// implementation (not the <see cref="ITypeLibraryRegistry"/> contract)
    /// to support exhaustive-coverage tests; downstream consumers should use
    /// <see cref="TryGetType"/> for ordinary lookups. Order is the
    /// registration order in the constructor.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredTypeNames => _types.Keys.ToList().AsReadOnly();

    public TypeLibraryRegistry()
    {
        var types = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);

        // DAO ----------------------------------------------------------------
        Register(types, BuildDaoRecordset());
        Register(types, BuildDaoField());
        Register(types, BuildDaoDatabase());
        Register(types, BuildDaoQueryDef());
        Register(types, BuildDaoTableDef());
        Register(types, BuildDaoFields());
        Register(types, BuildDaoRecordsets());

        // ADO ----------------------------------------------------------------
        Register(types, BuildAdoRecordset());
        Register(types, BuildAdoConnection());
        Register(types, BuildAdoCommand());
        Register(types, BuildAdoField());

        // Office -------------------------------------------------------------
        Register(types, BuildOfficeFileDialog());

        // ADO collection (referenced as default-member return type by ADO.Recordset)
        Register(types, BuildAdoFields());

        // Access -------------------------------------------------------------
        Register(types, BuildAccessForm());
        Register(types, BuildAccessReport());
        Register(types, BuildAccessControl());
        // Intermediate base for data-bound controls (TextBox, ComboBox, ListBox).
        // Keeps the data-bound surface (ControlSource, Value, BeforeUpdate, etc.)
        // off of non-data-bound subtypes (Label, CommandButton).
        Register(types, BuildAccessDataBoundControl());
        // Common control subtypes — register AFTER their bases so the
        // BaseTypeName references resolve. Construction-time validation
        // below catches any oversight.
        Register(types, BuildAccessTextBox());
        Register(types, BuildAccessComboBox());
        Register(types, BuildAccessListBox());
        Register(types, BuildAccessCommandButton());
        Register(types, BuildAccessLabel());

        Register(types, BuildAccessModule());
        Register(types, BuildAccessDoCmd());
        Register(types, BuildAccessApplication());

        _types = types;

        ValidateRegistryAtConstruction();
    }

    /// <summary>
    /// Construction-time validation: catches cycles in the BaseTypeName chain,
    /// missing base types, and unresolved DefaultMemberName references. Fail-fast
    /// at construction time so curation errors surface as <see cref="InvalidOperationException"/>
    /// at startup rather than silently degrading lookup behavior at runtime.
    /// </summary>
    private void ValidateRegistryAtConstruction()
    {
        foreach (var (name, t) in _types)
        {
            // Validate BaseTypeName chain: every base must exist; the chain
            // must terminate (no cycles).
            if (t.BaseTypeName is not null)
            {
                if (!_types.ContainsKey(t.BaseTypeName))
                {
                    throw new InvalidOperationException(
                        $"TypeLibraryRegistry: type '{name}' declares BaseTypeName='{t.BaseTypeName}' but no such type is registered.");
                }
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
                var cursor = t.BaseTypeName;
                while (cursor is not null)
                {
                    if (!visited.Add(cursor))
                    {
                        throw new InvalidOperationException(
                            $"TypeLibraryRegistry: cycle detected in BaseTypeName chain starting at '{name}'.");
                    }
                    if (!_types.TryGetValue(cursor, out var bt)) break;
                    cursor = bt.BaseTypeName;
                }
            }

            // Validate DefaultMemberName resolves (directly or via inheritance).
            if (t.DefaultMemberName is not null)
            {
                var cursor = name;
                bool resolved = false;
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (cursor is not null && visited.Add(cursor))
                {
                    if (!_types.TryGetValue(cursor, out var bt)) break;
                    if (bt.Members.ContainsKey(t.DefaultMemberName))
                    {
                        resolved = true;
                        break;
                    }
                    cursor = bt.BaseTypeName;
                }
                if (!resolved)
                {
                    throw new InvalidOperationException(
                        $"TypeLibraryRegistry: type '{name}' declares DefaultMemberName='{t.DefaultMemberName}' but no member with that name is reachable via the BaseTypeName chain.");
                }
            }
        }
    }

    public bool TryGetType(string typeName, out TypeInfo? info)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            info = null;
            return false;
        }
        if (_types.TryGetValue(typeName, out var t))
        {
            info = t;
            return true;
        }
        info = null;
        return false;
    }

    public bool TryGetMember(string typeName, string memberName, out MemberInfo? info)
    {
        if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(memberName))
        {
            info = null;
            return false;
        }

        // First find the requested type's effective default-member name (walk
        // inheritance to the first declared DefaultMemberName). This is what
        // makes the IsDefault projection consistent between TryGetMember and
        // TryGetDefaultMember — both return IsDefault=true for the type's
        // effective default member regardless of where the underlying record
        // lives in the inheritance chain.
        string? effectiveDefault = ResolveEffectiveDefaultMemberName(typeName);

        // Walk the BaseTypeName chain looking for the member. ValidateRegistryAtConstruction
        // already prevents cycles, so the walk is bounded by the chain depth;
        // the 32-hop ceiling is defense-in-depth for code paths added later.
        var current = typeName;
        for (int hops = 0; hops < 32; hops++)
        {
            if (!_types.TryGetValue(current, out var t)) break;
            if (t.Members.TryGetValue(memberName, out var m))
            {
                // Project IsDefault to be requested-type-relative in BOTH
                // directions: true if this member is the effective default
                // of the requested type, false otherwise. The two-way
                // projection matters for the case where a future subtype
                // inherits a member marked IsDefault=true on its base but
                // the subtype itself declares a different default — in
                // that scenario, callers querying via the subtype must not
                // see the inherited member tagged as the subtype's default.
                bool isEffectiveDefault =
                    effectiveDefault is not null
                    && string.Equals(m.Name, effectiveDefault, StringComparison.OrdinalIgnoreCase);
                info = m.IsDefault == isEffectiveDefault
                    ? m
                    : m with { IsDefault = isEffectiveDefault };
                return true;
            }
            if (t.BaseTypeName is null) break;
            current = t.BaseTypeName;
        }
        info = null;
        return false;
    }

    /// <summary>
    /// Walk the BaseTypeName chain starting at <paramref name="typeName"/> and
    /// return the first declared <see cref="TypeInfo.DefaultMemberName"/>. A
    /// subtype's declared default takes precedence over its base's. Returns
    /// <c>null</c> if no type in the chain declares a default.
    /// </summary>
    private string? ResolveEffectiveDefaultMemberName(string typeName)
    {
        var current = typeName;
        for (int hops = 0; hops < 32; hops++)
        {
            if (!_types.TryGetValue(current, out var t)) return null;
            if (t.DefaultMemberName is not null) return t.DefaultMemberName;
            if (t.BaseTypeName is null) return null;
            current = t.BaseTypeName;
        }
        return null;
    }

    public bool TryGetDefaultMember(string typeName, out MemberInfo? info)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            info = null;
            return false;
        }
        var current = typeName;
        for (int hops = 0; hops < 32; hops++)
        {
            if (!_types.TryGetValue(current, out var t)) break;
            if (t.DefaultMemberName is not null)
            {
                // The default may live on this type or be inherited; reuse
                // TryGetMember so the inheritance walk also covers the
                // default-member-name lookup.
                if (TryGetMember(t.Name, t.DefaultMemberName, out var found))
                {
                    // Project IsDefault=true onto the returned MemberInfo
                    // when accessed via this default-member path. The
                    // underlying entry on a base type may be IsDefault=false
                    // (e.g., Access.Control.Value is not Control's default,
                    // but it IS Access.TextBox's default — TextBox declares
                    // it via DefaultMemberName="Value"). Records' with-
                    // expressions give us a clean projected copy.
                    info = found!.IsDefault ? found : found with { IsDefault = true };
                    return true;
                }
            }
            if (t.BaseTypeName is null) break;
            current = t.BaseTypeName;
        }
        info = null;
        return false;
    }

    private static void Register(Dictionary<string, TypeInfo> types, TypeInfo entry)
    {
        // Fail-fast on duplicate registration. Catches accidental
        // double-Register or an entry whose Build*() method returns the
        // wrong canonical name.
        if (types.ContainsKey(entry.Name))
        {
            throw new InvalidOperationException(
                $"TypeLibraryRegistry: type '{entry.Name}' is registered more than once. Each type must be registered exactly once.");
        }
        types[entry.Name] = entry;
    }

    /// <summary>
    /// Build a member dictionary from the supplied entries. Fails fast on
    /// duplicate names — a curator typo or a property/event collision is a
    /// curation error that should be visible at construction time, not
    /// silently swallowed via last-write-wins.
    /// </summary>
    private static IReadOnlyDictionary<string, MemberInfo> Members(params MemberInfo[] members)
    {
        var dict = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in members)
        {
            if (!dict.TryAdd(m.Name, m))
            {
                throw new InvalidOperationException(
                    $"TypeLibraryRegistry: duplicate member name '{m.Name}' in a single type's member list. " +
                    "If a property and an event share a name (e.g., Form.Dirty, Report.Page), curate only one " +
                    "(prefer the property — event-handler recognition is name-pattern-based, not registry-based).");
            }
        }
        return dict;
    }

    private static MemberInfo Prop(string name, string? returnType = null, bool isDefault = false) =>
        new() { Name = name, Kind = MemberKind.Property, ReturnType = returnType, IsDefault = isDefault };

    private static MemberInfo Method(string name, string? returnType = null) =>
        new() { Name = name, Kind = MemberKind.Method, ReturnType = returnType };

    private static MemberInfo Event(string name) =>
        new() { Name = name, Kind = MemberKind.Event, ReturnType = null };

    // ========================================================================
    // DAO
    // ========================================================================
    // Source: Microsoft Office VBA Reference - DAO Object Library (MSDN /
    // learn.microsoft.com/en-us/office/vba/access/concepts/data-access-objects/).
    // Cross-checked against the test-builder/ Python tool's fixture-emitting
    // expectations and Recordset patterns documented in the parser plan
    // Appendix A.
    // ========================================================================

    /// <summary>
    /// DAO.Recordset — Source: MSDN DAO Recordset object reference
    /// (learn.microsoft.com/en-us/office/client-developer/access/desktop-database-reference/recordset-object-dao).
    /// Most-common navigation, edit, find/seek, and lifecycle members. Default
    /// member is Fields (so <c>rs!FieldName</c> dispatches through it).
    /// </summary>
    private static TypeInfo BuildDaoRecordset() => new()
    {
        Name = "DAO.Recordset",
        TypeLibrary = "DAO",
        DefaultMemberName = "Fields",
        Members = Members(
            // Navigation methods
            Method("MoveFirst"), Method("MoveLast"), Method("MoveNext"),
            Method("MovePrevious"), Method("Move"),
            // Edit methods
            Method("AddNew"), Method("Edit"), Method("Update"),
            Method("CancelUpdate"), Method("Delete"),
            // Find/seek methods
            Method("FindFirst"), Method("FindLast"),
            Method("FindNext"), Method("FindPrevious"), Method("Seek"),
            // Lifecycle (note: OpenRecordset lives on Database/QueryDef/TableDef,
            // not on Recordset — use Clone() for sub-recordset creation)
            Method("Close"), Method("Clone", "DAO.Recordset"),
            Method("Requery"),
            // State properties
            Prop("BOF", "Boolean"), Prop("EOF", "Boolean"),
            Prop("Bookmark"), Prop("Bookmarkable", "Boolean"),
            Prop("AbsolutePosition", "Long"),
            Prop("RecordCount", "Long"),
            Prop("PercentPosition", "Single"),
            Prop("EditMode", "Long"),
            Prop("LockEdits", "Boolean"),
            Prop("NoMatch", "Boolean"),
            Prop("Updatable", "Boolean"),
            Prop("Type", "Long"),
            // Member-collection / default chain
            Prop("Fields", "DAO.Fields", isDefault: true),
            // Data-shape
            Prop("Filter"), Prop("Sort"), Prop("Index"),
            Prop("Name"),
            Prop("DateCreated", "Date"), Prop("LastUpdated", "Date"),
            Prop("LastModified")
        ),
    };

    /// <summary>
    /// DAO.Field — Source: MSDN DAO Field object reference. Default member is
    /// Value (so <c>fld = "..."</c> writes the field's value).
    /// </summary>
    private static TypeInfo BuildDaoField() => new()
    {
        Name = "DAO.Field",
        TypeLibrary = "DAO",
        DefaultMemberName = "Value",
        Members = Members(
            Prop("Name"), Prop("Type", "Long"),
            Prop("Value", isDefault: true),
            Prop("Size", "Long"),
            Prop("Attributes", "Long"),
            Prop("AllowZeroLength", "Boolean"),
            Prop("DefaultValue"),
            Prop("OrdinalPosition", "Integer"),
            Prop("Required", "Boolean"),
            Prop("ValidationRule"), Prop("ValidationText"),
            Prop("CollatingOrder", "Long"),
            Prop("FieldSize", "Long"),
            Prop("OriginalValue"),
            Prop("VisibleValue"),
            Method("AppendChunk"), Method("GetChunk", "Variant"),
            Method("CreateProperty", "DAO.Property")
        ),
    };

    /// <summary>
    /// DAO.Database — Source: MSDN DAO Database object reference. Returned by
    /// <c>Application.CurrentDb()</c>; carries OpenRecordset / Execute /
    /// CreateQueryDef / etc.
    /// </summary>
    private static TypeInfo BuildDaoDatabase() => new()
    {
        Name = "DAO.Database",
        TypeLibrary = "DAO",
        DefaultMemberName = null,
        Members = Members(
            Prop("Name"), Prop("Connect"),
            Prop("Containers"), Prop("QueryDefs"),
            Prop("Recordsets"), Prop("Relations"), Prop("TableDefs"),
            Prop("Updatable", "Boolean"), Prop("Version"),
            Prop("RecordsAffected", "Long"),
            Prop("CollatingOrder", "Long"),
            Prop("DesignMasterID"),
            Method("Close"),
            Method("CreateQueryDef", "DAO.QueryDef"),
            Method("CreateRelation", "DAO.Relation"),
            Method("CreateTableDef", "DAO.TableDef"),
            Method("CreateProperty", "DAO.Property"),
            Method("Execute"), Method("OpenRecordset", "DAO.Recordset"),
            Method("NewPassword"), Method("Synchronize"),
            Method("MakeReplica"), Method("PopulatePartial")
        ),
    };

    /// <summary>
    /// DAO.QueryDef — Source: MSDN DAO QueryDef object reference. Used heavily
    /// by parameterized-query patterns: <c>qd.Parameters("p1") = ...; Set rs = qd.OpenRecordset()</c>.
    /// </summary>
    private static TypeInfo BuildDaoQueryDef() => new()
    {
        Name = "DAO.QueryDef",
        TypeLibrary = "DAO",
        DefaultMemberName = null,
        Members = Members(
            Prop("Name"), Prop("SQL"),
            Prop("Parameters"), Prop("Type", "Long"),
            Prop("RecordsAffected", "Long"),
            Prop("ReturnsRecords", "Boolean"),
            Prop("Connect"), Prop("MaxRecords", "Long"),
            Prop("ODBCTimeout", "Integer"),
            Prop("CacheSize", "Long"),
            Prop("DateCreated", "Date"), Prop("LastUpdated", "Date"),
            Method("Close"), Method("Execute"),
            Method("OpenRecordset", "DAO.Recordset"),
            Method("CreateProperty", "DAO.Property")
        ),
    };

    /// <summary>
    /// DAO.TableDef — Source: MSDN DAO TableDef object reference. Carries the
    /// schema of a stored table; <c>td.Fields</c> is its default-member chain.
    /// </summary>
    private static TypeInfo BuildDaoTableDef() => new()
    {
        Name = "DAO.TableDef",
        TypeLibrary = "DAO",
        DefaultMemberName = null,
        Members = Members(
            Prop("Name"), Prop("Fields", "DAO.Fields"),
            Prop("Indexes"), Prop("RecordCount", "Long"),
            Prop("ValidationRule"), Prop("ValidationText"),
            Prop("Connect"), Prop("SourceTableName"),
            Prop("Updatable", "Boolean"), Prop("Attributes", "Long"),
            Prop("DateCreated", "Date"), Prop("LastUpdated", "Date"),
            Method("CreateField", "DAO.Field"),
            Method("CreateIndex", "DAO.Index"),
            Method("CreateProperty", "DAO.Property"),
            Method("OpenRecordset", "DAO.Recordset"),
            Method("RefreshLink")
        ),
    };

    /// <summary>
    /// DAO.Fields — Source: MSDN DAO Fields collection reference. Default
    /// member is Item, which is what <c>rs.Fields("Name")</c> and
    /// <c>rs.Fields(0)</c> dispatch through.
    /// </summary>
    private static TypeInfo BuildDaoFields() => new()
    {
        Name = "DAO.Fields",
        TypeLibrary = "DAO",
        DefaultMemberName = "Item",
        Members = Members(
            Prop("Count", "Integer"),
            Prop("Item", "DAO.Field", isDefault: true),
            Method("Append"), Method("Delete"), Method("Refresh")
        ),
    };

    /// <summary>
    /// DAO.Recordsets — Source: MSDN DAO Recordsets collection reference.
    /// Holds the open Recordset objects on a Database. Default member Item.
    /// </summary>
    private static TypeInfo BuildDaoRecordsets() => new()
    {
        Name = "DAO.Recordsets",
        TypeLibrary = "DAO",
        DefaultMemberName = "Item",
        Members = Members(
            Prop("Count", "Integer"),
            Prop("Item", "DAO.Recordset", isDefault: true),
            Method("Refresh")
        ),
    };

    // ========================================================================
    // ADO
    // ========================================================================
    // Source: Microsoft ActiveX Data Objects Reference (MSDN /
    // learn.microsoft.com/en-us/sql/ado/). Subset focused on the surface
    // commonly used from Access VBA where ADO is preferred over DAO
    // (server-side queries, parameterized commands).
    // ========================================================================

    /// <summary>
    /// ADO.Recordset — Source: MSDN ActiveX Data Objects (ADO) Reference,
    /// Recordset object. Subset of the surface used from Access VBA when
    /// ADO is preferred over DAO. Default member is Fields.
    /// </summary>
    private static TypeInfo BuildAdoRecordset() => new()
    {
        Name = "ADO.Recordset",
        TypeLibrary = "ADO",
        DefaultMemberName = "Fields",
        Members = Members(
            // Navigation
            Method("MoveFirst"), Method("MoveLast"),
            Method("MoveNext"), Method("MovePrevious"), Method("Move"),
            // Edit / lifecycle
            Method("Open"), Method("Close"),
            Method("AddNew"), Method("Update"), Method("CancelUpdate"),
            Method("Delete"), Method("Find"), Method("Save"),
            Method("Requery"), Method("Clone", "ADO.Recordset"),
            // Properties
            Prop("ActiveConnection"), Prop("BOF", "Boolean"),
            Prop("EOF", "Boolean"), Prop("Bookmark"),
            Prop("CursorType", "Long"), Prop("EditMode", "Long"),
            Prop("Filter"), Prop("LockType", "Long"),
            Prop("MaxRecords", "Long"), Prop("RecordCount", "Long"),
            Prop("Source"), Prop("State", "Long"),
            Prop("Status", "Long"), Prop("AbsolutePosition", "Long"),
            Prop("Fields", "ADO.Fields", isDefault: true)
        ),
    };

    /// <summary>
    /// ADO.Connection — Source: MSDN ADO Connection object. Used for
    /// server-side database connections (typically against SQL Server or
    /// other ODBC sources from Access).
    /// </summary>
    private static TypeInfo BuildAdoConnection() => new()
    {
        Name = "ADO.Connection",
        TypeLibrary = "ADO",
        DefaultMemberName = null,
        Members = Members(
            Prop("ConnectionString"), Prop("Provider"),
            Prop("State", "Long"), Prop("Errors"),
            Prop("Mode", "Long"), Prop("CommandTimeout", "Long"),
            Prop("ConnectionTimeout", "Long"),
            Prop("DefaultDatabase"), Prop("Version"),
            Method("Open"), Method("Close"),
            Method("Execute", "ADO.Recordset"),
            Method("BeginTrans", "Long"),
            Method("CommitTrans"), Method("RollbackTrans"),
            Method("Cancel")
        ),
    };

    /// <summary>
    /// ADO.Command — Source: MSDN ADO Command object. Used for parameterized
    /// queries against ADO Connections.
    /// </summary>
    private static TypeInfo BuildAdoCommand() => new()
    {
        Name = "ADO.Command",
        TypeLibrary = "ADO",
        DefaultMemberName = null,
        Members = Members(
            Prop("ActiveConnection"), Prop("CommandText"),
            Prop("CommandType", "Long"), Prop("CommandTimeout", "Long"),
            Prop("Parameters"), Prop("Prepared", "Boolean"),
            Prop("Name"), Prop("State", "Long"),
            Method("Execute", "ADO.Recordset"),
            Method("CreateParameter", "ADO.Parameter"),
            Method("Cancel")
        ),
    };

    /// <summary>
    /// ADO.Field — Source: MSDN ADO Field object. Default member is Value
    /// (so <c>fld = "..."</c> writes the field's value), mirroring DAO.Field.
    /// </summary>
    private static TypeInfo BuildAdoField() => new()
    {
        Name = "ADO.Field",
        TypeLibrary = "ADO",
        DefaultMemberName = "Value",
        Members = Members(
            Prop("Name"), Prop("Type", "Long"),
            Prop("Value", isDefault: true),
            Prop("DefinedSize", "Long"), Prop("ActualSize", "Long"),
            Prop("Attributes", "Long"), Prop("NumericScale", "Byte"),
            Prop("Precision", "Byte"), Prop("Status", "Long"),
            Prop("OriginalValue"), Prop("UnderlyingValue"),
            Method("AppendChunk"), Method("GetChunk", "Variant")
        ),
    };

    /// <summary>
    /// ADO.Fields — Source: MSDN ADO Fields collection. Default member Item.
    /// Referenced as the return type of <c>ADO.Recordset.Fields</c>.
    /// </summary>
    private static TypeInfo BuildAdoFields() => new()
    {
        Name = "ADO.Fields",
        TypeLibrary = "ADO",
        DefaultMemberName = "Item",
        Members = Members(
            Prop("Count", "Long"),
            Prop("Item", "ADO.Field", isDefault: true),
            Method("Append"), Method("Delete"), Method("Refresh"),
            Method("Update"), Method("CancelUpdate"),
            Method("Resync")
        ),
    };

    // ========================================================================
    // Office
    // ========================================================================
    // Source: Microsoft Office Object Library reference (MSDN /
    // learn.microsoft.com/en-us/office/vba/api/overview/library-reference/).
    // ========================================================================

    /// <summary>
    /// Office.FileDialog — Source: MSDN Office FileDialog object reference.
    /// Used for file-open / file-save / folder-pick dialogs from Access VBA
    /// (<c>Application.FileDialog(msoFileDialogOpen)</c>).
    /// </summary>
    private static TypeInfo BuildOfficeFileDialog() => new()
    {
        Name = "Office.FileDialog",
        TypeLibrary = "Office",
        DefaultMemberName = null,
        Members = Members(
            Prop("AllowMultiSelect", "Boolean"), Prop("ButtonName"),
            Prop("DialogType", "Long"), Prop("FilterIndex", "Long"),
            Prop("Filters"), Prop("InitialFileName"),
            Prop("InitialView", "Long"), Prop("SelectedItems"),
            Prop("Title"), Prop("Item"),
            Method("Show", "Long"), Method("Execute")
        ),
    };

    // ========================================================================
    // Access
    // ========================================================================
    // Source: Microsoft Access Object Model Reference (MSDN /
    // learn.microsoft.com/en-us/office/vba/api/overview/access/object-model).
    // The Access object surface is intentionally split: Access.Form +
    // Access.Report + Access.Control + Access.Module + Access.DoCmd +
    // Access.Application. Control subtypes (TextBox, ComboBox, ...) are
    // NOT registered separately in v1; callers fall back to Access.Control.
    // ========================================================================

    /// <summary>
    /// Access.Form — Source: MSDN Microsoft Access Object Model, Form object.
    /// The most-encountered Access object in VBA: data binding properties,
    /// navigation methods, and the form-module event surface.
    /// </summary>
    private static TypeInfo BuildAccessForm() => new()
    {
        Name = "Access.Form",
        TypeLibrary = "Access",
        DefaultMemberName = null,
        Members = Members(
            // Identity / structure
            Prop("Name"), Prop("Caption"),
            Prop("Section"), Prop("Module", "Access.Module"),
            Prop("Hwnd", "Long"),
            Prop("Visible", "Boolean"),
            // Data
            Prop("RecordSource"), Prop("Filter"),
            Prop("FilterOn", "Boolean"), Prop("OrderBy"),
            Prop("OrderByOn", "Boolean"),
            Prop("Recordset", "DAO.Recordset"),
            Prop("RecordsetClone", "DAO.Recordset"),
            Prop("AllowAdditions", "Boolean"),
            Prop("AllowDeletions", "Boolean"),
            Prop("AllowEdits", "Boolean"),
            Prop("DataEntry", "Boolean"),
            Prop("AllowFilters", "Boolean"),
            Prop("Dirty", "Boolean"),
            // Navigation / state
            Prop("ActiveControl", "Access.Control"),
            Prop("CurrentRecord", "Long"),
            Prop("NewRecord", "Boolean"),
            // Methods
            Method("Refresh"), Method("Repaint"),
            Method("Requery"), Method("Recalc"),
            Method("SetFocus"), Method("GoToPage"),
            // Events (declared in form modules). Where a name would
            // collide with a property (e.g., Dirty), the property wins
            // and the event is omitted — resolver-side event-handler
            // recognition uses the <Object>_<Event> naming convention
            // (e.g., Form_Open, txtFirstName_BeforeUpdate) rather than
            // registry lookup, so the absence of a duplicate-name event
            // entry is not a functional gap.
            Event("Open"), Event("Load"), Event("Current"),
            Event("Close"), Event("Unload"),
            Event("Undo"), // Dirty (event) collides with Dirty (property)
            Event("BeforeUpdate"), Event("AfterUpdate"),
            Event("BeforeInsert"), Event("AfterInsert"),
            Event("BeforeDelConfirm"), Event("AfterDelConfirm"),
            Event("Delete"),
            Event("KeyDown"), Event("KeyPress"), Event("KeyUp"),
            Event("Click"), Event("DblClick"),
            Event("MouseDown"), Event("MouseUp"), Event("MouseMove"),
            Event("GotFocus"), Event("LostFocus"),
            Event("Activate"), Event("Deactivate"),
            Event("Resize"), Event("Timer")
        ),
    };

    /// <summary>
    /// Access.Report — Source: MSDN Microsoft Access Object Model, Report object.
    /// Mostly read-only at runtime; Page-level event surface fires during print
    /// formatting.
    /// </summary>
    private static TypeInfo BuildAccessReport() => new()
    {
        Name = "Access.Report",
        TypeLibrary = "Access",
        DefaultMemberName = null,
        Members = Members(
            Prop("Name"), Prop("Caption"),
            Prop("Section"), Prop("Module", "Access.Module"),
            Prop("Hwnd", "Long"), Prop("Visible", "Boolean"),
            Prop("RecordSource"), Prop("Filter"),
            Prop("FilterOn", "Boolean"), Prop("OrderBy"),
            Prop("OrderByOn", "Boolean"),
            Prop("ActiveControl", "Access.Control"),
            Prop("Page", "Long"), Prop("Pages", "Long"),
            Prop("PrintCount", "Long"),
            Prop("MoveLayout", "Boolean"),
            Prop("NextRecord", "Boolean"),
            Prop("FormatCount", "Integer"),
            Method("SetFocus"), Method("Circle"), Method("Line"),
            // Events. Page (event) collides with Page (property — current
            // page number Long) so the property wins; resolver-side event
            // recognition uses Report_Page naming. Same convention as
            // Access.Form. Error (event) likewise collides with the
            // collection name "Errors" on adjacent ADO types but does
            // not collide on Report itself, so keep it.
            Event("Open"), Event("Close"), Event("Load"), Event("Unload"),
            Event("NoData"), Event("Error"),
            Event("Activate"), Event("Deactivate"),
            // Per-section events (handled by section objects but commonly seen)
            Event("Format"), Event("Print"), Event("Retreat")
        ),
    };

    /// <summary>
    /// Access.Control — Source: MSDN Microsoft Access Object Model, Control
    /// object. The truly universal control surface — members and events that
    /// apply to every control type, including non-data-bound controls like
    /// Label and CommandButton. Data-bound members (ControlSource, Value,
    /// BeforeUpdate, etc.) live on <c>Access.DataBoundControl</c> below.
    /// Subtype-specific members live on the corresponding subtype.
    /// </summary>
    private static TypeInfo BuildAccessControl() => new()
    {
        Name = "Access.Control",
        TypeLibrary = "Access",
        BaseTypeName = null,
        DefaultMemberName = null,
        Members = Members(
            // Identity / structure
            Prop("Name"), Prop("Tag"),
            Prop("ControlType", "Integer"),
            // Visibility / sizing
            Prop("Visible", "Boolean"),
            Prop("Enabled", "Boolean"),
            Prop("Width", "Long"), Prop("Height", "Long"),
            Prop("Left", "Long"), Prop("Top", "Long"),
            // Appearance
            Prop("ForeColor", "Long"), Prop("BackColor", "Long"),
            Prop("BorderStyle", "Integer"),
            Prop("FontName"), Prop("FontSize", "Integer"),
            Prop("FontBold", "Boolean"), Prop("FontItalic", "Boolean"),
            Prop("FontUnderline", "Boolean"),
            // Container references / chrome
            Prop("Parent"), Prop("Section"),
            Prop("StatusBarText"),
            // Universal methods
            Method("SetFocus"), Method("SizeToFit"),
            // Universal events — fire on essentially any control type
            // (including Labels, which can have OnClick handlers).
            Event("Click"), Event("DblClick"),
            Event("MouseDown"), Event("MouseUp"), Event("MouseMove")
        ),
    };

    /// <summary>
    /// Access.DataBoundControl — intermediate base for data-bound controls
    /// (TextBox, ComboBox, ListBox). Carries the value / control-source
    /// surface plus the focus / typing / dirty event chain that applies to
    /// editable / data-bound controls but NOT to Label / CommandButton.
    /// This type is a curation construct — there is no single COM type
    /// with this exact name in the Access object model. Source: MSDN
    /// Access Control object reference, restricted to the data-bound
    /// member subset.
    /// </summary>
    private static TypeInfo BuildAccessDataBoundControl() => new()
    {
        Name = "Access.DataBoundControl",
        TypeLibrary = "Access",
        BaseTypeName = "Access.Control",
        DefaultMemberName = null,
        Members = Members(
            // Data-bound surface
            Prop("ControlSource"),
            Prop("Value"),
            Prop("DefaultValue"),
            Prop("Format"), Prop("InputMask"),
            Prop("ValidationRule"), Prop("ValidationText"),
            Prop("Locked", "Boolean"),
            // Methods specific to data-bound / requeryable controls
            Method("Requery"),
            // Focus / typing / value-change event chain
            Event("BeforeUpdate"), Event("AfterUpdate"),
            Event("Change"), Event("Updated"),
            Event("Enter"), Event("Exit"),
            Event("GotFocus"), Event("LostFocus"),
            Event("KeyDown"), Event("KeyPress"), Event("KeyUp")
        ),
    };

    /// <summary>
    /// Access.TextBox — Source: MSDN Access TextBox control reference.
    /// Inherits the data-bound surface (ControlSource, Value, BeforeUpdate,
    /// etc.) from <c>Access.DataBoundControl</c>, which itself inherits the
    /// universal surface from <c>Access.Control</c>. The subtype-specific
    /// surface here adds Text (the formatted-display alternative to Value),
    /// the selection range, and the typing/dirty event chain. Default member
    /// is Value (so <c>txtFirstName = "Bob"</c> writes the value); the Value
    /// property record lives on <c>Access.DataBoundControl</c> and the
    /// inheritance walk resolves it.
    /// </summary>
    private static TypeInfo BuildAccessTextBox() => new()
    {
        Name = "Access.TextBox",
        TypeLibrary = "Access",
        BaseTypeName = "Access.DataBoundControl",
        DefaultMemberName = "Value",
        Members = Members(
            Prop("Text"),
            Prop("SelStart", "Integer"), Prop("SelLength", "Integer"),
            Prop("SelText"),
            Event("Dirty"), Event("Undo")
        ),
    };

    /// <summary>
    /// Access.ComboBox — Source: MSDN Access ComboBox control reference.
    /// Inherits the data-bound surface from <c>Access.DataBoundControl</c>;
    /// the subtype-specific surface here adds the row-source / column /
    /// list-position members and the NotInList event. Default member is Value
    /// (record inherited from <c>Access.DataBoundControl</c>).
    /// </summary>
    private static TypeInfo BuildAccessComboBox() => new()
    {
        Name = "Access.ComboBox",
        TypeLibrary = "Access",
        BaseTypeName = "Access.DataBoundControl",
        DefaultMemberName = "Value",
        Members = Members(
            Prop("Text"),
            Prop("RowSource"), Prop("RowSourceType"),
            Prop("BoundColumn", "Integer"),
            Prop("ColumnCount", "Integer"),
            Prop("ColumnWidths"),
            Prop("ListIndex", "Long"),
            Prop("ListCount", "Long"),
            Prop("Selected"),
            Prop("Column"),
            Prop("ItemData"),
            Prop("LimitToList", "Boolean"),
            Method("Dropdown"),
            Event("NotInList"),
            Event("Dirty"), Event("Undo")
        ),
    };

    /// <summary>
    /// Access.ListBox — Source: MSDN Access ListBox control reference.
    /// Inherits the data-bound surface from <c>Access.DataBoundControl</c>.
    /// Adds row-source / column / multi-select members. Default member is
    /// Value (record inherited from <c>Access.DataBoundControl</c>).
    /// </summary>
    private static TypeInfo BuildAccessListBox() => new()
    {
        Name = "Access.ListBox",
        TypeLibrary = "Access",
        BaseTypeName = "Access.DataBoundControl",
        DefaultMemberName = "Value",
        Members = Members(
            Prop("RowSource"), Prop("RowSourceType"),
            Prop("BoundColumn", "Integer"),
            Prop("ColumnCount", "Integer"),
            Prop("ColumnWidths"),
            Prop("ListIndex", "Long"),
            Prop("ListCount", "Long"),
            Prop("Selected"),
            Prop("Column"),
            Prop("ItemData"),
            Prop("ItemsSelected"),
            Prop("MultiSelect", "Integer")
        ),
    };

    /// <summary>
    /// Access.CommandButton — Source: MSDN Access CommandButton control
    /// reference. Inherits the universal control surface from
    /// <c>Access.Control</c>. No default member; user code typically only
    /// sets Caption / Picture / Hyperlink and wires OnClick.
    /// </summary>
    private static TypeInfo BuildAccessCommandButton() => new()
    {
        Name = "Access.CommandButton",
        TypeLibrary = "Access",
        BaseTypeName = "Access.Control",
        DefaultMemberName = null,
        Members = Members(
            Prop("Caption"),
            Prop("Picture"),
            Prop("PictureType", "Integer"),
            Prop("Hyperlink"),
            Prop("Default", "Boolean"),
            Prop("Cancel", "Boolean"),
            Prop("Transparent", "Boolean")
        ),
    };

    /// <summary>
    /// Access.Label — Source: MSDN Access Label control reference. Inherits
    /// the universal control surface from <c>Access.Control</c>; adds
    /// Caption (the label's display text) and AutoLabel-related members.
    /// </summary>
    private static TypeInfo BuildAccessLabel() => new()
    {
        Name = "Access.Label",
        TypeLibrary = "Access",
        BaseTypeName = "Access.Control",
        DefaultMemberName = null,
        Members = Members(
            Prop("Caption"),
            Prop("AutoLabel", "Boolean"),
            Prop("HyperlinkAddress"),
            Prop("HyperlinkSubAddress"),
            Prop("TextAlign", "Integer"),
            Prop("FontWeight", "Integer")
        ),
    };

    /// <summary>
    /// Access.Module — Source: MSDN Access Module object reference. The
    /// container for VBA code modules; rarely used at runtime but present in
    /// some metaprogramming patterns (rare).
    /// </summary>
    private static TypeInfo BuildAccessModule() => new()
    {
        Name = "Access.Module",
        TypeLibrary = "Access",
        DefaultMemberName = null,
        Members = Members(
            Prop("Name"), Prop("Lines"),
            Prop("CountOfLines", "Long"),
            Prop("CountOfDeclarationLines", "Long"),
            Prop("Type", "Integer"),
            Method("AddFromFile"), Method("AddFromString"),
            Method("InsertText"), Method("InsertLines"),
            Method("DeleteLines"), Method("ReplaceLine"),
            Method("Find", "Boolean")
        ),
    };

    /// <summary>
    /// Access.DoCmd — Source: MSDN Access DoCmd object reference. The
    /// pseudo-singleton that exposes most navigation, data-manipulation,
    /// and chrome commands as methods. Does not have a default member.
    /// </summary>
    private static TypeInfo BuildAccessDoCmd() => new()
    {
        Name = "Access.DoCmd",
        TypeLibrary = "Access",
        DefaultMemberName = null,
        Members = Members(
            // Object lifecycle / navigation
            Method("OpenForm"), Method("OpenReport"),
            Method("OpenQuery"), Method("OpenTable"),
            Method("OpenModule"), Method("OpenView"),
            Method("OpenFunction"), Method("OpenStoredProcedure"),
            Method("OpenDataAccessPage"),
            Method("Close"), Method("Quit"),
            Method("RunCommand"), Method("RunMacro"),
            Method("RunSQL"), Method("Run"),
            // Data manipulation
            Method("ApplyFilter"), Method("ShowAllRecords"),
            Method("FindRecord"), Method("FindNext"),
            Method("GoToRecord"), Method("GoToControl"),
            Method("GoToPage"), Method("SearchForRecord"),
            Method("RefreshRecord"),
            // UI / chrome
            Method("SetWarnings"), Method("Hourglass"),
            Method("Beep"), Method("Echo"),
            Method("Maximize"), Method("Minimize"),
            Method("Restore"), Method("Save"),
            Method("ShowToolbar"), Method("MoveSize"),
            Method("SelectObject"),
            // Output
            Method("OutputTo"), Method("PrintOut"),
            Method("SendObject"),
            Method("TransferDatabase"), Method("TransferText"),
            Method("TransferSpreadsheet"),
            Method("CopyDatabaseFile"),
            Method("DeleteObject"), Method("Rename"),
            Method("CopyObject"),
            // Property setting via DoCmd
            Method("SetProperty")
        ),
    };

    /// <summary>
    /// Access.Application — Source: MSDN Access Application object reference.
    /// Note that <c>CurrentDb</c> is a method (not a property) per MSDN — it
    /// refreshes the DAO Database wrapper around the current Access database.
    /// Code may invoke it as <c>Application.CurrentDb</c> (no parens) or
    /// <c>CurrentDb()</c> at top level; both call shapes resolve to the same
    /// member.
    /// </summary>
    private static TypeInfo BuildAccessApplication() => new()
    {
        Name = "Access.Application",
        TypeLibrary = "Access",
        DefaultMemberName = null,
        Members = Members(
            Prop("Name"), Prop("Version"), Prop("BuildVersion"),
            Prop("UserName"), Prop("Path"),
            Prop("CurrentProject"), Prop("CurrentData"),
            Prop("Forms"), Prop("Reports"),
            Prop("Modules"), Prop("Macros"),
            Prop("Screen"),
            Prop("DoCmd", "Access.DoCmd"),
            Prop("References"),
            Prop("FileDialog", "Office.FileDialog"),
            Prop("IsCompiled", "Boolean"),
            Prop("MenuBar"), Prop("ShortcutMenuBar"),
            Method("CurrentDb", "DAO.Database"),
            Method("CodeDb", "DAO.Database"),
            Method("Run"), Method("Quit"),
            Method("NewCurrentDatabase"),
            Method("OpenCurrentDatabase"),
            Method("CloseCurrentDatabase"),
            Method("Echo"), Method("Hourglass"), Method("Beep"),
            Method("RefreshDatabaseWindow"),
            Method("CompactRepair"),
            Method("GetOption"), Method("SetOption")
        ),
    };
}
