using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.BuildingBlocks.Storage.Extensions;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Infrastructure.Services;

namespace ToeicSpace.Content.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("ContentDatabase")
            ?? throw new InvalidOperationException("ContentDatabase connection string is required.");
        services.AddDbContext<ContentDbContext>(options => options.UseMySql(connection,
            new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ICourseModuleRepository, CourseModuleRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ILessonUploadRepository, LessonUploadRepository>();
        services.AddScoped<IThumbnailUploadRepository, ThumbnailUploadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddCloudflareR2Storage(configuration);
        services.AddScoped<IThumbnailObjectStore, R2ThumbnailObjectStore>();
        services.AddScoped<ILessonMediaStore, R2LessonMediaStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<ExpiredThumbnailCleanup>();
        services.AddHostedService<ExpiredLessonUploadCleanup>();
        return services;
    }
}
