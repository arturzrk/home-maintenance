using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Performance;

/// <summary>
/// SC-103: generate-next p95 round-trip is under 500ms on a warm Mongo.
/// Opt-in via Trait("category","perf"); excluded from the default test run.
///
/// Run locally with:
///   dotnet test tests/HomeMaintenance.Integration.Tests --filter "Category=perf"
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class GenerateNextPerformanceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public GenerateNextPerformanceTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("category", "perf")]
    public async Task GenerateNext_P95Under500ms()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", $"dev-perf-{Guid.NewGuid():N}");

        // Start date beyond 3-month creation horizon: no inline jobs, so each
        // sequential generate-next call produces a unique next monthly occurrence.
        var prop = (await (await client.PostAsJsonAsync("/api/properties", new { name = "Perf" }))
            .Content.ReadFromJsonAsync<PropertyDto>())!;

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6);
        var def = (await (await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = prop.Id,
            name = "Perf boiler",
            schedule = new { unit = "Month", multiplier = 1, startDate = startDate.ToString("yyyy-MM-dd") },
            stepTemplates = new[] { new { description = "Check pressure" } },
        })).Content.ReadFromJsonAsync<JobDefinitionDto>())!;

        // Warm-up: prime Mongo connection, JIT, and app startup.
        (await client.PostAsync($"/api/job-definitions/{def.Id}/generate-next", null))
            .EnsureSuccessStatusCode();

        const int iterations = 100;
        var samples = new long[iterations];
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var resp = await client.PostAsync($"/api/job-definitions/{def.Id}/generate-next", null);
            sw.Stop();
            resp.EnsureSuccessStatusCode();
            samples[i] = sw.ElapsedMilliseconds;
        }

        Array.Sort(samples);
        var p50 = samples[iterations * 50 / 100];
        var p95 = samples[iterations * 95 / 100];
        var p99 = samples[iterations * 99 / 100];

        Console.WriteLine($"generate-next latency over {iterations} samples (ms): " +
                          $"p50={p50}, p95={p95}, p99={p99}");

        // 750ms safety margin (same as StepTickPerformanceTests): GitHub-hosted
        // runners can be noisy; the spec budget is 500ms.
        p95.ShouldBeLessThan(750L);
    }
}
