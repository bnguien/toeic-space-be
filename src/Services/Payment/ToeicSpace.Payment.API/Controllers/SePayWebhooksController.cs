using ToeicSpace.Payment.Application.DTOs.SePay;
using MediatR;
using ToeicSpace.Payment.Application.Features.SePay.Commands.ProcessSePayWebhook;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ToeicSpace.Payment.API.Controllers;

[ApiController]
[Route("api/v1/webhooks/sepay")]
[AllowAnonymous]
public sealed class SePayWebhooksController(ISender sender, IConfiguration config,
    ILogger<SePayWebhooksController> log) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] SePayWebhook webhook, CancellationToken ct)
    {
        var key = config["SePay:WebhookApiKey"];
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length < 24 ||
            !header.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase) ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(key)),
                SHA256.HashData(Encoding.UTF8.GetBytes(header[7..]))))
        {
            log.LogWarning("SePay webhook rejected: missing or invalid API key.");
            return Unauthorized();
        }
        await sender.Send(new ProcessSePayWebhookCommand(webhook), ct);
        return Ok(new { success = true });
    }
}
