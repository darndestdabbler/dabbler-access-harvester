using AccessHarvester.Pipeline.Classification;
using AccessHarvester.Pipeline.Sanitization;
using Xunit;
using static AccessHarvester.Pipeline.Tests.Sanitization.TestFixtures;

namespace AccessHarvester.Pipeline.Tests.Sanitization;

public class EffectiveCategoryResolverTests
{
    [Fact]
    public void Default_strategy_per_category_matches_plan_3_5()
    {
        var c1 = Col("c1", ColumnCategory.DirectIdentifierPii);
        Assert.Equal(MaskingStrategy.MockReplace,   EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.DirectIdentifierPii,   c1));

        var c2 = Col("c2", ColumnCategory.QuasiIdentifier);
        Assert.Equal(MaskingStrategy.Generalize,    EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.QuasiIdentifier,       c2));

        var c3 = Col("c3", ColumnCategory.SensitiveNonPii);
        Assert.Equal(MaskingStrategy.Redact,        EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.SensitiveNonPii,       c3));

        var c4 = Col("c4", ColumnCategory.FreeTextMemo, memo: true);
        Assert.Equal(MaskingStrategy.ScrubFreeText, EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.FreeTextMemo,          c4));

        var c5 = Col("c5", ColumnCategory.LookupReference);
        Assert.Equal(MaskingStrategy.Preserve,      EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.LookupReference,       c5));

        var c6 = Col("c6", ColumnCategory.SystemAudit);
        Assert.Equal(MaskingStrategy.Regenerate,    EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.SystemAudit,           c6));

        var c7 = Col("c7", ColumnCategory.BusinessNaturalKey);
        Assert.Equal(MaskingStrategy.Preserve,      EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.BusinessNaturalKey,    c7));

        var c8 = Col("c8", ColumnCategory.GenericNonSensitive);
        Assert.Equal(MaskingStrategy.Preserve,      EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.GenericNonSensitive,   c8));

        var c9 = Col("c9", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true);
        Assert.Equal(MaskingStrategy.RemapId,       EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.SurrogateAutonumberPk, c9));

        var c10 = Col("c10", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true,
            humanOverride: EmptyColOverride with { PreserveAutonumber = true });
        Assert.Equal(MaskingStrategy.Preserve,      EffectiveCategoryResolver.DefaultStrategyFor(ColumnCategory.SurrogateAutonumberPk, c10));
    }

    [Fact]
    public void Resolve_uses_fk_graph_for_pk_columns()
    {
        var doc = Doc(Table("Person",
            Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true)));
        var graphResult = FkGraphBuilder.Build(doc);

        var column = doc.Tables[0].Columns[0];
        var decision = EffectiveCategoryResolver.Resolve(column,
            new TableColumn("Person", "PersonID"), graphResult.Graph);

        Assert.Equal(MaskingStrategy.RemapId, decision.Strategy);
    }

    [Fact]
    public void Resolve_uses_fk_graph_for_fk_columns()
    {
        var doc = Doc(
            Table("Person",
                Col("PersonID", ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true)),
            Table("Visit",
                Col("VisitID",  ColumnCategory.SurrogateAutonumberPk, pk: true, autoNumber: true),
                Col("PersonID", ColumnCategory.ForeignKey, fk: true,
                    fkTarget: new ClassifiedFkTarget("Person", "PersonID"))));
        var graphResult = FkGraphBuilder.Build(doc);

        var fkColumn = doc.Tables[1].Columns[1];
        var decision = EffectiveCategoryResolver.Resolve(fkColumn,
            new TableColumn("Visit", "PersonID"), graphResult.Graph);

        Assert.Equal(MaskingStrategy.RemapId, decision.Strategy);
    }

    [Fact]
    public void Human_category_override_takes_precedence_over_guessed()
    {
        var col = Col("c", ColumnCategory.GenericNonSensitive,
            humanOverride: EmptyColOverride with { Category = ColumnCategory.DirectIdentifierPii });
        var doc = Doc(Table("T", col));
        var graphResult = FkGraphBuilder.Build(doc);

        var decision = EffectiveCategoryResolver.Resolve(col,
            new TableColumn("T", "c"), graphResult.Graph);

        Assert.Equal(ColumnCategory.DirectIdentifierPii, decision.Category);
        Assert.Equal(MaskingStrategy.MockReplace,        decision.Strategy);
    }

    [Fact]
    public void Human_masking_strategy_override_wins_on_non_key_column()
    {
        var col = Col("Notes", ColumnCategory.FreeTextMemo, memo: true,
            humanOverride: EmptyColOverride with { MaskingStrategy = MaskingStrategy.Redact });
        var doc = Doc(Table("T", col));
        var graphResult = FkGraphBuilder.Build(doc);

        var decision = EffectiveCategoryResolver.Resolve(col,
            new TableColumn("T", "Notes"), graphResult.Graph);

        Assert.Equal(MaskingStrategy.Redact, decision.Strategy);
    }
}
