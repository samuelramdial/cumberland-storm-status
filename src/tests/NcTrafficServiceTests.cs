using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

// If NcTrafficService has a namespace, add it here, e.g.:
// using Server.Infrastructure;  // <-- change to match your file

public class NcTrafficServiceTests
{
    private static HttpClient MakeClient(string json)
    {
        var handler = new StubHttpMessageHandler(json, HttpStatusCode.OK);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://eapps.ncdot.gov/services/traffic-prod/v1/")
        };
    }

    private static ServiceProvider BuildProvider(HttpClient client, IConfiguration? cfg = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMemoryCache();               // in case service uses IMemoryCache
        services.AddSingleton<HttpClient>(client);
        cfg ??= new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton(cfg);

        services.AddTransient<NcTrafficService>(); // let DI pick the ctor
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetClosuresAsync_Returns_Item_With_Coordinates()
    {
        // Minimal payload; include many aliases your parser might consider.
        // We also include "status":"OPEN" since that's what the current service tends to return.
        var json = @"[
          {
            ""id"": 123,
            ""road"": ""I-95"",
            ""name"": ""I-95"",
            ""status"": ""OPEN"",
            ""latitude"": 35.0527,
            ""longitude"": -78.8784,
            ""attributes"": {
              ""id"": 123,
              ""road"": ""I-95"",
              ""name"": ""I-95"",
              ""status"": ""OPEN"",
              ""latitude"": 35.0527,
              ""longitude"": -78.8784
            }
          }
        ]";

        using var sp = BuildProvider(MakeClient(json));
        var svc = sp.GetRequiredService<NcTrafficService>();

        var list = await svc.GetClosuresAsync("Cumberland", filterStatus: "", CancellationToken.None);

        list.Should().NotBeNull();
        list.Should().HaveCount(1);
        var rc = list[0];
        rc.UpdatedAt.Should().NotBe(default);  // service should set/update a timestamp
        rc.Lat.Should().Be(35.0527);
        rc.Lng.Should().Be(-78.8784);
        rc.Status.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetClosuresAsync_Filter_Open_Returns_Items()
    {
        // Two items, both effectively OPEN for the current parser behavior.
        var json = @"[
          { ""id"": 1, ""road"": ""US-301"", ""status"": ""OPEN"", ""latitude"": 35.1, ""longitude"": -78.9,
            ""attributes"": { ""id"": 1, ""road"": ""US-301"", ""status"": ""OPEN"", ""latitude"": 35.1, ""longitude"": -78.9 } },
          { ""id"": 2, ""road"": ""NC-24"",  ""status"": ""OPEN"", ""latitude"": 35.2, ""longitude"": -78.8,
            ""attributes"": { ""id"": 2, ""road"": ""NC-24"",  ""status"": ""OPEN"", ""latitude"": 35.2, ""longitude"": -78.8 } }
        ]";

        using var sp = BuildProvider(MakeClient(json));
        var svc = sp.GetRequiredService<NcTrafficService>();

        var all = await svc.GetClosuresAsync("Cumberland", filterStatus: "", CancellationToken.None);
        var open = await svc.GetClosuresAsync("Cumberland", filterStatus: "OPEN", CancellationToken.None);
        var other = await svc.GetClosuresAsync("Cumberland", filterStatus: "CLOSED", CancellationToken.None);

        all.Should().HaveCount(2);
        open.Should().HaveCount(all.Count); // with current mapping, OPEN matches all
        other.Should().BeEmpty();           // nothing maps to CLOSED in this fixture
    }
}
