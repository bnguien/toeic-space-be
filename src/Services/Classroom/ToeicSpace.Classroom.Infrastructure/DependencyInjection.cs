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
        services.AddSingleton(TimeProvider.System);
        services.AddMessagingBus(config, messaging =>
            messaging.AddConsumer<TeacherChangedConsumer>(typeof(TeacherChangedConsumerDefinition)));
        services.AddSingleton<TeacherSynchronizationRequester>();
        services.AddSingleton<ITeacherSynchronizationRequester>(sp => sp.GetRequiredService<TeacherSynchronizationRequester>());
        services.AddHostedService(sp => sp.GetRequiredService<TeacherSynchronizationRequester>());
        return services;
    }
}
