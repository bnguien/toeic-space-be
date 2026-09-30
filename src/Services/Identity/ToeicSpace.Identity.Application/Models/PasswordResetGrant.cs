namespace ToeicSpace.Identity.Application.Models;

public sealed record PasswordResetGrant(Guid UserId, string CredentialStamp);
