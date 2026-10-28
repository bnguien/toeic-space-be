using ToeicSpace.BuildingBlocks.Messaging;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Messaging.Outbox;

namespace ToeicSpace.Identity.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<UserToken> UserTokens => Set<UserToken>();

    public DbSet<UserOAuth> UserOAuthAccounts => Set<UserOAuth>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void EnqueueIntegrationEvent(IIntegrationEvent message) => OutboxMessages.Add(IdentityOutbox.Create(message));

    public override int SaveChanges()
    {
        CaptureUserChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CaptureUserChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    private void CaptureUserChanges()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<User>().ToArray())
        {
            var relevantChange = entry.State is EntityState.Added or EntityState.Deleted
                || entry.State == EntityState.Modified && new[]
                {
                    nameof(User.FullName), nameof(User.Email), nameof(User.AvatarUrl),
                    nameof(User.Role), nameof(User.Status), nameof(User.DeletedAt), nameof(User.EmailVerifiedAt)
                }.Any(name => entry.Property(name).IsModified);
            if (!relevantChange) continue;

            var user = entry.Entity;
            user.Version = entry.State == EntityState.Added
                ? Math.Max(1, user.Version)
                : entry.Property(x => x.Version).OriginalValue + 1;

            var deleted = entry.State == EntityState.Deleted || user.DeletedAt is not null;
            EnqueueIntegrationEvent(new TeacherChangedIntegrationEvent(
                user.Id, user.FullName, user.Email, user.AvatarUrl,
                user.Role.ToString(), deleted ? "Deleted" : user.Status.ToString(),
                !deleted && user.Role == UserRole.Teacher && user.Status == UserStatus.Active,
                user.Version));
            EnqueueIntegrationEvent(new StudentChangedIntegrationEvent(
                user.Id, user.FullName, user.Email, user.Role.ToString(),
                deleted ? "Deleted" : user.Status.ToString(),
                !deleted && user.EmailVerifiedAt is not null,
                !deleted && user.Role == UserRole.User && user.Status == UserStatus.Active && user.EmailVerifiedAt is not null,
                user.Version));
        }
    }
}
