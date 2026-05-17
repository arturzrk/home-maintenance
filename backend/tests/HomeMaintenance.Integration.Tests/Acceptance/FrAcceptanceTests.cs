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
/// Spec-cross-referenced acceptance tests. Each test name encodes the
/// FR-id from polaris-specs/001-property-job-step/spec.md so reviewers
/// can trace coverage at a glance.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class FrAcceptanceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public FrAcceptanceTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return c;
    }

    // FR-009: Create Property with Name (non-empty, trimmed, max 100 chars).
    [Fact]
    public async Task FR_009_CreateProperty_RequiresNameWithin1To100()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");

        (await alice.PostAsJsonAsync("/api/properties", new { name = "" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await alice.PostAsJsonAsync("/api/properties", new { name = new string('x', 101) }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var resp = await alice.PostAsJsonAsync("/api/properties", new { name = new string('x', 100) });
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var ok = await alice.PostAsJsonAsync("/api/properties", new { name = "  Main House  " });
        var dto = await ok.Content.ReadFromJsonAsync<PropertyDto>();
        dto!.Name.ShouldBe("Main House"); // trimmed
    }

    // FR-014: CreateJob must reference a Property owned by the caller.
    [Fact]
    public async Task FR_014_CreateJob_RequiresPropertyOwnedByCaller()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var prop = (await (await alice.PostAsJsonAsync("/api/properties",
            new { name = "Alice Place" })).Content.ReadFromJsonAsync<PropertyDto>())!;

        var resp = await bob.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = prop.Id,
            name = "Hijack",
            dueDate = (string?)null,
            steps = Array.Empty<object>(),
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().ShouldBe("not_found");
    }

    // FR-018a: CompleteJob rejects when any Step is incomplete.
    [Fact]
    public async Task FR_018_CompleteJob_RejectsIfAnyStepIncomplete()
    {
        var (client, job) = await CreateJobWithSteps("a", "b");
        var resp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().ShouldBe("steps_incomplete");
    }

    // FR-018b: CompleteJob rejects a Job with zero Steps.
    [Fact]
    public async Task FR_018_CompleteJob_RejectsIfZeroSteps()
    {
        var (client, job) = await CreateJobWithSteps();
        var resp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().ShouldBe("job_has_no_steps");
    }

    // FR-022: RemoveStep renumbers Orders contiguously from 0.
    [Fact]
    public async Task FR_022_RemoveStep_RenumbersOrdersContiguously()
    {
        var (client, job) = await CreateJobWithSteps("a", "b", "c");
        var resp = await client.DeleteAsync($"/api/jobs/{job.Id}/steps/{job.Steps[1].Id}");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<JobDetailDto>();
        updated!.Steps.Select(s => s.Order).ShouldBe(new[] { 0, 1 });
    }

    // FR-023: ReorderSteps rejects a partial list.
    [Fact]
    public async Task FR_023_ReorderSteps_RejectsPartialList()
    {
        var (client, job) = await CreateJobWithSteps("a", "b", "c");
        var partial = job.Steps.Take(2).Select(s => s.Id).ToArray();
        var resp = await client.PutAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/order",
            new { orderedStepIds = partial });
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // FR-028: ids are GUID-N shape (32 hex chars).
    [Fact]
    public async Task FR_028_AllIdsAreGuidLike()
    {
        var (client, job) = await CreateJobWithSteps("a", "b");
        AssertGuidN(job.Id);
        AssertGuidN(job.PropertyId);
        foreach (var s in job.Steps) AssertGuidN(s.Id);

        static void AssertGuidN(string id)
        {
            id.Length.ShouldBe(32);
            id.ShouldAllBe(c => Uri.IsHexDigit(c));
        }
    }

    // FR-029: every server-issued timestamp is UTC.
    [Fact]
    public async Task FR_029_AllTimestampsAreUtc()
    {
        var (client, job) = await CreateJobWithSteps("a");
        var stepId = job.Steps[0].Id;
        var tickResp = await client.PostAsync($"/api/jobs/{job.Id}/steps/{stepId}/tick", null);
        var ticked = (await tickResp.Content.ReadFromJsonAsync<JobDetailDto>())!;
        var completedAt = ticked.Steps[0].CompletedAt;
        completedAt.ShouldNotBeNull();
        completedAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);

        var completeResp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        var completed = (await completeResp.Content.ReadFromJsonAsync<JobDetailDto>())!;
        completed.CompletedAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    // FR-019: a Completed Job is immutable. (Spec-named pointer to the
    // sealing matrix - one canonical case here for trace; full matrix in
    // SealingMatrixTests.)
    [Fact]
    public async Task FR_019_CompletedJob_RejectsRename()
    {
        var (client, job) = await CreateJobWithSteps("a");
        await client.PostAsync($"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}/tick", null);
        (await client.PostAsync($"/api/jobs/{job.Id}/complete", null)).EnsureSuccessStatusCode();

        var resp = await client.PatchAsJsonAsync($"/api/jobs/{job.Id}", new { name = "Renamed" });
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().ShouldBe("job_completed");
    }

    private async Task<(HttpClient client, JobDetailDto job)> CreateJobWithSteps(
        params string[] stepDescriptions)
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = (await (await client.PostAsJsonAsync("/api/properties",
            new { name = "House" })).Content.ReadFromJsonAsync<PropertyDto>())!;
        var jobResp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = prop.Id,
            name = "Job",
            dueDate = (string?)null,
            steps = stepDescriptions.Select(d => new { description = d }).ToArray(),
        });
        jobResp.EnsureSuccessStatusCode();
        var job = (await jobResp.Content.ReadFromJsonAsync<JobDetailDto>())!;
        return (client, job);
    }
}
