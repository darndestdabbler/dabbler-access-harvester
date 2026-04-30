using System.Linq;
using BlazorAddAnOrder.Contracts;
using BlazorAddAnOrder.Services.Stubs;
using Microsoft.AspNetCore.Mvc;

namespace BlazorAddAnOrder.Services.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly InMemoryCatalog _catalog;

    public CustomersController(InMemoryCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public ActionResult<CustomerLookupResponse[]> List()
    {
        return Ok(_catalog.ListCustomers()
            .Select(c => new CustomerLookupResponse
            {
                Id = c.Id,
                Name = c.Name,
                StandardTaxStatusId = c.StandardTaxStatusId,
            })
            .ToArray());
    }

    [HttpGet("{id:long}")]
    public ActionResult<CustomerLookupResponse> Get(long id)
    {
        var c = _catalog.FindCustomer(id);
        if (c is null)
        {
            return NotFound();
        }

        return Ok(new CustomerLookupResponse
        {
            Id = c.Id,
            Name = c.Name,
            StandardTaxStatusId = c.StandardTaxStatusId,
        });
    }
}
