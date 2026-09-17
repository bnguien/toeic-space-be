namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface IOtpHasher
{
    string Hash(string otp);

    bool Verify(string otp, string expectedHash);
}
