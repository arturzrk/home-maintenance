using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeMaintenance.Application.Assets.Dto;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Assets;

[Collection(nameof(ApiFactory))]
public sealed class AssetEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AssetEndpointsTests(ApiFactory factory) => _factory = factory;

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

    private static async Task<AssetDto> CreateAsset(
        HttpClient client, string propertyId, string name = "Boiler", string? category = null, string? notes = null)
    {
        var resp = await client.PostAsJsonAsync("/api/assets", new { propertyId, name, category, notes });
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<AssetDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assets", new { propertyId = "x", name = "Boiler" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ValidRequest_Returns201_WithFieldsAndLocation()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);

        var response = await client.PostAsJsonAsync("/api/assets", new
        {
            propertyId = property.Id,
            name = "Boiler",
            category = "Heating",
            notes = "Installed 2020",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldStartWith("/api/assets/");

        var dto = await response.Content.ReadFromJsonAsync<AssetDto>(TestJson.Options);
        dto.ShouldNotBeNull();
        dto!.PropertyId.ShouldBe(property.Id);
        dto.Name.ShouldBe("Boiler");
        dto.Category.ShouldBe("Heating");
        dto.Notes.ShouldBe("Installed 2020");
        dto.IsObsolete.ShouldBeFalse();
    }

    [Fact]
    public async Task Post_MissingName_Returns400()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);

        var response = await client.PostAsJsonAsync("/api/assets", new { propertyId = property.Id, name = "" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_NameOver200_Returns400()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);

        var response = await client.PostAsJsonAsync(
            "/api/assets", new { propertyId = property.Id, name = new string('x', 201) });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ForeignProperty_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var aliceProperty = await CreateProperty(alice);

        var response = await bob.PostAsJsonAsync("/api/assets", new { propertyId = aliceProperty.Id, name = "Boiler" });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_ReturnsOnlyAssetsOfThatProperty()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var prop1 = await CreateProperty(client, "House 1");
        var prop2 = await CreateProperty(client, "House 2");
        await CreateAsset(client, prop1.Id, "Boiler");
        await CreateAsset(client, prop1.Id, "Roof");
        await CreateAsset(client, prop2.Id, "Lawn mower");

        var list = await client.GetFromJsonAsync<List<AssetDto>>(
            $"/api/assets?propertyId={prop1.Id}", TestJson.Options);

        list.ShouldNotBeNull();
        list!.Count.ShouldBe(2);
        list.Select(a => a.Name).ShouldBe(new[] { "Boiler", "Roof" });
    }

    [Fact]
    public async Task List_ForeignProperty_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var aliceProperty = await CreateProperty(alice);

        var response = await bob.GetAsync($"/api/assets?propertyId={aliceProperty.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ById_ReturnsAsset()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var created = await CreateAsset(client, property.Id, "Boiler", "Heating");

        var fetched = await client.GetFromJsonAsync<AssetDto>($"/api/assets/{created.Id}", TestJson.Options);

        fetched.ShouldNotBeNull();
        fetched!.Id.ShouldBe(created.Id);
        fetched.Name.ShouldBe("Boiler");
    }

    [Fact]
    public async Task Get_CrossOwner_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var asset = await CreateAsset(alice, property.Id);

        var response = await bob.GetAsync($"/api/assets/{asset.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_UpdatesEachField_AndObsoleteRoundTrips()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id, "Boiler", "Heating", "old note");

        var patch = await client.PatchAsJsonAsync($"/api/assets/{asset.Id}", new
        {
            name = "New Boiler",
            category = "HVAC",
            notes = "new note",
            isObsolete = true,
        });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = await patch.Content.ReadFromJsonAsync<AssetDto>(TestJson.Options);
        dto!.Name.ShouldBe("New Boiler");
        dto.Category.ShouldBe("HVAC");
        dto.Notes.ShouldBe("new note");
        dto.IsObsolete.ShouldBeTrue();

        var reactivate = await client.PatchAsJsonAsync($"/api/assets/{asset.Id}", new { isObsolete = false });
        var reactivated = await reactivate.Content.ReadFromJsonAsync<AssetDto>(TestJson.Options);
        reactivated!.IsObsolete.ShouldBeFalse();
        reactivated.Name.ShouldBe("New Boiler");
    }

    [Fact]
    public async Task Patch_EmptyStringClearsCategoryAndNotes()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id, "Boiler", "Heating", "note");

        var patch = await client.PatchAsJsonAsync($"/api/assets/{asset.Id}", new { category = "", notes = "" });

        var dto = await patch.Content.ReadFromJsonAsync<AssetDto>(TestJson.Options);
        dto!.Category.ShouldBeNull();
        dto.Notes.ShouldBeNull();
    }

    [Fact]
    public async Task Patch_NoChanges_Returns400()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        var response = await client.PatchAsJsonAsync($"/api/assets/{asset.Id}", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_CrossOwner_Returns404()
    {
        var alice = ClientAs($"alice-{Guid.NewGuid():N}");
        var bob = ClientAs($"bob-{Guid.NewGuid():N}");
        var property = await CreateProperty(alice);
        var asset = await CreateAsset(alice, property.Id);

        var response = await bob.PatchAsJsonAsync($"/api/assets/{asset.Id}", new { name = "Hijack" });
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_IsNotRouted()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");
        var property = await CreateProperty(client);
        var asset = await CreateAsset(client, property.Id);

        var response = await client.DeleteAsync($"/api/assets/{asset.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }
}
