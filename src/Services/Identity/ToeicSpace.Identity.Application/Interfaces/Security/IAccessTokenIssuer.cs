using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Application.Interfaces.Security;

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(User user, DateTimeOffset issuedAt);
}
