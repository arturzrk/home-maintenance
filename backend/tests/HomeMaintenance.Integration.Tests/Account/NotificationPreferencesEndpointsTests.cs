using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeMaintenance.Application.Account.Dto;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Account;

[Collection(nameof(ApiFactory))]
public sealed class NotificationPreferencesEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NotificationPreferencesEndpointsTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientAs(string sub, string? email = null)
    {
        var client = _factory.CreateClient();
        var token = email is null ? $"dev-{sub}" : $"dev-{sub}:{email}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/notification-preferences");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_NoEmailClaimEverSent_DefaultsToRemindersEnabled_WithNullEmail()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var client = ClientAs(sub); // no email suffix - capture never runs

        var dto = await client.GetFromJsonAsync<NotificationPreferencesDto>(
            "/api/account/notification-preferences", TestJson.Options);

        dto.ShouldNotBeNull();
        dto!.Email.ShouldBeNull();
        dto.RemindersEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_AfterAuthenticatedRequestWithEmailClaim_ReturnsCapturedEmail()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var email = $"{sub}@example.com";
        var client = ClientAs(sub, email);

        // Any authenticated request triggers the auth-pipeline capture -
        // this GET itself is that first authenticated request.
        var dto = await client.GetFromJsonAsync<NotificationPreferencesDto>(
            "/api/account/notification-preferences", TestJson.Options);

        dto.ShouldNotBeNull();
        dto!.Email.ShouldBe(email);
        dto.RemindersEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Patch_TogglesReminders_AndPersistsAcrossRequests()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var email = $"{sub}@example.com";
        var client = ClientAs(sub, email);

        // Establish the profile first (capture runs on this request).
        await client.GetAsync("/api/account/notification-preferences");

        var patch = await client.PatchAsJsonAsync(
            "/api/account/notification-preferences", new { remindersEnabled = false });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patched = await patch.Content.ReadFromJsonAsync<NotificationPreferencesDto>(TestJson.Options);
        patched!.RemindersEnabled.ShouldBeFalse();

        var reGet = await client.GetFromJsonAsync<NotificationPreferencesDto>(
            "/api/account/notification-preferences", TestJson.Options);
        reGet!.RemindersEnabled.ShouldBeFalse();

        var reEnable = await client.PatchAsJsonAsync(
            "/api/account/notification-preferences", new { remindersEnabled = true });
        var reEnabled = await reEnable.Content.ReadFromJsonAsync<NotificationPreferencesDto>(TestJson.Options);
        reEnabled!.RemindersEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Patch_NoProfileCapturedYet_Returns404()
    {
        var sub = $"alice-{Guid.NewGuid():N}";
        var client = ClientAs(sub); // no email suffix on this or any prior request

        var response = await client.PatchAsJsonAsync(
            "/api/account/notification-preferences", new { remindersEnabled = false });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_DifferentOwners_HaveIndependentPreferences()
    {
        var aliceSub = $"alice-{Guid.NewGuid():N}";
        var bobSub = $"bob-{Guid.NewGuid():N}";
        var alice = ClientAs(aliceSub, $"{aliceSub}@example.com");
        var bob = ClientAs(bobSub, $"{bobSub}@example.com");

        await alice.PatchAsJsonAsync("/api/account/notification-preferences", new { remindersEnabled = false });

        var aliceDto = await alice.GetFromJsonAsync<NotificationPreferencesDto>(
            "/api/account/notification-preferences", TestJson.Options);
        var bobDto = await bob.GetFromJsonAsync<NotificationPreferencesDto>(
            "/api/account/notification-preferences", TestJson.Options);

        aliceDto!.RemindersEnabled.ShouldBeFalse();
        bobDto!.RemindersEnabled.ShouldBeTrue();
    }
}
