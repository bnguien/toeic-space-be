using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Middlewares;

public sealed class ContentExceptionHandler(ILogger<ContentExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var (status, code, message) = error switch
        {
            ContentRuleException rule => (rule.StatusCode, rule.Code, rule.Message),
            _ => (500, "internal_error", "An unexpected error occurred.")
        };
        if (status == 500) logger.LogError(error, "Unhandled Content error");
        var problem = new ProblemDetails
        {
            Status = status, Title = status switch
            {
                400 => "Bad Request", 401 => "Unauthorized", 403 => "Forbidden", 404 => "Not Found",
                409 => "Conflict", 429 => "Too Many Requests", 503 => "Service Unavailable", _ => "Internal Server Error"
            },
            Detail = message, Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        if (error is ContentRuleException { Extensions: { } extensions })
            foreach (var (key, value) in extensions) problem.Extensions[key] = value;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
