using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ToeicSpace.Classroom.Infrastructure.Persistence;

public sealed class ClassroomDbContextFactory : IDesignTimeDbContextFactory<ClassroomDbContext>
{
    public ClassroomDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ClassroomDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__ClassroomDatabase for EF tooling.");
        var options = new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))).Options;
        return new ClassroomDbContext(options);
    }
}
