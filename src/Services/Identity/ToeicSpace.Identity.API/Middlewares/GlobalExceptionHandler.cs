using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.API.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is AppException appException)
        {
            await HandleAppExceptionAsync(
                httpContext,
                appException,
                cancellationToken);

            return true;
        }

        await HandleUnhandledExceptionAsync(
            httpContext,
            exception,
            cancellationToken);

        return true;
    }

    private async Task HandleAppExceptionAsync(
        HttpContext context,
        AppException exception,
        CancellationToken cancellationToken)
    {
        var statusCode = MapToStatusCode(exception.Type);

        _logger.LogWarning(
            "Handled application exception. Type: {Type}, Code: {Code}, Message: {Message}",
            exception.Type,
            exception.Code,
            exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = MapToTitle(exception.Type),
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        if (!string.IsNullOrWhiteSpace(exception.Code))
        {
            problemDetails.Extensions["code"] =
                exception.Code;
        }

        if (exception.ValidationErrors is not null)
        {
            problemDetails.Extensions["errors"] =
                exception.ValidationErrors;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType =
            "application/problem+json";

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);
    }

    private async Task HandleUnhandledExceptionAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception occurred");

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred. Please try again later.",
            Instance = context.Request.Path
        };

        context.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        context.Response.ContentType =
            "application/problem+json";

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);
    }

    private static int MapToStatusCode(
        ErrorType type)
        => type switch
        {
            ErrorType.Validation =>
                StatusCodes.Status400BadRequest,

            ErrorType.Unauthenticated =>
                StatusCodes.Status401Unauthorized,

            ErrorType.Forbidden =>
                StatusCodes.Status403Forbidden,

            ErrorType.NotFound =>
                StatusCodes.Status404NotFound,

            ErrorType.Conflict =>
                StatusCodes.Status409Conflict,

            ErrorType.TooManyRequests =>
                StatusCodes.Status429TooManyRequests,

            _ =>
                StatusCodes.Status500InternalServerError
        };

    private static string MapToTitle(
        ErrorType type)
        => type switch
        {
            ErrorType.Validation => "Bad Request",
            ErrorType.Unauthenticated => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            ErrorType.TooManyRequests => "Too Many Requests",
            _ => "Internal Server Error"
        };
}