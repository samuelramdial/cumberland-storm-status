using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Single-project setup (server.csproj) with Api/, Domain/, Infrastructure/ inside.
// No namespaces required if your codebase doesn't use them.

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON: enums as "OPEN"/"PARTIAL"/"CLOSED"
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// NCDOT client service (used by RoadClosuresController)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<NcTrafficService>(); // ctor sets BaseAddress in the service

// If you ever skip the Vite proxy and call API directly from http://localhost:5173,
// uncomment this CORS block and the app.UseCors("DevCors") line below.
/*
builder.Services.AddCors(o =>
{
    o.AddPolicy("DevCors", p => p
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});
*/

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// If you're running pure HTTP on :5052, keep HTTPS redirection off in dev.
// app.UseHttpsRedirection();

// Uncomment if you enabled the DevCors policy above.
// app.UseCors("DevCors");

app.MapControllers();

// Optional: redirect root to Swagger when browsing to http://localhost:5052
// app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

