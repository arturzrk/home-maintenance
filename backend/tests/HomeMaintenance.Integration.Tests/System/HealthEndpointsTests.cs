using System.Net;
using System.Text.Json;
using HomeMaintenance.API.Middleware;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.System;

/// <summary>
/// The standardized health endpoint quartet: /health, /liveness,
/// /readiness, /detailed. The shared fixture runs a healthy MongoDB
/// container, so unhealthy readiness is exercised by appending an
/// extra failing "ready"-tagged check instead of touching Mongo.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class HealthEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task Health_Returns200_WithHealthyStatusAndVersion()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("status").GetString().ShouldBe("healthy");
        json.GetProperty("version").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Liveness_Returns200()
    {
        var response = await _client.GetAsync("/liveness");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("status").GetString().ShouldBe("healthy");
    }

    [Fact]
    public async Task Readiness_Returns200_WhenDependenciesAvailable()
    {
        var response = await _client.GetAsync("/readiness");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("status").GetString().ShouldBe("healthy");
    }

    [Fact]
    public async Task Readiness_Returns503_WhenAReadyDependencyIsUnhealthy()
    {
        using var unreadyClient = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddHealthChecks().AddCheck(
                    "forced-unready",
                    () => HealthCheckResult.Unhealthy("forced for test"),
                    tags: ["ready"])))
            .CreateClient();

        var response = await unreadyClient.GetAsync("/readiness");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var json = await ReadJsonAsync(response);
        json.GetProperty("status").GetString().ShouldBe("unhealthy");

        // Liveness must stay 200 through a dependency outage.
        var liveness = await unreadyClient.GetAsync("/liveness");
        liveness.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Detailed_IncludesAllComponents_WithLatency()
    {
        var response = await _client.GetAsync("/detailed");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("status").GetString().ShouldBe("healthy");
        json.GetProperty("uptimeSeconds").GetInt64().ShouldBeGreaterThanOrEqualTo(0);

        var mongodb = json.GetProperty("checks").GetProperty("mongodb");
        mongodb.GetProperty("status").GetString().ShouldBe("healthy");
        mongodb.GetProperty("latencyMs").GetInt64().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Detailed_EchoesSuppliedCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/detailed");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "health-e2e-correlation");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)
            .ShouldHaveSingleItem()
            .ShouldBe("health-e2e-correlation");
        var json = await ReadJsonAsync(response);
        json.GetProperty("correlationId").GetString().ShouldBe("health-e2e-correlation");
    }

    [Fact]
    public async Task VersionIsConsistent_AcrossRootAndHealth()
    {
        var rootJson = await ReadJsonAsync(await _client.GetAsync("/"));
        var healthJson = await ReadJsonAsync(await _client.GetAsync("/health"));
        var detailedJson = await ReadJsonAsync(await _client.GetAsync("/detailed"));

        var version = rootJson.GetProperty("version").GetString();
        version.ShouldNotBeNullOrWhiteSpace();
        version.ShouldNotBe("unknown");
        healthJson.GetProperty("version").GetString().ShouldBe(version);
        detailedJson.GetProperty("version").GetString().ShouldBe(version);
    }
}
