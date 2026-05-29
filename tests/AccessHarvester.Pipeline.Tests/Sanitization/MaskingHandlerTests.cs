using System;
using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;
using static AccessHarvester.Pipeline.Tests.Sanitization.TestFixtures;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

public class MaskingHandlerTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    private static MaskingContext Ctx(ClassifiedColumn col, RemapRegistry? r = null)
        => new("T", col, r ?? new RemapRegistry(), Epoch);

    [Fact]
    public void Preserve_returns_original_unchanged()
    {
        var col = Col("c", ColumnCategory.GenericNonSensitive);
        Assert.Equal("abc", new PreserveHandler().Apply("abc", Ctx(col), 0));
        Assert.Null(new PreserveHandler().Apply(null, Ctx(col), 0));
    }

    [Fact]
    public void MockReplace_with_format_pattern_renders_deterministic_digits()
    {
        // Same column + same row index => same output (determinism).
        var col = Col("SSN", ColumnCategory.DirectIdentifierPii, formatPattern: "###-##-####");
        var v1 = new MockReplaceHandler().Apply("orig", Ctx(col), 0)?.ToString();
        var v2 = new MockReplaceHandler().Apply("orig", Ctx(col), 0)?.ToString();
        Assert.Equal(v1, v2);
        Assert.Matches(@"^\d{3}-\d{2}-\d{4}$", v1!);
    }

    [Fact]
    public void MockReplace_different_row_indexes_diverge()
    {
        var col = Col("SSN", ColumnCategory.DirectIdentifierPii, formatPattern: "###-##-####");
        var v1 = new MockReplaceHandler().Apply("a", Ctx(col), 0)?.ToString();
        var v2 = new MockReplaceHandler().Apply("a", Ctx(col), 1)?.ToString();
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void MockReplace_without_pattern_uses_column_label()
    {
        var col = Col("FirstName", ColumnCategory.DirectIdentifierPii);
        var v = new MockReplaceHandler().Apply("Jane", Ctx(col), 7)?.ToString();
        Assert.Equal("MOCK-FirstName-7", v);
    }

    [Fact]
    public void MockReplace_null_stays_null()
    {
        var col = Col("c", ColumnCategory.DirectIdentifierPii);
        Assert.Null(new MockReplaceHandler().Apply(null, Ctx(col), 0));
    }

    [Fact]
    public void Generalize_dob_collapses_to_january_first()
    {
        var col = Col("DateOfBirth", ColumnCategory.QuasiIdentifier, dataType: "Date/Time");
        var v = new GeneralizeHandler().Apply(new DateTime(1985, 6, 12), Ctx(col), 0);
        Assert.Equal(new DateTime(1985, 1, 1), v);
    }

    [Fact]
    public void Generalize_zip5_truncates_to_zip3()
    {
        var col = Col("ZipCode", ColumnCategory.QuasiIdentifier);
        var v = new GeneralizeHandler().Apply("12345", Ctx(col), 0)?.ToString();
        Assert.Equal("12300", v);
    }

    [Fact]
    public void Redact_returns_constant_for_non_null()
    {
        var col = Col("ssn", ColumnCategory.SensitiveNonPii);
        Assert.Equal(RedactHandler.ConstantValue, new RedactHandler().Apply("999-99-9999", Ctx(col), 0));
        Assert.Null(new RedactHandler().Apply(null, Ctx(col), 0));
    }

    [Fact]
    public void ScrubFreeText_returns_lorem_of_matching_length_bucket()
    {
        var col = Col("Notes", ColumnCategory.FreeTextMemo, memo: true, dataType: "Memo");
        var v = new ScrubFreeTextHandler().Apply("Secret patient observation", Ctx(col), 0)?.ToString();
        Assert.NotNull(v);
        Assert.Equal("Secret patient observation".Length, v!.Length);
        Assert.DoesNotContain("Secret", v);
        Assert.StartsWith("Lorem", v);
    }

    [Fact]
    public void ScrubFreeText_long_memo_repeats_lorem()
    {
        var col = Col("Notes", ColumnCategory.FreeTextMemo, memo: true, dataType: "Memo");
        var input = new string('a', 1000);
        var v = new ScrubFreeTextHandler().Apply(input, Ctx(col), 0)?.ToString();
        Assert.Equal(1000, v!.Length);
        Assert.StartsWith("Lorem", v);
    }

    [Fact]
    public void Regenerate_for_datetime_column_writes_epoch()
    {
        var col = Col("LastModified", ColumnCategory.SystemAudit, dataType: "Date/Time", required: true);
        var v = new RegenerateHandler().Apply(new DateTime(2020, 1, 1), Ctx(col), 0);
        Assert.Equal(Epoch.UtcDateTime, v);
    }

    [Fact]
    public void Regenerate_for_string_column_writes_constant()
    {
        var col = Col("CreatedBy", ColumnCategory.SystemAudit, dataType: "Text", required: true);
        var v = new RegenerateHandler().Apply("alice", Ctx(col), 0);
        Assert.Equal(RegenerateHandler.ConstantValue, v);
    }

    [Fact]
    public void Regenerate_nullable_null_stays_null()
    {
        var col = Col("CreatedBy", ColumnCategory.SystemAudit, dataType: "Text", required: false);
        Assert.Null(new RegenerateHandler().Apply(null, Ctx(col), 0));
    }

    [Fact]
    public void RemapId_for_pk_uses_self_table_column()
    {
        var registry = new RemapRegistry(startValue: 100);
        var col = Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true);
        var first  = new RemapIdHandler().Apply(42, Ctx(col, registry), 0);
        var second = new RemapIdHandler().Apply(42, Ctx(col, registry), 1);
        Assert.Equal(first, second);
        Assert.Equal(100L, first);
    }

    [Fact]
    public void RemapId_for_fk_uses_fk_target()
    {
        var registry = new RemapRegistry(startValue: 100);
        // Force the PK's mapping to exist first.
        var pkCol = Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true);
        new RemapIdHandler().Apply(42, new MaskingContext("Person", pkCol, registry, Epoch), 0);

        var fkCol = Col("PersonID", ColumnCategory.ForeignKey, fk: true,
            fkTarget: new ClassifiedFkTarget("Person", "PersonID"));
        var fkValue = new RemapIdHandler().Apply(42, new MaskingContext("Visit", fkCol, registry, Epoch), 0);

        Assert.Equal(100L, fkValue);
    }
}
