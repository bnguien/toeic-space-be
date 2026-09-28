using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.API.Security;

/// <summary>
/// Endpoints authenticated by the refresh cookie require a custom header.
/// Cross-site forms cannot send it, and the CORS allow-list blocks it from other origins.
/// This backs up the SameSite=Strict cookie.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireCsrfHeaderAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-CSRF-Protection";
    public const string HeaderValue = "1";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var headers = context.HttpContext.Request.Headers;

        if (headers.TryGetValue(HeaderName, out var value) && value == HeaderValue)
        {
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "The request is missing the required anti-forgery header.",
            Instance = context.HttpContext.Request.Path,
            Extensions = { ["code"] = ErrorCodes.TokenInvalid }
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { "application/problem+json" }
        };
    }
}
