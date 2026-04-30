using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorAddAnOrder.Services.Domain;

public sealed class InventoryAllocationService
{
    private static readonly OrderDetailStatus[] EligibleStatuses =
    {
        OrderDetailStatus.Allocated,
        OrderDetailStatus.NoStock,
        OrderDetailStatus.OnOrder,
    };

    public IReadOnlyList<PendingOrderDetail> AllocateInventory(
        IEnumerable<PendingOrderDetail> pendingDetails,
        int physicalAvailable,
        int onOrderAvailable)
    {
        int remainingPhysical = physicalAvailable;
        int remainingCombined = physicalAvailable + onOrderAvailable;

        return pendingDetails
            .OrderBy(detail => detail.OrderDate)
            .ThenBy(detail => detail.OrderId)
            .Select(detail =>
            {
                if (!EligibleStatuses.Contains(detail.Status))
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

    public ReallocationResult ReallocateAfterProductChange(
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

        return new ReallocationResult(previous, current);
    }
}

public sealed record ReallocationResult(
    IReadOnlyList<PendingOrderDetail> PreviousProduct,
    IReadOnlyList<PendingOrderDetail> CurrentProduct);
