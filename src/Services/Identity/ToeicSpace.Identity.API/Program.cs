using ToeicSpace.Identity.API;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

// Must run first so HTTPS redirection, rate limits and logs see the real client.
app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseApiServices();

app.UseRateLimiter();

app.MapControllers();

app.Run();
