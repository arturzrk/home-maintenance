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
/// SC-007 / FR-019 / FR-027: every mutation against a Completed Job
/// returns HTTP 400 with a problem+json body whose <c>code</c>
/// identifies the seal, and leaves the Job state unchanged.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class SealingMatrixTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SealingMatrixTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("addStep", "job_completed")]
    [InlineData("removeStep", "job_completed")]
    [InlineData("editStep", "job_completed")]
    [InlineData("reorderSteps", "job_completed")]
    [InlineData("tickStep", "job_completed")]
    [InlineData("untickStep", "job_completed")]
    [InlineData("patchJob", "job_completed")]
    [InlineData("completeAgain", "job_already_completed")]
    public async Task Mutation_OnCompletedJob_Returns400_AndStateUnchanged(
        string mutation, string expectedCode)
    {
        var client = NewAliceClient();
        var completed = await SetupCompletedJob(client);

        var resp = await Attempt(client, completed, mutation);

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("code").GetString().ShouldBe(expectedCode);

        // Re-fetch and assert state is unchanged.
        var refetch = await client.GetFromJsonAsync<JobDetailDto>($"/api/jobs/{completed.Id}", TestJson.Options);
        refetch!.Status.ShouldBe(completed.Status);
        refetch.Name.ShouldBe(completed.Name);
        refetch.Steps.Count.ShouldBe(completed.Steps.Count);
        for (var i = 0; i < refetch.Steps.Count; i++)
        {
            refetch.Steps[i].Id.ShouldBe(completed.Steps[i].Id);
            refetch.Steps[i].Description.ShouldBe(completed.Steps[i].Description);
            refetch.Steps[i].IsCompleted.ShouldBe(completed.Steps[i].IsCompleted);
            refetch.Steps[i].Order.ShouldBe(completed.Steps[i].Order);
        }
    }

    private HttpClient NewAliceClient()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"dev-alice-{Guid.NewGuid():N}");
        return c;
    }

    private static async Task<JobDetailDto> SetupCompletedJob(HttpClient client)
    {
        var propResp = await client.PostAsJsonAsync("/api/properties", new { name = "House" });
        propResp.EnsureSuccessStatusCode();
        var prop = (await propResp.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var jobResp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = prop.Id,
            name = "Service boiler",
            dueDate = (string?)null,
            steps = new[]
            {
                new { description = "Shut off gas" },
                new { description = "Drain system" },
            },
        });
        jobResp.EnsureSuccessStatusCode();
        var job = (await jobResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
        foreach (var s in job.Steps)
            (await client.PostAsync($"/api/jobs/{job.Id}/steps/{s.Id}/tick", null))
                .EnsureSuccessStatusCode();
        var completeResp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        completeResp.EnsureSuccessStatusCode();
        return (await completeResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> Attempt(
        HttpClient client, JobDetailDto job, string mutation) => mutation switch
    {
        "addStep" => client.PostAsJsonAsync(
            $"/api/jobs/{job.Id}/steps", new { description = "Hijack" }),
        "removeStep" => client.DeleteAsync(
            $"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}"),
        "editStep" => client.PatchAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}",
            new { description = "Hijack" }),
        "reorderSteps" => client.PutAsJsonAsync(
            $"/api/jobs/{job.Id}/steps/order",
            new { orderedStepIds = job.Steps.Reverse().Select(s => s.Id).ToArray() }),
        "tickStep" => client.PostAsync(
            $"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}/tick", null),
        "untickStep" => client.PostAsync(
            $"/api/jobs/{job.Id}/steps/{job.Steps[0].Id}/untick", null),
        "patchJob" => client.PatchAsJsonAsync(
            $"/api/jobs/{job.Id}", new { name = "Hijack" }),
        "completeAgain" => client.PostAsync(
            $"/api/jobs/{job.Id}/complete", null),
        _ => throw new ArgumentException($"Unknown mutation: {mutation}"),
    };
}
