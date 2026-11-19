using MediatR;
using ToeicSpace.Payment.Application.Features.Orders.Queries.GetCheckoutOrderStatus;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ToeicSpace.Payment.API.Controllers;

[ApiController]
[Route("api/v1/internal/orders")]
[AllowAnonymous]
public sealed class InternalOrdersController(ISender sender, IConfiguration config) : ControllerBase
{
    [HttpGet("by-checkout/{reference:guid}")]
    public async Task<IActionResult> Status(Guid reference, CancellationToken ct)
    {
        var key = config["InternalApi:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32 ||
            !Request.Headers.TryGetValue("X-Internal-Key", out var value) ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)),
                SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())))) return Unauthorized();
        var order = await sender.Send(new GetCheckoutOrderStatusQuery(reference), ct);
        return order is null ? NotFound() : Ok(order);
    }
}
