using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.Application.Assets.Dto;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Assets;

[Collection(nameof(ApiFactory))]
public sealed class AssetScopedWorkTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AssetScopedWorkTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return client;
    }

    private static async Task<PropertyDto> CreateProperty(HttpClient client, string name = "House")
    {
        var resp = await client.PostAsJsonAsync("/api/properties", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;
    }

    private static async Task<AssetDto> CreateAsset(HttpClient client, string propertyId, string name = "Boiler")
    {
        var resp = await client.PostAsJsonAsync("/api/assets", new { propertyId, name });
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<AssetDto>(TestJson.Options))!;
    }

    private static async Task SetObsolete(HttpClient client, string assetId, bool isObsolete)
    {
        var resp = await client.PatchAsJsonAsync($"/api/assets/{assetId}", new { isObsolete });
        resp.EnsureSuccessStatusCode();
    }

    private static object MonthlySchedule(string startDate = "2026-06-01")
        => new { unit = "Month", multiplier = 1, startDate };

    // ---- Job creation ----

    [Fact]
    public async Task CreateJob_WithValidAsset_EchoesAssetId()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        var resp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Service boiler",
            steps = Array.Empty<object>(),
            assetId = asset.Id,
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        dto!.AssetId.ShouldBe(asset.Id);
    }

    [Fact]
    public async Task CreateJob_WithForeignAsset_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var aliceProperty = await CreateProperty(alice);
        var aliceAsset = await CreateAsset(alice, aliceProperty.Id);
        var bobProperty = await CreateProperty(bob);

        var resp = await bob.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = bobProperty.Id,
            name = "Hijack",
            steps = Array.Empty<object>(),
            assetId = aliceAsset.Id,
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateJob_WithAssetFromOtherProperty_Returns400Mismatch()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop1 = await CreateProperty(client, "House 1");
        var prop2 = await CreateProperty(client, "House 2");
        var assetOnProp2 = await CreateAsset(client, prop2.Id);

        var resp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = prop1.Id,
            name = "Mismatch",
            steps = Array.Empty<object>(),
            assetId = assetOnProp2.Id,
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        problem.GetProperty("code").GetString().ShouldBe("asset_property_mismatch");
    }

    [Fact]
    public async Task CreateJob_WithObsoleteAsset_Returns400Obsolete()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);
        await SetObsolete(client, asset.Id, true);

        var resp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Too late",
            steps = Array.Empty<object>(),
            assetId = asset.Id,
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await resp.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        problem.GetProperty("code").GetString().ShouldBe("asset_obsolete");
    }

    [Fact]
    public async Task CreateJob_WithoutAsset_StillWorks()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);

        var resp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Plain job",
            steps = Array.Empty<object>(),
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        dto!.AssetId.ShouldBeNull();
    }

    // ---- Definition creation + inheritance ----

    [Fact]
    public async Task CreateDefinition_WithAsset_InlineGeneratedJobsInheritIt()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date).ToString("O");
        var resp = await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = property.Id,
            name = "Service boiler",
            schedule = MonthlySchedule(today),
            assetId = asset.Id,
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var def = await resp.Content.ReadFromJsonAsync<JobDefinitionDto>(TestJson.Options);
        def!.AssetId.ShouldBe(asset.Id);

        var jobs = await client.GetFromJsonAsync<JobListDto>(
            $"/api/jobs?propertyId={property.Id}", TestJson.Options);
        jobs!.Jobs.ShouldNotBeEmpty();
        jobs.Jobs.ShouldAllBe(j => j.AssetId == asset.Id);
    }

    [Fact]
    public async Task GenerateNext_InheritsAssetId()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        // Start far in the future -> no inline generation; first
        // generate-next creates the first occurrence.
        var futureStart = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(200).ToString("O");
        var createResp = await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = property.Id,
            name = "Future work",
            schedule = MonthlySchedule(futureStart),
            assetId = asset.Id,
        });
        var def = await createResp.Content.ReadFromJsonAsync<JobDefinitionDto>(TestJson.Options);

        var genResp = await client.PostAsync($"/api/job-definitions/{def!.Id}/generate-next", null);
        genResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var job = await genResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);
        job!.AssetId.ShouldBe(asset.Id);
    }

    [Fact]
    public async Task CreateDefinition_WithObsoleteAsset_Returns400()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);
        await SetObsolete(client, asset.Id, true);

        var resp = await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = property.Id,
            name = "Too late",
            schedule = MonthlySchedule(),
            assetId = asset.Id,
        });

        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Obsolete has no cascade ----

    [Fact]
    public async Task MarkingAssetObsolete_DoesNotChangeExistingWork()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        var jobResp = await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Existing job",
            steps = Array.Empty<object>(),
            assetId = asset.Id,
        });
        var job = await jobResp.Content.ReadFromJsonAsync<JobDetailDto>(TestJson.Options);

        await SetObsolete(client, asset.Id, true);

        var refetched = await client.GetFromJsonAsync<JobDetailDto>(
            $"/api/jobs/{job!.Id}", TestJson.Options);
        refetched!.AssetId.ShouldBe(asset.Id);
        refetched.Status.ShouldBe(HomeMaintenance.Domain.Jobs.JobStatus.Active);
    }

    // ---- List filters ----

    [Fact]
    public async Task JobsList_FilteredByAssetId_ReturnsOnlyScopedJobs()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Scoped",
            steps = Array.Empty<object>(),
            assetId = asset.Id,
        });
        await client.PostAsJsonAsync("/api/jobs", new
        {
            propertyId = property.Id,
            name = "Unscoped",
            steps = Array.Empty<object>(),
        });

        var filtered = await client.GetFromJsonAsync<JobListDto>(
            $"/api/jobs?assetId={asset.Id}", TestJson.Options);

        filtered!.Jobs.Count.ShouldBe(1);
        filtered.Jobs[0].Name.ShouldBe("Scoped");
    }

    [Fact]
    public async Task DefinitionsList_FilteredByAssetId_ReturnsOnlyScopedDefinitions()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = property.Id,
            name = "Scoped def",
            schedule = MonthlySchedule(),
            assetId = asset.Id,
        });
        await client.PostAsJsonAsync("/api/job-definitions", new
        {
            propertyId = property.Id,
            name = "Unscoped def",
            schedule = MonthlySchedule(),
        });

        var filtered = await client.GetFromJsonAsync<List<JobDefinitionDto>>(
            $"/api/job-definitions?assetId={asset.Id}", TestJson.Options);

        filtered!.Count.ShouldBe(1);
        filtered[0].Name.ShouldBe("Scoped def");
    }
}
