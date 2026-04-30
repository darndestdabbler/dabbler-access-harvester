using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAnEmployee;

public sealed class EmployeeDeletePolicyTests
{
    [Theory]
    [InlineData(1, 0, 0, 0, "orders")]
    [InlineData(0, 1, 0, 0, "purchase-orders")]
    [InlineData(0, 0, 1, 0, "supervisees")]
    [InlineData(0, 0, 0, 1, "privileges")]
    public void PrepareDelete_BlocksDeleteWhenAnyDependentCountIsNonZero(
        int orderCount,
        int purchaseOrderCount,
        int superviseeCount,
        int privilegeCount,
        string expectedBlockingReason)
    {
        var blocked = UpdateEmployeeWorkflow.PrepareDelete(
            new EmployeeDeleteDependencies
            {
                OrderCount = orderCount,
                PurchaseOrderCount = purchaseOrderCount,
                SuperviseeCount = superviseeCount,
                PrivilegeCount = privilegeCount,
            },
            employeeDisplayName: "Nancy Davolio",
            ordersMruEmployeeId: 10,
            purchaseOrdersMruEmployeeId: 11);

        Assert.False(blocked.CanDelete);
        Assert.Single(blocked.BlockingReasons);
        Assert.Contains(expectedBlockingReason, blocked.BlockingReasons);
        Assert.Empty(blocked.MruRemovals);
        Assert.Null(blocked.ConfirmationQueueName);
    }

    [Fact]
    public void PrepareDelete_ReportsEachBlockingReasonWithoutDependingOnEnumerationOrder()
    {
        var blocked = UpdateEmployeeWorkflow.PrepareDelete(
            new EmployeeDeleteDependencies
            {
                OrderCount = 1,
                PurchaseOrderCount = 2,
                SuperviseeCount = 3,
                PrivilegeCount = 4,
            },
            employeeDisplayName: "Nancy Davolio",
            ordersMruEmployeeId: 10,
            purchaseOrdersMruEmployeeId: 11);

        Assert.False(blocked.CanDelete);
        Assert.Equal(4, blocked.BlockingReasons.Count);
        Assert.Contains("orders", blocked.BlockingReasons);
        Assert.Contains("purchase-orders", blocked.BlockingReasons);
        Assert.Contains("supervisees", blocked.BlockingReasons);
        Assert.Contains("privileges", blocked.BlockingReasons);
        Assert.Empty(blocked.MruRemovals);
        Assert.Null(blocked.ConfirmationQueueName);
    }

    [Fact]
    public void PrepareDelete_QueuesMruCleanupAndConfirmationNameWhenDeleteIsAllowed()
    {
        var allowed = UpdateEmployeeWorkflow.PrepareDelete(
            new EmployeeDeleteDependencies(),
            employeeDisplayName: "Nancy Davolio",
            ordersMruEmployeeId: 42,
            purchaseOrdersMruEmployeeId: 77);

        Assert.True(allowed.CanDelete);
        Assert.Empty(allowed.BlockingReasons);
        Assert.Equal("Nancy Davolio", allowed.ConfirmationQueueName);
        Assert.Equal(2, allowed.MruRemovals.Count);
        Assert.Equal("Orders", allowed.MruRemovals[0].TableName);
        Assert.Equal(42, allowed.MruRemovals[0].PkValue);
        Assert.Equal("PurchaseOrders", allowed.MruRemovals[1].TableName);
        Assert.Equal(77, allowed.MruRemovals[1].PkValue);
    }

    [Fact]
    public void PrepareDelete_AllowsDeleteWithoutMruRemovalsWhenNoRecentItemsExist()
    {
        var allowed = UpdateEmployeeWorkflow.PrepareDelete(
            new EmployeeDeleteDependencies(),
            employeeDisplayName: "Andrew Fuller",
            ordersMruEmployeeId: null,
            purchaseOrdersMruEmployeeId: null);

        Assert.True(allowed.CanDelete);
        Assert.Empty(allowed.BlockingReasons);
        Assert.Empty(allowed.MruRemovals);
        Assert.Equal("Andrew Fuller", allowed.ConfirmationQueueName);
    }
}
