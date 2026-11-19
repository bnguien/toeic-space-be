using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ToeicSpace.Content.Infrastructure.Persistence;

public sealed class ContentDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ContentDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__ContentDatabase for EF tooling.");
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))).Options;
        return new ContentDbContext(options);
    }
}
