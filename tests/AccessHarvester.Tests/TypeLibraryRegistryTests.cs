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
        "Access.Control", "Access.DataBoundControl",
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
    [InlineData("Access.DataBoundControl", "ControlSource", MemberKind.Property, "")]
    [InlineData("Access.DataBoundControl", "BeforeUpdate", MemberKind.Event, "")]
    [InlineData("Access.DataBoundControl", "Requery", MemberKind.Method, "")]
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
    // Resolution via Access.DataBoundControl -> Access.Control:
    [InlineData("Access.TextBox", "Name")]                // -> Access.Control
    [InlineData("Access.TextBox", "Visible")]             // -> Access.Control
    [InlineData("Access.TextBox", "Click")]               // -> Access.Control event
    [InlineData("Access.TextBox", "BeforeUpdate")]        // -> Access.DataBoundControl event
    [InlineData("Access.TextBox", "ControlSource")]       // -> Access.DataBoundControl
    [InlineData("Access.TextBox", "Value")]               // -> Access.DataBoundControl (also TextBox's default)
    [InlineData("Access.ComboBox", "Name")]
    [InlineData("Access.ComboBox", "Visible")]
    [InlineData("Access.ComboBox", "Click")]
    [InlineData("Access.ComboBox", "AfterUpdate")]
    [InlineData("Access.ListBox", "Name")]
    [InlineData("Access.ListBox", "ControlSource")]
    [InlineData("Access.ListBox", "Value")]
    // Resolution via Access.Control directly (CommandButton + Label do NOT
    // pass through DataBoundControl):
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
    /// controls. Verifies the round-2 fix that introduced
    /// <c>Access.DataBoundControl</c> as an intermediate base — Label and
    /// CommandButton inherit from <c>Access.Control</c> directly, not from
    /// the data-bound layer, so <c>label.Value</c> / <c>button.BeforeUpdate</c>
    /// must report not-found.
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

    /// <summary>
    /// The same lookup on the base type that owns the underlying record must
    /// report IsDefault=false (Access.DataBoundControl.Value is not the
    /// default *of* Access.DataBoundControl — DataBoundControl declares no
    /// default member).
    /// </summary>
    [Fact]
    public void TryGetMember_OnBaseValue_ReportsIsDefaultFalseAtBase()
    {
        Assert.True(_registry.TryGetMember("Access.DataBoundControl", "Value", out var info));
        Assert.NotNull(info);
        Assert.False(info!.IsDefault);
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
    [InlineData("Access.TextBox", "Value")]   // declared at subtype, inherited member from Control
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
    [InlineData("Access.DataBoundControl", 20)]
    [InlineData("Access.TextBox", 6)]
    [InlineData("Access.ComboBox", 16)]
    [InlineData("Access.ListBox", 12)]
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
}
