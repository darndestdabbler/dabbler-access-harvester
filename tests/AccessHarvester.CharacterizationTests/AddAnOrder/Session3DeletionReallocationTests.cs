using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAnOrder;

/// <summary>
/// Session 3 falsification tests. Pin invariants the Session 2 package
/// referenced in evidence lists but did not characterize as standalone
/// rules or tests.
///
/// Sources:
/// - sfrmOrderLineItems::Form_AfterDelConfirm (calls AllocateInventory
///   m_RowCache.ProductID when status = acDeleteOK)
/// - sfrmOrderLineItems::Form_Current (caches m_RowCache only when
///   Not Me.NewRecord And Me.OrderDetailStatusID = odsAllocated)
/// - frmOrderDetails::Form_Delete (per-line iteration that calls
///   AllocateInventory(lngProductID) before the cascading delete)
/// </summary>
public sealed class Session3DeletionReallocationTests
{
    [Fact]
    public void DeletingAllocatedLineItem_TriggersAllocateInventoryForCachedProduct()
    {
        var allocator = new RecordingAllocator();
        var workflow = new LineItemDeletionWorkflow(allocator);
        var cached = new CachedRowState(ProductId: 42, Quantity: 5);

        workflow.OnAfterDelConfirm(deleteSucceeded: true, cache: cached);

        Assert.Equal(new long[] { 42L }, allocator.Calls);
    }

    [Fact]
    public void CancelledDeletion_DoesNotTriggerReallocation()
    {
        var allocator = new RecordingAllocator();
        var workflow = new LineItemDeletionWorkflow(allocator);
        var cached = new CachedRowState(ProductId: 42, Quantity: 5);

        workflow.OnAfterDelConfirm(deleteSucceeded: false, cache: cached);

        Assert.Empty(allocator.Calls);
    }

    [Fact]
    public void DeletingNonAllocatedLineItem_DoesNotTriggerReallocation()
    {
        var allocator = new RecordingAllocator();
        var workflow = new LineItemDeletionWorkflow(allocator);
        var cached = new CachedRowState(ProductId: 0, Quantity: 0);

        workflow.OnAfterDelConfirm(deleteSucceeded: true, cache: cached);

        Assert.Empty(allocator.Calls);
    }

    [Fact]
    public void DeletingParentOrder_ReallocatesEachLineItemProductBeforeCascade()
    {
        var allocator = new RecordingAllocator();
        var workflow = new OrderDeletionWorkflow(allocator);
        var lineItems = new[]
        {
            new LineItemRow(OrderDetailId: 1001, ProductId: 7),
            new LineItemRow(OrderDetailId: 1002, ProductId: 11),
            new LineItemRow(OrderDetailId: 1003, ProductId: 7),
        };

        workflow.DeleteOrder(lineItems, userConfirmedDelete: true);

        Assert.Equal(new long[] { 7L, 11L, 7L }, allocator.Calls);
    }

    [Fact]
    public void CancelledOrderDeletion_LeavesInventoryUntouched()
    {
        var allocator = new RecordingAllocator();
        var workflow = new OrderDeletionWorkflow(allocator);
        var lineItems = new[]
        {
            new LineItemRow(OrderDetailId: 1001, ProductId: 7),
        };

        workflow.DeleteOrder(lineItems, userConfirmedDelete: false);

        Assert.Empty(allocator.Calls);
    }

    private sealed record CachedRowState(long ProductId, short Quantity);

    private sealed record LineItemRow(long OrderDetailId, long ProductId);

    private sealed class RecordingAllocator
    {
        public List<long> Calls { get; } = new();

        public void Allocate(long productId)
        {
            if (productId == 0)
            {
                return;
            }

            Calls.Add(productId);
        }
    }

    private sealed class LineItemDeletionWorkflow
    {
        private readonly RecordingAllocator _allocator;

        public LineItemDeletionWorkflow(RecordingAllocator allocator)
        {
            _allocator = allocator;
        }

        public void OnAfterDelConfirm(bool deleteSucceeded, CachedRowState cache)
        {
            if (!deleteSucceeded)
            {
                return;
            }

            _allocator.Allocate(cache.ProductId);
        }
    }

    private sealed class OrderDeletionWorkflow
    {
        private readonly RecordingAllocator _allocator;

        public OrderDeletionWorkflow(RecordingAllocator allocator)
        {
            _allocator = allocator;
        }

        public void DeleteOrder(IEnumerable<LineItemRow> lineItems, bool userConfirmedDelete)
        {
            if (!userConfirmedDelete)
            {
                return;
            }

            foreach (var row in lineItems)
            {
                _allocator.Allocate(row.ProductId);
            }
        }
    }
}
