using Xunit;

namespace AccessHarvester.CharacterizationTests.UpdateAProduct;

public sealed class ProductReorderQuantityTests
{
    [Fact]
    public void ReorderQuantity_ReturnsMinimumReorder_WhenSupplyAlreadyMeetsTargetPlusBacklog()
    {
        // Available 10 + OnOrder 5 = 15 supply; NoStock 0 + Target 10 = 10 demand.
        // Supply >= demand → return minimum reorder quantity, NOT zero.
        var qty = UpdateProductWorkflow.ProductReorderQuantity(
            quantityAvailableToSell: 10,
            onOrderQuantity: 5,
            noStockQuantity: 0,
            targetLevel: 10,
            minimumReorderQuantity: 6);

        Assert.Equal(6, qty);
    }

    [Fact]
    public void ReorderQuantity_ReturnsShortfall_WhenSupplyIsBelowDemandAndShortfallExceedsMinimum()
    {
        // Supply 5; demand 25 (NoStock 5 + Target 20). Shortfall = 20.
        // Minimum 6 is below the shortfall, so reorder = shortfall.
        var qty = UpdateProductWorkflow.ProductReorderQuantity(
            quantityAvailableToSell: 5,
            onOrderQuantity: 0,
            noStockQuantity: 5,
            targetLevel: 20,
            minimumReorderQuantity: 6);

        Assert.Equal(20, qty);
    }

    [Fact]
    public void ReorderQuantity_ReturnsMinimum_WhenShortfallIsBelowMinimum()
    {
        // Supply 18; demand 20. Shortfall = 2. Minimum 10 wins.
        var qty = UpdateProductWorkflow.ProductReorderQuantity(
            quantityAvailableToSell: 10,
            onOrderQuantity: 8,
            noStockQuantity: 0,
            targetLevel: 20,
            minimumReorderQuantity: 10);

        Assert.Equal(10, qty);
    }

    [Fact]
    public void ReorderQuantity_ReturnsZero_WhenSupplyMeetsDemandAndMinimumIsZero()
    {
        var qty = UpdateProductWorkflow.ProductReorderQuantity(
            quantityAvailableToSell: 10,
            onOrderQuantity: 0,
            noStockQuantity: 0,
            targetLevel: 5,
            minimumReorderQuantity: 0);

        Assert.Equal(0, qty);
    }
}
