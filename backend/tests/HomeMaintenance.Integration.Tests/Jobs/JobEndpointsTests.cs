using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Jobs;

[Collection(nameof(ApiFactory))]
public sealed class JobEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public JobEndpointsTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return c;
    }

    private async Task<PropertyDto> CreateProperty(HttpClient client, string name = "Main House")
    {
        var resp = await client.PostAsJsonAsync("/api/properties", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PropertyDto>())!;
    }

    private async Task<JobDetailDto> CreateJob(HttpClient client, string propertyId, params string[] stepDescriptions)
    {
        var body = new
        {
            propertyId,
            name = "Service boiler",
            dueDate = (DateOnly?)null,
            steps = stepDescriptions.Select(d => new { description = d }).ToArray(),
        };
        var resp = await client.PostAsJsonAsync("/api/jobs", body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JobDetailDto>())!;
    }

    // ---- Anonymous ----

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/jobs", new { propertyId = "x", name = "y", steps = Array.Empty<object>() });
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Create ----

    [Fact]
    public async Task Post_Job_AgainstOwnedProperty_Returns201_WithSteps()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);

        var body = new
        {
            propertyId = property.Id,
            name = "Service boiler",
            dueDate = "2026-06-01",
            steps = new[] { new { description = "Shut off gas" }, new { description = "Replace filter" } },
        };
        var resp = await alice.PostAsJsonAsync("/api/jobs", body);

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        resp.Headers.Location.ShouldNotBeNull();
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>();
        detail!.Status.ShouldBe(JobStatus.Active);
        detail.Steps.Count.ShouldBe(2);
        detail.Steps.Select(s => s.Order).ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public async Task Post_Job_AgainstOtherOwnersProperty_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);

        var body = new
        {
            propertyId = prop.Id,
            name = "Hijacked",
            dueDate = (DateOnly?)null,
            steps = Array.Empty<object>(),
        };
        var resp = await bob.PostAsJsonAsync("/api/jobs", body);

        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_Job_EmptyName_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var body = new { propertyId = prop.Id, name = "", steps = Array.Empty<object>() };
        (await alice.PostAsJsonAsync("/api/jobs", body)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Job_StepDescriptionTooLong_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var body = new
        {
            propertyId = prop.Id,
            name = "Job",
            steps = new[] { new { description = new string('x', 501) } },
        };
        (await alice.PostAsJsonAsync("/api/jobs", body)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Read ----

    [Fact]
    public async Task Get_Job_OwnedByCaller_ReturnsDetailWithSteps()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b");

        var resp = await alice.GetAsync($"/api/jobs/{job.Id}");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>();
        detail!.Steps.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Get_Job_OwnedByOther_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        (await bob.GetAsync($"/api/jobs/{job.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_Jobs_FilteredByProperty_ReturnsOnlyMatching()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var alice = ClientAs(sub);
        var prop1 = await CreateProperty(alice, "House 1");
        var prop2 = await CreateProperty(alice, "House 2");
        await CreateJob(alice, prop1.Id, "x");
        await CreateJob(alice, prop1.Id, "y");
        await CreateJob(alice, prop2.Id, "z");

        var list = await alice.GetFromJsonAsync<JobListDto>($"/api/jobs?propertyId={prop1.Id}");

        list!.Jobs.Count.ShouldBe(2);
        list.Jobs.All(j => j.PropertyId == prop1.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task List_Jobs_ReturnsOnlyCallersJobs()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var aliceProp = await CreateProperty(alice);
        var bobProp = await CreateProperty(bob);
        await CreateJob(alice, aliceProp.Id, "alice-job");
        await CreateJob(bob, bobProp.Id, "bob-job");

        var aliceJobs = await alice.GetFromJsonAsync<JobListDto>("/api/jobs");
        var bobJobs = await bob.GetFromJsonAsync<JobListDto>("/api/jobs");

        aliceJobs!.Jobs.Count.ShouldBe(1);
        bobJobs!.Jobs.Count.ShouldBe(1);
        aliceJobs.Jobs[0].PropertyId.ShouldBe(aliceProp.Id);
        bobJobs.Jobs[0].PropertyId.ShouldBe(bobProp.Id);
    }

    // ---- Tick / untick ----

    [Fact]
    public async Task Tick_Step_SetsCompletedAt()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b");
        var stepId = job.Steps[0].Id;

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/steps/{stepId}/tick", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>();
        detail!.Steps[0].IsCompleted.ShouldBeTrue();
        detail.Steps[0].CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Untick_Step_ClearsCompletedAt()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");
        var stepId = job.Steps[0].Id;
        await alice.PostAsync($"/api/jobs/{job.Id}/steps/{stepId}/tick", content: null);

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/steps/{stepId}/untick", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>();
        detail!.Steps[0].IsCompleted.ShouldBeFalse();
        detail.Steps[0].CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Tick_UnknownStep_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/steps/missing/tick", content: null);
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Complete ----

    [Fact]
    public async Task Complete_AllStepsTicked_TransitionsToCompleted()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b");
        foreach (var step in job.Steps)
            await alice.PostAsync($"/api/jobs/{job.Id}/steps/{step.Id}/tick", content: null);

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/complete", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>();
        detail!.Status.ShouldBe(JobStatus.Completed);
        detail.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Complete_WithIncompleteStep_Returns400_steps_incomplete()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b");

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/complete", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("steps_incomplete");
    }

    [Fact]
    public async Task Complete_WithZeroSteps_Returns400_job_has_no_steps()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id); // no steps

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/complete", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("job_has_no_steps");
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_Returns400_job_already_completed()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");
        await alice.PostAsync($"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}/tick", content: null);
        (await alice.PostAsync($"/api/jobs/{job.Id}/complete", content: null)).EnsureSuccessStatusCode();

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/complete", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("job_already_completed");
    }

    [Fact]
    public async Task Tick_OnCompletedJob_Returns400_job_completed()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");
        await alice.PostAsync($"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}/tick", content: null);
        await alice.PostAsync($"/api/jobs/{job.Id}/complete", content: null);

        var resp = await alice.PostAsync($"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}/tick", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("job_completed");
    }
}
