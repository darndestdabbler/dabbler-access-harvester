using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Vba.Parser.Tests;

// Contract tests for VbaModuleBodyLoader — strips the canonical leading
// VBA module-header block (VERSION / BEGIN..END / Attribute VB_*) and
// captures it as typed ModuleHeaderMetadata. Per spec
// `docs/session-sets/vba-attribute-header-strip/spec.md` "In scope" §6.
public class VbaModuleBodyLoaderTests
{
    // ── Canonical strip / metadata extraction ────────────────────────

    [Fact]
    public void Load_strips_full_savestext_header_and_populates_all_recognized_fields()
    {
        const string source = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "Form_frmClientEdit"
            Attribute VB_GlobalNameSpace = False
            Attribute VB_Creatable = False
            Attribute VB_PredeclaredId = True
            Attribute VB_Exposed = False
            Option Compare Database
            Option Explicit

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.NotNull(header);
        Assert.Equal(1, header!.VersionMajor);
        Assert.Equal(0, header.VersionMinor);
        Assert.True(header.MultiUse);
        Assert.Equal("Form_frmClientEdit", header.VbName);
        Assert.False(header.VbGlobalNameSpace);
        Assert.False(header.VbCreatable);
        Assert.True(header.VbPredeclaredId);
        Assert.False(header.VbExposed);
        Assert.Empty(header.AdditionalAttributes);

        Assert.StartsWith("Option Compare Database", body);
        Assert.EndsWith("Option Explicit\n", body);
    }

    [Fact]
    public void Load_strips_attribute_only_header_yaml_mirror_shape()
    {
        // The dominant case in this repo's YAML mirror: no
        // VERSION/BEGIN/END/VB_Name prefix, just the leading
        // `Attribute VB_*` lines.
        const string source = """
            Attribute VB_GlobalNameSpace = False
            Attribute VB_Creatable = True
            Attribute VB_PredeclaredId = True
            Attribute VB_Exposed = False
            Option Compare Database
            Option Explicit

            Public Sub Form_Load()
            End Sub

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.NotNull(header);
        Assert.Null(header!.VersionMajor);
        Assert.Null(header.VersionMinor);
        Assert.Null(header.MultiUse);
        Assert.Null(header.VbName);
        Assert.False(header.VbGlobalNameSpace);
        Assert.True(header.VbCreatable);
        Assert.True(header.VbPredeclaredId);
        Assert.False(header.VbExposed);
        Assert.StartsWith("Option Compare Database", body);
    }

    [Fact]
    public void Load_returns_unchanged_body_and_null_header_when_no_header_present()
    {
        const string source = """
            Option Explicit

            Public Function Add(a As Long, b As Long) As Long
                Add = a + b
            End Function

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.Null(header);
        Assert.Equal(source, body);
    }

    // ── Idempotence ─────────────────────────────────────────────────

    [Fact]
    public void Load_is_idempotent_on_stripped_body()
    {
        const string source = """
            Attribute VB_GlobalNameSpace = False
            Attribute VB_Creatable = True
            Option Explicit

            """;

        var (firstBody, firstHeader) = VbaModuleBodyLoader.Load(source);
        Assert.NotNull(firstHeader);

        var (secondBody, secondHeader) = VbaModuleBodyLoader.Load(firstBody);

        Assert.Null(secondHeader);
        Assert.Equal(firstBody, secondBody);
    }

    // ── Newline + BOM tolerance ─────────────────────────────────────

    [Fact]
    public void Load_tolerates_crlf_line_endings()
    {
        const string source = "Attribute VB_GlobalNameSpace = False\r\n" +
                              "Attribute VB_Creatable = True\r\n" +
                              "Option Explicit\r\n";

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.NotNull(header);
        Assert.False(header!.VbGlobalNameSpace);
        Assert.True(header.VbCreatable);
        Assert.Equal("Option Explicit\r\n", body);
    }

    [Fact]
    public void Load_tolerates_leading_utf8_bom()
    {
        const string source = "﻿Attribute VB_GlobalNameSpace = False\nOption Explicit\n";

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.NotNull(header);
        Assert.False(header!.VbGlobalNameSpace);
        // BOM is stripped regardless of whether a header followed.
        Assert.Equal("Option Explicit\n", body);
    }

    [Fact]
    public void Load_strips_bom_even_when_no_header_present()
    {
        const string source = "﻿Option Explicit\n";

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.Null(header);
        Assert.Equal("Option Explicit\n", body);
    }

    // ── Class (.cls) module header ──────────────────────────────────

    [Fact]
    public void Load_handles_class_module_header_with_creatable_true()
    {
        const string source = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "ClsClient"
            Attribute VB_GlobalNameSpace = False
            Attribute VB_Creatable = True
            Attribute VB_PredeclaredId = False
            Attribute VB_Exposed = True
            Option Explicit

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.NotNull(header);
        Assert.Equal("ClsClient", header!.VbName);
        Assert.True(header.VbCreatable);
        Assert.False(header.VbPredeclaredId);
        Assert.True(header.VbExposed);
        Assert.Equal("Option Explicit\n", body);
    }

    // ── Malformed / partial headers — no heuristic strip ────────────

    [Fact]
    public void Load_does_not_strip_when_version_present_without_begin_end()
    {
        const string source = """
            VERSION 1.0 CLASS
            Option Explicit

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.Null(header);
        Assert.Equal(source, body);
    }

    [Fact]
    public void Load_does_not_strip_when_version_followed_directly_by_attributes_without_begin_end()
    {
        // Regression for the verifier-flagged Major: `VERSION + Attribute+`
        // without `BEGIN..END` is structurally malformed and must NOT
        // be heuristic-stripped.
        const string source = """
            VERSION 1.0 CLASS
            Attribute VB_Name = "ClsMalformed"
            Attribute VB_Creatable = True
            Option Explicit

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.Null(header);
        Assert.Equal(source, body);
    }

    [Fact]
    public void Load_does_not_strip_when_version_begin_present_without_attributes()
    {
        // VERSION + BEGIN..END but no trailing `Attribute VB_*` lines.
        // The regex requires at least one Attribute line to anchor —
        // without it, this is malformed and we leave the source alone.
        const string source = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Option Explicit

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.Null(header);
        Assert.Equal(source, body);
    }

    [Fact]
    public void Load_does_not_strip_in_body_attribute_lookalike()
    {
        // An `Attribute`-shaped line that is NOT at start-of-source —
        // e.g., a buried constant declaration. Not the canonical
        // leading block; do not touch it.
        const string source = """
            Option Explicit

            Public Sub Demo()
                Const Attribute = "not a header"
                Debug.Print Attribute
            End Sub

            """;

        var (body, header) = VbaModuleBodyLoader.Load(source);

        Assert.Null(header);
        Assert.Equal(source, body);
    }

    // ── Unknown VB_* attributes land in AdditionalAttributes ────────

    [Fact]
    public void Load_collects_unknown_vb_attributes_into_additional_attributes_map()
    {
        const string source = """
            Attribute VB_GlobalNameSpace = False
            Attribute VB_Description = "A demo module"
            Attribute VB_Helpfile = "demo.chm"
            Option Explicit

            """;

        var (_, header) = VbaModuleBodyLoader.Load(source);

        Assert.NotNull(header);
        Assert.Equal(2, header!.AdditionalAttributes.Count);
        Assert.Equal("\"A demo module\"", header.AdditionalAttributes["Description"]);
        Assert.Equal("\"demo.chm\"", header.AdditionalAttributes["Helpfile"]);
        // Sorted Ordinal so AdditionalAttributes is deterministic.
        Assert.Equal(new[] { "Description", "Helpfile" },
            header.AdditionalAttributes.Keys.ToArray());
    }

    // ── LoadAndParse attaches Header to Module ──────────────────────

    [Fact]
    public void LoadAndParse_attaches_header_to_module_ast()
    {
        const string source = """
            Attribute VB_GlobalNameSpace = False
            Attribute VB_Creatable = True
            Attribute VB_PredeclaredId = True
            Attribute VB_Exposed = False
            Option Explicit

            Public Sub Form_Load()
            End Sub

            """;

        var (ast, result) = VbaModuleBodyLoader.LoadAndParse(source);

        Assert.NotNull(ast.Header);
        Assert.True(ast.Header!.VbPredeclaredId);
        Assert.False(ast.Header.VbExposed);
        // The stripped body parses without error-severity diagnostics.
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        // At least one ModuleItem (the Option Explicit + Sub) should be present.
        Assert.NotEmpty(ast.Items);
    }

    [Fact]
    public void LoadAndParse_leaves_header_null_when_source_has_no_header()
    {
        const string source = """
            Option Explicit

            Public Sub NoHeader()
            End Sub

            """;

        var (ast, _) = VbaModuleBodyLoader.LoadAndParse(source);

        Assert.Null(ast.Header);
    }
}
