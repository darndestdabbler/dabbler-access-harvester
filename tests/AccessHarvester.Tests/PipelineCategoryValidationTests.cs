using System.Linq;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic pins for the <c>--types</c> known-category source of truth (006 F-1-3).
/// <see cref="CorePipeline.KnownCategories"/> is what the CLI validates a <c>--types</c>
/// filter against; an unknown value is otherwise passed straight into the pure
/// <see cref="CorePipeline.WithCategories"/> set filter, matches nothing, and the run
/// reports "Ok: 0, Failed: 0" and exits 0 — the silent empty success this set fixes.
/// The CLI exit-code behaviour itself is pinned black-box in
/// <c>AccessHarvester.Integration.Cli.CliTypesValidationTests</c>.
/// </summary>
public class PipelineCategoryValidationTests
{
    [Theory]
    [InlineData("Forms")]
    [InlineData("Reports")]
    [InlineData("Macros")]
    [InlineData("ModulesAndClasses")]
    [InlineData("Queries")]
    [InlineData("Tables")]
    [InlineData("Relationships")]
    [InlineData("Navigation")] // added by Set 012 (SwitchboardExporter / Navigation translator)
    public void KnownCategories_IncludesEverySupportedCategory(string category)
    {
        Assert.Contains(category, CorePipeline.Default().KnownCategories);
    }

    [Fact]
    public void KnownCategories_IsCaseInsensitiveDeduplicatedUnion()
    {
        var known = CorePipeline.Default().KnownCategories;

        // Every emitted category appears exactly once even though Forms/Reports/etc.
        // are present as both an exporter and a translator (and an enricher).
        Assert.Equal(known.Count, known.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
        // Navigation is the post-012 addition the stale usage text omitted.
        Assert.Contains("Navigation", known);
    }

    [Fact]
    public void WithCategories_UnknownValue_FiltersToEmptyPipeline()
    {
        // This is exactly the silent-no-op the CLI now rejects: an unknown value
        // matches nothing, leaving a pipeline with no work to do.
        var filtered = CorePipeline.Default().WithCategories(new[] { "Formz" });
        Assert.True(filtered.IsEmpty);
    }

    [Fact]
    public void WithCategories_EmptySelection_IsEmpty()
    {
        Assert.True(CorePipeline.Default().WithCategories(System.Array.Empty<string>()).IsEmpty);
    }

    [Fact]
    public void WithCategories_ValidValue_IsNotEmptyAndCaseInsensitive()
    {
        Assert.False(CorePipeline.Default().WithCategories(new[] { "forms" }).IsEmpty);
        Assert.Contains("Forms", CorePipeline.Default().WithCategories(new[] { "forms" }).ExporterCategories);
    }
}
