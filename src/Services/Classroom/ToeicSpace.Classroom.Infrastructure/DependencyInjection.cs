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

namespace ToeicSpace.Classroom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("ClassroomDatabase")
            ?? throw new InvalidOperationException("ClassroomDatabase connection string is required.");
        services.AddDbContext<ClassroomDbContext>(o => o.UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITeacherReadRepository, TeacherReadRepository>();
        services.AddScoped<IStudentReadRepository, StudentReadRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IEnrollmentImportReader, EnrollmentImportReader>();
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
        services.AddHostedService<ClassAutoStartJob>();
        return services;
    }
}
