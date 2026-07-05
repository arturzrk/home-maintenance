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
public sealed class StepMutationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StepMutationTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return c;
    }

    private async Task<PropertyDto> CreateProperty(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/properties", new { name = "Main House" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;
    }

    private async Task<JobDetailDto> CreateJob(HttpClient client, string propertyId, params string[] steps)
    {
        var resp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId,
            name = "Service boiler",
            dueDate = (string?)null,
            steps = steps.Select(d => new { description = d }).ToArray(),
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
    }

    private async Task<JobDetailDto> Complete(HttpClient client, JobDetailDto job)
    {
        foreach (var s in job.Steps)
            (await client.PostAsync($"/api/jobs/{job.Id}/steps/{s.Id}/tick", null)).EnsureSuccessStatusCode();
        var resp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
    }

    // ---- AddStep ----

    [Fact]
    public async Task AddStep_AppendsAtEnd()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b");

        var resp = await alice.PostAsJsonAsync($"/api/jobs/{job.Id}/steps", new { description = "c" });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        detail!.Steps.Count.ShouldBe(3);
        detail.Steps[2].Description.ShouldBe("c");
        detail.Steps[2].Order.ShouldBe(2);
    }

    [Fact]
    public async Task AddStep_Empty_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await alice.PostAsJsonAsync($"/api/jobs/{job.Id}/steps", new { description = "" });
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddStep_OnCompletedJob_Returns400_job_completed()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");
        await Complete(alice, job);

        var resp = await alice.PostAsJsonAsync($"/api/jobs/{job.Id}/steps", new { description = "c" });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("code").GetString().ShouldBe("job_completed");
    }

    // ---- RemoveStep ----

    [Fact]
    public async Task RemoveStep_Renumbers()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b", "c");

        var resp = await alice.DeleteAsync($"/api/jobs/{job.Id}/steps/{job.Steps[1].Id}");

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        detail!.Steps.Count.ShouldBe(2);
        detail.Steps.Select(s => s.Order).ShouldBe(new[] { 0, 1 });
        detail.Steps.Select(s => s.Description).ShouldBe(new[] { "a", "c" });
    }

    [Fact]
    public async Task RemoveStep_UnknownId_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await alice.DeleteAsync($"/api/jobs/{job.Id}/steps/missing");
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- EditStepDescription ----

    [Fact]
    public async Task EditStepDescription_UpdatesText()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");
        var stepId = job.Steps[0].Id;

        var resp = await alice.PatchAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/{stepId}",
            new { description = "Updated description" });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        detail!.Steps[0].Description.ShouldBe("Updated description");
    }

    [Fact]
    public async Task EditStepDescription_OverLimit_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await alice.PatchAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}",
            new { description = new string('x', 501) });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- ReorderSteps ----

    [Fact]
    public async Task ReorderSteps_FullList_Succeeds()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b", "c");
        var reversed = job.Steps.Reverse().Select(s => s.Id).ToArray();

        var resp = await alice.PutAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/order",
            new { orderedStepIds = reversed });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        detail!.Steps.Select(s => s.Id).ShouldBe(reversed);
        detail.Steps.Select(s => s.Order).ShouldBe(new[] { 0, 1, 2 });
    }

    [Fact]
    public async Task ReorderSteps_PartialList_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b", "c");

        var resp = await alice.PutAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/order",
            new { orderedStepIds = job.Steps.Take(2).Select(s => s.Id).ToArray() });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReorderSteps_UnknownId_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b");
        var ids = job.Steps.Select(s => s.Id).ToList();
        ids[1] = "nope";

        var resp = await alice.PutAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/order",
            new { orderedStepIds = ids });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- PATCH /api/jobs/{id} ----

    [Fact]
    public async Task PatchJob_Rename_Updates()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await alice.PatchAsJsonAsync($"/api/jobs/{job.Id}", new { name = "Renamed" });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        detail!.Name.ShouldBe("Renamed");
    }

    [Fact]
    public async Task PatchJob_SetDueDate_Updates()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await alice.PatchAsJsonAsync(
            $"/api/jobs/{job.Id}",
            new { dueDate = "2026-12-31" });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        detail!.DueDate.ShouldBe(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task PatchJob_ClearDueDate_Updates()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");
        // First set a date
        await alice.PatchAsJsonAsync($"/api/jobs/{job.Id}", new { dueDate = "2026-12-31" });
        // Then clear by sending explicit null
        var json = "{\"dueDate\":null}";
        var resp = await alice.PatchAsync($"/api/jobs/{job.Id}",
            new StringContent(json, global::System.Text.Encoding.UTF8, "application/json"));

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        detail!.DueDate.ShouldBeNull();
    }

    [Fact]
    public async Task PatchJob_EmptyBody_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await alice.PatchAsJsonAsync($"/api/jobs/{job.Id}", new { });
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchJob_OnCompletedJob_Returns400_job_completed()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");
        await Complete(alice, job);

        var resp = await alice.PatchAsJsonAsync($"/api/jobs/{job.Id}", new { name = "Renamed" });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("code").GetString().ShouldBe("job_completed");
    }

    // ---- Sealing matrix: every step mutation on a Completed Job ----

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("edit")]
    [InlineData("reorder")]
    public async Task StepMutation_OnCompletedJob_Returns400_job_completed(string op)
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a", "b");
        var completed = await Complete(alice, job);
        var stepId = completed.Steps[0].Id;

        HttpResponseMessage resp = op switch
        {
            "add" => await alice.PostAsJsonAsync($"/api/jobs/{job.Id}/steps", new { description = "c" }),
            "remove" => await alice.DeleteAsync($"/api/jobs/{job.Id}/steps/{stepId}"),
            "edit" => await alice.PatchAsJsonAsync(
                $"/api/jobs/{job.Id}/steps/{stepId}",
                new { description = "Updated" }),
            "reorder" => await alice.PutAsJsonAsync(
                $"/api/jobs/{job.Id}/steps/order",
                new { orderedStepIds = completed.Steps.Reverse().Select(s => s.Id).ToArray() }),
            _ => throw new ArgumentException(op),
        };

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("code").GetString().ShouldBe("job_completed");
    }

    // ---- Cross-owner ----

    [Fact]
    public async Task AddStep_AsNonOwner_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var prop = await CreateProperty(alice);
        var job = await CreateJob(alice, prop.Id, "a");

        var resp = await bob.PostAsJsonAsync(
            $"/api/jobs/{job.Id}/steps",
            new { description = "Hijack" });
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
