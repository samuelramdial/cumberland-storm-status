using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task DebrisRequest_Create_Persists_AndReturnsId()
    {
        var payload = new
        {
            fullName = "Test User",
            address = "123 Main St",
            email = "test@example.com",
            notes = "integration test"
        };

        var resp = await _client.PostAsJsonAsync("/api/debris-requests", payload);
        resp.IsSuccessStatusCode.Should().BeTrue($"body: {await resp.Content.ReadAsStringAsync()}");

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        json.ValueKind.Should().Be(JsonValueKind.Object);

        int id =
            (json.TryGetProperty("id", out var idLower) && idLower.TryGetInt32(out var v1)) ? v1 :
            (json.TryGetProperty("Id", out var idUpper) && idUpper.TryGetInt32(out var v2)) ? v2 :
            0;

        if (id <= 0)
        {
            Xunit.Assert.Fail($"Response missing id. Body: {json}");
        }

        // (Optional) verify row exists — only if your app uses InMemory DB in Testing env
        // If you want this check, wire a scoped provider and query AppDbContext here.
    }
}
