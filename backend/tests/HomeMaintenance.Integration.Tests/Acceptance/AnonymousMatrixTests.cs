using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Acceptance;

/// <summary>
/// SC-006: every non-/health endpoint MUST reject anonymous callers with
/// HTTP 401 and a problem+json body whose <c>code</c> is "unauthorized".
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class AnonymousMatrixTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AnonymousMatrixTests(ApiFactory factory) => _factory = factory;

    public static IEnumerable<object[]> ProtectedEndpoints() => new[]
    {
        new object[] { HttpMethod.Get, "/api/properties", null! },
        new object[] { HttpMethod.Post, "/api/properties", new { name = "x" } },
        new object[] { HttpMethod.Get, "/api/properties/some-id", null! },
        new object[] { HttpMethod.Patch, "/api/properties/some-id", new { name = "x" } },

        new object[] { HttpMethod.Get, "/api/jobs", null! },
        new object[] { HttpMethod.Post, "/api/jobs",
            new { propertyId = "p", name = "j", dueDate = (string?)null, steps = Array.Empty<object>() } },
        new object[] { HttpMethod.Get, "/api/jobs/some-id", null! },
        new object[] { HttpMethod.Patch, "/api/jobs/some-id", new { name = "x" } },
        new object[] { HttpMethod.Post, "/api/jobs/some-id/complete", null! },

        new object[] { HttpMethod.Post, "/api/jobs/some-id/steps", new { description = "x" } },
        new object[] { HttpMethod.Delete, "/api/jobs/some-id/steps/step-id", null! },
        new object[] { HttpMethod.Patch, "/api/jobs/some-id/steps/step-id", new { description = "x" } },
        new object[] { HttpMethod.Put, "/api/jobs/some-id/steps/order", new { orderedStepIds = new[] { "x" } } },
        new object[] { HttpMethod.Post, "/api/jobs/some-id/steps/step-id/tick", null! },
        new object[] { HttpMethod.Post, "/api/jobs/some-id/steps/step-id/untick", null! },

        // SC-106: job-definition endpoints (Slice 2)
        new object[] { HttpMethod.Post, "/api/job-definitions",
            new { propertyId = "p", name = "n",
                  schedule = new { unit = "Month", multiplier = 1, startDate = "2026-01-01" },
                  stepTemplates = Array.Empty<object>() } },
        new object[] { HttpMethod.Get, "/api/job-definitions", null! },
        new object[] { HttpMethod.Get, "/api/job-definitions/some-id", null! },
        new object[] { HttpMethod.Patch, "/api/job-definitions/some-id", new { name = "x" } },
        new object[] { HttpMethod.Post, "/api/job-definitions/some-id/generate-next", null! },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task NoAuthHeader_Returns401_ProblemDetails(
        HttpMethod method, string path, object? body)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);

        var resp = await client.SendAsync(request);

        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        resp.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var bodyJson = await resp.Content.ReadFromJsonAsync<JsonElement>();
        bodyJson.GetProperty("code").GetString().ShouldBe("unauthorized");
        bodyJson.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task GarbageBearerToken_Returns401_ProblemDetails(
        HttpMethod method, string path, object? body)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-real-token");
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);

        var resp = await client.SendAsync(request);

        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var bodyJson = await resp.Content.ReadFromJsonAsync<JsonElement>();
        bodyJson.GetProperty("code").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task Health_AllowsAnonymous_Returns200()
    {
        var resp = await _factory.CreateClient().GetAsync("/health");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Root_AllowsAnonymous_Returns200()
    {
        var resp = await _factory.CreateClient().GetAsync("/");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
