using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAnOrder;

/// <summary>
/// Characterization tests for inventory side effects in the Add-an-Order flow.
/// Sources:
/// - modInventory::AllocateInventory
/// - sfrmOrderLineItems::ReallocateInventory
/// - sfrmOrderLineItems::Form_AfterUpdate
/// </summary>
public sealed class InventoryAllocationBehaviorTests
{
    [Fact]
    public void AllocateInventory_ProcessesOldestDemandFirstWithoutPartialAllocation()
    {
        var allocator = new InventoryAllocator();
        var details = new[]
        {
            new PendingOrderDetail(101, new DateTime(2026, 4, 1), 2, OrderDetailStatus.NoStock),
            new PendingOrderDetail(102, new DateTime(2026, 4, 2), 3, OrderDetailStatus.NoStock),
            new PendingOrderDetail(103, new DateTime(2026, 4, 3), 2, OrderDetailStatus.NoStock),
        };

        var result = allocator.AllocateInventory(details, physicalAvailable: 4, onOrderAvailable: 0);

        Assert.Equal(OrderDetailStatus.Allocated, result[0].Status);
        Assert.Equal(OrderDetailStatus.NoStock, result[1].Status);
        Assert.Equal(OrderDetailStatus.Allocated, result[2].Status);
    }

    [Fact]
    public void AllocateInventory_UsesOnOrderSupplyBeforeLeavingALineAtNoStock()
    {
        var allocator = new InventoryAllocator();
        var details = new[]
        {
            new PendingOrderDetail(101, new DateTime(2026, 4, 1), 2, OrderDetailStatus.NoStock),
            new PendingOrderDetail(102, new DateTime(2026, 4, 2), 3, OrderDetailStatus.NoStock),
            new PendingOrderDetail(103, new DateTime(2026, 4, 3), 4, OrderDetailStatus.NoStock),
        };

        var result = allocator.AllocateInventory(details, physicalAvailable: 2, onOrderAvailable: 3);

        Assert.Equal(OrderDetailStatus.Allocated, result[0].Status);
        Assert.Equal(OrderDetailStatus.OnOrder, result[1].Status);
        Assert.Equal(OrderDetailStatus.NoStock, result[2].Status);
    }

    [Fact]
    public void ReallocateInventory_RebalancesPreviousAndCurrentProductsWhenProductChanges()
    {
        var allocator = new InventoryAllocator();
        var previousProductDetails = new[]
        {
            new PendingOrderDetail(201, new DateTime(2026, 4, 1), 3, OrderDetailStatus.NoStock),
        };
        var currentProductDetails = new[]
        {
            new PendingOrderDetail(301, new DateTime(2026, 4, 2), 2, OrderDetailStatus.NoStock),
        };

        var result = allocator.ReallocateAfterProductChange(
            cachedProductId: 10,
            currentProductId: 11,
            previousProductDetails: previousProductDetails,
            currentProductDetails: currentProductDetails,
            previousPhysicalAvailable: 3,
            previousOnOrderAvailable: 0,
            currentPhysicalAvailable: 2,
            currentOnOrderAvailable: 0);

        Assert.Equal(OrderDetailStatus.Allocated, Assert.Single(result.PreviousProduct).Status);
        Assert.Equal(OrderDetailStatus.Allocated, Assert.Single(result.CurrentProduct).Status);
    }

    [Fact]
    public void AllocateInventory_IgnoresStatusesOutsideAllocatedNoStockAndOnOrder()
    {
        var allocator = new InventoryAllocator();
        var details = new[]
        {
            new PendingOrderDetail(101, new DateTime(2026, 4, 1), 2, OrderDetailStatus.NoStock),
            new PendingOrderDetail(102, new DateTime(2026, 4, 2), 2, OrderDetailStatus.Shipped),
            new PendingOrderDetail(103, new DateTime(2026, 4, 3), 2, OrderDetailStatus.Invoiced),
        };

        var result = allocator.AllocateInventory(details, physicalAvailable: 2, onOrderAvailable: 0);

        Assert.Equal(OrderDetailStatus.Allocated, result[0].Status);
        Assert.Equal(OrderDetailStatus.Shipped, result[1].Status);
        Assert.Equal(OrderDetailStatus.Invoiced, result[2].Status);
    }

    [Fact]
    public void ReallocateInventory_DoesNotTouchPreviousProductWhenCurrentRowWasNotAllocated()
    {
        var allocator = new InventoryAllocator();
        var result = allocator.ReallocateAfterProductChange(
            cachedProductId: 0,
            currentProductId: 11,
            previousProductDetails: [new PendingOrderDetail(201, new DateTime(2026, 4, 1), 3, OrderDetailStatus.NoStock)],
            currentProductDetails: [new PendingOrderDetail(301, new DateTime(2026, 4, 2), 2, OrderDetailStatus.NoStock)],
            previousPhysicalAvailable: 3,
            previousOnOrderAvailable: 0,
            currentPhysicalAvailable: 2,
            currentOnOrderAvailable: 0);

        Assert.Empty(result.PreviousProduct);
        Assert.Equal(OrderDetailStatus.Allocated, Assert.Single(result.CurrentProduct).Status);
    }

    private sealed class InventoryAllocator
    {
        public IReadOnlyList<PendingOrderDetail> AllocateInventory(
            IEnumerable<PendingOrderDetail> pendingDetails,
            int physicalAvailable,
            int onOrderAvailable)
        {
            int remainingPhysical = physicalAvailable;
            int remainingCombined = physicalAvailable + onOrderAvailable;
            var eligibleStatuses = new[]
            {
                OrderDetailStatus.Allocated,
                OrderDetailStatus.NoStock,
                OrderDetailStatus.OnOrder,
            };

            return pendingDetails
                .OrderBy(detail => detail.OrderDate)
                .ThenBy(detail => detail.OrderId)
                .Select(detail =>
                {
                    if (!eligibleStatuses.Contains(detail.Status))
                    {
                        return detail;
                    }

                    OrderDetailStatus nextStatus;

                    if (detail.Quantity <= remainingPhysical)
                    {
                        nextStatus = OrderDetailStatus.Allocated;
                        remainingPhysical -= detail.Quantity;
                        remainingCombined -= detail.Quantity;
                    }
                    else if (detail.Quantity <= remainingCombined)
                    {
                        nextStatus = OrderDetailStatus.OnOrder;
                        remainingCombined -= detail.Quantity;
                    }
                    else
                    {
                        nextStatus = OrderDetailStatus.NoStock;
                    }

                    return detail with { Status = nextStatus };
                })
                .ToArray();
        }

        public (IReadOnlyList<PendingOrderDetail> PreviousProduct, IReadOnlyList<PendingOrderDetail> CurrentProduct)
            ReallocateAfterProductChange(
                long cachedProductId,
                long currentProductId,
                IEnumerable<PendingOrderDetail> previousProductDetails,
                IEnumerable<PendingOrderDetail> currentProductDetails,
                int previousPhysicalAvailable,
                int previousOnOrderAvailable,
                int currentPhysicalAvailable,
                int currentOnOrderAvailable)
        {
            IReadOnlyList<PendingOrderDetail> previous = Array.Empty<PendingOrderDetail>();
            if (cachedProductId != 0 && cachedProductId != currentProductId)
            {
                previous = AllocateInventory(
                    previousProductDetails,
                    previousPhysicalAvailable,
                    previousOnOrderAvailable);
            }

            IReadOnlyList<PendingOrderDetail> current = AllocateInventory(
                currentProductDetails,
                currentPhysicalAvailable,
                currentOnOrderAvailable);

            return (previous, current);
        }
    }
}
