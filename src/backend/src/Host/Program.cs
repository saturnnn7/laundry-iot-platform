using Laundry.BuildingBlocks.Api;
using Laundry.BuildingBlocks.Api.Problems;
using Laundry.BuildingBlocks.Api.Security;
using Laundry.Host;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddApiProblemDetails();
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();
app.UseApiProblemDetails();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapEndpoints();
app.Run();

public partial class Program;
