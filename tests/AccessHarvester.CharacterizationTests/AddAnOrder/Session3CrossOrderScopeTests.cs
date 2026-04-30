using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAnOrder;

/// <summary>
/// Session 3 falsification tests for the cross-order rebalancing scope
/// of inventory allocation. The Session 2 tests demonstrated oldest-first
/// ordering across distinct OrderIds, but did not assert that allocation
/// is intentionally cross-order in scope. These tests pin that intent
/// directly.
///
/// Sources:
/// - modInventory::AllocateInventory (the SQL filters by ProductID and
///   OrderDetailStatusID in (1,4,5) only — there is NO OrderID filter,
///   so eligible demand from every order participates in rebalancing.)
/// </summary>
public sealed class Session3CrossOrderScopeTests
{
    [Fact]
    public void AllocateInventory_RebalancesAcrossOrdersForTheSameProduct()
    {
        var allocator = new InventoryAllocator();
        var details = new[]
        {
            new PendingOrderDetail(101, new DateTime(2026, 4, 1), 3, OrderDetailStatus.NoStock),
            new PendingOrderDetail(202, new DateTime(2026, 4, 2), 2, OrderDetailStatus.NoStock),
            new PendingOrderDetail(303, new DateTime(2026, 4, 3), 5, OrderDetailStatus.NoStock),
        };

        var result = allocator.AllocateInventory(details, physicalAvailable: 5, onOrderAvailable: 0);

        var participatingOrderIds = result.Select(detail => detail.OrderId).ToArray();
        Assert.Equal(new long[] { 101L, 202L, 303L }, participatingOrderIds);
        Assert.Equal(OrderDetailStatus.Allocated, result[0].Status);
        Assert.Equal(OrderDetailStatus.Allocated, result[1].Status);
        Assert.Equal(OrderDetailStatus.NoStock, result[2].Status);
    }

    [Fact]
    public void AllocateInventory_DoesNotPreferLineItemsBelongingToTheNewlyEditedOrder()
    {
        var allocator = new InventoryAllocator();
        var details = new[]
        {
            new PendingOrderDetail(101, new DateTime(2026, 4, 1), 4, OrderDetailStatus.NoStock),
            new PendingOrderDetail(999, new DateTime(2026, 4, 5), 4, OrderDetailStatus.NoStock),
        };

        var result = allocator.AllocateInventory(details, physicalAvailable: 4, onOrderAvailable: 0);

        Assert.Equal(101L, result[0].OrderId);
        Assert.Equal(OrderDetailStatus.Allocated, result[0].Status);
        Assert.Equal(999L, result[1].OrderId);
        Assert.Equal(OrderDetailStatus.NoStock, result[1].Status);
    }

    [Fact]
    public void AllocateInventory_BreaksDateTiesByOrderIdAscending()
    {
        var allocator = new InventoryAllocator();
        var sameInstant = new DateTime(2026, 4, 1, 9, 0, 0);
        var details = new[]
        {
            new PendingOrderDetail(303, sameInstant, 2, OrderDetailStatus.NoStock),
            new PendingOrderDetail(101, sameInstant, 2, OrderDetailStatus.NoStock),
            new PendingOrderDetail(202, sameInstant, 2, OrderDetailStatus.NoStock),
        };

        var result = allocator.AllocateInventory(details, physicalAvailable: 4, onOrderAvailable: 0);

        var orderIdSequence = result.Select(detail => detail.OrderId).ToArray();
        Assert.Equal(new long[] { 101L, 202L, 303L }, orderIdSequence);
        Assert.Equal(OrderDetailStatus.Allocated, result[0].Status);
        Assert.Equal(OrderDetailStatus.Allocated, result[1].Status);
        Assert.Equal(OrderDetailStatus.NoStock, result[2].Status);
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
    }
}
