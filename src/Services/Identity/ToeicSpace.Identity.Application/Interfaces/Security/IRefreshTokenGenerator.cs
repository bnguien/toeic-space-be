namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface IRefreshTokenGenerator
{
    /// <summary>
    /// Creates an opaque, high-entropy refresh token. Only its hash is stored.
    /// </summary>
    string Generate();

    string Hash(string refreshToken);
}
