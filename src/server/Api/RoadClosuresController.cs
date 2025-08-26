// Api/RoadClosuresController.cs
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class RoadClosuresController : ControllerBase
{
    // One HttpClient for the app (simple + OK for dev)
    private static readonly HttpClient _http = new HttpClient
    {
        BaseAddress = new Uri("https://eapps.ncdot.gov/services/traffic-prod/v1/")
    };

    // Default to Cumberland County (26)
    private const int DefaultCountyId = 26;

    // GET /api/roadclosures  (Swagger will show this)
    [HttpGet]
    public async Task<ActionResult<List<RoadClosure>>> Get(
        [FromQuery] string? status,
        [FromQuery] int? countyId,
        [FromQuery] string? county,  // optional: name like "Wake"
        CancellationToken ct = default)
    {
        var id = countyId ?? await ResolveCountyIdAsync(county, ct) ?? DefaultCountyId;

        // Per-county endpoint you asked for:
        // e.g. /counties/26/incidents?verbose=true&recent=true
        using var resp = await _http.GetAsync($"counties/{id}/incidents?verbose=true&recent=true", ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var list = new List<RoadClosure>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Ok(list);

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            // Sometimes attributes are nested under { attributes: {...}, geometry: {...} }
            var attrs = el;
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("attributes", out var a))
                attrs = a;

            // ID (stable fallback if missing)
            int idVal = TryGetInt(attrs, "id", "incidentId", "OBJECTID") ?? 0;

            // Coordinates (also check geometry block)
            double? lat = TryGetDouble(attrs, "latitude", "lat", "LAT", "y");
            double? lng = TryGetDouble(attrs, "longitude", "lng", "lon", "long", "LON", "x");
            if ((!lat.HasValue || !lng.HasValue) && el.TryGetProperty("geometry", out var geom))
            {
                lat ??= TryGetDouble(geom, "y", "lat", "latitude", "LAT");
                lng ??= TryGetDouble(geom, "x", "lng", "lon", "long", "longitude", "LON");
            }

            // Naming
            var roadRaw = TryGetString(attrs, "road", "roadName", "ROUTE_NAME", "ROUTE", "streetName", "STREET", "STREET_NM", "roadway", "name");
            var primaryRt = TryGetString(attrs, "primaryRoute", "routeName", "route");
            var headline = TryGetString(attrs, "headline", "title");
            var description = TryGetString(attrs, "description", "desc", "details", "detail", "message", "COMMENTS", "comments", "shortDescription");
            var commonName = TryGetString(attrs, "commonName", "CROSS_STREET", "crossStreetCommonName");
            string roadName = BestRoadName(roadRaw, primaryRt, headline, description, commonName, lat, lng);

            // Status
            var condition = TryGetString(attrs, "condition", "STATE", "status", "impact");
            var lanesClosed = TryGetInt(attrs, "lanesClosed", "numLanesClosed");
            var lanesTotal = TryGetInt(attrs, "lanesTotal", "numLanes");
            var statusStr = MapStatus(condition, lanesClosed, lanesTotal, description);

            // Timestamps
            var updated = TryGetDate(attrs, "lastUpdate", "updatedAt", "last_updated", "UPDATED", "update_time", "lastUpdateTime")
                          ?? DateTime.UtcNow;

            // Note (human-friendly details)
            var type = TryGetString(attrs, "incidentType", "incidentTypeDesc", "eventType", "eventTypeDesc", "type", "category");
            var direction = TryGetString(attrs, "direction", "dir", "directionOfTravel", "DIRECTION");
            var location = TryGetString(attrs, "location", "LOC_DESC");
            var schedule = TryGetString(attrs, "constructionDateTime", "workSchedule");
            var detour = TryGetString(attrs, "detour", "detourDescription");
            var note = ComposeNote(type, direction, lanesClosed, description, location, schedule, detour);

            if (idVal == 0) idVal = MakeStableId(roadName, lat, lng);

            list.Add(new RoadClosure
            {
                Id = idVal,
                RoadName = roadName,
                Status = statusStr,   // "OPEN" | "PARTIAL" | "CLOSED"
                Note = note,
                UpdatedAt = updated,
                Lat = lat,
                Lng = lng
            });
        }

        // Optional status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            var f = status.Trim().ToUpperInvariant();
            if (f is "OPEN" or "PARTIAL" or "CLOSED")
                list = list.Where(r => string.Equals(r.Status, f, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(list.OrderByDescending(r => r.UpdatedAt).ToList());
    }

    // Alias so your frontend keeps calling /api/closures
    [HttpGet("/api/closures")]
    public Task<ActionResult<List<RoadClosure>>> GetAlias(
        [FromQuery] string? status,
        [FromQuery] int? countyId,
        [FromQuery] string? county,
        CancellationToken ct = default)
        => Get(status, countyId, county, ct);

    // ---------------- helpers (inline; no DTOs/extra classes) ----------------

    private static async Task<int?> ResolveCountyIdAsync(string? county, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(county)) return null;

        using var resp = await _http.GetAsync("counties", ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStreamAsync(ct));
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

        static string Norm(string s) => s.Replace(" County", "", StringComparison.OrdinalIgnoreCase).Trim();

        var want = Norm(county);
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var id = c.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var n) ? n : (int?)null;
            var name = c.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (id is null || string.IsNullOrWhiteSpace(name)) continue;

            var nm = Norm(name);
            if (string.Equals(nm, want, StringComparison.OrdinalIgnoreCase)) return id;

            if (nm.Contains(want, StringComparison.OrdinalIgnoreCase) || want.Contains(nm, StringComparison.OrdinalIgnoreCase))
                return id;
        }
        return null;
    }

    private static string BestRoadName(string? road, string? primaryRt, string? headline, string? description, string? common, double? lat, double? lng)
    {
        foreach (var s in new[] { road, primaryRt, headline, description, common })
            if (!string.IsNullOrWhiteSpace(s)) return s.Trim();

        var token = TryExtractRouteToken(headline ?? description ?? "");
        if (!string.IsNullOrEmpty(token)) return token;

        if (lat is double la && lng is double lo) return $"({la:F4}, {lo:F4})";
        return "Unknown Road";
    }

    private static string? TryExtractRouteToken(string text)
    {
        var tokens = new[] { "I-", "US-", "NC-", "SR ", "NC HWY", "US HWY" };
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

    private static string MapStatus(string? condition, int? lanesClosed, int? lanesTotal, string? reason)
    {
        var cond = (condition ?? "").ToUpperInvariant();

        if (lanesTotal is > 0 && lanesClosed is >= 0 && lanesClosed >= lanesTotal) return "CLOSED";
        if (cond.Contains("ROAD CLOSED")) return "CLOSED";
        if (cond.Contains("LANE CLOSED")) return "PARTIAL";
        if (cond.Contains("CLOSED")) return "PARTIAL";

        var rsn = (reason ?? "").ToUpperInvariant();
        if (rsn.Contains("LANE") && rsn.Contains("CLOSED")) return "PARTIAL";
        if (rsn.Contains("ROAD") && rsn.Contains("CLOSED")) return "CLOSED";

        if ((lanesClosed ?? 0) > 0) return "PARTIAL";
        return "OPEN";
    }

    private static string? ComposeNote(string? type, string? direction, int? lanesClosed, string? reason, string? location, string? schedule, string? detour)
    {
        static string NormDir(string? d)
        {
            if (string.IsNullOrWhiteSpace(d)) return "";
            var t = d.Trim().ToUpperInvariant();
            return t switch
            {
                "A" or "ALL" or "ALL DIRECTIONS" => "All directions",
                "N" or "NB" or "NORTH" or "NORTHBOUND" => "NB",
                "S" or "SB" or "SOUTH" or "SOUTHBOUND" => "SB",
                "E" or "EB" or "EAST" or "EASTBOUND" => "EB",
                "W" or "WB" or "WEST" or "WESTBOUND" => "WB",
                "O" or "OUTER" or "OUTER LOOP" => "Outer Loop",
                "I" or "INNER" or "INNER LOOP" => "Inner Loop",
                _ => t
            };
        }

        static string? LanesLabel(int? n) => (!n.HasValue || n.Value <= 0) ? null : (n == 1 ? "1 lane closed" : $"{n} lanes closed");

        var headerParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(type)) headerParts.Add(type!.Trim());
        var nd = NormDir(direction);
        if (!string.IsNullOrEmpty(nd)) headerParts.Add(nd);
        var lanesText = LanesLabel(lanesClosed);
        if (!string.IsNullOrEmpty(lanesText)) headerParts.Add(lanesText);

        var header = string.Join(" — ", headerParts.Where(p => !string.IsNullOrWhiteSpace(p)));

        var bodyParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(reason)) bodyParts.Add(reason!.Trim());
        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location!.Trim();
            if (string.IsNullOrWhiteSpace(reason) || !reason.Contains(loc, StringComparison.OrdinalIgnoreCase))
                bodyParts.Add(loc);
        }
        if (!string.IsNullOrWhiteSpace(schedule)) bodyParts.Add($"Work hours: {schedule!.Trim()}");
        if (!string.IsNullOrWhiteSpace(detour)) bodyParts.Add($"Detour: {detour!.Trim()}");

        var body = string.Join(" ", bodyParts);

        if (string.IsNullOrEmpty(header) && string.IsNullOrEmpty(body)) return null;
        if (string.IsNullOrEmpty(header)) return body;
        if (string.IsNullOrEmpty(body)) return header;
        return $"{header}. {body}";
    }

    private static int MakeStableId(string roadName, double? lat, double? lng)
    {
        // a tiny stable hash for when the feed doesn't give an id
        var s = $"{roadName}|{lat?.ToString("F6", CultureInfo.InvariantCulture)}|{lng?.ToString("F6", CultureInfo.InvariantCulture)}";
        unchecked
        {
            int h = 23;
            foreach (var ch in s) h = (h * 31) + ch;
            return Math.Abs(h);
        }
    }

    private static string? TryGetString(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.ToString(); // sometimes labels are numeric
            }
        }
        return null;
    }

    private static int? TryGetInt(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
            }
        }
        return null;
    }

    private static double? TryGetDouble(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
                if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds)) return ds;
            }
        }
        return null;
    }

    private static DateTime? TryGetDate(JsonElement obj, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (obj.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.String)
                {
                    var s = v.GetString();
                    if (!string.IsNullOrWhiteSpace(s) &&
                        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                    {
                        return dto.UtcDateTime;
                    }
                }
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms))
                {
                    var dto = (ms > 10_000_000_000)
                        ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                        : DateTimeOffset.FromUnixTimeSeconds(ms);
                    return dto.UtcDateTime;
                }
            }
        }
        return null;
    }
}
