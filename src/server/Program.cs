using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// MVC controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core (SQLite)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only force HTTPS outside dev
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

// ---- Dev seed: create DB & sample data ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (!db.Zones.Any())
    {
        db.Zones.AddRange(
            new Zone { Name = "North", ColorHex = "#2b6cb0" },
            new Zone { Name = "South", ColorHex = "#38a169" }
        );
    }

    if (!db.RoadClosures.Any())
    {
        db.RoadClosures.AddRange(
            new RoadClosure { RoadName = "Pamalee Dr", Status = "PARTIAL", Note = "Standing water", Lat = 35.0930, Lng = -78.9220 },
            new RoadClosure { RoadName = "Cedar Creek Rd", Status = "CLOSED", Note = "Debris on roadway", Lat = 35.0187, Lng = -78.7994 }
        );
    }
    await db.SaveChangesAsync();
}
// -------------------------------------------

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
public partial class Program { }
