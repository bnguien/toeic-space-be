using Microsoft.EntityFrameworkCore.Design;

namespace ToeicSpace.Assessment.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used exclusively by EF Core CLI tools (dotnet ef migrations).
/// This factory reads configuration from appsettings, user-secrets, and environment variables.
/// It is NOT used at runtime — runtime DbContext is registered via DependencyInjection.cs.
/// </summary>
public sealed class AssessmentDbContextFactory
    : IDesignTimeDbContextFactory<AssessmentDbContext>
{
    public AssessmentDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable(
            "ASPNETCORE_ENVIRONMENT") ?? "Development";

        var basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "ToeicSpace.Assessment.API");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetFullPath(basePath))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(
                $"appsettings.{environment}.json",
                optional: true)
            .AddUserSecrets<AssessmentDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(
            "AssessmentDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'AssessmentDatabase' was not found. " +
                "Set it via environment variable 'ConnectionStrings__AssessmentDatabase' " +
                "or in appsettings.json of the API project.");

        var options = new DbContextOptionsBuilder<AssessmentDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;

        return new AssessmentDbContext(options);
    }
}
