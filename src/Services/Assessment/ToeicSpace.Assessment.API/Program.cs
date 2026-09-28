using ToeicSpace.Assessment.API;
using ToeicSpace.Assessment.Application;
using ToeicSpace.Assessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

// Must run first so HTTPS redirection, rate limits and audit logs see the real client.
app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseApiServices();

app.MapControllers();

app.Run();
