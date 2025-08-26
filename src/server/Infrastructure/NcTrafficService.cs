using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

// Single-file service. No namespaces needed for your project style.

public sealed class NcTrafficService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Default to Cumberland County (ID 26) per your URL
    private const int DefaultCountyId = 26;           // Cumberland
    private const string DefaultCountyName = "Cumberland";

    public NcTrafficService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://eapps.ncdot.gov/services/traffic-prod/v1/");
        _cache = cache;
    }

    /// <summary>
    /// Returns closures for the given county (by name), or defaults to Cumberland (ID 26).
    /// Optional filter by "OPEN"|"PARTIAL"|"CLOSED".
    /// </summary>
    public async Task<IReadOnlyList<RoadClosure>> GetClosuresAsync(string? countyName, string? filterStatus, CancellationToken ct)
    {
        // If a county name was provided, try to resolve it to an ID; otherwise use 26.
        var countyId = DefaultCountyId;
        if (!string.IsNullOrWhiteSpace(countyName))
        {
            var maybe = await TryGetCountyIdByNameAsync(countyName!, ct);
            if (maybe is int id) countyId = id;
        }

        // Cache per-county for 60s while you develop
        var cacheKey = $"ncdot-incidents-county-{countyId}";
        var incidents = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await GetCountyIncidentsAsync(countyId, ct);
        }) ?? new List<NcdotIncidentDto>();

        var mapped = incidents.Select(MapIncidentToClosure).ToList();

        if (!string.IsNullOrWhiteSpace(filterStatus))
        {
            var f = filterStatus.Trim().ToUpperInvariant();
            if (f is "OPEN" or "PARTIAL" or "CLOSED")
                mapped = mapped.Where(m => string.Equals(m.Status, f, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return mapped.OrderByDescending(m => m.UpdatedAt).ToList();
    }

    // ---------- HTTP calls ----------

    // NEW: use the per-county endpoint you provided
    private async Task<List<NcdotIncidentDto>> GetCountyIncidentsAsync(int countyId, CancellationToken ct)
    {
        // e.g. counties/26/incidents?verbose=true&recent=true
        var url = $"counties/{countyId}/incidents?verbose=true&recent=true";
        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<List<NcdotIncidentDto>>(JsonOpts, ct);
        return json ?? new List<NcdotIncidentDto>();
    }

    // Keep a name→id lookup so you can pass ?county=Wake etc.
    private async Task<int?> TryGetCountyIdByNameAsync(string countyNameRaw, CancellationToken ct)
    {
        var counties = await _http.GetFromJsonAsync<List<NcdotCountyDto>>("counties", JsonOpts, ct);
        if (counties == null || counties.Count == 0) return null;

        string norm(string s) => s.Replace(" County", "", StringComparison.OrdinalIgnoreCase).Trim();

        var wanted = norm(countyNameRaw);
        var exact = counties.FirstOrDefault(c => norm(c.Name).Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact.Id;

        var contains = counties.FirstOrDefault(c =>
            norm(c.Name).Contains(wanted, StringComparison.OrdinalIgnoreCase) ||
            wanted.Contains(norm(c.Name), StringComparison.OrdinalIgnoreCase));
        return contains?.Id;
    }

    // ---------- Mapping ----------

    private static RoadClosure MapIncidentToClosure(NcdotIncidentDto x)
    {
        var (lat, lng) = ExtractLatLng(x);

        return new RoadClosure
        {
            Id = x.Id,
            RoadName = BestRoadName(x),
            Status = NormalizeStatus(x.Status, x.Impact), // "OPEN" | "PARTIAL" | "CLOSED"
            Note = BuildNote(x),
            UpdatedAt = x.LastUpdated ?? x.UpdatedOn ?? x.StartTime ?? DateTime.UtcNow,
            Lat = lat,
            Lng = lng
        };
    }

    private static (double? lat, double? lng) ExtractLatLng(NcdotIncidentDto x)
    {
        if (x.Latitude is double la && x.Longitude is double lo) return (la, lo);
        if (TryParseLatLng(x.Location, out var l1, out var g1)) return (l1, g1);
        if (TryParseLatLng(x.Point, out var l2, out var g2)) return (l2, g2);
        return (null, null);
    }

    private static bool TryParseLatLng(JsonElement? elem, out double? lat, out double? lng)
    {
        lat = null; lng = null;
        if (elem is null) return false;
        var e = elem.Value;

        if (e.ValueKind == JsonValueKind.Object)
        {
            if (TryGetNumber(e, "lat", out var la) && (TryGetNumber(e, "lng", out var lg) || TryGetNumber(e, "lon", out lg)))
            { lat = la; lng = lg; return true; }

            if (TryGetNumber(e, "latitude", out la) && TryGetNumber(e, "longitude", out var lo))
            { lat = la; lng = lo; return true; }

            if (e.TryGetProperty("coordinates", out var coords) && coords.ValueKind == JsonValueKind.Array && coords.GetArrayLength() >= 2)
            {
                var lonOk = coords[0].TryGetDouble(out var lon);
                var latOk = coords[1].TryGetDouble(out var lat2);
                if (lonOk && latOk) { lat = lat2; lng = lon; return true; }
            }
        }

        if (e.ValueKind == JsonValueKind.Array && e.GetArrayLength() >= 2)
        {
            var a = e[0]; var b = e[1];
            if (a.TryGetDouble(out var n0) && b.TryGetDouble(out var n1))
            {
                if (n0 >= -90 && n0 <= 90 && n1 >= -180 && n1 <= 180) { lat = n0; lng = n1; return true; }
                if (n1 >= -90 && n1 <= 90 && n0 >= -180 && n0 <= 180) { lat = n1; lng = n0; return true; }
            }
        }

        if (e.ValueKind == JsonValueKind.String)
        {
            var s = e.GetString() ?? "";
            var sep = s.Contains(';') ? ';' : ',';
            var parts = s.Split(sep);
            if (parts.Length >= 2 &&
                double.TryParse(parts[0].Trim(), out var la) &&
                double.TryParse(parts[1].Trim(), out var lo))
            { lat = la; lng = lo; return true; }
        }

        return false;
    }

    private static bool TryGetNumber(JsonElement obj, string name, out double value)
    {
        value = default;
        foreach (var prop in obj.EnumerateObject())
        {
            if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var d)) { value = d; return true; }
            if (prop.Value.ValueKind == JsonValueKind.String && double.TryParse(prop.Value.GetString(), out var ds)) { value = ds; return true; }
        }
        return false;
    }

    private static string BestRoadName(NcdotIncidentDto x)
    {
        var candidates = new[]
        {
            x.RoadName,
            x.PrimaryRoute, x.RouteName, x.Road, x.Street, x.Highway, x.Route,
            x.Title, x.Headline, x.Description
        };
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c.Trim();

        var text = (x.Title ?? x.Headline ?? x.Description ?? "").Trim();
        var route = TryExtractRouteToken(text);
        if (!string.IsNullOrEmpty(route)) return route;

        if (x.Latitude is double la && x.Longitude is double lo) return $"({la:F4}, {lo:F4})";
        return "Unknown Road";
    }

    private static string? TryExtractRouteToken(string text)
    {
        var tokens = new[] { "I-", "US-", "NC-", "SR ", "NC Hwy", "US Hwy" };
        foreach (var t in tokens)
        {
            var idx = text.IndexOf(t, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var end = idx;
                while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != ',' && text[end] != ';') end++;
                var cand = text.Substring(idx, end - idx).Trim();
                if (cand.Length >= t.Length + 1) return cand.ToUpperInvariant();
            }
        }
        return null;
    }

    private static string NormalizeStatus(string? status, string? impact)
    {
        var s = (status ?? impact ?? "").ToLowerInvariant();
        if (s.Contains("closed") || s.Contains("block")) return "CLOSED";
        if (s.Contains("lane") || s.Contains("shoulder") || s.Contains("partial") || s.Contains("alternating"))
            return "PARTIAL";
        return "OPEN";
    }

    private static string? BuildNote(NcdotIncidentDto x)
    {
        var pieces = new[] { x.Description, x.EventType, x.Impact, x.Direction }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var text = string.Join(" · ", pieces!);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

/* -------------------------
   DTOs (flexible with JsonElement)
   ------------------------- */

internal sealed class NcdotCountyDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class NcdotIncidentDto
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("headline")] public string? Headline { get; set; }
    [JsonPropertyName("roadName")] public string? RoadName { get; set; }
    [JsonPropertyName("primaryRoute")] public string? PrimaryRoute { get; set; }
    [JsonPropertyName("routeName")] public string? RouteName { get; set; }
    [JsonPropertyName("road")] public string? Road { get; set; }
    [JsonPropertyName("street")] public string? Street { get; set; }
    [JsonPropertyName("highway")] public string? Highway { get; set; }
    [JsonPropertyName("route")] public string? Route { get; set; }

    [JsonPropertyName("eventType")] public string? EventType { get; set; }
    [JsonPropertyName("impact")] public string? Impact { get; set; }
    [JsonPropertyName("direction")] public string? Direction { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }

    [JsonPropertyName("lastUpdated")] public DateTime? LastUpdated { get; set; }
    [JsonPropertyName("updatedOn")] public DateTime? UpdatedOn { get; set; }
    [JsonPropertyName("startTime")] public DateTime? StartTime { get; set; }

    [JsonPropertyName("latitude")] public double? Latitude { get; set; }
    [JsonPropertyName("longitude")] public double? Longitude { get; set; }

    [JsonPropertyName("location")] public JsonElement? Location { get; set; } // object/array/string
    [JsonPropertyName("point")] public JsonElement? Point { get; set; }    // object/array/string
}
