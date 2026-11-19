namespace ToeicSpace.Identity.API.Contracts;

/// <param name="ResetToken">Native clients only; browsers send the reset cookie instead.</param>
public sealed record ConfirmPasswordResetRequest(
    string NewPassword, string ConfirmPassword, string? ResetToken = null)
{
    public override string ToString() => nameof(ConfirmPasswordResetRequest);
}

/// <summary>Body of refresh and logout for native clients; browsers send the refresh cookie instead.</summary>
public sealed record RefreshTokenRequest(string? RefreshToken)
{
    public override string ToString() => nameof(RefreshTokenRequest);
}

public sealed record RequestChangePasswordOtpRequest(string CurrentPassword)
{
    public override string ToString() => nameof(RequestChangePasswordOtpRequest);
}

public sealed record ChangePasswordRequest(
    string CurrentPassword, string NewPassword, string ConfirmPassword, string Otp)
{
    public override string ToString() => nameof(ChangePasswordRequest);
}
