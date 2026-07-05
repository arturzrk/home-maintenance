using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Properties;

[Collection(nameof(ApiFactory))]
public sealed class PropertyEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PropertyEndpointsTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"dev-{sub}");
        return client;
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/properties", new { name = "X" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Property_AsAuthenticatedUser_Returns201_WithLocation()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/properties", new { name = "Main House" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldStartWith("/api/properties/");

        var dto = await response.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options);
        dto.ShouldNotBeNull();
        dto!.Name.ShouldBe("Main House");
        dto.Id.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Post_Property_EmptyName_Returns400()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/properties", new { name = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Property_NameTooLong_Returns400()
    {
        var client = ClientAs($"alice-{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/properties", new { name = new string('x', 101) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Properties_ReturnsOnlyCallersOwn()
    {
        var aliceSub = $"alice-{Guid.NewGuid():N}";
        var bobSub = $"bob-{Guid.NewGuid():N}";

        await ClientAs(aliceSub).PostAsJsonAsync("/api/properties", new { name = "Alice Place" });
        await ClientAs(bobSub).PostAsJsonAsync("/api/properties", new { name = "Bob Place" });

        var aliceList = await ClientAs(aliceSub).GetFromJsonAsync<PropertyListDto>("/api/properties", TestJson.Options);
        aliceList!.Properties.Select(p => p.Name).ShouldContain("Alice Place");
        aliceList.Properties.Select(p => p.Name).ShouldNotContain("Bob Place");

        var bobList = await ClientAs(bobSub).GetFromJsonAsync<PropertyListDto>("/api/properties", TestJson.Options);
        bobList!.Properties.Select(p => p.Name).ShouldContain("Bob Place");
        bobList.Properties.Select(p => p.Name).ShouldNotContain("Alice Place");
    }

    [Fact]
    public async Task List_Properties_OrderedByName()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        await ClientAs(sub).PostAsJsonAsync("/api/properties", new { name = "Zebra" });
        await ClientAs(sub).PostAsJsonAsync("/api/properties", new { name = "Apple" });
        await ClientAs(sub).PostAsJsonAsync("/api/properties", new { name = "Mango" });

        var list = await ClientAs(sub).GetFromJsonAsync<PropertyListDto>("/api/properties", TestJson.Options);

        var names = list!.Properties.Select(p => p.Name).ToList();
        names.ShouldBe(new[] { "Apple", "Mango", "Zebra" });
    }

    [Fact]
    public async Task Get_Property_OwnedByCaller_Returns200()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var created = await ClientAs(sub).PostAsJsonAsync("/api/properties", new { name = "Main House" });
        var dto = (await created.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var response = await ClientAs(sub).GetAsync($"/api/properties/{dto.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await response.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options);
        fetched!.Name.ShouldBe("Main House");
    }

    [Fact]
    public async Task Get_Property_OwnedByOther_Returns404_WithoutLeak()
    {
        var aliceSub = $"alice-{Guid.NewGuid():N}";
        var bobSub = $"bob-{Guid.NewGuid():N}";
        var created = await ClientAs(aliceSub).PostAsJsonAsync("/api/properties", new { name = "Alice Place" });
        var dto = (await created.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var response = await ClientAs(bobSub).GetAsync($"/api/properties/{dto.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // Body should be problem+json with code=not_found and a correlationId.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("code").GetString().ShouldBe("not_found");
        body.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Get_Property_NonExistentId_Returns404()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var response = await ClientAs(sub).GetAsync($"/api/properties/{Guid.NewGuid():N}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_Property_ValidName_Returns200()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var created = await ClientAs(sub).PostAsJsonAsync("/api/properties", new { name = "Old Name" });
        var dto = (await created.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var response = await ClientAs(sub).PatchAsJsonAsync($"/api/properties/{dto.Id}", new { name = "New Name" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options);
        updated!.Name.ShouldBe("New Name");

        // Confirm persistence
        var refetch = await ClientAs(sub).GetFromJsonAsync<PropertyDto>($"/api/properties/{dto.Id}", TestJson.Options);
        refetch!.Name.ShouldBe("New Name");
    }

    [Fact]
    public async Task Patch_Property_OwnedByOther_Returns404()
    {
        var aliceSub = $"alice-{Guid.NewGuid():N}";
        var bobSub = $"bob-{Guid.NewGuid():N}";
        var created = await ClientAs(aliceSub).PostAsJsonAsync("/api/properties", new { name = "Alice Place" });
        var dto = (await created.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var response = await ClientAs(bobSub).PatchAsJsonAsync($"/api/properties/{dto.Id}", new { name = "Hijacked" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_Property_EmptyName_Returns400()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var created = await ClientAs(sub).PostAsJsonAsync("/api/properties", new { name = "Main House" });
        var dto = (await created.Content.ReadFromJsonAsync<PropertyDto>(TestJson.Options))!;

        var response = await ClientAs(sub).PatchAsJsonAsync($"/api/properties/{dto.Id}", new { name = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
