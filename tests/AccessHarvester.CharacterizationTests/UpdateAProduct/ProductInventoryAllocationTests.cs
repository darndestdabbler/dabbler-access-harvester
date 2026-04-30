using System;
using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

public sealed class ProductInventoryAllocationTests
{
    [Fact]
    public void AllocateInventory_AssignsAllocated_WhenPhysicalStockSatisfiesLine()
    {
        var pending = new[]
        {
            new ProductOrderLineSnapshot
            {
                OrderId = 100,
                OrderDate = new DateTime(2026, 4, 1),
                OrderDetailId = 1000,
                Quantity = 5,
                Status = OrderDetailStatus.NoStock,
            },
        };

        var result = UpdateProductWorkflow.AllocateInventory(pending, physicallyAvailable: 10, onOrderQuantity: 0);

        Assert.Equal(OrderDetailStatus.Allocated, Assert.Single(result).Status);
    }

    [Fact]
    public void AllocateInventory_AssignsOnOrder_WhenPhysicalIsExhaustedButOnOrderCovers()
    {
        var pending = new[]
        {
            new ProductOrderLineSnapshot
            {
                OrderId = 100,
                OrderDate = new DateTime(2026, 4, 1),
                OrderDetailId = 1000,
                Quantity = 8,
                Status = OrderDetailStatus.NoStock,
            },
        };

        var result = UpdateProductWorkflow.AllocateInventory(pending, physicallyAvailable: 0, onOrderQuantity: 10);

        Assert.Equal(OrderDetailStatus.OnOrder, Assert.Single(result).Status);
    }

    [Fact]
    public void AllocateInventory_AssignsNoStock_WhenNeitherPhysicalNorOnOrderCanCoverLine()
    {
        var pending = new[]
        {
            new ProductOrderLineSnapshot
            {
                OrderId = 100,
                OrderDate = new DateTime(2026, 4, 1),
                OrderDetailId = 1000,
                Quantity = 12,
                Status = OrderDetailStatus.OnOrder,
            },
        };

        var result = UpdateProductWorkflow.AllocateInventory(pending, physicallyAvailable: 4, onOrderQuantity: 5);

        Assert.Equal(OrderDetailStatus.NoStock, Assert.Single(result).Status);
    }

    [Fact]
    public void AllocateInventory_WalksLinesOldestOrderDateFirstAndUsesOrderIdAsTiebreaker()
    {
        // Two lines on the same date; allocate-able stock is enough for one.
        // The lower OrderId must win the Allocated assignment; the higher OrderId
        // becomes NoStock because nothing is available or on-order to cover it.
        var pending = new[]
        {
            new ProductOrderLineSnapshot
            {
                OrderId = 200,
                OrderDate = new DateTime(2026, 4, 1),
                OrderDetailId = 2002,
                Quantity = 5,
                Status = OrderDetailStatus.NoStock,
            },
            new ProductOrderLineSnapshot
            {
                OrderId = 100,
                OrderDate = new DateTime(2026, 4, 1),
                OrderDetailId = 1001,
                Quantity = 5,
                Status = OrderDetailStatus.NoStock,
            },
        };

        var result = UpdateProductWorkflow.AllocateInventory(pending, physicallyAvailable: 5, onOrderQuantity: 0);

        Assert.Equal(2, result.Count);
        Assert.Equal(100, result[0].OrderId);
        Assert.Equal(OrderDetailStatus.Allocated, result[0].Status);
        Assert.Equal(200, result[1].OrderId);
        Assert.Equal(OrderDetailStatus.NoStock, result[1].Status);
    }

    [Fact]
    public void AllocateInventory_OnlyConsidersAllocatableStatuses()
    {
        var pending = new[]
        {
            new ProductOrderLineSnapshot
            {
                OrderId = 100,
                OrderDate = new DateTime(2026, 4, 1),
                OrderDetailId = 1000,
                Quantity = 3,
                Status = OrderDetailStatus.Invoiced,
            },
            new ProductOrderLineSnapshot
            {
                OrderId = 101,
                OrderDate = new DateTime(2026, 4, 2),
                OrderDetailId = 1010,
                Quantity = 3,
                Status = OrderDetailStatus.Shipped,
            },
            new ProductOrderLineSnapshot
            {
                OrderId = 102,
                OrderDate = new DateTime(2026, 4, 3),
                OrderDetailId = 1020,
                Quantity = 3,
                Status = OrderDetailStatus.New,
            },
            new ProductOrderLineSnapshot
            {
                OrderId = 103,
                OrderDate = new DateTime(2026, 4, 4),
                OrderDetailId = 1030,
                Quantity = 3,
                Status = OrderDetailStatus.Allocated,
            },
        };

        var result = UpdateProductWorkflow.AllocateInventory(pending, physicallyAvailable: 100, onOrderQuantity: 0);

        var only = Assert.Single(result);
        Assert.Equal(103, only.OrderId);
        Assert.Equal(OrderDetailStatus.Allocated, only.Status);
    }

    [Fact]
    public void AllocateInventory_ConsumesPhysicalThenSpillsRemainingDemandIntoOnOrderAndNoStock()
    {
        var pending = new[]
        {
            new ProductOrderLineSnapshot
            {
                OrderId = 1,
                OrderDate = new DateTime(2026, 4, 1),
                OrderDetailId = 11,
                Quantity = 5,
                Status = OrderDetailStatus.NoStock,
            },
            new ProductOrderLineSnapshot
            {
                OrderId = 2,
                OrderDate = new DateTime(2026, 4, 2),
                OrderDetailId = 22,
                Quantity = 5,
                Status = OrderDetailStatus.NoStock,
            },
            new ProductOrderLineSnapshot
            {
                OrderId = 3,
                OrderDate = new DateTime(2026, 4, 3),
                OrderDetailId = 33,
                Quantity = 5,
                Status = OrderDetailStatus.NoStock,
            },
        };

        var result = UpdateProductWorkflow.AllocateInventory(pending, physicallyAvailable: 5, onOrderQuantity: 5);

        Assert.Collection(result,
            line => Assert.Equal(OrderDetailStatus.Allocated, line.Status),
            line => Assert.Equal(OrderDetailStatus.OnOrder, line.Status),
            line => Assert.Equal(OrderDetailStatus.NoStock, line.Status));
    }
}
