using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Middleware;

[Collection(nameof(ApiFactory))]
public sealed class CorrelationAndProblemDetailsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CorrelationAndProblemDetailsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Response_AlwaysCarriesXCorrelationIdHeader()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).ShouldBeTrue();
        var id = values!.Single();
        id.ShouldNotBeNullOrWhiteSpace();
        id.Length.ShouldBeGreaterThan(8);
    }

    [Fact]
    public async Task Request_WithInboundCorrelationId_EchoesIt()
    {
        var client = _factory.CreateClient();
        var inboundId = "test-correlation-12345";

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Correlation-Id", inboundId);
        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("X-Correlation-Id", out var values).ShouldBeTrue();
        values!.Single().ShouldBe(inboundId);
    }

    [Fact]
    public async Task Anonymous_To_Authenticated_Endpoint_Returns401_WithProblemDetailsBody()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/_authping");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("code").GetString().ShouldBe("unauthorized");
        body.GetProperty("status").GetInt32().ShouldBe(401);

        var correlationInBody = body.GetProperty("correlationId").GetString();
        response.Headers.TryGetValues("X-Correlation-Id", out var headerValues).ShouldBeTrue();
        correlationInBody.ShouldBe(headerValues!.Single());
    }
}
