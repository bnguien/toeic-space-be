using System.Text.Json.Serialization;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using ToeicSpace.BuildingBlocks.Security.Extensions;
using ToeicSpace.Classroom.API.Middlewares;
using ToeicSpace.Classroom.API.Security;
using ToeicSpace.Classroom.Application;
using ToeicSpace.Classroom.Infrastructure;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

var seedClassroomDemo = args.Contains("--seed-classroom-demo", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--seed-classroom-demo").ToArray());
builder.Services.AddInfrastructureServices(builder.Configuration, !seedClassroomDemo);
builder.Services.AddApplicationServices();
if (!seedClassroomDemo)
{
    builder.Services.AddExceptionHandler<ClassroomExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddKestrelHardening();
    builder.Services.AddTrustedForwardedHeaders(builder.Configuration);
    builder.Services.AddToeicSpaceJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorizationBuilder()
        .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
        .AddPolicy("Admin", p => p.RequireRole("Admin"))
        .AddPolicy("AdminOrTeacher", p => p.RequireRole("Admin", "Teacher"))
        .AddPolicy(AuthorizationPolicies.AnalyticsReader, p => p
            .RequireAuthenticatedUser()
            .RequireRole(AuthorizationPolicies.AnalyticsReaderRoles));
    builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Identity access token."
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });
}
var app = builder.Build();
if (seedClassroomDemo)
{
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("--seed-classroom-demo is permitted only in Development or Testing.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
    await db.Database.MigrateAsync();
    var report = await scope.ServiceProvider.GetRequiredService<ClassroomDemoSeeder>().SeedAsync();
    Console.WriteLine($"Classroom demo seed complete: {report.Centers} centers, {report.Rooms} rooms, " +
        $"{report.Classes} classes, {report.Enrollments} active enrollments, {report.Schedules} schedules, " +
        $"{report.Sessions} sessions. Created primary demo records: {report.CreatedPrimaryRecords}; reused records: {report.ReusedRecords}; " +
        $"existing recurring occurrences skipped: {report.SkippedOccurrences}.");
    return;
}
app.Services.GetRequiredService<Hangfire.IRecurringJobManager>().AddOrUpdate<
    ToeicSpace.Classroom.Infrastructure.Services.GoogleMeet.GoogleMeetProvisioningJob>(
    "classroom-google-meet-reconcile", x => x.ScanAsync(CancellationToken.None), "*/1 * * * *");
app.UseForwardedHeaders();
app.UseSecurityHeaders();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
