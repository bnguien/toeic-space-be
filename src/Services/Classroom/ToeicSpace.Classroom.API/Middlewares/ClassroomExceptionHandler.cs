using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Classroom.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ToeicSpace.Classroom.API.Middlewares;

public sealed class ClassroomExceptionHandler(ILogger<ClassroomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var (status, code, message) = error switch
        {
            ClassRuleException e => (e.StatusCode, e.Code, e.Message),
            DbUpdateConcurrencyException => (409, "concurrency_conflict", "Session changed; reload and retry."),
            DbUpdateException { InnerException: MySqlException { Number: 1062 } } =>
                (409, "unique_constraint", "A session or schedule already exists; reload and retry."),
            DbUpdateException { InnerException: MySqlException { Number: 1205 or 1213 } } =>
                (409, "concurrency_conflict", "Scheduling contention; retry."),
            MySqlException { Number: 1205 or 1213 } =>
                (409, "concurrency_conflict", "Scheduling contention; retry."),
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
        if (error is ClassRuleException { Errors: { } errors }) problem.Extensions["errors"] = errors;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
