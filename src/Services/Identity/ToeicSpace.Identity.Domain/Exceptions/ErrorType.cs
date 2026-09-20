namespace ToeicSpace.Identity.Domain.Exceptions;

public enum ErrorType
{
    Validation,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    TooManyRequests
}