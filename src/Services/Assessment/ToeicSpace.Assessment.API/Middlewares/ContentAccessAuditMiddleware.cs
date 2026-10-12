using System.Diagnostics;
using ToeicSpace.Assessment.Application.Interfaces.Security;

namespace ToeicSpace.Assessment.API.Middlewares;

/// <summary>
/// Records every request made with content-manager rights (drafts, answer keys, edits),
/// so access to protected content can be traced back to an account.
/// Query strings are left out because search terms are free text.
/// </summary>
public sealed class ContentAccessAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ContentAccessAuditMiddleware> _logger;

    public ContentAccessAuditMiddleware(
        RequestDelegate next,
        ILogger<ContentAccessAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        if (!currentUser.CanManageContent)
        {
            await _next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            _logger.LogInformation(
                "Content access by {UserId} from {ClientIp}: {Method} {Path} responded {StatusCode} in {ElapsedMs:0} ms",
                currentUser.UserId,
                context.Connection.RemoteIpAddress,
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
