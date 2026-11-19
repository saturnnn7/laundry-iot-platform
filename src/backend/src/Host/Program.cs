using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Api.Http;
using Laundry.BuildingBlocks.Api.Problems;
using Laundry.BuildingBlocks.Api.Security;
using Laundry.BuildingBlocks.Infrastructure.DependencyInjection;
using Laundry.Host;
using Laundry.Host.Health;
using Laundry.Host.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var healthChecks = builder.Services.AddHealthChecks();
var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.Add(new HealthCheckRegistration(
        "postgres",
        _ => new PostgresHealthCheck(connectionString),
        failureStatus: null,
        tags: null));
}

builder.Services.AddApiForwardedHeaders(builder.Configuration);
builder.Services.AddApiProblemDetails();
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddApiDocument();
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateModulesAsync();
}

var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", true);

app.UseApiForwardedHeaders();
app.UseApiProblemDetails();

if (swaggerEnabled)
{
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Laundry IoT Platform API v1"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

if (swaggerEnabled)
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapEndpoints();
app.Run();

public partial class Program;
