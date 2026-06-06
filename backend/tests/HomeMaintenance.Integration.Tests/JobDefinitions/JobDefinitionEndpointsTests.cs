using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.API.Endpoints;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.JobDefinitions;

[Collection(nameof(ApiFactory))]
public sealed class JobDefinitionEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public JobDefinitionEndpointsTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return c;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<PropertyDto> CreateProperty(HttpClient client, string name = "Main House")
    {
        var resp = await client.PostAsJsonAsync("/api/properties", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PropertyDto>())!;
    }

    private static object Schedule(string unit, int multiplier, DateOnly startDate, DateOnly? endDate = null)
        => new
        {
            unit,
            multiplier,
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate?.ToString("yyyy-MM-dd"),
        };

    private async Task<JobDefinitionDto> CreateDefinition(
        HttpClient client,
        string propertyId,
        object schedule,
        string name = "Service boiler",
        params string[] stepDescriptions)
    {
        var body = new
        {
            propertyId,
            name,
            schedule,
            stepTemplates = stepDescriptions.Select(d => new { description = d }).ToArray(),
        };
        var resp = await client.PostAsJsonAsync("/api/job-definitions", body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JobDefinitionDto>())!;
    }

    // ---- Anonymous ----

    [Fact]
    public async Task POST_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync(
            "/api/job-definitions",
            new { propertyId = "x", name = "y", schedule = Schedule("Month", 1, Today), stepTemplates = Array.Empty<object>() });
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/job-definitions");
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PATCH_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient().PatchAsJsonAsync("/api/job-definitions/some-id", new { name = "New name" });
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Create ----

    [Fact]
    public async Task POST_ValidRequest_Returns201WithDefinitionAndGeneratesJobs()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);

        var body = new
        {
            propertyId = property.Id,
            name = "Service boiler",
            schedule = Schedule("Month", 1, Today),
            stepTemplates = new[] { new { description = "Shut off gas" } },
        };
        var resp = await alice.PostAsJsonAsync("/api/job-definitions", body);

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        resp.Headers.Location.ShouldNotBeNull();
        var dto = await resp.Content.ReadFromJsonAsync<JobDefinitionDto>();
        dto!.Name.ShouldBe("Service boiler");
        dto.StepTemplates.Count.ShouldBe(1);
        dto.Schedule.Unit.ShouldBe("Month");

        var jobs = await alice.GetFromJsonAsync<JobListDto>($"/api/jobs?propertyId={property.Id}");
        jobs!.Jobs.ShouldNotBeEmpty();
        jobs.Jobs.ShouldContain(j => j.JobDefinitionId == dto.Id);
    }

    [Fact]
    public async Task POST_EmptyName_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);

        var body = new
        {
            propertyId = property.Id,
            name = "",
            schedule = Schedule("Month", 1, Today),
            stepTemplates = Array.Empty<object>(),
        };
        (await alice.PostAsJsonAsync("/api/job-definitions", body)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_MultiplierZero_Returns400()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);

        var body = new
        {
            propertyId = property.Id,
            name = "Service boiler",
            schedule = Schedule("Month", 0, Today),
            stepTemplates = Array.Empty<object>(),
        };
        (await alice.PostAsJsonAsync("/api/job-definitions", body)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CrossPropertyOwnership_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);

        var body = new
        {
            propertyId = property.Id,
            name = "Hijacked",
            schedule = Schedule("Month", 1, Today),
            stepTemplates = Array.Empty<object>(),
        };
        (await bob.PostAsJsonAsync("/api/job-definitions", body)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Read ----

    [Fact]
    public async Task GET_List_Returns200WithOwnedDefinitions()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var aliceProp = await CreateProperty(alice);
        var bobProp = await CreateProperty(bob);
        await CreateDefinition(alice, aliceProp.Id, Schedule("Month", 1, Today));
        await CreateDefinition(bob, bobProp.Id, Schedule("Month", 1, Today));

        var aliceList = await alice.GetFromJsonAsync<List<JobDefinitionDto>>("/api/job-definitions");
        aliceList!.Count.ShouldBe(1);
        aliceList[0].PropertyId.ShouldBe(aliceProp.Id);
    }

    [Fact]
    public async Task GET_List_FilteredByPropertyId_Returns200Subset()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop1 = await CreateProperty(alice, "House 1");
        var prop2 = await CreateProperty(alice, "House 2");
        await CreateDefinition(alice, prop1.Id, Schedule("Month", 1, Today), "Def 1");
        await CreateDefinition(alice, prop1.Id, Schedule("Month", 1, Today), "Def 2");
        await CreateDefinition(alice, prop2.Id, Schedule("Month", 1, Today), "Def 3");

        var filtered = await alice.GetFromJsonAsync<List<JobDefinitionDto>>($"/api/job-definitions?propertyId={prop1.Id}");
        filtered!.Count.ShouldBe(2);
        filtered.All(d => d.PropertyId == prop1.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GET_ById_Owned_Returns200()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, Today));

        var resp = await alice.GetAsync($"/api/job-definitions/{created.Id}");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<JobDefinitionDto>();
        dto!.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task GET_ById_CrossOwner_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, Today));

        (await bob.GetAsync($"/api/job-definitions/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Update ----

    [Fact]
    public async Task PATCH_Rename_Returns200WithUpdatedName()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, Today));

        var resp = await alice.PatchAsJsonAsync($"/api/job-definitions/{created.Id}", new { name = "Renamed boiler service" });
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<JobDefinitionDto>();
        dto!.Name.ShouldBe("Renamed boiler service");
    }

    [Fact]
    public async Task PATCH_ChangeSchedule_Returns200AndSchedulePersisted()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, Today));

        var newStart = Today.AddMonths(1);
        var resp = await alice.PatchAsJsonAsync(
            $"/api/job-definitions/{created.Id}",
            new { schedule = Schedule("Week", 2, newStart) });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<JobDefinitionDto>();
        dto!.Schedule.Unit.ShouldBe("Week");
        dto.Schedule.Multiplier.ShouldBe(2);
        dto.Schedule.StartDate.ShouldBe(newStart);
    }

    [Fact]
    public async Task PATCH_AddStepTemplate_Returns200AndStepPresent()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, Today));

        var resp = await alice.PatchAsJsonAsync(
            $"/api/job-definitions/{created.Id}",
            new { addStepTemplates = new[] { new { description = "Check pressure gauge" } } });

        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<JobDefinitionDto>();
        dto!.StepTemplates.ShouldContain(s => s.Description == "Check pressure gauge");
    }

    [Fact]
    public async Task PATCH_CrossOwner_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, Today));

        var resp = await bob.PatchAsJsonAsync($"/api/job-definitions/{created.Id}", new { name = "Hijacked rename" });
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Generate next occurrence ----

    [Fact]
    public async Task GenerateNext_Success_Returns201WithJobDtoAndLocationHeader()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        // Start date beyond the 3-month creation horizon: zero jobs generated at creation time,
        // so generate-next falls back to Schedule.StartDate as the first occurrence.
        var farFuture = Today.AddMonths(6);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, farFuture));

        var resp = await alice.PostAsync($"/api/job-definitions/{created.Id}/generate-next", content: null);

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        resp.Headers.Location.ShouldNotBeNull();
        resp.Headers.Location!.ToString().ShouldStartWith("/api/jobs/");
        var dto = await resp.Content.ReadFromJsonAsync<JobDetailDto>();
        dto!.JobDefinitionId.ShouldBe(created.Id);
        dto.DueDate.ShouldBe(farFuture);
    }

    [Fact]
    public async Task GenerateNext_DuplicateOccurrence_Returns400WithCode()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var farFuture = Today.AddMonths(6);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, farFuture));

        // The handler's "already generated?" check and the job insert are two separate
        // operations (read-then-write), so the duplicate guard is exercised by racing
        // concurrent requests against the same first-occurrence candidate (no jobs exist
        // yet, so every racer computes nextOccurrence = Schedule.StartDate). With enough
        // concurrent racers, at least one is guaranteed to observe a sibling's already-
        // inserted job and get rejected with the duplicate code.
        var calls = Enumerable.Range(0, 8)
            .Select(_ => alice.PostAsync($"/api/job-definitions/{created.Id}/generate-next", content: null))
            .ToArray();
        var responses = await Task.WhenAll(calls);

        responses.ShouldContain(r => r.StatusCode == HttpStatusCode.Created);
        var failed = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.BadRequest);
        failed.ShouldNotBeNull();
        var problem = await failed!.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("next_occurrence_already_exists");
    }

    [Fact]
    public async Task GenerateNext_CrossOwner_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var created = await CreateDefinition(alice, property.Id, Schedule("Month", 1, Today.AddMonths(6)));

        var resp = await bob.PostAsync($"/api/job-definitions/{created.Id}/generate-next", content: null);
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateNext_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsync("/api/job-definitions/some-id/generate-next", content: null);
        resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
