using BlazorAddAnOrder.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BlazorAddAnOrder.Services;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get() => new HealthResponse("ok");
}
