using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public IntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Closures_Endpoint_OK()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/closures");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
