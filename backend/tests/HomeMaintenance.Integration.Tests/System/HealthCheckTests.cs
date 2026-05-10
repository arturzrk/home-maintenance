using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.System;

/// <summary>
/// Smoke-tests that verify the API is reachable and MongoDB is healthy.
/// These are the first integration tests in the minimal working set.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class HealthCheckTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Health_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Root_ReturnsRunningStatus()
    {
        var response = await _client.GetAsync("/");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Running");
    }
}
