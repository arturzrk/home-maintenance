using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Domain.Properties;
using HomeMaintenance.Infrastructure.Email;
using HomeMaintenance.Infrastructure.Scheduling;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Scheduling;

/// <summary>
/// ReminderDigestServiceTests proves the digest's decision logic against a
/// FakeEmailSender, and ResendEmailSenderTests proves ResendEmailSender's
/// request shape against a hand-built instance - neither exercises the two
/// composed together. This closes that seam: a real ReminderDigestService,
/// backed by the ApiFactory's real Mongo repositories, driving a real
/// ResendEmailSender (only its HttpMessageHandler is faked, so no network
/// call happens), proving a due job actually produces a well-formed POST to
/// Resend's API carrying the owner's email, subject, and job content.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class ReminderDigestResendDeliveryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReminderDigestResendDeliveryTests(ApiFactory factory) => _factory = factory;

    private sealed class StubDateTimeProvider : IDateTimeProvider
    {
        public DateOnly UtcToday { get; }
        public StubDateTimeProvider(DateOnly today) => UtcToday = today;
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task DueJob_ProducesWellFormedResendRequest_ForOwnersEmail()
    {
        const string apiKey = "re_test_key";
        const string fromAddress = "reminders@notify.maintained.house";
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = $"reminder-{Guid.NewGuid():N}@example.com";
        var today = new DateOnly(2026, 6, 1);

        var hostScope = _factory.Services.CreateScope();
        var jobs = hostScope.ServiceProvider.GetRequiredService<IJobRepository>();
        var profiles = hostScope.ServiceProvider.GetRequiredService<IOwnerProfileRepository>();
        var properties = hostScope.ServiceProvider.GetRequiredService<IPropertyRepository>();

        var capturingHandler = new CapturingHttpMessageHandler();
        var http = new HttpClient(capturingHandler) { BaseAddress = new Uri("https://api.resend.com/") };
        var emailOptions = Options.Create(new EmailOptions
        {
            FromAddress = fromAddress,
            Resend = new ResendOptions { ApiKey = apiKey },
        });
        var emailSender = new ResendEmailSender(http, emailOptions);

        var services = new ServiceCollection();
        services.AddSingleton(jobs);
        services.AddSingleton(profiles);
        services.AddSingleton(properties);
        services.AddSingleton<IEmailSender>(emailSender);
        services.AddSingleton<IDateTimeProvider>(new StubDateTimeProvider(today));
        services.Configure<FrontendOptions>(o => o.BaseUrl = "https://app.example.com");
        var provider = services.BuildServiceProvider();

        var service = new ReminderDigestService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ReminderDigestService>.Instance);

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);
        var job = Job.Create(
            $"job-{Guid.NewGuid():N}", owner, property.Id, "Boiler service", today.AddDays(-1), new[] { "Step" });
        await jobs.AddAsync(job, CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        capturingHandler.LastRequest.ShouldNotBeNull();
        capturingHandler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        capturingHandler.LastRequest.RequestUri.ShouldBe(new Uri("https://api.resend.com/emails"));
        capturingHandler.LastRequest.Headers.Authorization.ShouldBe(
            new AuthenticationHeaderValue("Bearer", apiKey));

        var body = JsonSerializer.Deserialize<JsonElement>(capturingHandler.LastRequestBody!);
        body.GetProperty("from").GetString().ShouldBe(fromAddress);
        body.GetProperty("to")[0].GetString().ShouldBe(email);
        body.GetProperty("subject").GetString().ShouldBe("1 maintenance job needs your attention");
        var html = body.GetProperty("html").GetString();
        html.ShouldNotBeNull();
        html.ShouldContain($"https://app.example.com/jobs/{job.Id}");
        html.ShouldContain("https://app.example.com/settings/notifications");
    }
}
