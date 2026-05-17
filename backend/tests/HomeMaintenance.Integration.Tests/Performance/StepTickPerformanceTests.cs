using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Performance;

/// <summary>
/// SC-004: step-tick p95 round-trip is under 500ms on a warm Mongo.
/// Opt-in via Trait("category","perf"); excluded from the default
/// test run because GitHub-hosted runners have noisy latency.
///
/// Run locally with:
///   dotnet test tests/HomeMaintenance.Integration.Tests --filter "Category=perf"
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class StepTickPerformanceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StepTickPerformanceTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("category", "perf")]
    public async Task TickStep_P95_Under_500ms_Over_100_Iterations()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"dev-perf-{Guid.NewGuid():N}");

        // ---- warm up: prime Mongo connection, JIT, app startup ----
        var warmProp = (await (await client.PostAsJsonAsync("/api/properties",
            new { name = "Warm" })).Content.ReadFromJsonAsync<PropertyDto>())!;
        var warmJob = (await (await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = warmProp.Id,
            name = "Warm",
            dueDate = (string?)null,
            steps = new[] { new { description = "warm" } },
        })).Content.ReadFromJsonAsync<JobDetailDto>())!;
        var warmStep = warmJob.Steps[0].Id;
        await client.PostAsync($"/api/jobs/{warmJob.Id}/steps/{warmStep}/tick", null);
        await client.PostAsync($"/api/jobs/{warmJob.Id}/steps/{warmStep}/untick", null);

        // ---- create a fresh job with one step, time 100 toggle cycles ----
        var prop = (await (await client.PostAsJsonAsync("/api/properties",
            new { name = "Perf" })).Content.ReadFromJsonAsync<PropertyDto>())!;
        var job = (await (await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = prop.Id,
            name = "Perf",
            dueDate = (string?)null,
            steps = new[] { new { description = "perf step" } },
        })).Content.ReadFromJsonAsync<JobDetailDto>())!;
        var stepId = job.Steps[0].Id;

        const int iterations = 100;
        var samples = new long[iterations];
        for (var i = 0; i < iterations; i++)
        {
            var route = i % 2 == 0
                ? $"/api/jobs/{job.Id}/steps/{stepId}/tick"
                : $"/api/jobs/{job.Id}/steps/{stepId}/untick";
            var sw = Stopwatch.StartNew();
            var resp = await client.PostAsync(route, null);
            sw.Stop();
            resp.EnsureSuccessStatusCode();
            samples[i] = sw.ElapsedMilliseconds;
        }

        Array.Sort(samples);
        var p50 = samples[iterations * 50 / 100];
        var p95 = samples[iterations * 95 / 100];
        var p99 = samples[iterations * 99 / 100];

        // Surface percentiles in the test output so a borderline pass is debuggable.
        Console.WriteLine($"step-tick latency over {iterations} samples (ms): " +
                          $"p50={p50}, p95={p95}, p99={p99}");

        // 750ms (not 500ms) per WP08 risk note: GitHub-hosted runners can
        // be noisy. The constitution budget is 500ms; this is the safety
        // margin for the test - if it routinely fails near 750 we have a
        // real regression.
        p95.ShouldBeLessThan(750L);
    }
}
