using Laundry.BuildingBlocks.Api;
using Laundry.Host;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapEndpoints();
app.Run();

public partial class Program;
