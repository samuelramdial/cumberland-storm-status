using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------- EF Core: choose provider by environment ----------
if (builder.Environment.IsEnvironment("Testing"))
{
    // Used by integration tests to avoid provider conflicts
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseInMemoryDatabase("tests-" + Guid.NewGuid().ToString("N")));
}
else
{
    var cs = builder.Configuration.GetConnectionString("Default") ?? "Data Source=stormstatus.db";
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(cs));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Dev-only convenience (DON'T run in tests)
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.Run();

// Needed for WebApplicationFactory in tests
public partial class Program { }
