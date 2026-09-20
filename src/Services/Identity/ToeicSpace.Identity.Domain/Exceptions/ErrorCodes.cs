namespace ToeicSpace.Identity.Domain.Exceptions;

public static class ErrorCodes
{
    // Auth
    public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string EmailNotVerified = "AUTH_EMAIL_NOT_VERIFIED";
    public const string EmailAlreadyVerified = "AUTH_EMAIL_ALREADY_VERIFIED";
    public const string PasswordSameAsOld = "AUTH_PASSWORD_SAME_AS_OLD";
    public const string PasswordsDoNotMatch = "AUTH_PASSWORDS_DO_NOT_MATCH";

    // Token
    public const string TokenInvalid = "TOKEN_INVALID";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string TokenRevoked = "TOKEN_REVOKED";

    // OAuth
    public const string OAuthAlreadyLinked = "OAUTH_ALREADY_LINKED";
    public const string OAuthEmailMismatch = "OAUTH_EMAIL_MISMATCH";

    // User
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string EmailAlreadyExists = "USER_EMAIL_ALREADY_EXISTS";
    public const string PhoneAlreadyExists = "USER_PHONE_ALREADY_EXISTS";
    public const string RegistrationCouldNotBeCompleted =
        "REGISTRATION_COULD_NOT_BE_COMPLETED";

    // OTP
    public const string InvalidOrExpiredOtp = "OTP_INVALID_OR_EXPIRED";
    public const string OtpRateLimitExceeded = "OTP_RATE_LIMIT_EXCEEDED";

    // Validation
    public const string ValidationError = "VALIDATION_ERROR";
}
