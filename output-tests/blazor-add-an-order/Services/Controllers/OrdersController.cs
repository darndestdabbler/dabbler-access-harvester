using System.Collections.Generic;
using System.Linq;
using BlazorAddAnOrder.Contracts;
using BlazorAddAnOrder.Services.Domain;
using BlazorAddAnOrder.Services.Persistence;
using BlazorAddAnOrder.Services.Persistence.Entities;
using BlazorAddAnOrder.Services.Persistence.Mappings;
using Microsoft.AspNetCore.Mvc;

namespace BlazorAddAnOrder.Services.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderCreationService _orderCreation;
    private readonly OrderLineItemService _lineItems;
    private readonly OrdersStore _orders;

    public OrdersController(
        OrderCreationService orderCreation,
        OrderLineItemService lineItems,
        OrdersStore orders)
    {
        _orderCreation = orderCreation;
        _lineItems = lineItems;
        _orders = orders;
    }

    [HttpPost]
    public ActionResult<OrderResponse> Create([FromBody] OrderRequest request)
    {
        var draft = new OrderDraft();
        _orderCreation.BeginOrderEdit(draft, request.EmployeeId);

        if (request.CustomerId is null)
        {
            return UnprocessableEntity(new ValidationProblemResponse
            {
                Message = "Customer is required to create an order.",
                Errors = { "CustomerId is required." },
            });
        }

        draft.CustomerId = request.CustomerId;
        _orderCreation.ApplyCustomerSelection(draft);

        if (!_orderCreation.CanSave(draft))
        {
            return UnprocessableEntity(new ValidationProblemResponse
            {
                Message = "Order is missing required fields.",
                Errors = { "Required order fields are not all present." },
            });
        }

        var lineDrafts = new List<OrderLineItemDraft>();
        var seenProducts = new HashSet<long>();
        var errors = new List<string>();

        foreach (var lineRequest in request.LineItems)
        {
            if (!_lineItems.CanAddProductToOrder(lineRequest.ProductId, seenProducts))
            {
                errors.Add($"Product {lineRequest.ProductId} appears more than once on this order.");
                continue;
            }

            var line = new OrderLineItemDraft
            {
                OrderId = 0,
                ProductId = lineRequest.ProductId,
                Quantity = lineRequest.Quantity,
            };

            _lineItems.BeginLineItem(line);
            _lineItems.ApplyProductSelection(line);
            _lineItems.PrepareForSave(line);

            // Validate against the OrderId-set check by stamping a placeholder; persistence stamps the real id below.
            line.OrderId = -1;
            if (!_lineItems.CanSave(line))
            {
                errors.Add($"Line for product {lineRequest.ProductId} is invalid (quantity must be > 0 and product must have a unit price).");
                continue;
            }
            line.OrderId = 0;

            seenProducts.Add(line.ProductId);
            lineDrafts.Add(line);
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(new ValidationProblemResponse
            {
                Message = "One or more line items are invalid.",
                Errors = errors,
            });
        }

        var stored = _orders.Persist(draft, lineDrafts);
        return CreatedAtAction(nameof(Get), new { id = stored.Id }, MapToResponse(stored));
    }

    [HttpGet("{id:long}")]
    public ActionResult<OrderResponse> Get(long id)
    {
        var entity = _orders.Find(id);
        if (entity is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(entity));
    }

    private static OrderResponse MapToResponse(OrderEntity entity)
    {
        var draft = OrderMappings.ToDraft(entity);
        return new OrderResponse
        {
            Id = entity.Id,
            EmployeeId = draft.EmployeeId,
            CustomerId = draft.CustomerId,
            TaxRate = draft.TaxRate,
            TaxStatusId = draft.TaxStatusId,
            Status = draft.Status?.ToString(),
            OrderDate = draft.OrderDate,
            LineItems = entity.LineItems.Select(li =>
            {
                var lineDraft = OrderMappings.ToDraft(li);
                return new LineItemResponse
                {
                    Id = li.Id,
                    OrderId = li.OrderId,
                    ProductId = li.ProductId,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    Status = lineDraft.Status.ToString(),
                };
            }).ToList(),
        };
    }
}
