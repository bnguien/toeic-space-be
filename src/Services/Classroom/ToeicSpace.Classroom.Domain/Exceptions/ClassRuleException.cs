namespace ToeicSpace.Classroom.Domain.Exceptions;

public sealed class ClassRuleException(string code, string message, int statusCode = 400, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;

    public static ClassRuleException NotFound() => new("class_not_found", "Class not found.", 404);

    public static ClassRuleException InvalidState() => new("invalid_state", "Operation is not allowed in this class state.", 409);
}
