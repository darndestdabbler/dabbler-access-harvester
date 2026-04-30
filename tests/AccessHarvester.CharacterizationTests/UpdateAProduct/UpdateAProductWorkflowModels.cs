using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

internal enum OrderDetailStatus
{
    Allocated = 1,
    Invoiced = 2,
    New = 3,
    NoStock = 4,
    OnOrder = 5,
    Shipped = 6,
}

internal enum PurchaseOrderStatus
{
    Approve = 1,
    Closed = 2,
    New = 3,
    Submitted = 4,
    Received = 5,
}

internal sealed class ProductDraft
{
    public long? ProductId { get; set; }

    public string? ProductCode { get; set; }

    public string? ProductName { get; set; }

    public decimal? StandardUnitCost { get; set; }

    public decimal? UnitPrice { get; set; }

    public long? ProductCategoryId { get; set; }

    public bool? Discontinued { get; set; }

    public int? TargetLevel { get; set; }

    public int? MinimumReorderQuantity { get; set; }
}

internal sealed class ProductOrderLineSnapshot
{
    public long OrderId { get; set; }

    public DateTime OrderDate { get; set; }

    public long OrderDetailId { get; set; }

    public OrderDetailStatus Status { get; set; }

    public int Quantity { get; set; }
}

internal sealed class ProductPurchaseOrderLineSnapshot
{
    public long PurchaseOrderId { get; set; }

    public PurchaseOrderStatus Status { get; set; }

    public int Quantity { get; set; }
}

internal sealed class PurchaseOrderLineItem
{
    public long PurchaseOrderId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }
}

internal sealed class ReorderRequest
{
    public required ProductDraft Product { get; init; }

    public required int OnOrderQuantity { get; init; }

    public required int CalculatedReorderQuantity { get; init; }

    public required IReadOnlyList<long> AvailableVendorIds { get; init; }

    public long? ExistingOpenPurchaseOrderId { get; init; }

    public long? ExistingNewPurchaseOrderForVendorId { get; init; }

    public long? UserSelectedVendorId { get; init; }

    public int? UserEnteredQuantity { get; init; }
}

internal enum ReorderOutcomeKind
{
    Cancelled,
    OpenedExistingOpenPurchaseOrder,
    AppendedToExistingNewPurchaseOrder,
    CreatedNewPurchaseOrder,
}

internal sealed class ReorderOutcome
{
    public required ReorderOutcomeKind Kind { get; init; }

    public long? PurchaseOrderId { get; init; }

    public int? Quantity { get; init; }

    public long? VendorId { get; init; }

    public string? CancelReason { get; init; }
}

internal static class UpdateProductWorkflow
{
    private static readonly HashSet<PurchaseOrderStatus> NonClosedPoStatuses = new()
    {
        PurchaseOrderStatus.Approve,
        PurchaseOrderStatus.New,
        PurchaseOrderStatus.Submitted,
    };

    private static readonly HashSet<OrderDetailStatus> AllocatableStatuses = new()
    {
        OrderDetailStatus.Allocated,
        OrderDetailStatus.NoStock,
        OrderDetailStatus.OnOrder,
    };

    public static bool CanSave(ProductDraft product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductCode)) return false;
        if (string.IsNullOrWhiteSpace(product.ProductName)) return false;
        if (!product.StandardUnitCost.HasValue || product.StandardUnitCost.Value <= 0m) return false;
        if (!product.UnitPrice.HasValue) return false;
        if (!product.ProductCategoryId.HasValue) return false;
        if (!product.Discontinued.HasValue) return false;
        return true;
    }

    public static string FormatProductCode(long productId, string categoryCode)
    {
        return "NWT" + categoryCode + "-" + productId;
    }

    public static bool CanDeleteProduct(int orderDetailReferenceCount, int purchaseOrderDetailReferenceCount)
    {
        return orderDetailReferenceCount == 0 && purchaseOrderDetailReferenceCount == 0;
    }

    public static IReadOnlyList<string> DeleteProductCascade(
        long productId,
        bool confirmed,
        int orderDetailReferenceCount,
        int purchaseOrderDetailReferenceCount)
    {
        if (!confirmed) return Array.Empty<string>();
        if (!CanDeleteProduct(orderDetailReferenceCount, purchaseOrderDetailReferenceCount))
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            $"DELETE FROM StockTake WHERE ProductID = {productId}",
            $"DELETE FROM ProductVendors WHERE ProductID = {productId}",
            $"DELETE FROM Products WHERE ProductID = {productId}",
        };
    }

    public static bool ReorderIsBlockedByOpenPurchaseOrder(int onOrderQuantity, long? existingOpenPurchaseOrderId)
    {
        return onOrderQuantity != 0 && existingOpenPurchaseOrderId.HasValue;
    }

    public static bool IsNonClosedPurchaseOrderStatus(PurchaseOrderStatus status)
    {
        return NonClosedPoStatuses.Contains(status);
    }

    public static ReorderOutcome ExecuteReorder(ReorderRequest request)
    {
        if (request.Product.Discontinued == true)
        {
            return new ReorderOutcome
            {
                Kind = ReorderOutcomeKind.Cancelled,
                CancelReason = "discontinued",
            };
        }

        if (ReorderIsBlockedByOpenPurchaseOrder(request.OnOrderQuantity, request.ExistingOpenPurchaseOrderId))
        {
            return new ReorderOutcome
            {
                Kind = ReorderOutcomeKind.OpenedExistingOpenPurchaseOrder,
                PurchaseOrderId = request.ExistingOpenPurchaseOrderId,
            };
        }

        if (request.AvailableVendorIds.Count == 0)
        {
            return new ReorderOutcome
            {
                Kind = ReorderOutcomeKind.Cancelled,
                CancelReason = "vendor-required",
            };
        }

        long vendorId;
        if (request.AvailableVendorIds.Count == 1)
        {
            vendorId = request.AvailableVendorIds[0];
        }
        else
        {
            if (!request.UserSelectedVendorId.HasValue)
            {
                return new ReorderOutcome
                {
                    Kind = ReorderOutcomeKind.Cancelled,
                    CancelReason = "vendor-not-selected",
                };
            }
            vendorId = request.UserSelectedVendorId.Value;
        }

        int orderQuantity = ResolveReorderQuantity(
            calculatedReorderQuantity: request.CalculatedReorderQuantity,
            minimumReorderQuantity: request.Product.MinimumReorderQuantity ?? 0,
            userEnteredQuantity: request.UserEnteredQuantity);

        if (orderQuantity == 0)
        {
            return new ReorderOutcome
            {
                Kind = ReorderOutcomeKind.Cancelled,
                CancelReason = "quantity-zero",
            };
        }

        if (request.ExistingNewPurchaseOrderForVendorId.HasValue)
        {
            return new ReorderOutcome
            {
                Kind = ReorderOutcomeKind.AppendedToExistingNewPurchaseOrder,
                PurchaseOrderId = request.ExistingNewPurchaseOrderForVendorId,
                Quantity = orderQuantity,
                VendorId = vendorId,
            };
        }

        return new ReorderOutcome
        {
            Kind = ReorderOutcomeKind.CreatedNewPurchaseOrder,
            Quantity = orderQuantity,
            VendorId = vendorId,
        };
    }

    public static int ResolveReorderQuantity(
        int calculatedReorderQuantity,
        int minimumReorderQuantity,
        int? userEnteredQuantity)
    {
        if (calculatedReorderQuantity > 0)
        {
            return calculatedReorderQuantity;
        }

        if (minimumReorderQuantity > 0)
        {
            return minimumReorderQuantity;
        }

        return userEnteredQuantity ?? 0;
    }

    public static IReadOnlyList<PurchaseOrderLineItem> AddPurchaseOrderDetail(
        IList<PurchaseOrderLineItem> existingLines,
        long purchaseOrderId,
        long productId,
        int quantityToAdd,
        decimal unitCost)
    {
        var match = existingLines.FirstOrDefault(line =>
            line.PurchaseOrderId == purchaseOrderId && line.ProductId == productId);

        if (match is null)
        {
            existingLines.Add(new PurchaseOrderLineItem
            {
                PurchaseOrderId = purchaseOrderId,
                ProductId = productId,
                Quantity = quantityToAdd,
                UnitCost = unitCost,
            });
        }
        else
        {
            match.Quantity += quantityToAdd;
        }

        return existingLines.ToArray();
    }

    public static int ProductReorderQuantity(
        int quantityAvailableToSell,
        int onOrderQuantity,
        int noStockQuantity,
        int targetLevel,
        int minimumReorderQuantity)
    {
        int supply = quantityAvailableToSell + onOrderQuantity;
        int demand = noStockQuantity + targetLevel;

        if (supply >= demand)
        {
            return minimumReorderQuantity;
        }

        int shortfall = demand - supply;
        return Math.Max(shortfall, minimumReorderQuantity);
    }

    public static IReadOnlyList<ProductOrderLineSnapshot> AllocateInventory(
        IEnumerable<ProductOrderLineSnapshot> pendingOrderLineItems,
        int physicallyAvailable,
        int onOrderQuantity)
    {
        int available = physicallyAvailable;
        int allocatable = physicallyAvailable + onOrderQuantity;

        var ordered = pendingOrderLineItems
            .Where(item => AllocatableStatuses.Contains(item.Status))
            .OrderBy(item => item.OrderDate)
            .ThenBy(item => item.OrderId)
            .ToArray();

        var result = new List<ProductOrderLineSnapshot>(ordered.Length);
        foreach (var item in ordered)
        {
            var assigned = AssignAllocationStatus(item, ref available, ref allocatable);
            result.Add(new ProductOrderLineSnapshot
            {
                OrderId = item.OrderId,
                OrderDate = item.OrderDate,
                OrderDetailId = item.OrderDetailId,
                Quantity = item.Quantity,
                Status = assigned,
            });
        }

        return result;
    }

    private static OrderDetailStatus AssignAllocationStatus(
        ProductOrderLineSnapshot item,
        ref int physicallyAvailable,
        ref int totalAllocatable)
    {
        if (item.Quantity <= physicallyAvailable)
        {
            physicallyAvailable -= item.Quantity;
            totalAllocatable -= item.Quantity;
            return OrderDetailStatus.Allocated;
        }

        if (item.Quantity <= totalAllocatable)
        {
            totalAllocatable -= item.Quantity;
            return OrderDetailStatus.OnOrder;
        }

        return OrderDetailStatus.NoStock;
    }
}
