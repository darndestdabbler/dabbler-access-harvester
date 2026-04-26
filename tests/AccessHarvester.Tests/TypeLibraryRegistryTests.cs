using System.Linq;
using AccessHarvester.Access;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="TypeLibraryRegistry"/>. Cover the
/// contract surface (<see cref="ITypeLibraryRegistry"/>): known-type lookups,
/// known-member lookups (including the inheritance walk), default-member
/// resolution, case-insensitivity, graceful degradation for null / empty /
/// unknown inputs, and a frozen per-type catalog assertion that catches
/// accidental member removals.
/// </summary>
public class TypeLibraryRegistryTests
{
    private readonly TypeLibraryRegistry _impl = new();
    private readonly ITypeLibraryRegistry _registry;

    public TypeLibraryRegistryTests()
    {
        _registry = _impl;
    }

    /// <summary>Canonical list of every registered type, used by several theories.</summary>
    private static readonly string[] AllRegisteredTypeNamesArray =
    {
        "DAO.Recordset", "DAO.Field", "DAO.Database", "DAO.QueryDef",
        "DAO.TableDef", "DAO.Fields", "DAO.Recordsets",
        "ADO.Recordset", "ADO.Connection", "ADO.Command", "ADO.Field", "ADO.Fields",
        "Office.FileDialog",
        "Access.Form", "Access.Report",
        "Access.Control",
        "Access.TextBox", "Access.ComboBox", "Access.ListBox",
        "Access.CommandButton", "Access.Label",
        "Access.Module", "Access.DoCmd", "Access.Application",
    };

    public static IEnumerable<object[]> AllRegisteredTypeNames =>
        AllRegisteredTypeNamesArray.Select(n => new object[] { n });

    // ===================================================================
    // Known-type lookups for every registered type.
    // ===================================================================

    [Theory]
    [MemberData(nameof(AllRegisteredTypeNames))]
    public void TryGetType_RegisteredTypes_ReturnsTrue(string typeName)
    {
        Assert.True(_registry.TryGetType(typeName, out var info));
        Assert.NotNull(info);
        Assert.Equal(typeName, info!.Name);
    }

    [Fact]
    public void TryGetType_UnknownType_ReturnsFalseAndNullInfo()
    {
        Assert.False(_registry.TryGetType("DAO.MadeUpType", out var info));
        Assert.Null(info);
    }

    [Fact]
    public void TryGetType_LookupIsCaseInsensitive()
    {
        Assert.True(_registry.TryGetType("dao.recordset", out var info));
        Assert.NotNull(info);
        Assert.Equal("DAO.Recordset", info!.Name);
    }

    // ===================================================================
    // Graceful degradation: null / empty inputs never throw.
    // ===================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryGetType_NullOrEmpty_ReturnsFalseAndDoesNotThrow(string? typeName)
    {
        Assert.False(_registry.TryGetType(typeName!, out var info));
        Assert.Null(info);
    }

    [Theory]
    [InlineData(null, "MoveNext")]
    [InlineData("", "MoveNext")]
    [InlineData("DAO.Recordset", null)]
    [InlineData("DAO.Recordset", "")]
    [InlineData(null, null)]
    public void TryGetMember_NullOrEmpty_ReturnsFalseAndDoesNotThrow(
        string? typeName, string? memberName)
    {
        Assert.False(_registry.TryGetMember(typeName!, memberName!, out var info));
        Assert.Null(info);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryGetDefaultMember_NullOrEmpty_ReturnsFalseAndDoesNotThrow(string? typeName)
    {
        Assert.False(_registry.TryGetDefaultMember(typeName!, out var info));
        Assert.Null(info);
    }

    // ===================================================================
    // Direct member lookups — sample of representative members.
    // ===================================================================

    [Theory]
    // Type, member, expected kind, expected return type ("" means null/void).
    // Representative members across every registered type. Catches:
    //   - wrong member kinds (e.g., Access.Application.CurrentDb originally
    //     marked Property; corrected to Method in round 1).
    //   - wrong return types (e.g., RecordCount must be Long, not Integer).
    //   - members removed during refactoring.
    [InlineData("DAO.Recordset", "MoveNext", MemberKind.Method, "")]
    [InlineData("DAO.Recordset", "EOF", MemberKind.Property, "Boolean")]
    [InlineData("DAO.Recordset", "RecordCount", MemberKind.Property, "Long")]
    [InlineData("DAO.Recordset", "Fields", MemberKind.Property, "DAO.Fields")]
    [InlineData("DAO.Recordset", "Clone", MemberKind.Method, "DAO.Recordset")]
    [InlineData("DAO.Field", "Value", MemberKind.Property, "")]
    [InlineData("DAO.Field", "Required", MemberKind.Property, "Boolean")]
    [InlineData("DAO.Database", "OpenRecordset", MemberKind.Method, "DAO.Recordset")]
    [InlineData("DAO.Database", "Updatable", MemberKind.Property, "Boolean")]
    [InlineData("DAO.QueryDef", "SQL", MemberKind.Property, "")]
    [InlineData("DAO.QueryDef", "ReturnsRecords", MemberKind.Property, "Boolean")]
    [InlineData("DAO.TableDef", "Fields", MemberKind.Property, "DAO.Fields")]
    [InlineData("DAO.Fields", "Item", MemberKind.Property, "DAO.Field")]
    [InlineData("DAO.Fields", "Count", MemberKind.Property, "Integer")]
    [InlineData("DAO.Recordsets", "Item", MemberKind.Property, "DAO.Recordset")]
    [InlineData("ADO.Recordset", "Open", MemberKind.Method, "")]
    [InlineData("ADO.Recordset", "EOF", MemberKind.Property, "Boolean")]
    [InlineData("ADO.Recordset", "Fields", MemberKind.Property, "ADO.Fields")]
    [InlineData("ADO.Connection", "Execute", MemberKind.Method, "ADO.Recordset")]
    [InlineData("ADO.Connection", "State", MemberKind.Property, "Long")]
    [InlineData("ADO.Command", "CommandText", MemberKind.Property, "")]
    [InlineData("ADO.Command", "Execute", MemberKind.Method, "ADO.Recordset")]
    [InlineData("ADO.Field", "Value", MemberKind.Property, "")]
    [InlineData("ADO.Fields", "Item", MemberKind.Property, "ADO.Field")]
    [InlineData("ADO.Fields", "Count", MemberKind.Property, "Long")]
    [InlineData("Office.FileDialog", "Show", MemberKind.Method, "Long")]
    [InlineData("Office.FileDialog", "AllowMultiSelect", MemberKind.Property, "Boolean")]
    [InlineData("Access.Form", "RecordSource", MemberKind.Property, "")]
    [InlineData("Access.Form", "BeforeUpdate", MemberKind.Event, "")]
    [InlineData("Access.Form", "Recordset", MemberKind.Property, "DAO.Recordset")]
    [InlineData("Access.Form", "Refresh", MemberKind.Method, "")]
    [InlineData("Access.Report", "Page", MemberKind.Property, "Long")]
    [InlineData("Access.Report", "Pages", MemberKind.Property, "Long")]
    [InlineData("Access.Control", "Name", MemberKind.Property, "")]
    [InlineData("Access.Control", "Click", MemberKind.Event, "")]
    [InlineData("Access.Control", "Visible", MemberKind.Property, "Boolean")]
    [InlineData("Access.TextBox", "ControlSource", MemberKind.Property, "")]
    [InlineData("Access.TextBox", "BeforeUpdate", MemberKind.Event, "")]
    [InlineData("Access.TextBox", "Requery", MemberKind.Method, "")]
    [InlineData("Access.TextBox", "Text", MemberKind.Property, "")]
    [InlineData("Access.TextBox", "Dirty", MemberKind.Event, "")]
    [InlineData("Access.ComboBox", "RowSource", MemberKind.Property, "")]
    [InlineData("Access.ComboBox", "NotInList", MemberKind.Event, "")]
    [InlineData("Access.ListBox", "MultiSelect", MemberKind.Property, "Integer")]
    [InlineData("Access.CommandButton", "Caption", MemberKind.Property, "")]
    [InlineData("Access.CommandButton", "Default", MemberKind.Property, "Boolean")]
    [InlineData("Access.Label", "Caption", MemberKind.Property, "")]
    [InlineData("Access.Label", "AutoLabel", MemberKind.Property, "Boolean")]
    [InlineData("Access.Module", "CountOfLines", MemberKind.Property, "Long")]
    [InlineData("Access.DoCmd", "OpenForm", MemberKind.Method, "")]
    [InlineData("Access.DoCmd", "RunSQL", MemberKind.Method, "")]
    [InlineData("Access.Application", "CurrentDb", MemberKind.Method, "DAO.Database")]
    [InlineData("Access.Application", "DoCmd", MemberKind.Property, "Access.DoCmd")]
    [InlineData("Access.Application", "FileDialog", MemberKind.Property, "Office.FileDialog")]
    public void TryGetMember_KnownMembers_ResolveWithExpectedKindAndReturnType(
        string typeName, string memberName, MemberKind expectedKind, string expectedReturnType)
    {
        Assert.True(_registry.TryGetMember(typeName, memberName, out var info));
        Assert.NotNull(info);
        Assert.Equal(memberName, info!.Name);
        Assert.Equal(expectedKind, info.Kind);
        var expected = string.IsNullOrEmpty(expectedReturnType) ? null : expectedReturnType;
        Assert.Equal(expected, info.ReturnType);
    }

    [Fact]
    public void TryGetMember_UnknownMember_ReturnsFalseAndNullInfo()
    {
        Assert.False(_registry.TryGetMember("DAO.Recordset", "MadeUpMethod", out var info));
        Assert.Null(info);
    }

    [Fact]
    public void TryGetMember_UnknownType_ReturnsFalseAndNullInfo()
    {
        Assert.False(_registry.TryGetMember("DAO.MadeUpType", "MoveNext", out var info));
        Assert.Null(info);
    }

    [Fact]
    public void TryGetMember_LookupIsCaseInsensitiveOnBothTypeAndMember()
    {
        Assert.True(_registry.TryGetMember("DAO.RECORDSET", "movenext", out var info));
        Assert.NotNull(info);
        Assert.Equal("MoveNext", info!.Name);
    }

    // ===================================================================
    // Inheritance walk — subtype lookups fall through to Access.Control.
    // ===================================================================

    [Theory]
    // Universal control members on data-bound subtypes resolve via the
    // Access.Control base.
    [InlineData("Access.TextBox", "Name")]
    [InlineData("Access.TextBox", "Visible")]
    [InlineData("Access.TextBox", "Click")]
    [InlineData("Access.ComboBox", "Name")]
    [InlineData("Access.ComboBox", "Visible")]
    [InlineData("Access.ComboBox", "Click")]
    [InlineData("Access.ListBox", "Name")]
    // Universal control members on non-data-bound subtypes resolve via
    // Access.Control directly.
    [InlineData("Access.CommandButton", "Name")]
    [InlineData("Access.CommandButton", "Click")]
    [InlineData("Access.CommandButton", "Visible")]
    [InlineData("Access.Label", "Name")]
    [InlineData("Access.Label", "Visible")]
    [InlineData("Access.Label", "Click")]
    public void TryGetMember_SubtypeMember_ResolvesViaInheritedBase(string subtype, string memberName)
    {
        Assert.True(_registry.TryGetMember(subtype, memberName, out var info));
        Assert.NotNull(info);
        Assert.Equal(memberName, info!.Name);
    }

    /// <summary>
    /// Negative coverage: data-bound members must NOT resolve on non-data-bound
    /// controls. The data-bound surface (ControlSource, Value, BeforeUpdate,
    /// etc.) is composed only into TextBox/ComboBox/ListBox via the registry's
    /// private DataBoundControlMembers helper. Label and CommandButton inherit
    /// from <c>Access.Control</c> directly with none of those members, so
    /// <c>label.Value</c> / <c>button.BeforeUpdate</c> must report not-found.
    /// </summary>
    [Theory]
    [InlineData("Access.Label", "Value")]
    [InlineData("Access.Label", "ControlSource")]
    [InlineData("Access.Label", "BeforeUpdate")]
    [InlineData("Access.Label", "AfterUpdate")]
    [InlineData("Access.Label", "Change")]
    [InlineData("Access.Label", "DefaultValue")]
    [InlineData("Access.Label", "ValidationRule")]
    [InlineData("Access.CommandButton", "Value")]
    [InlineData("Access.CommandButton", "ControlSource")]
    [InlineData("Access.CommandButton", "BeforeUpdate")]
    [InlineData("Access.CommandButton", "AfterUpdate")]
    [InlineData("Access.CommandButton", "Change")]
    public void TryGetMember_DataBoundMembersOnNonDataBoundControls_ReturnFalse(
        string nonDataBoundType, string dataBoundMember)
    {
        Assert.False(
            _registry.TryGetMember(nonDataBoundType, dataBoundMember, out var info),
            $"Data-bound member '{dataBoundMember}' must not resolve on non-data-bound type '{nonDataBoundType}'.");
        Assert.Null(info);
    }

    /// <summary>
    /// IsDefault projection consistency: <c>TryGetMember</c> on a subtype's
    /// effective default member must return <c>IsDefault=true</c> even when
    /// the underlying record lives on a base type with <c>IsDefault=false</c>.
    /// Symmetric with <c>TryGetDefaultMember</c>.
    /// </summary>
    [Theory]
    [InlineData("Access.TextBox", "Value")]
    [InlineData("Access.ComboBox", "Value")]
    [InlineData("Access.ListBox", "Value")]
    public void TryGetMember_OnSubtypeEffectiveDefault_ProjectsIsDefaultTrue(
        string subtype, string defaultMemberName)
    {
        Assert.True(_registry.TryGetMember(subtype, defaultMemberName, out var info));
        Assert.NotNull(info);
        Assert.Equal(defaultMemberName, info!.Name);
        Assert.True(info.IsDefault,
            $"TryGetMember('{subtype}', '{defaultMemberName}') must project IsDefault=true to stay consistent with TryGetDefaultMember.");
    }

    [Theory]
    [InlineData("Access.TextBox", "Text")]                // subtype-specific
    [InlineData("Access.TextBox", "SelStart")]
    [InlineData("Access.ComboBox", "RowSource")]
    [InlineData("Access.ComboBox", "NotInList")]          // subtype event
    [InlineData("Access.ListBox", "MultiSelect")]
    [InlineData("Access.CommandButton", "Caption")]
    [InlineData("Access.CommandButton", "Default")]
    [InlineData("Access.Label", "Caption")]
    public void TryGetMember_SubtypeSpecificMember_ResolvesDirectly(string subtype, string memberName)
    {
        Assert.True(_registry.TryGetMember(subtype, memberName, out var info));
        Assert.NotNull(info);
        Assert.Equal(memberName, info!.Name);
    }

    // ===================================================================
    // Default-member resolution, including inheritance.
    // ===================================================================

    [Theory]
    [InlineData("DAO.Recordset", "Fields")]
    [InlineData("DAO.Field", "Value")]
    [InlineData("DAO.Fields", "Item")]
    [InlineData("DAO.Recordsets", "Item")]
    [InlineData("ADO.Recordset", "Fields")]
    [InlineData("ADO.Field", "Value")]
    [InlineData("ADO.Fields", "Item")]
    // TextBox/ComboBox/ListBox declare Value as their default; the underlying
    // Value member is composed in via DataBoundControlMembers and projected to
    // IsDefault=true on lookup.
    [InlineData("Access.TextBox", "Value")]
    [InlineData("Access.ComboBox", "Value")]
    [InlineData("Access.ListBox", "Value")]
    public void TryGetDefaultMember_TypesWithDefaults_ResolveCorrectly(
        string typeName, string expectedDefaultName)
    {
        Assert.True(_registry.TryGetDefaultMember(typeName, out var info));
        Assert.NotNull(info);
        Assert.Equal(expectedDefaultName, info!.Name);
    }

    [Theory]
    [InlineData("DAO.Database")]
    [InlineData("Access.Form")]
    [InlineData("Access.Report")]
    [InlineData("Access.Control")]            // base has no default
    [InlineData("Access.CommandButton")]
    [InlineData("Access.Label")]
    [InlineData("Access.DoCmd")]
    [InlineData("Access.Application")]
    public void TryGetDefaultMember_TypesWithoutDefaults_ReturnFalse(string typeName)
    {
        Assert.False(_registry.TryGetDefaultMember(typeName, out var info));
        Assert.Null(info);
    }

    [Fact]
    public void TryGetDefaultMember_UnknownType_ReturnsFalse()
    {
        Assert.False(_registry.TryGetDefaultMember("DAO.MadeUpType", out var info));
        Assert.Null(info);
    }

    // ===================================================================
    // Catalog invariants: every registered type's metadata is consistent.
    // ===================================================================

    [Fact]
    public void EveryDefaultMemberName_ResolvesViaTryGetDefaultMemberAndIsProjectedAsDefault()
    {
        foreach (var name in AllRegisteredTypeNamesArray)
        {
            Assert.True(_registry.TryGetType(name, out var info), $"Type missing: {name}");
            if (info!.DefaultMemberName is not null)
            {
                Assert.True(
                    _registry.TryGetDefaultMember(info.Name, out var m),
                    $"{name} declares DefaultMemberName='{info.DefaultMemberName}' but TryGetDefaultMember returns false.");
                Assert.Equal(info.DefaultMemberName, m!.Name);
                Assert.True(m.IsDefault,
                    $"{name}.{info.DefaultMemberName} resolved via TryGetDefaultMember but the returned MemberInfo does not carry IsDefault=true.");
            }
        }
    }

    [Fact]
    public void AtMostOneMemberPerType_CarriesIsDefaultTrue()
    {
        foreach (var name in AllRegisteredTypeNamesArray)
        {
            Assert.True(_registry.TryGetType(name, out var info), $"Type missing: {name}");
            int defaultCount = 0;
            foreach (var member in info!.Members.Values)
            {
                if (member.IsDefault) defaultCount++;
            }
            Assert.True(defaultCount <= 1,
                $"{name} has {defaultCount} members marked IsDefault=true; at most one is allowed at a single inheritance level.");
        }
    }

    [Fact]
    public void EveryRegisteredType_DeclaresExpectedTypeLibrary()
    {
        var expectedLibs = new HashSet<string> { "DAO", "ADO", "Office", "Access" };

        foreach (var name in AllRegisteredTypeNamesArray)
        {
            Assert.True(_registry.TryGetType(name, out var info), $"Type missing: {name}");
            Assert.Contains(info!.TypeLibrary, expectedLibs);
            Assert.StartsWith(info.TypeLibrary + ".", name);
        }
    }

    /// <summary>
    /// Catalog-completeness sentinel: the test's <see cref="AllRegisteredTypeNamesArray"/>
    /// must equal the registry's actual registered set. Catches the case where
    /// curation adds a new type to the registry but forgets to extend the
    /// per-type test coverage (count theory, default-member invariants, etc.).
    /// </summary>
    [Fact]
    public void RegisteredTypeSet_MatchesCatalogExactly()
    {
        var actual = new HashSet<string>(_impl.RegisteredTypeNames, StringComparer.OrdinalIgnoreCase);
        var expected = new HashSet<string>(AllRegisteredTypeNamesArray, StringComparer.OrdinalIgnoreCase);

        var added = actual.Except(expected, StringComparer.OrdinalIgnoreCase).ToList();
        var removed = expected.Except(actual, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.True(
            added.Count == 0 && removed.Count == 0,
            $"Registered type set drifted from the test catalog. " +
            $"Added (in registry, missing from catalog): [{string.Join(", ", added)}]. " +
            $"Removed (in catalog, missing from registry): [{string.Join(", ", removed)}]. " +
            $"Update AllRegisteredTypeNamesArray and the per-type theories accordingly.");
    }

    [Fact]
    public void EveryBaseTypeName_PointsAtARegisteredType()
    {
        foreach (var name in AllRegisteredTypeNamesArray)
        {
            Assert.True(_registry.TryGetType(name, out var info), $"Type missing: {name}");
            if (info!.BaseTypeName is not null)
            {
                Assert.True(_registry.TryGetType(info.BaseTypeName, out _),
                    $"{name}.BaseTypeName='{info.BaseTypeName}' does not resolve to a registered type.");
            }
        }
    }

    // ===================================================================
    // Frozen per-type member-count catalog. Catches accidental removals
    // during future curation: a type whose member count changes triggers
    // a clear diff requiring the curator to update both the registry and
    // this list. Counts are direct-only (do not include inherited members).
    // ===================================================================

    [Theory]
    [InlineData("DAO.Recordset", 38)]
    [InlineData("DAO.Field", 18)]
    [InlineData("DAO.Database", 23)]
    [InlineData("DAO.QueryDef", 16)]
    [InlineData("DAO.TableDef", 17)]
    [InlineData("DAO.Fields", 5)]
    [InlineData("DAO.Recordsets", 3)]
    [InlineData("ADO.Recordset", 30)]
    [InlineData("ADO.Connection", 16)]
    [InlineData("ADO.Command", 11)]
    [InlineData("ADO.Field", 13)]
    [InlineData("ADO.Fields", 8)]
    [InlineData("Office.FileDialog", 12)]
    [InlineData("Access.Form", 55)]
    [InlineData("Access.Report", 32)]
    [InlineData("Access.Control", 27)]
    // Data-bound subtypes carry the 20 shared DataBoundControlMembers plus
    // their own subtype-specific members directly (no fabricated intermediate
    // base type in the registry).
    [InlineData("Access.TextBox", 26)]
    [InlineData("Access.ComboBox", 36)]
    [InlineData("Access.ListBox", 32)]
    [InlineData("Access.CommandButton", 7)]
    [InlineData("Access.Label", 6)]
    [InlineData("Access.Module", 12)]
    [InlineData("Access.DoCmd", 46)]
    [InlineData("Access.Application", 32)]
    public void EveryRegisteredType_HasExpectedDirectMemberCount(string typeName, int expectedCount)
    {
        Assert.True(_registry.TryGetType(typeName, out var info), $"Type missing: {typeName}");
        Assert.Equal(expectedCount, info!.Members.Count);
    }

    // ===================================================================
    // Frozen exhaustive per-type member catalog. Each registered type's
    // full direct member surface — Name, Kind, ReturnType, and IsDefault —
    // is pinned below. A change to the registry that the curator did not
    // also reflect in this catalog fails a named per-type test, naming
    // the missing/extra/changed member explicitly.
    //
    // This is the strictly stronger sibling of
    // EveryRegisteredType_HasExpectedDirectMemberCount above: same regression
    // surface, but it catches the case where a removal and an addition
    // happen in the same edit and leave the count unchanged.
    //
    // The catalog was captured from the v1 registry source. Future
    // additions: append the new type's row here AND add it to
    // AllRegisteredTypeNamesArray so RegisteredTypeSet_MatchesCatalogExactly
    // and the new EveryRegisteredType_HasEntryInExpectedCatalog sentinel
    // both stay green.
    // ===================================================================

    /// <summary>
    /// Frozen catalog of every registered type's direct member surface
    /// (Name, Kind, ReturnType, IsDefault). Captured exactly from the
    /// v1 registry, so any future curation drift surfaces as a named
    /// per-type test failure.
    /// </summary>
    private static readonly Dictionary<string, (string Name, MemberKind Kind, string? ReturnType, bool IsDefault)[]>
        ExpectedDirectMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DAO.Recordset"] = new (string, MemberKind, string?, bool)[]
        {
            ("MoveFirst", MemberKind.Method, null, false),
            ("MoveLast", MemberKind.Method, null, false),
            ("MoveNext", MemberKind.Method, null, false),
            ("MovePrevious", MemberKind.Method, null, false),
            ("Move", MemberKind.Method, null, false),
            ("AddNew", MemberKind.Method, null, false),
            ("Edit", MemberKind.Method, null, false),
            ("Update", MemberKind.Method, null, false),
            ("CancelUpdate", MemberKind.Method, null, false),
            ("Delete", MemberKind.Method, null, false),
            ("FindFirst", MemberKind.Method, null, false),
            ("FindLast", MemberKind.Method, null, false),
            ("FindNext", MemberKind.Method, null, false),
            ("FindPrevious", MemberKind.Method, null, false),
            ("Seek", MemberKind.Method, null, false),
            ("Close", MemberKind.Method, null, false),
            ("Clone", MemberKind.Method, "DAO.Recordset", false),
            ("Requery", MemberKind.Method, null, false),
            ("BOF", MemberKind.Property, "Boolean", false),
            ("EOF", MemberKind.Property, "Boolean", false),
            ("Bookmark", MemberKind.Property, null, false),
            ("Bookmarkable", MemberKind.Property, "Boolean", false),
            ("AbsolutePosition", MemberKind.Property, "Long", false),
            ("RecordCount", MemberKind.Property, "Long", false),
            ("PercentPosition", MemberKind.Property, "Single", false),
            ("EditMode", MemberKind.Property, "Long", false),
            ("LockEdits", MemberKind.Property, "Boolean", false),
            ("NoMatch", MemberKind.Property, "Boolean", false),
            ("Updatable", MemberKind.Property, "Boolean", false),
            ("Type", MemberKind.Property, "Long", false),
            ("Fields", MemberKind.Property, "DAO.Fields", true),
            ("Filter", MemberKind.Property, null, false),
            ("Sort", MemberKind.Property, null, false),
            ("Index", MemberKind.Property, null, false),
            ("Name", MemberKind.Property, null, false),
            ("DateCreated", MemberKind.Property, "Date", false),
            ("LastUpdated", MemberKind.Property, "Date", false),
            ("LastModified", MemberKind.Property, null, false),
        },
        ["DAO.Field"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Type", MemberKind.Property, "Long", false),
            ("Value", MemberKind.Property, null, true),
            ("Size", MemberKind.Property, "Long", false),
            ("Attributes", MemberKind.Property, "Long", false),
            ("AllowZeroLength", MemberKind.Property, "Boolean", false),
            ("DefaultValue", MemberKind.Property, null, false),
            ("OrdinalPosition", MemberKind.Property, "Integer", false),
            ("Required", MemberKind.Property, "Boolean", false),
            ("ValidationRule", MemberKind.Property, null, false),
            ("ValidationText", MemberKind.Property, null, false),
            ("CollatingOrder", MemberKind.Property, "Long", false),
            ("FieldSize", MemberKind.Property, "Long", false),
            ("OriginalValue", MemberKind.Property, null, false),
            ("VisibleValue", MemberKind.Property, null, false),
            ("AppendChunk", MemberKind.Method, null, false),
            ("GetChunk", MemberKind.Method, "Variant", false),
            ("CreateProperty", MemberKind.Method, "DAO.Property", false),
        },
        ["DAO.Database"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Connect", MemberKind.Property, null, false),
            ("Containers", MemberKind.Property, "DAO.Containers", false),
            ("QueryDefs", MemberKind.Property, "DAO.QueryDefs", false),
            ("Recordsets", MemberKind.Property, "DAO.Recordsets", false),
            ("Relations", MemberKind.Property, "DAO.Relations", false),
            ("TableDefs", MemberKind.Property, "DAO.TableDefs", false),
            ("Updatable", MemberKind.Property, "Boolean", false),
            ("Version", MemberKind.Property, null, false),
            ("RecordsAffected", MemberKind.Property, "Long", false),
            ("CollatingOrder", MemberKind.Property, "Long", false),
            ("DesignMasterID", MemberKind.Property, null, false),
            ("Close", MemberKind.Method, null, false),
            ("CreateQueryDef", MemberKind.Method, "DAO.QueryDef", false),
            ("CreateRelation", MemberKind.Method, "DAO.Relation", false),
            ("CreateTableDef", MemberKind.Method, "DAO.TableDef", false),
            ("CreateProperty", MemberKind.Method, "DAO.Property", false),
            ("Execute", MemberKind.Method, null, false),
            ("OpenRecordset", MemberKind.Method, "DAO.Recordset", false),
            ("NewPassword", MemberKind.Method, null, false),
            ("Synchronize", MemberKind.Method, null, false),
            ("MakeReplica", MemberKind.Method, null, false),
            ("PopulatePartial", MemberKind.Method, null, false),
        },
        ["DAO.QueryDef"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("SQL", MemberKind.Property, null, false),
            ("Parameters", MemberKind.Property, "DAO.Parameters", false),
            ("Type", MemberKind.Property, "Long", false),
            ("RecordsAffected", MemberKind.Property, "Long", false),
            ("ReturnsRecords", MemberKind.Property, "Boolean", false),
            ("Connect", MemberKind.Property, null, false),
            ("MaxRecords", MemberKind.Property, "Long", false),
            ("ODBCTimeout", MemberKind.Property, "Integer", false),
            ("CacheSize", MemberKind.Property, "Long", false),
            ("DateCreated", MemberKind.Property, "Date", false),
            ("LastUpdated", MemberKind.Property, "Date", false),
            ("Close", MemberKind.Method, null, false),
            ("Execute", MemberKind.Method, null, false),
            ("OpenRecordset", MemberKind.Method, "DAO.Recordset", false),
            ("CreateProperty", MemberKind.Method, "DAO.Property", false),
        },
        ["DAO.TableDef"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Fields", MemberKind.Property, "DAO.Fields", false),
            ("Indexes", MemberKind.Property, "DAO.Indexes", false),
            ("RecordCount", MemberKind.Property, "Long", false),
            ("ValidationRule", MemberKind.Property, null, false),
            ("ValidationText", MemberKind.Property, null, false),
            ("Connect", MemberKind.Property, null, false),
            ("SourceTableName", MemberKind.Property, null, false),
            ("Updatable", MemberKind.Property, "Boolean", false),
            ("Attributes", MemberKind.Property, "Long", false),
            ("DateCreated", MemberKind.Property, "Date", false),
            ("LastUpdated", MemberKind.Property, "Date", false),
            ("CreateField", MemberKind.Method, "DAO.Field", false),
            ("CreateIndex", MemberKind.Method, "DAO.Index", false),
            ("CreateProperty", MemberKind.Method, "DAO.Property", false),
            ("OpenRecordset", MemberKind.Method, "DAO.Recordset", false),
            ("RefreshLink", MemberKind.Method, null, false),
        },
        ["DAO.Fields"] = new (string, MemberKind, string?, bool)[]
        {
            ("Count", MemberKind.Property, "Integer", false),
            ("Item", MemberKind.Property, "DAO.Field", true),
            ("Append", MemberKind.Method, null, false),
            ("Delete", MemberKind.Method, null, false),
            ("Refresh", MemberKind.Method, null, false),
        },
        ["DAO.Recordsets"] = new (string, MemberKind, string?, bool)[]
        {
            ("Count", MemberKind.Property, "Integer", false),
            ("Item", MemberKind.Property, "DAO.Recordset", true),
            ("Refresh", MemberKind.Method, null, false),
        },
        ["ADO.Recordset"] = new (string, MemberKind, string?, bool)[]
        {
            ("MoveFirst", MemberKind.Method, null, false),
            ("MoveLast", MemberKind.Method, null, false),
            ("MoveNext", MemberKind.Method, null, false),
            ("MovePrevious", MemberKind.Method, null, false),
            ("Move", MemberKind.Method, null, false),
            ("Open", MemberKind.Method, null, false),
            ("Close", MemberKind.Method, null, false),
            ("AddNew", MemberKind.Method, null, false),
            ("Update", MemberKind.Method, null, false),
            ("CancelUpdate", MemberKind.Method, null, false),
            ("Delete", MemberKind.Method, null, false),
            ("Find", MemberKind.Method, null, false),
            ("Save", MemberKind.Method, null, false),
            ("Requery", MemberKind.Method, null, false),
            ("Clone", MemberKind.Method, "ADO.Recordset", false),
            ("ActiveConnection", MemberKind.Property, null, false),
            ("BOF", MemberKind.Property, "Boolean", false),
            ("EOF", MemberKind.Property, "Boolean", false),
            ("Bookmark", MemberKind.Property, null, false),
            ("CursorType", MemberKind.Property, "Long", false),
            ("EditMode", MemberKind.Property, "Long", false),
            ("Filter", MemberKind.Property, null, false),
            ("LockType", MemberKind.Property, "Long", false),
            ("MaxRecords", MemberKind.Property, "Long", false),
            ("RecordCount", MemberKind.Property, "Long", false),
            ("Source", MemberKind.Property, null, false),
            ("State", MemberKind.Property, "Long", false),
            ("Status", MemberKind.Property, "Long", false),
            ("AbsolutePosition", MemberKind.Property, "Long", false),
            ("Fields", MemberKind.Property, "ADO.Fields", true),
        },
        ["ADO.Connection"] = new (string, MemberKind, string?, bool)[]
        {
            ("ConnectionString", MemberKind.Property, null, false),
            ("Provider", MemberKind.Property, null, false),
            ("State", MemberKind.Property, "Long", false),
            ("Errors", MemberKind.Property, null, false),
            ("Mode", MemberKind.Property, "Long", false),
            ("CommandTimeout", MemberKind.Property, "Long", false),
            ("ConnectionTimeout", MemberKind.Property, "Long", false),
            ("DefaultDatabase", MemberKind.Property, null, false),
            ("Version", MemberKind.Property, null, false),
            ("Open", MemberKind.Method, null, false),
            ("Close", MemberKind.Method, null, false),
            ("Execute", MemberKind.Method, "ADO.Recordset", false),
            ("BeginTrans", MemberKind.Method, "Long", false),
            ("CommitTrans", MemberKind.Method, null, false),
            ("RollbackTrans", MemberKind.Method, null, false),
            ("Cancel", MemberKind.Method, null, false),
        },
        ["ADO.Command"] = new (string, MemberKind, string?, bool)[]
        {
            ("ActiveConnection", MemberKind.Property, null, false),
            ("CommandText", MemberKind.Property, null, false),
            ("CommandType", MemberKind.Property, "Long", false),
            ("CommandTimeout", MemberKind.Property, "Long", false),
            ("Parameters", MemberKind.Property, "ADO.Parameters", false),
            ("Prepared", MemberKind.Property, "Boolean", false),
            ("Name", MemberKind.Property, null, false),
            ("State", MemberKind.Property, "Long", false),
            ("Execute", MemberKind.Method, "ADO.Recordset", false),
            ("CreateParameter", MemberKind.Method, "ADO.Parameter", false),
            ("Cancel", MemberKind.Method, null, false),
        },
        ["ADO.Field"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Type", MemberKind.Property, "Long", false),
            ("Value", MemberKind.Property, null, true),
            ("DefinedSize", MemberKind.Property, "Long", false),
            ("ActualSize", MemberKind.Property, "Long", false),
            ("Attributes", MemberKind.Property, "Long", false),
            ("NumericScale", MemberKind.Property, "Byte", false),
            ("Precision", MemberKind.Property, "Byte", false),
            ("Status", MemberKind.Property, "Long", false),
            ("OriginalValue", MemberKind.Property, null, false),
            ("UnderlyingValue", MemberKind.Property, null, false),
            ("AppendChunk", MemberKind.Method, null, false),
            ("GetChunk", MemberKind.Method, "Variant", false),
        },
        ["ADO.Fields"] = new (string, MemberKind, string?, bool)[]
        {
            ("Count", MemberKind.Property, "Long", false),
            ("Item", MemberKind.Property, "ADO.Field", true),
            ("Append", MemberKind.Method, null, false),
            ("Delete", MemberKind.Method, null, false),
            ("Refresh", MemberKind.Method, null, false),
            ("Update", MemberKind.Method, null, false),
            ("CancelUpdate", MemberKind.Method, null, false),
            ("Resync", MemberKind.Method, null, false),
        },
        ["Office.FileDialog"] = new (string, MemberKind, string?, bool)[]
        {
            ("AllowMultiSelect", MemberKind.Property, "Boolean", false),
            ("ButtonName", MemberKind.Property, null, false),
            ("DialogType", MemberKind.Property, "Long", false),
            ("FilterIndex", MemberKind.Property, "Long", false),
            ("Filters", MemberKind.Property, "Office.FileDialogFilters", false),
            ("InitialFileName", MemberKind.Property, null, false),
            ("InitialView", MemberKind.Property, "Long", false),
            ("SelectedItems", MemberKind.Property, "Office.FileDialogSelectedItems", false),
            ("Title", MemberKind.Property, null, false),
            ("Item", MemberKind.Property, null, false),
            ("Show", MemberKind.Method, "Long", false),
            ("Execute", MemberKind.Method, null, false),
        },
        ["Access.Form"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Caption", MemberKind.Property, null, false),
            ("Section", MemberKind.Property, null, false),
            ("Module", MemberKind.Property, "Access.Module", false),
            ("Hwnd", MemberKind.Property, "Long", false),
            ("Visible", MemberKind.Property, "Boolean", false),
            ("RecordSource", MemberKind.Property, null, false),
            ("Filter", MemberKind.Property, null, false),
            ("FilterOn", MemberKind.Property, "Boolean", false),
            ("OrderBy", MemberKind.Property, null, false),
            ("OrderByOn", MemberKind.Property, "Boolean", false),
            ("Recordset", MemberKind.Property, "DAO.Recordset", false),
            ("RecordsetClone", MemberKind.Property, "DAO.Recordset", false),
            ("AllowAdditions", MemberKind.Property, "Boolean", false),
            ("AllowDeletions", MemberKind.Property, "Boolean", false),
            ("AllowEdits", MemberKind.Property, "Boolean", false),
            ("DataEntry", MemberKind.Property, "Boolean", false),
            ("AllowFilters", MemberKind.Property, "Boolean", false),
            ("Dirty", MemberKind.Property, "Boolean", false),
            ("ActiveControl", MemberKind.Property, "Access.Control", false),
            ("CurrentRecord", MemberKind.Property, "Long", false),
            ("NewRecord", MemberKind.Property, "Boolean", false),
            ("Refresh", MemberKind.Method, null, false),
            ("Repaint", MemberKind.Method, null, false),
            ("Requery", MemberKind.Method, null, false),
            ("Recalc", MemberKind.Method, null, false),
            ("SetFocus", MemberKind.Method, null, false),
            ("GoToPage", MemberKind.Method, null, false),
            ("Open", MemberKind.Event, null, false),
            ("Load", MemberKind.Event, null, false),
            ("Current", MemberKind.Event, null, false),
            ("Close", MemberKind.Event, null, false),
            ("Unload", MemberKind.Event, null, false),
            ("Undo", MemberKind.Event, null, false),
            ("BeforeUpdate", MemberKind.Event, null, false),
            ("AfterUpdate", MemberKind.Event, null, false),
            ("BeforeInsert", MemberKind.Event, null, false),
            ("AfterInsert", MemberKind.Event, null, false),
            ("BeforeDelConfirm", MemberKind.Event, null, false),
            ("AfterDelConfirm", MemberKind.Event, null, false),
            ("Delete", MemberKind.Event, null, false),
            ("KeyDown", MemberKind.Event, null, false),
            ("KeyPress", MemberKind.Event, null, false),
            ("KeyUp", MemberKind.Event, null, false),
            ("Click", MemberKind.Event, null, false),
            ("DblClick", MemberKind.Event, null, false),
            ("MouseDown", MemberKind.Event, null, false),
            ("MouseUp", MemberKind.Event, null, false),
            ("MouseMove", MemberKind.Event, null, false),
            ("GotFocus", MemberKind.Event, null, false),
            ("LostFocus", MemberKind.Event, null, false),
            ("Activate", MemberKind.Event, null, false),
            ("Deactivate", MemberKind.Event, null, false),
            ("Resize", MemberKind.Event, null, false),
            ("Timer", MemberKind.Event, null, false),
        },
        ["Access.Report"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Caption", MemberKind.Property, null, false),
            ("Section", MemberKind.Property, null, false),
            ("Module", MemberKind.Property, "Access.Module", false),
            ("Hwnd", MemberKind.Property, "Long", false),
            ("Visible", MemberKind.Property, "Boolean", false),
            ("RecordSource", MemberKind.Property, null, false),
            ("Filter", MemberKind.Property, null, false),
            ("FilterOn", MemberKind.Property, "Boolean", false),
            ("OrderBy", MemberKind.Property, null, false),
            ("OrderByOn", MemberKind.Property, "Boolean", false),
            ("ActiveControl", MemberKind.Property, "Access.Control", false),
            ("Page", MemberKind.Property, "Long", false),
            ("Pages", MemberKind.Property, "Long", false),
            ("PrintCount", MemberKind.Property, "Long", false),
            ("MoveLayout", MemberKind.Property, "Boolean", false),
            ("NextRecord", MemberKind.Property, "Boolean", false),
            ("FormatCount", MemberKind.Property, "Integer", false),
            ("SetFocus", MemberKind.Method, null, false),
            ("Circle", MemberKind.Method, null, false),
            ("Line", MemberKind.Method, null, false),
            ("Open", MemberKind.Event, null, false),
            ("Close", MemberKind.Event, null, false),
            ("Load", MemberKind.Event, null, false),
            ("Unload", MemberKind.Event, null, false),
            ("NoData", MemberKind.Event, null, false),
            ("Error", MemberKind.Event, null, false),
            ("Activate", MemberKind.Event, null, false),
            ("Deactivate", MemberKind.Event, null, false),
            ("Format", MemberKind.Event, null, false),
            ("Print", MemberKind.Event, null, false),
            ("Retreat", MemberKind.Event, null, false),
        },
        ["Access.Control"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Tag", MemberKind.Property, null, false),
            ("ControlType", MemberKind.Property, "Integer", false),
            ("Visible", MemberKind.Property, "Boolean", false),
            ("Enabled", MemberKind.Property, "Boolean", false),
            ("Width", MemberKind.Property, "Long", false),
            ("Height", MemberKind.Property, "Long", false),
            ("Left", MemberKind.Property, "Long", false),
            ("Top", MemberKind.Property, "Long", false),
            ("ForeColor", MemberKind.Property, "Long", false),
            ("BackColor", MemberKind.Property, "Long", false),
            ("BorderStyle", MemberKind.Property, "Integer", false),
            ("FontName", MemberKind.Property, null, false),
            ("FontSize", MemberKind.Property, "Integer", false),
            ("FontBold", MemberKind.Property, "Boolean", false),
            ("FontItalic", MemberKind.Property, "Boolean", false),
            ("FontUnderline", MemberKind.Property, "Boolean", false),
            ("Parent", MemberKind.Property, null, false),
            ("Section", MemberKind.Property, null, false),
            ("StatusBarText", MemberKind.Property, null, false),
            ("SetFocus", MemberKind.Method, null, false),
            ("SizeToFit", MemberKind.Method, null, false),
            ("Click", MemberKind.Event, null, false),
            ("DblClick", MemberKind.Event, null, false),
            ("MouseDown", MemberKind.Event, null, false),
            ("MouseUp", MemberKind.Event, null, false),
            ("MouseMove", MemberKind.Event, null, false),
        },
        // Note: Access.TextBox / ComboBox / ListBox carry the 20 shared
        // DataBoundControlMembers (composed in by the registry's private
        // helper) plus their own subtype-specific members directly. Value's
        // IsDefault flag is stored as `false` on the underlying member; the
        // projection to `IsDefault=true` happens at TryGetMember /
        // TryGetDefaultMember lookup time (verified by separate tests below).
        ["Access.TextBox"] = new (string, MemberKind, string?, bool)[]
        {
            ("ControlSource", MemberKind.Property, null, false),
            ("Value", MemberKind.Property, null, true),
            ("DefaultValue", MemberKind.Property, null, false),
            ("Format", MemberKind.Property, null, false),
            ("InputMask", MemberKind.Property, null, false),
            ("ValidationRule", MemberKind.Property, null, false),
            ("ValidationText", MemberKind.Property, null, false),
            ("Locked", MemberKind.Property, "Boolean", false),
            ("Requery", MemberKind.Method, null, false),
            ("BeforeUpdate", MemberKind.Event, null, false),
            ("AfterUpdate", MemberKind.Event, null, false),
            ("Change", MemberKind.Event, null, false),
            ("Updated", MemberKind.Event, null, false),
            ("Enter", MemberKind.Event, null, false),
            ("Exit", MemberKind.Event, null, false),
            ("GotFocus", MemberKind.Event, null, false),
            ("LostFocus", MemberKind.Event, null, false),
            ("KeyDown", MemberKind.Event, null, false),
            ("KeyPress", MemberKind.Event, null, false),
            ("KeyUp", MemberKind.Event, null, false),
            ("Text", MemberKind.Property, null, false),
            ("SelStart", MemberKind.Property, "Integer", false),
            ("SelLength", MemberKind.Property, "Integer", false),
            ("SelText", MemberKind.Property, null, false),
            ("Dirty", MemberKind.Event, null, false),
            ("Undo", MemberKind.Event, null, false),
        },
        ["Access.ComboBox"] = new (string, MemberKind, string?, bool)[]
        {
            ("ControlSource", MemberKind.Property, null, false),
            ("Value", MemberKind.Property, null, true),
            ("DefaultValue", MemberKind.Property, null, false),
            ("Format", MemberKind.Property, null, false),
            ("InputMask", MemberKind.Property, null, false),
            ("ValidationRule", MemberKind.Property, null, false),
            ("ValidationText", MemberKind.Property, null, false),
            ("Locked", MemberKind.Property, "Boolean", false),
            ("Requery", MemberKind.Method, null, false),
            ("BeforeUpdate", MemberKind.Event, null, false),
            ("AfterUpdate", MemberKind.Event, null, false),
            ("Change", MemberKind.Event, null, false),
            ("Updated", MemberKind.Event, null, false),
            ("Enter", MemberKind.Event, null, false),
            ("Exit", MemberKind.Event, null, false),
            ("GotFocus", MemberKind.Event, null, false),
            ("LostFocus", MemberKind.Event, null, false),
            ("KeyDown", MemberKind.Event, null, false),
            ("KeyPress", MemberKind.Event, null, false),
            ("KeyUp", MemberKind.Event, null, false),
            ("Text", MemberKind.Property, null, false),
            ("RowSource", MemberKind.Property, null, false),
            ("RowSourceType", MemberKind.Property, null, false),
            ("BoundColumn", MemberKind.Property, "Integer", false),
            ("ColumnCount", MemberKind.Property, "Integer", false),
            ("ColumnWidths", MemberKind.Property, null, false),
            ("ListIndex", MemberKind.Property, "Long", false),
            ("ListCount", MemberKind.Property, "Long", false),
            ("Selected", MemberKind.Property, null, false),
            ("Column", MemberKind.Property, null, false),
            ("ItemData", MemberKind.Property, null, false),
            ("LimitToList", MemberKind.Property, "Boolean", false),
            ("Dropdown", MemberKind.Method, null, false),
            ("NotInList", MemberKind.Event, null, false),
            ("Dirty", MemberKind.Event, null, false),
            ("Undo", MemberKind.Event, null, false),
        },
        ["Access.ListBox"] = new (string, MemberKind, string?, bool)[]
        {
            ("ControlSource", MemberKind.Property, null, false),
            ("Value", MemberKind.Property, null, true),
            ("DefaultValue", MemberKind.Property, null, false),
            ("Format", MemberKind.Property, null, false),
            ("InputMask", MemberKind.Property, null, false),
            ("ValidationRule", MemberKind.Property, null, false),
            ("ValidationText", MemberKind.Property, null, false),
            ("Locked", MemberKind.Property, "Boolean", false),
            ("Requery", MemberKind.Method, null, false),
            ("BeforeUpdate", MemberKind.Event, null, false),
            ("AfterUpdate", MemberKind.Event, null, false),
            ("Change", MemberKind.Event, null, false),
            ("Updated", MemberKind.Event, null, false),
            ("Enter", MemberKind.Event, null, false),
            ("Exit", MemberKind.Event, null, false),
            ("GotFocus", MemberKind.Event, null, false),
            ("LostFocus", MemberKind.Event, null, false),
            ("KeyDown", MemberKind.Event, null, false),
            ("KeyPress", MemberKind.Event, null, false),
            ("KeyUp", MemberKind.Event, null, false),
            ("RowSource", MemberKind.Property, null, false),
            ("RowSourceType", MemberKind.Property, null, false),
            ("BoundColumn", MemberKind.Property, "Integer", false),
            ("ColumnCount", MemberKind.Property, "Integer", false),
            ("ColumnWidths", MemberKind.Property, null, false),
            ("ListIndex", MemberKind.Property, "Long", false),
            ("ListCount", MemberKind.Property, "Long", false),
            ("Selected", MemberKind.Property, null, false),
            ("Column", MemberKind.Property, null, false),
            ("ItemData", MemberKind.Property, null, false),
            ("ItemsSelected", MemberKind.Property, null, false),
            ("MultiSelect", MemberKind.Property, "Integer", false),
        },
        ["Access.CommandButton"] = new (string, MemberKind, string?, bool)[]
        {
            ("Caption", MemberKind.Property, null, false),
            ("Picture", MemberKind.Property, null, false),
            ("PictureType", MemberKind.Property, "Integer", false),
            ("Hyperlink", MemberKind.Property, null, false),
            ("Default", MemberKind.Property, "Boolean", false),
            ("Cancel", MemberKind.Property, "Boolean", false),
            ("Transparent", MemberKind.Property, "Boolean", false),
        },
        ["Access.Label"] = new (string, MemberKind, string?, bool)[]
        {
            ("Caption", MemberKind.Property, null, false),
            ("AutoLabel", MemberKind.Property, "Boolean", false),
            ("HyperlinkAddress", MemberKind.Property, null, false),
            ("HyperlinkSubAddress", MemberKind.Property, null, false),
            ("TextAlign", MemberKind.Property, "Integer", false),
            ("FontWeight", MemberKind.Property, "Integer", false),
        },
        ["Access.Module"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Lines", MemberKind.Property, null, false),
            ("CountOfLines", MemberKind.Property, "Long", false),
            ("CountOfDeclarationLines", MemberKind.Property, "Long", false),
            ("Type", MemberKind.Property, "Integer", false),
            ("AddFromFile", MemberKind.Method, null, false),
            ("AddFromString", MemberKind.Method, null, false),
            ("InsertText", MemberKind.Method, null, false),
            ("InsertLines", MemberKind.Method, null, false),
            ("DeleteLines", MemberKind.Method, null, false),
            ("ReplaceLine", MemberKind.Method, null, false),
            ("Find", MemberKind.Method, "Boolean", false),
        },
        ["Access.DoCmd"] = new (string, MemberKind, string?, bool)[]
        {
            ("OpenForm", MemberKind.Method, null, false),
            ("OpenReport", MemberKind.Method, null, false),
            ("OpenQuery", MemberKind.Method, null, false),
            ("OpenTable", MemberKind.Method, null, false),
            ("OpenModule", MemberKind.Method, null, false),
            ("OpenView", MemberKind.Method, null, false),
            ("OpenFunction", MemberKind.Method, null, false),
            ("OpenStoredProcedure", MemberKind.Method, null, false),
            ("OpenDataAccessPage", MemberKind.Method, null, false),
            ("Close", MemberKind.Method, null, false),
            ("Quit", MemberKind.Method, null, false),
            ("RunCommand", MemberKind.Method, null, false),
            ("RunMacro", MemberKind.Method, null, false),
            ("RunSQL", MemberKind.Method, null, false),
            ("Run", MemberKind.Method, null, false),
            ("ApplyFilter", MemberKind.Method, null, false),
            ("ShowAllRecords", MemberKind.Method, null, false),
            ("FindRecord", MemberKind.Method, null, false),
            ("FindNext", MemberKind.Method, null, false),
            ("GoToRecord", MemberKind.Method, null, false),
            ("GoToControl", MemberKind.Method, null, false),
            ("GoToPage", MemberKind.Method, null, false),
            ("SearchForRecord", MemberKind.Method, null, false),
            ("RefreshRecord", MemberKind.Method, null, false),
            ("SetWarnings", MemberKind.Method, null, false),
            ("Hourglass", MemberKind.Method, null, false),
            ("Beep", MemberKind.Method, null, false),
            ("Echo", MemberKind.Method, null, false),
            ("Maximize", MemberKind.Method, null, false),
            ("Minimize", MemberKind.Method, null, false),
            ("Restore", MemberKind.Method, null, false),
            ("Save", MemberKind.Method, null, false),
            ("ShowToolbar", MemberKind.Method, null, false),
            ("MoveSize", MemberKind.Method, null, false),
            ("SelectObject", MemberKind.Method, null, false),
            ("OutputTo", MemberKind.Method, null, false),
            ("PrintOut", MemberKind.Method, null, false),
            ("SendObject", MemberKind.Method, null, false),
            ("TransferDatabase", MemberKind.Method, null, false),
            ("TransferText", MemberKind.Method, null, false),
            ("TransferSpreadsheet", MemberKind.Method, null, false),
            ("CopyDatabaseFile", MemberKind.Method, null, false),
            ("DeleteObject", MemberKind.Method, null, false),
            ("Rename", MemberKind.Method, null, false),
            ("CopyObject", MemberKind.Method, null, false),
            ("SetProperty", MemberKind.Method, null, false),
        },
        ["Access.Application"] = new (string, MemberKind, string?, bool)[]
        {
            ("Name", MemberKind.Property, null, false),
            ("Version", MemberKind.Property, null, false),
            ("BuildVersion", MemberKind.Property, null, false),
            ("UserName", MemberKind.Property, null, false),
            ("Path", MemberKind.Property, null, false),
            ("CurrentProject", MemberKind.Property, "Access.CurrentProject", false),
            ("CurrentData", MemberKind.Property, "Access.CurrentData", false),
            ("Forms", MemberKind.Property, "Access.Forms", false),
            ("Reports", MemberKind.Property, "Access.Reports", false),
            ("Modules", MemberKind.Property, "Access.Modules", false),
            ("Macros", MemberKind.Property, "Access.AllMacros", false),
            ("Screen", MemberKind.Property, "Access.Screen", false),
            ("DoCmd", MemberKind.Property, "Access.DoCmd", false),
            ("References", MemberKind.Property, "Access.References", false),
            ("FileDialog", MemberKind.Property, "Office.FileDialog", false),
            ("IsCompiled", MemberKind.Property, "Boolean", false),
            ("MenuBar", MemberKind.Property, null, false),
            ("ShortcutMenuBar", MemberKind.Property, null, false),
            ("CurrentDb", MemberKind.Method, "DAO.Database", false),
            ("CodeDb", MemberKind.Method, "DAO.Database", false),
            ("Run", MemberKind.Method, null, false),
            ("Quit", MemberKind.Method, null, false),
            ("NewCurrentDatabase", MemberKind.Method, null, false),
            ("OpenCurrentDatabase", MemberKind.Method, null, false),
            ("CloseCurrentDatabase", MemberKind.Method, null, false),
            ("Echo", MemberKind.Method, null, false),
            ("Hourglass", MemberKind.Method, null, false),
            ("Beep", MemberKind.Method, null, false),
            ("RefreshDatabaseWindow", MemberKind.Method, null, false),
            ("CompactRepair", MemberKind.Method, null, false),
            ("GetOption", MemberKind.Method, null, false),
            ("SetOption", MemberKind.Method, null, false),
        },
    };

    /// <summary>
    /// Sentinel: every registered type must have an entry in
    /// <see cref="ExpectedDirectMembers"/>. Catches the "added a new type
    /// to the registry but forgot to extend the catalog" case.
    /// </summary>
    [Fact]
    public void EveryRegisteredType_HasEntryInExpectedCatalog()
    {
        foreach (var name in _impl.RegisteredTypeNames)
        {
            Assert.True(
                ExpectedDirectMembers.ContainsKey(name),
                $"Registered type '{name}' has no entry in ExpectedDirectMembers. " +
                $"Add one (mirror the registry's Build*() method exactly), and update " +
                $"AllRegisteredTypeNamesArray + the count theory if needed.");
        }
    }

    /// <summary>
    /// Exhaustive per-type member-metadata check. For every registered type,
    /// the catalog's expected (Name, Kind, ReturnType, IsDefault) tuples must
    /// exactly equal the registry's actual direct members. Compared as a set
    /// keyed by Name (registration order is not part of the contract). Fails
    /// per type so the diagnostic names exactly which member changed.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllRegisteredTypeNames))]
    public void EveryRegisteredType_DirectMembersMatchExpectedCatalog(string typeName)
    {
        Assert.True(
            ExpectedDirectMembers.TryGetValue(typeName, out var expected),
            $"No expected member catalog for type '{typeName}'. Add one to ExpectedDirectMembers.");
        Assert.True(_registry.TryGetType(typeName, out var info), $"Type missing: {typeName}");
        Assert.NotNull(info);

        var expectedByName = expected!
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var actualByName = info!.Members.Values
            .ToDictionary(
                m => m.Name,
                m => (m.Name, m.Kind, m.ReturnType, m.IsDefault),
                StringComparer.OrdinalIgnoreCase);

        var added = actualByName.Keys.Except(expectedByName.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var removed = expectedByName.Keys.Except(actualByName.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(
            added.Count == 0 && removed.Count == 0,
            $"Member set drift for '{typeName}'. " +
            $"Added (in registry, missing from catalog): [{string.Join(", ", added)}]. " +
            $"Removed (in catalog, missing from registry): [{string.Join(", ", removed)}]. " +
            $"Update ExpectedDirectMembers and the count theory accordingly.");

        foreach (var (memberName, expectedTuple) in expectedByName)
        {
            var actualTuple = actualByName[memberName];
            Assert.True(
                expectedTuple.Kind == actualTuple.Kind
                    && expectedTuple.ReturnType == actualTuple.ReturnType
                    && expectedTuple.IsDefault == actualTuple.IsDefault,
                $"Member '{typeName}.{memberName}' drifted. " +
                $"Expected: Kind={expectedTuple.Kind}, ReturnType={expectedTuple.ReturnType ?? "null"}, IsDefault={expectedTuple.IsDefault}. " +
                $"Actual:   Kind={actualTuple.Kind}, ReturnType={actualTuple.ReturnType ?? "null"}, IsDefault={actualTuple.IsDefault}.");
        }
    }
}
