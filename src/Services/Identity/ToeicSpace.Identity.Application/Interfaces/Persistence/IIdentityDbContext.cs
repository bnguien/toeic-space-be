using Microsoft.EntityFrameworkCore;
using ToeicSpace.Identity.Domain.Entities;

namespace ToeicSpace.Identity.Application.Interfaces.Persistence;

/// <summary>
/// Read access to the Identity tables for the fixed EF Core queries of the analytics feature.
/// Writes keep going through the repositories.
/// </summary>
public interface IIdentityDbContext
{
    DbSet<User> Users { get; }

    DbSet<UserToken> UserTokens { get; }

    DbSet<UserOAuth> UserOAuthAccounts { get; }
}
