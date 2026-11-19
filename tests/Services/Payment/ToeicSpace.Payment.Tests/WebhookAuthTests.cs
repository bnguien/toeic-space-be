using ToeicSpace.Payment.Application.DTOs.SePay;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Payment.API.Controllers;
using Xunit;

namespace ToeicSpace.Payment.Tests;

public sealed class WebhookAuthTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Bearer some-token")]
    [InlineData("Apikey wrong-token")]
    public async Task RejectsUnauthorizedNotifications(string? authorization)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SePay:WebhookApiKey"] = "this-is-a-test-webhook-key-with-32-characters" }).Build();
        var controller = new SePayWebhooksController(null!, config, NullLogger<SePayWebhooksController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (authorization is not null) controller.Request.Headers.Authorization = authorization;
        var result = await controller.Receive(new SePayWebhook(1, null, null, null, null, null, null, 0, null), default);
        Assert.IsType<UnauthorizedResult>(result);
    }
}
