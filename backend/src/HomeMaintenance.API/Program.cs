using Azure.Monitor.OpenTelemetry.AspNetCore;
using HomeMaintenance.API.Endpoints;
using HomeMaintenance.API.Middleware;
using HomeMaintenance.Application;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Infrastructure;
using HomeMaintenance.Infrastructure.Auth;
using MongoDB.Driver;
using OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Layer registrations
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddAppAuthentication(builder.Configuration, builder.Environment);

// Azure Monitor OpenTelemetry distro. Auto-instruments ASP.NET Core
// (every request becomes a span), HttpClient, and exceptions. Reads the
// connection string from the APPLICATIONINSIGHTS_CONNECTION_STRING env
// var; silently no-ops when the var is unset, so local dev is unaffected
// unless a developer opts in.
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

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

// Correlation id stamps every request first so it's available to every
// downstream middleware and handler. The unauthorized translator wraps
// auth/authz so its tail logic runs after Authorization short-circuits
// a 401, allowing us to upgrade the empty response to RFC 7807 JSON.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<UnauthorizedProblemDetailsMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints

// Health check - public, used by the frontend to confirm the backend is reachable.
app.MapHealthChecks("/health").AllowAnonymous();

// Root - public smoke-test that the API is running.
app.MapGet("/", () => Results.Ok(new
{
    Service = "HomeMaintenance API",
    Version = "0.1.1",
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

// Property aggregate (WP03).
app.MapPropertyEndpoints();

// Job aggregate (WP05).
app.MapJobEndpoints();

app.Run();

// Needed for integration test WebApplicationFactory
public partial class Program { }
