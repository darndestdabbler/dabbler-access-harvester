using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Regression test deferred from Session 1 (nice-to-have R4): mutating the
/// input <c>ParametersUsed</c> dictionary AFTER <see cref="ReportBaselineService.Capture"/>
/// returns must not change the serialized aggregates. The contract is
/// already enforced via the compute-time
/// <c>ToImmutableSortedDictionary</c> snapshot in
/// <see cref="ReportAggregateComputer"/>; this test pins it so a future
/// refactor that drops the snapshot regresses loudly.
/// </summary>
public class ReportBaselineServiceParametersUsedMutationTests
{
    private sealed class StubExecutor : IReportQueryExecutor
    {
        public DataTable Execute(string sql, IReadOnlyList<object?>? positionalParameters)
        {
            var t = new DataTable();
            t.Columns.Add("OrderID", typeof(int));
            t.Rows.Add(1);
            return t;
        }
    }

    [Fact]
    public void Capture_SnapshotsParametersUsed_SoPostComputeMutationsDoNotAffectSerializedJson()
    {
        var svc = new ReportBaselineService(new StubExecutor(), harvesterVersion: "v-test");

        // Mutable dict; values match the SQL's two placeholders.
        var parameters = new Dictionary<string, object?>
        {
            ["Min"] = 5,
            ["Max"] = 100,
        };

        var input = new ReportAggregateInput
        {
            ReportName = "rptByRange",
            RecordSource = "PARAMETERS [Min] LONG, [Max] LONG; SELECT * FROM Orders WHERE OrderID >= [Min] AND OrderID <= [Max]",
            ParametersUsed = parameters,
        };

        var aggregates = svc.Capture(input, new object?[] { 5, 100 });
        var jsonBefore = ReportAggregatesWriter.Serialize(aggregates);

        // Mutate the input dictionary after Capture; the snapshot must
        // already have decoupled the aggregates from this state.
        parameters["Min"] = 999;
        parameters["Max"] = 999;
        parameters["Surprise"] = "added-after-capture";

        var jsonAfter = ReportAggregatesWriter.Serialize(aggregates);
        Assert.Equal(jsonBefore, jsonAfter);

        // Spot-check the snapshot reflects the values present at Capture time.
        using var doc = JsonDocument.Parse(jsonAfter);
        var snapshotted = doc.RootElement.GetProperty("Provenance").GetProperty("ParametersUsed");
        Assert.Equal(5, snapshotted.GetProperty("Min").GetInt32());
        Assert.Equal(100, snapshotted.GetProperty("Max").GetInt32());
        Assert.False(snapshotted.TryGetProperty("Surprise", out _));
    }
}
