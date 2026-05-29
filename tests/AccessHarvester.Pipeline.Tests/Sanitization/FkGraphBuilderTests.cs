using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;
using static AccessHarvester.Pipeline.Tests.Sanitization.TestFixtures;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

public class FkGraphBuilderTests
{
    [Fact]
    public void Surrogate_autonumber_pk_remaps_by_default()
    {
        var doc = Doc(Table("Person",
            Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true)));

        var result = FkGraphBuilder.Build(doc);

        Assert.True(result.Graph.PkRemapDecisions[new TableColumn("Person", "PersonID")]);
    }

    [Fact]
    public void Preserve_autonumber_human_flag_keeps_pk_unchanged()
    {
        var doc = Doc(Table("Person",
            Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true,
                humanOverride: EmptyColOverride with { PreserveAutonumber = true })));

        var result = FkGraphBuilder.Build(doc);

        Assert.False(result.Graph.PkRemapDecisions[new TableColumn("Person", "PersonID")]);
    }

    [Fact]
    public void Business_natural_key_pk_does_not_remap_by_default()
    {
        var doc = Doc(Table("Codes",
            Col("Code", ColumnCategory.BusinessNaturalKey, pk: true)));

        var result = FkGraphBuilder.Build(doc);

        Assert.False(result.Graph.PkRemapDecisions[new TableColumn("Codes", "Code")]);
    }

    [Fact]
    public void Fk_inherits_remap_decision_from_referenced_pk()
    {
        var doc = Doc(
            Table("Person",
                Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true)),
            Table("Visit",
                Col("VisitID",  ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true),
                Col("PersonID", ColumnCategory.ForeignKey, fk: true,
                    fkTarget: new ClassifiedFkTarget("Person", "PersonID"))));

        var result = FkGraphBuilder.Build(doc);

        Assert.Equal(MaskingStrategy.RemapId,
            result.Graph.FkEffectiveStrategies[new TableColumn("Visit", "PersonID")]);
    }

    [Fact]
    public void Fk_to_preserved_pk_preserves()
    {
        var doc = Doc(
            Table("Codes",
                Col("Code", ColumnCategory.BusinessNaturalKey, pk: true)),
            Table("Event",
                Col("EventID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true),
                Col("Code",    ColumnCategory.ForeignKey, fk: true,
                    fkTarget: new ClassifiedFkTarget("Codes", "Code"))));

        var result = FkGraphBuilder.Build(doc);

        Assert.Equal(MaskingStrategy.Preserve,
            result.Graph.FkEffectiveStrategies[new TableColumn("Event", "Code")]);
    }

    [Fact]
    public void Missing_fk_target_is_reported_and_preserved()
    {
        // FK references a table that is not in the document.
        var doc = Doc(
            Table("Event",
                Col("EventID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true),
                Col("OrphanRef", ColumnCategory.ForeignKey, fk: true,
                    fkTarget: new ClassifiedFkTarget("Missing", "Id"))));

        var result = FkGraphBuilder.Build(doc);

        Assert.Equal(MaskingStrategy.Preserve,
            result.Graph.FkEffectiveStrategies[new TableColumn("Event", "OrphanRef")]);
        Assert.Contains(new TableColumn("Missing", "Id"), result.MissingFkTargets);
    }

    [Fact]
    public void Tablecolumn_key_is_case_insensitive()
    {
        // Access identifiers are case-insensitive; the FK target may differ
        // in case from the PK column the document declares.
        var doc = Doc(
            Table("Person",
                Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true)),
            Table("Visit",
                Col("VisitID",  ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true),
                Col("PersonID", ColumnCategory.ForeignKey, fk: true,
                    fkTarget: new ClassifiedFkTarget("PERSON", "personid"))));

        var result = FkGraphBuilder.Build(doc);

        Assert.Equal(MaskingStrategy.RemapId,
            result.Graph.FkEffectiveStrategies[new TableColumn("Visit", "PersonID")]);
        Assert.Empty(result.MissingFkTargets);
    }

    [Fact]
    public void Human_override_remap_id_on_business_key_pk_triggers_remap()
    {
        // Schema allows humanOverride.maskingStrategy = remap-id on any column;
        // for a PK with a business-natural-key default, the explicit override wins.
        var doc = Doc(Table("Codes",
            Col("Code", ColumnCategory.BusinessNaturalKey, pk: true,
                humanOverride: EmptyColOverride with { MaskingStrategy = MaskingStrategy.RemapId })));

        var result = FkGraphBuilder.Build(doc);

        Assert.True(result.Graph.PkRemapDecisions[new TableColumn("Codes", "Code")]);
    }
}
