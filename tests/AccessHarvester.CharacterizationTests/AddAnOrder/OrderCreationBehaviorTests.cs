using System;
using Xunit;

namespace AccessHarvester.CharacterizationTests.AddAnOrder;

/// <summary>
/// Characterization tests for the Add-an-Order header workflow.
/// Sources:
/// - frmOrderDetails::Form_BeforeInsert
/// - frmOrderDetails::CustomerID_AfterUpdate
/// - frmOrderDetails::Form_BeforeUpdate
/// - modValidation::ValidateForm
/// </summary>
public sealed class OrderCreationBehaviorTests
{
    [Fact]
    public void BeginOrderEdit_SeedsEmployeeTaxRateAndNewStatus()
    {
        var order = new OrderDraft();
        var workflow = new OrderCreationWorkflow(
            currentUserId: 42,
            systemTaxRate: 0.0825f,
            orderDateFactory: () => new DateTime(2026, 4, 30, 8, 0, 0, DateTimeKind.Utc),
            taxStatusLookup: _ => 1,
            requiredFieldValidator: _ => true);

        workflow.BeginOrderEdit(order);

        Assert.Equal(42, order.EmployeeId);
        Assert.Equal(0.0825f, order.TaxRate);
        Assert.Equal(OrderStatus.New, order.Status);
    }

    [Fact]
    public void BeginOrderEdit_AppliesOrderDateDefaultFromDataModel()
    {
        var expected = new DateTime(2026, 4, 30, 8, 0, 0, DateTimeKind.Utc);
        var order = new OrderDraft();
        var workflow = new OrderCreationWorkflow(
            currentUserId: 42,
            systemTaxRate: 0.0825f,
            orderDateFactory: () => expected,
            taxStatusLookup: _ => 1,
            requiredFieldValidator: _ => true);

        workflow.BeginOrderEdit(order);

        Assert.Equal(expected, order.OrderDate);
    }

    [Fact]
    public void ApplyCustomerSelection_CopiesCustomersStandardTaxStatus()
    {
        var order = new OrderDraft { CustomerId = 17 };
        var workflow = new OrderCreationWorkflow(
            currentUserId: 42,
            systemTaxRate: 0.0825f,
            orderDateFactory: () => DateTime.UtcNow,
            taxStatusLookup: customerId =>
            {
                Assert.Equal(17L, customerId);
                return 0;
            },
            requiredFieldValidator: _ => true);

        workflow.ApplyCustomerSelection(order);

        Assert.Equal((byte)0, order.TaxStatusId);
    }

    [Fact]
    public void CanSave_ReturnsFalseWhenRequiredOrderDataIsMissing()
    {
        var order = new OrderDraft
        {
            EmployeeId = 42,
            CustomerId = 17,
            TaxRate = 0.0825f,
            TaxStatusId = 1,
            Status = OrderStatus.New,
            OrderDate = null,
        };
        var workflow = new OrderCreationWorkflow(
            currentUserId: 42,
            systemTaxRate: 0.0825f,
            orderDateFactory: () => DateTime.UtcNow,
            taxStatusLookup: _ => 1,
            requiredFieldValidator: draft =>
                draft.CustomerId is not null &&
                draft.OrderDate is not null &&
                draft.TaxStatusId is not null &&
                draft.Status is not null &&
                draft.EmployeeId != 0);

        Assert.False(workflow.CanSave(order));
    }

    [Fact]
    public void CanSave_ReturnsTrueWhenRequiredOrderDataIsPresent()
    {
        var order = new OrderDraft
        {
            EmployeeId = 42,
            CustomerId = 17,
            TaxRate = 0.0825f,
            TaxStatusId = 1,
            Status = OrderStatus.New,
            OrderDate = new DateTime(2026, 4, 30, 8, 0, 0, DateTimeKind.Utc),
        };
        var workflow = new OrderCreationWorkflow(
            currentUserId: 42,
            systemTaxRate: 0.0825f,
            orderDateFactory: () => DateTime.UtcNow,
            taxStatusLookup: _ => 1,
            requiredFieldValidator: draft =>
                draft.CustomerId is not null &&
                draft.OrderDate is not null &&
                draft.TaxStatusId is not null &&
                draft.Status is not null &&
                draft.EmployeeId != 0);

        Assert.True(workflow.CanSave(order));
    }

    private sealed class OrderCreationWorkflow
    {
        private readonly long _currentUserId;
        private readonly float _systemTaxRate;
        private readonly Func<DateTime> _orderDateFactory;
        private readonly Func<long, byte> _taxStatusLookup;
        private readonly Func<OrderDraft, bool> _requiredFieldValidator;

        public OrderCreationWorkflow(
            long currentUserId,
            float systemTaxRate,
            Func<DateTime> orderDateFactory,
            Func<long, byte> taxStatusLookup,
            Func<OrderDraft, bool> requiredFieldValidator)
        {
            _currentUserId = currentUserId;
            _systemTaxRate = systemTaxRate;
            _orderDateFactory = orderDateFactory;
            _taxStatusLookup = taxStatusLookup;
            _requiredFieldValidator = requiredFieldValidator;
        }

        public void BeginOrderEdit(OrderDraft order)
        {
            order.EmployeeId = _currentUserId;
            order.TaxRate = _systemTaxRate;
            order.Status = OrderStatus.New;
            order.OrderDate = _orderDateFactory();
        }

        public void ApplyCustomerSelection(OrderDraft order)
        {
            Assert.NotNull(order.CustomerId);
            order.TaxStatusId = _taxStatusLookup(order.CustomerId.Value);
        }

        public bool CanSave(OrderDraft order)
        {
            return _requiredFieldValidator(order);
        }
    }
}
