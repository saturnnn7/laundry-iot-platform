using System.Net;
using System.Text.Json;
using Laundry.BuildingBlocks.Application.Paging;
using Xunit;

namespace Laundry.Tests.Integration;

public class PageQueryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValidQueryReturnsListShape()
    {
        var client = factory.CreateClientWithTestEndpoints();
        var cursor = new PageCursor(DateTimeOffset.UtcNow, Guid.NewGuid()).Encode();

        var response = await client.GetAsync($"/api/v1/page-sample?limit=10&cursor={cursor}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("items").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=101", "limit")]
    [InlineData("cursor=garbage", "cursor")]
    public async Task BadParameterReturnsValidationProblemWithField(string query, string field)
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.GetAsync($"/api/v1/page-sample?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("validation", json.RootElement.GetProperty("type").GetString());
        var errors = json.RootElement.GetProperty("errors");
        Assert.Equal([field], errors.EnumerateObject().Select(p => p.Name));
        Assert.NotEmpty(errors.GetProperty(field).EnumerateArray());
    }

    [Fact]
    public async Task OpenApiDocumentShowsCursorAndLimit()
    {
        var client = factory.CreateClientWithTestEndpoints();

        var response = await client.GetAsync("/openapi/v1.json", Ct);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var parameters = json.RootElement.GetProperty("paths").GetProperty("/api/v1/page-sample").GetProperty("get").GetProperty("parameters");
        var names = parameters.EnumerateArray().Select(p => p.GetProperty("name").GetString()).Order(StringComparer.Ordinal);
        Assert.Equal(["cursor", "limit"], names);
    }
}
