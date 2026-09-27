using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Infrastructure.AuditLog;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoException = MongoDB.Driver.MongoException;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.AuditLog;

/// <summary>
/// Integration tests for the MongoDB audit log.
/// All tests run against the <c>Mongo</c> provider, which is set globally
/// in <see cref="ApiFactory._overrides"/> so the production-mode sink is
/// exercised in every integration test run.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class AuditLogIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuditLogIntegrationTests(ApiFactory factory) => _factory = factory;

    private HttpClient AuthClient(string sub)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return client;
    }

    private IMongoCollection<AuditLogDocument> AuditCollection() =>
        _factory.Services
            .GetRequiredService<IMongoDatabase>()
            .GetCollection<AuditLogDocument>(MongoAuditLog.CollectionName);

    // ── Happy-path: event persisted and queryable ────────────────────────

    [Fact]
    public async Task CreateProperty_PersistsAuditEvent_QueryableByActor()
    {
        var sub = $"audit-actor-{Guid.NewGuid():N}";
        var client = AuthClient(sub);

        var response = await client.PostAsJsonAsync(
            "/api/properties", new { name = "Audit Actor Test" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var events = await AuditCollection()
            .Find(Builders<AuditLogDocument>.Filter.Eq(d => d.Actor, sub))
            .ToListAsync();

        events.ShouldNotBeEmpty();
        events.First().EventType.ShouldBe(AuditEventTypes.PropertyCreated);
    }

    [Fact]
    public async Task CreateProperty_PersistsAuditEvent_QueryableByTarget()
    {
        var sub = $"audit-target-{Guid.NewGuid():N}";
        var client = AuthClient(sub);

        var response = await client.PostAsJsonAsync(
            "/api/properties", new { name = "Audit Target Test" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options);
        var expectedTarget = $"property:{dto!.Id}";

        var events = await AuditCollection()
            .Find(Builders<AuditLogDocument>.Filter.Eq(d => d.Target, expectedTarget))
            .ToListAsync();

        events.ShouldNotBeEmpty();
        events.First().Actor.ShouldBe(sub);
    }

    [Fact]
    public async Task AuditDocument_ContainsFullEventShape()
    {
        var sub = $"audit-shape-{Guid.NewGuid():N}";
        var client = AuthClient(sub);

        await client.PostAsJsonAsync("/api/properties", new { name = "Shape Test" });

        var doc = await AuditCollection().Find(
            Builders<AuditLogDocument>.Filter.And(
                Builders<AuditLogDocument>.Filter.Eq(d => d.Actor, sub),
                Builders<AuditLogDocument>.Filter.Eq(d => d.EventType, AuditEventTypes.PropertyCreated)))
            .FirstOrDefaultAsync();

        doc.ShouldNotBeNull();
        doc!.CorrelationId.ShouldNotBeNullOrEmpty();
        doc.Timestamp.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-2));
        doc.Payload.ShouldNotBeNull();
        doc.Payload!.ContainsKey("name").ShouldBeTrue();
    }

    // ── Failure semantics: audit failure leaves mutation intact ──────────

    [Fact]
    public async Task AuditInsertFailure_RequestSucceeds_MutationPersisted()
    {
        // Override IAuditLog with a decorator wrapping a stub that always
        // throws, simulating a failed Mongo insert. The AppInsightsAuditMirror
        // catches the failure and does NOT rethrow, so the domain request
        // still returns 201.
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Remove the existing (non-keyed) IAuditLog singleton registration.
                // The Mongo path also registers a keyed "mongo-inner" singleton,
                // so filter to non-keyed descriptors to avoid multiple matches.
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAuditLog)
                         && d.Lifetime == ServiceLifetime.Singleton
                         && !d.IsKeyedService);
                if (descriptor is not null)
                    services.Remove(descriptor);

                // Replace with a mirror whose inner always throws.
                services.AddSingleton<IAuditLog>(sp =>
                    new AppInsightsAuditMirror(
                        new AlwaysThrowingAuditLog(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AppInsightsAuditMirror>>()));
            }));

        var sub = $"audit-fail-{Guid.NewGuid():N}";
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"dev-{sub}");

        // Domain mutation must succeed even when the audit insert throws.
        var response = await client.PostAsJsonAsync(
            "/api/properties", new { name = "Fail Test Property" });
        response.StatusCode.ShouldBe(
            HttpStatusCode.Created,
            "domain mutation must succeed even when audit insert fails");

        // Mutation is in the database.
        var dto = await response.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options);
        dto.ShouldNotBeNull();
        dto!.Name.ShouldBe("Fail Test Property");

        // No audit document was written (insert always threw).
        var auditDocs = await factory.Services
            .GetRequiredService<IMongoDatabase>()
            .GetCollection<AuditLogDocument>(MongoAuditLog.CollectionName)
            .Find(Builders<AuditLogDocument>.Filter.Eq(d => d.Actor, sub))
            .ToListAsync();
        auditDocs.ShouldBeEmpty("no audit document should persist when the insert throws");
    }

    /// <summary>Stub that always fails to simulate a broken audit insert.</summary>
    private sealed class AlwaysThrowingAuditLog : IAuditLog
    {
        public Task RecordAsync(AuditEvent evt, CancellationToken ct = default) =>
            Task.FromException(new MongoException("Simulated audit-insert failure"));
    }
}
