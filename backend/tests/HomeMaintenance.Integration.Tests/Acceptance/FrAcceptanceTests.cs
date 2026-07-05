using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using HomeMaintenance.Infrastructure.Scheduling;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
        var dto = await ok.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options);
        dto!.Name.ShouldBe("Main House"); // trimmed
    }

    // FR-014: CreateJob must reference a Property owned by the caller.
    [Fact]
    public async Task FR_014_CreateJob_RequiresPropertyOwnedByCaller()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var prop = (await (await alice.PostAsJsonAsync("/api/properties",
            new { name = "Alice Place" })).Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var resp = await bob.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = prop.Id,
            name = "Hijack",
            dueDate = (string?)null,
            steps = Array.Empty<object>(),
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("code").GetString().ShouldBe("not_found");
    }

    // FR-018a: CompleteJob rejects when any Step is incomplete.
    [Fact]
    public async Task FR_018_CompleteJob_RejectsIfAnyStepIncomplete()
    {
        var (client, job) = await CreateJobWithSteps("a", "b");
        var resp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("code").GetString().ShouldBe("steps_incomplete");
    }

    // FR-018b: CompleteJob rejects a Job with zero Steps.
    [Fact]
    public async Task FR_018_CompleteJob_RejectsIfZeroSteps()
    {
        var (client, job) = await CreateJobWithSteps();
        var resp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("code").GetString().ShouldBe("job_has_no_steps");
    }

    // FR-022: RemoveStep renumbers Orders contiguously from 0.
    [Fact]
    public async Task FR_022_RemoveStep_RenumbersOrdersContiguously()
    {
        var (client, job) = await CreateJobWithSteps("a", "b", "c");
        var resp = await client.DeleteAsync($"/api/jobs/{job.Id}/steps/{job.Steps[1].Id}");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
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
        var ticked = (await tickResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
        var completedAt = ticked.Steps[0].CompletedAt;
        completedAt.ShouldNotBeNull();
        completedAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);

        var completeResp = await client.PostAsync($"/api/jobs/{job.Id}/complete", null);
        var completed = (await completeResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
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
        (await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("code").GetString().ShouldBe("job_completed");
    }

    private async Task<(HttpClient client, JobDetailDto job)> CreateJobWithSteps(
        params string[] stepDescriptions)
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = (await (await client.PostAsJsonAsync("/api/properties",
            new { name = "House" })).Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;
        var jobResp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = prop.Id,
            name = "Job",
            dueDate = (string?)null,
            steps = stepDescriptions.Select(d => new { description = d }).ToArray(),
        });
        jobResp.EnsureSuccessStatusCode();
        var job = (await jobResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
        return (client, job);
    }

    // ---- Slice 2 FR acceptance tests (SC-102, SC-103, SC-104) ----

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static string DateStr(DateOnly d) => d.ToString("yyyy-MM-dd");

    // FR-104: editing step templates must not retroactively modify already-generated jobs.
    [Fact]
    public async Task FR_104_EditJobDefinitionSteps_DoesNotModifyAlreadyGeneratedJob()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = (await (await client.PostAsJsonAsync("/api/properties", new { name = "House" }))
            .Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        // 1. Create definition with 2 step templates (startDate = today triggers inline generation)
        var defResp = await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = prop.Id,
            name = "Service boiler",
            schedule = new { unit = "Month", multiplier = 1, startDate = DateStr(Today) },
            stepTemplates = new[] { new { description = "Step A" }, new { description = "Step B" } },
        });
        defResp.EnsureSuccessStatusCode();
        var definition = (await defResp.Content.ReadFromJsonAsync<JobDefinitionDto>(TestJson.Options))!;

        // 2-3. Find a generated job; assert it has 2 steps
        var jobList = (await client.GetFromJsonAsync<JobListDto>($"/api/jobs?propertyId={prop.Id}", TestJson.Options))!;
        var generated = jobList.Jobs.First(j => j.JobDefinitionId == definition.Id);
        var detail = (await (await client.GetAsync($"/api/jobs/{generated.Id}"))
            .Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
        detail.Steps.Count.ShouldBe(2);

        // 4. Add a 3rd step template to the definition
        (await client.PatchAsJsonAsync($"/api/job-definitions/{definition.Id}", new
        {
            addStepTemplates = new[] { new { description = "Step C" } },
        })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // 5-6. Original job still has exactly 2 steps (snapshot unchanged)
        var reloaded = (await (await client.GetAsync($"/api/jobs/{generated.Id}"))
            .Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
        reloaded.Steps.Count.ShouldBe(2);

        // 7-9. generate-next creates a NEW job from the updated template (3 steps)
        var genResp = await client.PostAsync($"/api/job-definitions/{definition.Id}/generate-next", null);
        genResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var newJob = (await genResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
        newJob.Steps.Count.ShouldBe(3);
    }

    // FR-113: running the scheduler twice must not produce duplicate jobs.
    [Fact]
    public async Task FR_113_SchedulerRunTwice_ProducesNoDuplicateJobs()
    {
        var today = new DateOnly(2026, 6, 1);
        var (service, definitions, _) = BuildGenerationService(today);
        var definition = MakeMonthlyDefinition(today);
        await definitions.AddAsync(definition, CancellationToken.None);

        await service.RunGenerationPassAsync(CancellationToken.None);
        var afterFirst = await CountJobsForDefinition(definition.Id, today);

        await service.RunGenerationPassAsync(CancellationToken.None);
        var afterSecond = await CountJobsForDefinition(definition.Id, today);

        afterSecond.ShouldBe(afterFirst);
    }

    // FR-117: generate-next must reject a duplicate occurrence.
    [Fact]
    public async Task FR_117_GenerateNext_RejectsIfOccurrenceAlreadyExists()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = (await (await client.PostAsJsonAsync("/api/properties", new { name = "House" }))
            .Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        // Far future start: no inline jobs generated, so all 20 concurrent calls target the same first occurrence.
        var farFuture = Today.AddMonths(6);
        var defResp = await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = prop.Id,
            name = "Service boiler",
            schedule = new { unit = "Month", multiplier = 1, startDate = DateStr(farFuture) },
            stepTemplates = new[] { new { description = "Check" } },
        });
        defResp.EnsureSuccessStatusCode();
        var definition = (await defResp.Content.ReadFromJsonAsync<JobDefinitionDto>(TestJson.Options))!;

        var calls = Enumerable.Range(0, 20)
            .Select(_ => client.PostAsync($"/api/job-definitions/{definition.Id}/generate-next", null))
            .ToArray();
        var responses = await Task.WhenAll(calls);

        responses.ShouldContain(r => r.StatusCode == HttpStatusCode.Created);
        var failed = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.BadRequest);
        failed.ShouldNotBeNull();
        var problem = await failed!.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        problem.GetProperty("code").GetString().ShouldBe("next_occurrence_already_exists");
    }

    // FR-111: scheduler generates occurrences within the 3-month horizon only.
    [Fact]
    public async Task FR_111_Scheduler_GeneratesOccurrencesWithinHorizon()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = (await (await client.PostAsJsonAsync("/api/properties", new { name = "House" }))
            .Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var defResp = await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = prop.Id,
            name = "Service boiler",
            schedule = new { unit = "Month", multiplier = 1, startDate = DateStr(Today) },
            stepTemplates = new[] { new { description = "Check" } },
        });
        defResp.EnsureSuccessStatusCode();
        var definition = (await defResp.Content.ReadFromJsonAsync<JobDefinitionDto>(TestJson.Options))!;

        var jobList = (await client.GetFromJsonAsync<JobListDto>($"/api/jobs?propertyId={prop.Id}", TestJson.Options))!;
        var generated = jobList.Jobs.Where(j => j.JobDefinitionId == definition.Id).ToList();

        // Occurrences within horizon must exist
        foreach (var n in new[] { 0, 1, 2, 3 })
            generated.ShouldContain(j => j.DueDate == Today.AddMonths(n),
                $"expected occurrence at Today+{n}m");

        // First occurrence outside the 3-month horizon must NOT exist
        generated.ShouldNotContain(j => j.DueDate == Today.AddMonths(4));
    }

    // FR-116: generate-next uses the earliest occurrence strictly after the latest generated job.
    [Fact]
    public async Task FR_116_GenerateNext_UsesEarliestOccurrenceAfterLatestJob()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop = (await (await client.PostAsJsonAsync("/api/properties", new { name = "House" }))
            .Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        // Inline generation creates: Today, Today+1m, Today+2m, Today+3m
        var defResp = await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = prop.Id,
            name = "Service boiler",
            schedule = new { unit = "Month", multiplier = 1, startDate = DateStr(Today) },
            stepTemplates = new[] { new { description = "Check" } },
        });
        defResp.EnsureSuccessStatusCode();
        var definition = (await defResp.Content.ReadFromJsonAsync<JobDefinitionDto>(TestJson.Options))!;

        // generate-next must return Today+4m (first occurrence after Today+3m)
        var genResp = await client.PostAsync($"/api/job-definitions/{definition.Id}/generate-next", null);
        genResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var newJob = (await genResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options))!;
        newJob.DueDate.ShouldBe(Today.AddMonths(4));
    }

    // ---- Service-layer helpers for FR tests that need a controllable clock ----

    private sealed class StubDateTimeProvider : IDateTimeProvider
    {
        public DateOnly UtcToday { get; set; }
        public StubDateTimeProvider(DateOnly today) => UtcToday = today;
    }

    private static readonly OwnerId FrTestOwner = new("fr-test");

    private (JobGeneratorService service, IJobDefinitionRepository definitions, IJobRepository jobs)
        BuildGenerationService(DateOnly today)
    {
        var hostScope = _factory.Services.CreateScope();
        var definitions = hostScope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();
        var jobs = hostScope.ServiceProvider.GetRequiredService<IJobRepository>();
        var audit = hostScope.ServiceProvider.GetRequiredService<IAuditLog>();
        var correlation = hostScope.ServiceProvider.GetRequiredService<ICorrelationContext>();
        var generationService = new JobGenerationService(jobs, audit, correlation);

        var services = new ServiceCollection();
        services.AddSingleton(definitions);
        services.AddSingleton(generationService);
        services.AddSingleton<IDateTimeProvider>(new StubDateTimeProvider(today));
        var provider = services.BuildServiceProvider();

        var service = new JobGeneratorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<JobGeneratorService>.Instance);

        return (service, definitions, jobs);
    }

    private static JobDefinition MakeMonthlyDefinition(DateOnly startDate)
        => JobDefinition.Create(
            $"def-{Guid.NewGuid():N}",
            FrTestOwner,
            "prop-fr",
            "Service boiler",
            new ScheduleDefinition(CadenceUnit.Month, 1, startDate, null),
            new[] { "Step A" });

    private async Task<int> CountJobsForDefinition(string definitionId, DateOnly from)
    {
        var jobs = _factory.Services.CreateScope()
            .ServiceProvider.GetRequiredService<IJobRepository>();
        var count = 0;
        for (var n = 0; n < 6; n++)
        {
            if (await jobs.HasGeneratedJobForOccurrenceAsync(
                    definitionId, from.AddMonths(n), CancellationToken.None))
                count++;
        }
        return count;
    }
}
