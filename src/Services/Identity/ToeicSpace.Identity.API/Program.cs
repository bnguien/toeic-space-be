using ToeicSpace.Identity.API;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();

app.UseApiServices();

app.UseRateLimiter();

app.MapControllers();

app.Run();
