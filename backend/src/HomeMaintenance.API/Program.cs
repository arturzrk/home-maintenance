using HomeMaintenance.Application;
using HomeMaintenance.Infrastructure;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ── Layer registrations ────────────────────────────────────────────────────
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// ── Health checks ──────────────────────────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString,
        name: "mongodb",
        tags: ["db", "ready"]);

// ── CORS (allow Next.js dev server) ───────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
                ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ── Swagger / OpenAPI ──────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseHttpsRedirection();

// ── Endpoints ─────────────────────────────────────────────────────────────

// Health check — used by the frontend to confirm the backend is reachable.
app.MapHealthChecks("/health");

// Root — quick smoke-test that the API is running.
app.MapGet("/", () => Results.Ok(new
{
    Service = "HomeMaintenance API",
    Version = "0.1.0",
    Status = "Running"
}))
.WithName("Root")
.WithTags("System");

app.Run();

// Needed for integration test WebApplicationFactory
public partial class Program { }
