using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Acceptance;

/// <summary>
/// SC-005: zero cross-owner data leakage. For every endpoint that takes
/// a resource id, signing in as user B and asking for user A's resource
/// MUST return 404 (not 403, no leak) with a problem+json body whose
/// <c>code</c> is "not_found" and a non-empty correlation id.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class CrossOwnerMatrixTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CrossOwnerMatrixTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return c;
    }

    private async Task<(PropertyDto property, JobDetailDto job)> SetupAlicesResources()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var propResp = await alice.PostAsJsonAsync("/api/properties", new { name = "Alice Place" });
        propResp.EnsureSuccessStatusCode();
        var property = (await propResp.Content.ReadFromJsonAsync<PropertyDto>())!;

        var jobResp = await alice.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Service boiler",
            dueDate = (string?)null,
            steps = new[] { new { description = "Shut off gas" } },
        });
        jobResp.EnsureSuccessStatusCode();
        var job = (await jobResp.Content.ReadFromJsonAsync<JobDetailDto>())!;
        return (property, job);
    }

    private static async Task AssertNotFoundWithProblemDetails(HttpResponseMessage resp)
    {
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        resp.Headers.TryGetValues("X-Correlation-Id", out var hdr).ShouldBeTrue();
        var headerCid = hdr!.Single();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("not_found");
        body.GetProperty("correlationId").GetString().ShouldBe(headerCid);
    }

    [Fact]
    public async Task Bob_Get_AlicesProperty_Returns404()
    {
        var (property, _) = await SetupAlicesResources();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .GetAsync($"/api/properties/{property.Id}");
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_Patch_AlicesProperty_Returns404()
    {
        var (property, _) = await SetupAlicesResources();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .PatchAsJsonAsync($"/api/properties/{property.Id}", new { name = "Hijacked" });
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_CreateJob_AgainstAlicesProperty_Returns404()
    {
        var (property, _) = await SetupAlicesResources();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}").PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Hijack attempt",
            dueDate = (string?)null,
            steps = Array.Empty<object>(),
        });
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_Get_AlicesJob_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}").GetAsync($"/api/jobs/{job.Id}");
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_Patch_AlicesJob_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .PatchAsJsonAsync($"/api/jobs/{job.Id}", new { name = "Hijack" });
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_Complete_AlicesJob_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .PostAsync($"/api/jobs/{job.Id}/complete", null);
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_AddStep_OnAlicesJob_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .PostAsJsonAsync($"/api/jobs/{job.Id}/steps", new { description = "Hijack" });
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_RemoveStep_OnAlicesJob_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var stepId = job.Steps[0].Id;
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .DeleteAsync($"/api/jobs/{job.Id}/steps/{stepId}");
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_EditStep_OnAlicesJob_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var stepId = job.Steps[0].Id;
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .PatchAsJsonAsync($"/api/jobs/{job.Id}/steps/{stepId}", new { description = "Hijack" });
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_Reorder_OnAlicesJob_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var ids = job.Steps.Select(s => s.Id).ToArray();
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}").PutAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/order",
            new { orderedStepIds = ids });
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_Tick_OnAlicesStep_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var stepId = job.Steps[0].Id;
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .PostAsync($"/api/jobs/{job.Id}/steps/{stepId}/tick", null);
        await AssertNotFoundWithProblemDetails(resp);
    }

    [Fact]
    public async Task Bob_Untick_OnAlicesStep_Returns404()
    {
        var (_, job) = await SetupAlicesResources();
        var stepId = job.Steps[0].Id;
        var resp = await ClientAs($"bob-{Guid.NewGuid():N}")
            .PostAsync($"/api/jobs/{job.Id}/steps/{stepId}/untick", null);
        await AssertNotFoundWithProblemDetails(resp);
    }
}
