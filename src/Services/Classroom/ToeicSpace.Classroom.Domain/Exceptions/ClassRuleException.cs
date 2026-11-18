namespace ToeicSpace.Classroom.Domain.Exceptions;

public sealed class ClassRuleException(string code, string message, int statusCode = 400, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;

    /// <summary>Messages per request parameter of a validation failure; written to the problem's <c>errors</c>.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ClassRuleException NotFound() => new("class_not_found", "Class not found.", 404);

    public static ClassRuleException InvalidState() => new("invalid_state", "Operation is not allowed in this class state.", 409);

    /// <summary>A 400 whose <c>errors</c> lists every failing parameter; the message repeats the first failure.</summary>
    public static ClassRuleException Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation_error", errors.Values.SelectMany(messages => messages).FirstOrDefault() ?? "The request is not valid.")
        {
            Errors = errors
        };
}
