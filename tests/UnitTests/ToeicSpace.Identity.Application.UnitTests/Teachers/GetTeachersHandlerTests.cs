using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.Identity.Application.Features.Teachers.Queries;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Infrastructure.Persistence;
using ToeicSpace.Identity.Infrastructure.Persistence.Repositories;

namespace ToeicSpace.Identity.Application.UnitTests.Teachers;

public sealed class GetTeachersHandlerTests
{
    [Fact]
    public async Task Search_only_returns_active_teachers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options;
        await using var db = new IdentityDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var active = new User { Id = Guid.NewGuid(), FullName = "Ada Teacher", Email = "ada@example.com", Role = UserRole.Teacher, Status = UserStatus.Active };
        var inactive = new User { Id = Guid.NewGuid(), FullName = "Inactive Teacher", Email = "inactive@example.com", Role = UserRole.Teacher, Status = UserStatus.Inactive };
        var other = new User { Id = Guid.NewGuid(), FullName = "Other User", Email = "other@example.com", Role = UserRole.User, Status = UserStatus.Active };
        db.Users.AddRange(active, inactive, other);
        await db.SaveChangesAsync();
        var handler = new GetTeachersHandler(new UserRepository(db));
        var page = await handler.Handle(new GetTeachersQuery("Ada", 1, 20), default);
        Assert.Single(page.Items);
        Assert.Equal(active.Id, page.Items[0].Id);
    }
}
