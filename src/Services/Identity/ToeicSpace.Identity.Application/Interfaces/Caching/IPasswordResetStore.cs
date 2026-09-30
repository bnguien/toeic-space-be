using ToeicSpace.Identity.Application.Models;

namespace ToeicSpace.Identity.Application.Interfaces.Caching;

public interface IPasswordResetStore
{
    Task<string?> BeginRequestAsync(string email, CancellationToken cancellationToken);

    Task<bool> SetOtpAsync(string email, string requestId, Guid userId,
        string credentialStamp, string otpHash, CancellationToken cancellationToken);

    Task<bool> VerifyAsync(string email, string otpHash, string tokenHash,
        CancellationToken cancellationToken);

    Task<PasswordResetGrant?> GetGrantAsync(string tokenHash, CancellationToken cancellationToken);

    Task<bool> ConsumeGrantAsync(string tokenHash, CancellationToken cancellationToken);
}
