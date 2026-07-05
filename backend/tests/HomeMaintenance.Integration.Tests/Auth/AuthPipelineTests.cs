using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Auth;

[Collection(nameof(ApiFactory))]
public sealed class AuthPipelineTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthPipelineTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AnonymousRequest_To_AuthPing_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/_authping");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_AllowsAnonymous_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Root_AllowsAnonymous_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DevStub_AcceptsBearer_dev_alice_AndResolvesOwnerId()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "dev-alice");

        var response = await client.GetAsync("/api/_authping");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthPingBody>(TestJson.Options);
        body.ShouldNotBeNull();
        body!.OwnerId.ShouldBe("alice");
    }

    [Fact]
    public async Task DevStub_EmptySub_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "dev-");

        var response = await client.GetAsync("/api/_authping");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // The production-stub assertion is verified at the unit-test level
    // (see HomeMaintenance.Unit.Tests.Infrastructure.Auth.AuthenticationExtensionsTests).
    // The integration-test path is impractical because WebApplicationFactory
    // locks the environment when WebApplication.CreateBuilder runs (inside
    // Program.cs), before any test-side `UseEnvironment` hook fires.

    private sealed record AuthPingBody(string OwnerId);
}
