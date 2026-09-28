namespace ToeicSpace.Assessment.Domain.Exceptions;

public class AppException : Exception
{
    public ErrorType Type { get; }

    public string? Code { get; }

    public IDictionary<string, string[]>? ValidationErrors { get; }

    public AppException(
        ErrorType type,
        string message,
        string? code = null)
        : base(message)
    {
        Type = type;
        Code = code;
    }

    public AppException(
        IDictionary<string, string[]> validationErrors)
        : base("One or more validation failures occurred.")
    {
        Type = ErrorType.Validation;
        Code = ErrorCodes.ValidationError;
        ValidationErrors = validationErrors;
    }

    public static AppException Validation(
        string message,
        string? code = null)
        => new(
            ErrorType.Validation,
            message,
            code);

    public static AppException NotFound(
        string message,
        string? code = null)
        => new(
            ErrorType.NotFound,
            message,
            code ?? ErrorCodes.NotFound);

    public static AppException NotFound(
        string entityName,
        object key,
        string? code = null)
        => new(
            ErrorType.NotFound,
            $"{entityName} with id '{key}' was not found.",
            code ?? ErrorCodes.NotFound);

    public static AppException Conflict(
        string message,
        string? code = null)
        => new(
            ErrorType.Conflict,
            message,
            code ?? ErrorCodes.Conflict);

    public static AppException Forbidden(
        string message,
        string? code = null)
        => new(
            ErrorType.Forbidden,
            message,
            code ?? ErrorCodes.Forbidden);
}
