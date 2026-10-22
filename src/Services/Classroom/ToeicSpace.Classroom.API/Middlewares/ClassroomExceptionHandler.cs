using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Middlewares;

public sealed class ClassroomExceptionHandler(ILogger<ClassroomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var (status, code, message) = error switch
        {
            ClassRuleException e => (e.StatusCode, e.Code, e.Message),
            _ => (500, "internal_error", "An unexpected error occurred.")
        };
        if (status == 500) logger.LogError(error, "Unhandled Classroom error");
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status switch
            {
                400 => "Bad Request", 401 => "Unauthorized", 403 => "Forbidden", 404 => "Not Found",
                409 => "Conflict", 503 => "Service Unavailable", _ => "Internal Server Error"
            },
            Detail = message, Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
