using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using ToeicSpace.BuildingBlocks.Security.Extensions;
using ToeicSpace.BuildingBlocks.Security.Http;
using ToeicSpace.Content.API.Controllers;
using ToeicSpace.Content.API.Middlewares;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application;
using ToeicSpace.Content.Infrastructure;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();

builder.Services.AddInfrastructure(
    builder.Configuration);
builder.Services.AddExceptionHandler<ContentExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddKestrelHardening();
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);
builder.Services.AddToeicSpaceJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));
builder.Services.AddVocabularyRateLimits();
builder.Services.AddNewsRateLimits();
builder.Services.AddDictationRateLimits();
builder.Services.AddVideoRateLimits();
builder.Services.AddGrammarRateLimits();
builder.Services.AddDictionaryRateLimits();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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

var app = builder.Build();

app.UseForwardedHeaders();
// Before the security headers: lets speech mp3 responses keep their public Cache-Control.
app.UsePublicCacheControl();
// News covers are public, content-addressed images: keep their long cache headers (no API no-store).
app.UseSecurityHeaders(new SecurityHeadersOptions { ExcludedPathPrefixes = ["/swagger", NewsController.CoversPath] });
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
