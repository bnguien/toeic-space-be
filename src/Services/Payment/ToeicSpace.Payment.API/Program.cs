using ToeicSpace.Payment.Domain.Exceptions;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ToeicSpace.BuildingBlocks.Security.Extensions;
using ToeicSpace.BuildingBlocks.Security.Http;
using ToeicSpace.Payment.Infrastructure;
using ToeicSpace.Payment.Application;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment() &&
    (builder.Configuration["InternalApi:Key"] is not { Length: >= 32 } ||
     builder.Configuration["SePay:WebhookApiKey"] is not { Length: >= 24 } ||
     string.IsNullOrWhiteSpace(builder.Configuration["SePay:Bank"]) ||
     string.IsNullOrWhiteSpace(builder.Configuration["SePay:AccountNumber"])))
    throw new InvalidOperationException("Internal API and SePay payment configuration are required in production.");
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddToeicSpaceJwtAuthentication(builder.Configuration);
builder.Services.AddKestrelHardening();
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
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
    options.AddSecurityDefinition("InternalKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Internal-Key",
        Description = "Shared internal key used by Content Service and reconciliation callers."
    });
});

var app = builder.Build();
app.UseForwardedHeaders();
// Log only HTTP result, never webhook headers, account numbers or transfer memos.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.Equals("/api/v1/webhooks/sepay", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }
    try { await next(); }
    finally
    {
        app.Logger.LogInformation("SePay webhook HTTP {StatusCode}.", context.Response.StatusCode);
    }
});
app.UseSecurityHeaders(new SecurityHeadersOptions { ExcludedPathPrefixes = ["/swagger"] });
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, code) = error switch
    {
        PaymentException rule => (rule.Status, rule.Code),
        KeyNotFoundException => (404, "not_found"),
        ArgumentException => (400, "invalid_request"),
        InvalidOperationException => (409, "conflict"),
        DbUpdateException { InnerException: MySqlException { Number: 1062 } } => (409, "duplicate_code"),
        DbUpdateException { InnerException: MySqlException { Number: 1205 or 1213 } } => (409, "concurrency_conflict"),
        MySqlException { Number: 1205 or 1213 } => (409, "concurrency_conflict"),
        _ => (500, "internal_error")
    };
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { code, message = status == 500 ? "Unexpected error." : error?.Message });
}));
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
