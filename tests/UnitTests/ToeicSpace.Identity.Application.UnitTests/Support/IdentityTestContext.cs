using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Identity.Application.Common.Sessions;
using ToeicSpace.Identity.Application.Features.Login.Commands;
using ToeicSpace.Identity.Application.Features.Logout.Commands;
using ToeicSpace.Identity.Application.Features.RefreshSession.Commands;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Persistence;
using ToeicSpace.Identity.Infrastructure.Persistence.Repositories;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Application.UnitTests.Support;

/// <summary>
/// Real repositories, hashers and token issuer over an in-memory SQLite database
/// (SQLite supports the ExecuteUpdate calls the refresh-token repository relies on).
/// </summary>
public sealed class IdentityTestContext : IAsyncDisposable
{
    public const string Password = "Correct-Horse-42";

    private readonly SqliteConnection _connection;

    private IdentityTestContext(SqliteConnection connection, IdentityDbContext dbContext)
    {
        _connection = connection;
        Db = dbContext;

        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PrivateKey = Convert.ToBase64String(signingKey.ExportPkcs8PrivateKey());
        PublicKey = Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo());

        AccessTokenIssuer = new JwtAccessTokenIssuer(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            PrivateKey = PrivateKey
        });

        Users = new UserRepository(Db);
        RefreshTokens = new RefreshTokenRepository(Db);
        SessionIssuer = new SessionIssuer(AccessTokenIssuer, TokenGenerator, RefreshTokens, SessionOptions, Time);
    }

    public string Issuer => "toeicspace-identity";

    public string Audience => "toeicspace-api";

    public string PrivateKey { get; }

    public string PublicKey { get; }

    public IdentityDbContext Db { get; }

    public FixedTimeProvider Time { get; } = new();

    public InMemoryLoginAttemptLimiter LoginAttemptLimiter { get; } = new(maxFailedAttempts: 5);

    public Pbkdf2PasswordHasher PasswordHasher { get; } = new();

    public RefreshTokenGenerator TokenGenerator { get; } = new();

    public SessionOptions SessionOptions { get; } = new()
    {
        RefreshTokenLifetime = TimeSpan.FromDays(7),
        IdleTimeout = TimeSpan.FromHours(24),
        MaxActiveSessions = 3
    };

    public JwtAccessTokenIssuer AccessTokenIssuer { get; }

    public UserRepository Users { get; }

    public RefreshTokenRepository RefreshTokens { get; }

    public SessionIssuer SessionIssuer { get; }

    public static async Task<IdentityTestContext> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        return new IdentityTestContext(connection, dbContext);
    }

    public async Task<User> AddUserAsync(
        string email = "admin@toeicspace.vn",
        UserRole role = UserRole.Admin,
        UserStatus status = UserStatus.Active,
        bool emailVerified = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Test User",
            Email = email,
            PasswordHash = PasswordHasher.Hash(Password),
            EmailVerifiedAt = emailVerified ? Time.GetUtcNow().UtcDateTime : null,
            Role = role,
            Status = status,
            CreatedAt = Time.GetUtcNow().UtcDateTime
        };

        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        return user;
    }

    public LoginHandler LoginHandler()
        => new(Users, PasswordHasher, LoginAttemptLimiter, SessionIssuer, NullLogger<LoginHandler>.Instance);

    public RefreshSessionHandler RefreshHandler()
        => new(RefreshTokens, TokenGenerator, Users, SessionIssuer, SessionOptions, Time, NullLogger<RefreshSessionHandler>.Instance);

    public LogoutHandler LogoutHandler()
        => new(RefreshTokens, TokenGenerator, NullLogger<LogoutHandler>.Instance);

    public Task<List<UserToken>> RefreshTokenRowsAsync(Guid userId)
        => Db.UserTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId)
            .OrderBy(token => token.CreatedAt)
            .ToListAsync();

    public async ValueTask DisposeAsync()
    {
        AccessTokenIssuer.Dispose();
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
