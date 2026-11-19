using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.BuildingBlocks.Messaging.MassTransit;
using ToeicSpace.Classroom.Infrastructure.Consumers;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using ToeicSpace.Classroom.Infrastructure.Services.Teachers;
using ToeicSpace.Classroom.Infrastructure.Services.Students;
using ToeicSpace.Classroom.Infrastructure.Services.Classes;
using ToeicSpace.Classroom.Infrastructure.Services.Scheduling;
using ToeicSpace.Classroom.Infrastructure.Persistence.Seeding;
using ToeicSpace.Classroom.Application.Features.GoogleMeet;
using ToeicSpace.Classroom.Infrastructure.Services.GoogleMeet;
using Hangfire;
using Hangfire.MySql;

namespace ToeicSpace.Classroom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration config, bool enableJobs = true)
    {
        var connection = config.GetConnectionString("ClassroomDatabase")
            ?? throw new InvalidOperationException("ClassroomDatabase connection string is required.");
        services.AddDbContext<ClassroomDbContext>(o => o.UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IFacilityRepository, FacilityRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITeacherReadRepository, TeacherReadRepository>();
        services.AddScoped<IStudentReadRepository, StudentReadRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IStudentConflictDetector, StudentConflictDetector>();
        services.AddScoped<IEnrollmentConflictPreviewService, EnrollmentConflictPreviewService>();
        services.AddScoped<IStudentConflictReadService, StudentConflictReadService>();
        services.AddScoped<ClassSchedulingService>();
        services.AddScoped<IClassSchedulingService>(sp => sp.GetRequiredService<ClassSchedulingService>());
        services.AddScoped<IStudentConflictPreviewService>(sp => sp.GetRequiredService<ClassSchedulingService>());
        services.AddScoped<ClassroomDemoSeeder>();
        services.AddHttpClient("GoogleMeet", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<GoogleMeetOperations>();
        services.AddScoped<IGoogleMeetOperations>(sp => sp.GetRequiredService<GoogleMeetOperations>());
        services.AddScoped<GoogleMeetProvisioningJob>();
        if (enableJobs)
        {
            services.AddHangfire(hangfire => hangfire
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseStorage(new MySqlStorage(connection, new MySqlStorageOptions
                { PrepareSchemaIfNecessary = true, TablesPrefix = "Hangfire" })));
            services.AddHangfireServer();
        }
        services.AddScoped<IEnrollmentImportReader, EnrollmentImportReader>();
        services.AddScoped<IClassroomAnalyticsReader, ClassroomAnalyticsReader>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_ => TimeZoneInfo.FindSystemTimeZoneById(
            config["Classroom:TimeZoneId"] ?? "Asia/Ho_Chi_Minh"));
        services.AddMessagingBus(config, messaging =>
        {
            messaging.AddConsumer<TeacherChangedConsumer>(typeof(TeacherChangedConsumerDefinition));
            messaging.AddConsumer<StudentChangedConsumer>(typeof(StudentChangedConsumerDefinition));
        });
        services.AddSingleton<TeacherSynchronizationRequester>();
        services.AddSingleton<ITeacherSynchronizationRequester>(sp => sp.GetRequiredService<TeacherSynchronizationRequester>());
        services.AddHostedService(sp => sp.GetRequiredService<TeacherSynchronizationRequester>());
        services.AddHostedService<StudentSynchronizationRequester>();
        if (enableJobs) services.AddHostedService<ClassAutoStartJob>();
        return services;
    }
}
