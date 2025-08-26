using System.Net;
using System.Net.Http;         // <-- add
using System.Threading.Tasks;  // <-- add
using FluentAssertions;
using Xunit;

public class ClosuresUnitTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ClosuresUnitTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // This hits the live NCDOT feed via your controller; keep skipped for stable CI.
    [Fact(Skip = "Hits live NCDOT feed; enable manually when online")]
    public async Task Closures_Returns_Json_Array()
    {
        var resp = await _client.GetAsync("/api/closures?countyId=26");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.TrimStart().Should().StartWith("[");
    }
}
