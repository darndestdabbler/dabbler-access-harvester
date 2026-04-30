using System.Linq;
using BlazorAddAnOrder.Contracts;
using BlazorAddAnOrder.Services.Stubs;
using Microsoft.AspNetCore.Mvc;

namespace BlazorAddAnOrder.Services.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly InMemoryCatalog _catalog;

    public ProductsController(InMemoryCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public ActionResult<ProductLookupResponse[]> List()
    {
        return Ok(_catalog.ListProducts()
            .Select(p => new ProductLookupResponse
            {
                Id = p.Id,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                PhysicalAvailable = p.PhysicalAvailable,
                OnOrderAvailable = p.OnOrderAvailable,
            })
            .ToArray());
    }

    [HttpGet("{id:long}")]
    public ActionResult<ProductLookupResponse> Get(long id)
    {
        var p = _catalog.FindProduct(id);
        if (p is null)
        {
            return NotFound();
        }

        return Ok(new ProductLookupResponse
        {
            Id = p.Id,
            Name = p.Name,
            UnitPrice = p.UnitPrice,
            PhysicalAvailable = p.PhysicalAvailable,
            OnOrderAvailable = p.OnOrderAvailable,
        });
    }
}
