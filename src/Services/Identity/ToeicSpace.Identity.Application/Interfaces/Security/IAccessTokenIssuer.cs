using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(User user, DateTimeOffset issuedAt);

    /// <summary>
    /// Read-only token for the AI assistant (scope agent.read, role Admin): it can call GET endpoints only.
    /// </summary>
    /// <param name="actor">
    /// Who the assistant acts for (validated X-On-Behalf-Of). Written to the <c>act</c> claim for audit only;
    /// it never changes the role or scope of the token. Null leaves the claim out.
    /// </param>
    IssuedAccessToken IssueForAgent(Guid subjectId, DateTimeOffset issuedAt, TimeSpan lifetime, string? actor = null);
}
