namespace ToeicSpace.Identity.API.Security;

/// <summary>
/// Native apps cannot rely on browser cookies: the Secure flag blocks them over plain HTTP in
/// development, and the app keeps its own encrypted token storage anyway. Requests with this header
/// receive the refresh and password-reset tokens in the body and send them back in the body.
/// In this mode cookies are never read or written, so a browser script that sets the header cannot
/// turn the HttpOnly refresh cookie into a readable token.
/// </summary>
public static class NativeClient
{
    public const string HeaderName = "X-Client-Type";
    public const string HeaderValue = "mobile";

    public static bool IsNativeClient(this HttpRequest request) =>
        request.Headers.TryGetValue(HeaderName, out var value) &&
        string.Equals(value, HeaderValue, StringComparison.OrdinalIgnoreCase);
}
