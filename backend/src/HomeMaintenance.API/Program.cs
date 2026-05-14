using HomeMaintenance.Application;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Infrastructure;
using HomeMaintenance.Infrastructure.Auth;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Layer registrations
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddAppAuthentication(builder.Configuration, builder.Environment);

// Health checks
builder.Services
    .AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<IMongoDatabase>(),
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

app.UseAuthentication();
app.UseAuthorization();

// Endpoints

// Health check - public, used by the frontend to confirm the backend is reachable.
app.MapHealthChecks("/health").AllowAnonymous();

// Root - public smoke-test that the API is running.
app.MapGet("/", () => Results.Ok(new
{
    Service = "HomeMaintenance API",
    Version = "0.1.0",
    Status = "Running"
}))
.WithName("Root")
.WithTags("System")
.AllowAnonymous();

// Dev-only authenticated echo endpoint - exposes the resolved OwnerId.
// Used by integration tests to verify the auth pipeline. Not present in
// non-Development environments.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/_authping",
        (IIdentityProvider identity) => Results.Ok(new { ownerId = identity.CurrentOwner.Value }))
        .RequireAuthorization()
        .WithName("AuthPing")
        .WithTags("System");
}

app.Run();

// Needed for integration test WebApplicationFactory
public partial class Program { }
