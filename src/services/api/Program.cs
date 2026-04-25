using Npgsql;
using System.Net.Http.Json;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(80));

var connStr = builder.Configuration["WEATHER_DATABASE_CONNECTION_STRING"] ?? builder.Configuration["ConnectionStrings:weather"] ?? Environment.GetEnvironmentVariable("WEATHER_DATABASE_CONNECTION_STRING");
var cors = builder.Configuration["WEATHER_CORS_ORIGINS"] ?? Environment.GetEnvironmentVariable("WEATHER_CORS_ORIGINS") ?? "";

builder.Services.AddCors(p => p.AddDefaultPolicy(pol =>
    pol.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddHttpClient("open-meteo", c =>
{
    c.BaseAddress = new Uri("https://api.open-meteo.com/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherApp/1.0 (volnyja)");
});

var app = builder.Build();
app.UseCors();
if (!string.IsNullOrWhiteSpace(connStr))
{
    try
    {
        await using var ds = NpgsqlDataSource.Create(connStr);
        await using var cmd = ds.CreateCommand(@"
            CREATE TABLE IF NOT EXISTS weather_snapshots (
                id              BIGSERIAL PRIMARY KEY,
                saved_at_utc    TIMESTAMPTZ NOT NULL,
                label           TEXT NOT NULL,
                latitude        DOUBLE PRECISION NOT NULL,
                longitude       DOUBLE PRECISION NOT NULL,
                temperature_c   REAL,
                weather_code    INT,
                raw_json        JSONB NOT NULL
            );
        ");
        await cmd.ExecuteNonQueryAsync();
    }
    catch
    { /* postgresql not ready on first start */ }
}

app.MapGet("/health", () => Results.Ok("ok"));
app.MapGet("/api/weather/current", async (IHttpClientFactory f, double lat = 52.52, double lon = 13.41) =>
{
    var c = f.CreateClient("open-meteo");
    var u = $"/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,relative_humidity_2m,weather_code&timezone=auto";
    var doc = await c.GetFromJsonAsync<JsonElement>(u);
    if (doc.ValueKind == JsonValueKind.Undefined) return Results.Problem("open-meteo failed", statusCode: 502);
    return Results.Json(doc, contentType: "application/json");
});
app.MapGet("/api/weather/snapshots", async (HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(connStr)) return Results.Problem("db not configured", statusCode: 500);
    await using var ds = NpgsqlDataSource.Create(connStr);
    await using var con = await ds.OpenConnectionAsync();
    await using var r = con.CreateCommand();
    r.CommandText = "SELECT id, saved_at_utc, label, latitude, longitude, temperature_c, weather_code, raw_json::text AS raw_json FROM weather_snapshots ORDER BY saved_at_utc DESC LIMIT 200";
    await using var rd = await r.ExecuteReaderAsync();
    var list = new List<Dictionary<string, object?>>();
    while (await rd.ReadAsync())
    {
        list.Add(new Dictionary<string, object?>
        {
            ["id"] = rd.GetInt64(0),
            ["savedAtUtc"] = rd.GetDateTime(1),
            ["label"] = rd.GetString(2),
            ["latitude"] = rd.GetDouble(3),
            ["longitude"] = rd.GetDouble(4),
            ["temperatureC"] = rd.IsDBNull(5) ? null : (float)rd.GetDouble(5),
            ["weatherCode"] = rd.IsDBNull(6) ? null : rd.GetInt32(6),
            ["raw"] = JsonSerializer.Deserialize<JsonElement>(rd.GetString(7)),
        });
    }
    return Results.Json(list);
});
app.MapPost("/api/weather/snapshots", async (HttpContext ctx, IHttpClientFactory http) =>
{
    if (string.IsNullOrWhiteSpace(connStr)) return Results.Problem("db not configured", statusCode: 500);
    var b = await JsonSerializer.DeserializeAsync<SaveBody>(ctx.Request.Body);
    if (b is null) return Results.BadRequest("body");
    var lat = b.Lat ?? 52.52;
    var lon = b.Lon ?? 13.41;
    var c = http.CreateClient("open-meteo");
    var u = $"/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,relative_humidity_2m,weather_code&timezone=auto";
    var doc = await c.GetFromJsonAsync<JsonElement>(u);
    var at = b.Label ?? "snapshot";
    var cur = doc.GetProperty("current");
    var t = cur.GetProperty("temperature_2m").GetDouble();
    var w = cur.GetProperty("weather_code").GetInt32();
    var json = doc.GetRawText();
    await using var ds = NpgsqlDataSource.Create(connStr);
    await using var con = await ds.OpenConnectionAsync();
    await using var ins = con.CreateCommand();
    ins.CommandText = "INSERT INTO weather_snapshots (saved_at_utc, label, latitude, longitude, temperature_c, weather_code, raw_json) VALUES ($1, $2, $3, $4, $5, $6, $7::jsonb) RETURNING id";
    ins.Parameters.AddWithValue(DateTime.UtcNow);
    ins.Parameters.AddWithValue(at);
    ins.Parameters.AddWithValue(lat);
    ins.Parameters.AddWithValue(lon);
    ins.Parameters.AddWithValue(t);
    ins.Parameters.AddWithValue(w);
    ins.Parameters.AddWithValue(json);
    var id = (long)(await ins.ExecuteScalarAsync() ?? 0L);
    return Results.Json(new { id });
});

app.Run();
record class SaveBody(double? Lat, double? Lon, string? Label);
