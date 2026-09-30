namespace ToeicSpace.Identity.API.Contracts;

public sealed record ConfirmPasswordResetRequest(string NewPassword, string ConfirmPassword)
{
    public override string ToString() => nameof(ConfirmPasswordResetRequest);
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword)
{
    public override string ToString() => nameof(ChangePasswordRequest);
}
