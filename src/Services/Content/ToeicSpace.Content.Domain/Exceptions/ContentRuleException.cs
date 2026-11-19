namespace ToeicSpace.Content.Domain.Exceptions;

public sealed class ContentRuleException(string code, string message, int statusCode = 400, Exception? inner = null)
    : Exception(message, inner)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    /// <summary>Extra ProblemDetails members (e.g. <c>videoId</c>, <c>unavailable</c>) next to <c>code</c>.</summary>
    public IReadOnlyDictionary<string, object?>? Extensions { get; init; }
    public static ContentRuleException NotFound() => new("course_not_found", "Course not found.", 404);
    public static ContentRuleException InvalidState() => new("invalid_state", "Operation is not allowed in this course state.", 409);
}
