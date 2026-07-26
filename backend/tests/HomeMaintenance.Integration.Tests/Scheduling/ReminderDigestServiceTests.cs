using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Domain.Properties;
using HomeMaintenance.Infrastructure.Scheduling;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Scheduling;

/// <summary>
/// The digest pass scans jobs system-wide (no owner filter, by design -
/// see IJobRepository.ListDueOrOverdueAsync). Because the ApiFactory's
/// Mongo instance is shared across every [Fact] in this class, each
/// test's assertions are scoped to its own globally-unique owner/email/
/// job id rather than raw counts of the whole Sent list, so leftover
/// data from sibling tests can never produce a false pass or fail.
/// </summary>
[Collection(nameof(ApiFactory))]
public sealed class ReminderDigestServiceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReminderDigestServiceTests(ApiFactory factory) => _factory = factory;

    private sealed class StubDateTimeProvider : IDateTimeProvider
    {
        public DateOnly UtcToday { get; }
        public StubDateTimeProvider(DateOnly today) => UtcToday = today;
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public HashSet<string> FailFor { get; } = new();

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            if (FailFor.Contains(toEmail))
                throw new InvalidOperationException($"Simulated delivery failure for {toEmail}");
            Sent.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Builds a ReminderDigestService backed by the real Mongo-backed
    /// repositories from the ApiFactory host, but with a stubbed clock and
    /// a fake email sender so sent digests can be asserted on directly.
    /// </summary>
    private (ReminderDigestService service, FakeEmailSender emailSender, IJobRepository jobs,
        IOwnerProfileRepository profiles, IPropertyRepository properties) BuildService(
            DateOnly today, string frontendBaseUrl = "https://app.example.com")
    {
        var hostScope = _factory.Services.CreateScope();
        var jobs = hostScope.ServiceProvider.GetRequiredService<IJobRepository>();
        var profiles = hostScope.ServiceProvider.GetRequiredService<IOwnerProfileRepository>();
        var properties = hostScope.ServiceProvider.GetRequiredService<IPropertyRepository>();
        var emailSender = new FakeEmailSender();

        var services = new ServiceCollection();
        services.AddSingleton(jobs);
        services.AddSingleton(profiles);
        services.AddSingleton(properties);
        services.AddSingleton<IEmailSender>(emailSender);
        services.AddSingleton<IDateTimeProvider>(new StubDateTimeProvider(today));
        services.Configure<FrontendOptions>(o => o.BaseUrl = frontendBaseUrl);
        var provider = services.BuildServiceProvider();

        var service = new ReminderDigestService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ReminderDigestService>.Instance);

        return (service, emailSender, jobs, profiles, properties);
    }

    private static Job MakeJob(OwnerId owner, string propertyId, string name, DateOnly? dueDate)
        => Job.Create($"job-{Guid.NewGuid():N}", owner, propertyId, name, dueDate, new[] { "Step" });

    private static string UniqueEmail() => $"reminder-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task DueWithinThreeDays_And_Overdue_AreIncluded_FourDaysOut_IsExcluded()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) = BuildService(today);

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);

        var overdue = MakeJob(owner, property.Id, "Overdue job", today.AddDays(-1));
        var dueSoon = MakeJob(owner, property.Id, "Due in 3 days", today.AddDays(3));
        var tooFarOut = MakeJob(owner, property.Id, "Due in 4 days", today.AddDays(4));
        foreach (var job in new[] { overdue, dueSoon, tooFarOut })
            await jobs.AddAsync(job, CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        var mine = emailSender.Sent.Where(e => e.To == email).ToList();
        mine.Count.ShouldBe(1);
        mine[0].Html.ShouldContain(overdue.Id);
        mine[0].Html.ShouldContain(dueSoon.Id);
        mine[0].Html.ShouldNotContain(tooFarOut.Id);
    }

    [Fact]
    public async Task CompletedJob_IsExcluded()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) = BuildService(today);

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);

        var job = Job.Create(
            $"job-{Guid.NewGuid():N}", owner, property.Id, "Overdue but done", today.AddDays(-1), new[] { "Step" });
        job.TickStep(job.Steps[0].Id, DateTime.UtcNow);
        job.Complete(DateTime.UtcNow);
        await jobs.AddAsync(job, CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        emailSender.Sent.ShouldNotContain(e => e.To == email);
    }

    [Fact]
    public async Task RemindersDisabled_OwnerIsSkipped()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) = BuildService(today);

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);
        await profiles.UpdateRemindersEnabledAsync(owner, false, CancellationToken.None);
        await jobs.AddAsync(MakeJob(owner, property.Id, "Overdue job", today.AddDays(-1)), CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        emailSender.Sent.ShouldNotContain(e => e.To == email);
    }

    [Fact]
    public async Task NoCapturedEmail_OwnerIsSkipped()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, _, properties) = BuildService(today);

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        // No profiles.UpsertEmailAsync call - owner never had an email claim captured.
        var job = MakeJob(owner, property.Id, "Overdue job", today.AddDays(-1));
        await jobs.AddAsync(job, CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        emailSender.Sent.ShouldNotContain(e => e.Html.Contains(job.Id));
    }

    [Fact]
    public async Task OneOwnersDeliveryFailure_DoesNotBlockOthers()
    {
        var failingOwner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var okOwner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var failingEmail = UniqueEmail();
        var okEmail = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) = BuildService(today);

        var failingProperty = Property.Create($"prop-{Guid.NewGuid():N}", failingOwner, "House A");
        var okProperty = Property.Create($"prop-{Guid.NewGuid():N}", okOwner, "House B");
        await properties.AddAsync(failingProperty, CancellationToken.None);
        await properties.AddAsync(okProperty, CancellationToken.None);

        await profiles.UpsertEmailAsync(failingOwner, failingEmail, CancellationToken.None);
        await profiles.UpsertEmailAsync(okOwner, okEmail, CancellationToken.None);
        emailSender.FailFor.Add(failingEmail);

        await jobs.AddAsync(MakeJob(failingOwner, failingProperty.Id, "Job A", today.AddDays(-1)), CancellationToken.None);
        var okJob = MakeJob(okOwner, okProperty.Id, "Job B", today.AddDays(-1));
        await jobs.AddAsync(okJob, CancellationToken.None);

        await Should.NotThrowAsync(() => service.RunDigestPassAsync(CancellationToken.None));

        emailSender.Sent.ShouldNotContain(e => e.To == failingEmail);
        emailSender.Sent.ShouldContain(e => e.To == okEmail && e.Html.Contains(okJob.Id));
    }

    [Fact]
    public async Task DigestLinksPointToJobPages_AndSettingsPage()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) = BuildService(today);

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);
        var job = MakeJob(owner, property.Id, "Boiler service", today.AddDays(-1));
        await jobs.AddAsync(job, CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        var mine = emailSender.Sent.Single(e => e.To == email);
        mine.Html.ShouldContain($"https://app.example.com/jobs/{job.Id}");
        mine.Html.ShouldContain("https://app.example.com/settings/notifications");
    }

    [Fact]
    public async Task TrailingSlashOnBaseUrl_DoesNotProduceDoubleSlashInLinks()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) =
            BuildService(today, frontendBaseUrl: "https://app.example.com/");

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);
        var job = MakeJob(owner, property.Id, "Boiler service", today.AddDays(-1));
        await jobs.AddAsync(job, CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        var mine = emailSender.Sent.Single(e => e.To == email);
        mine.Html.ShouldContain($"https://app.example.com/jobs/{job.Id}");
        mine.Html.ShouldNotContain($"https://app.example.com//jobs/{job.Id}");
    }

    [Fact]
    public async Task TrailingWhitespaceAndSlashOnBaseUrl_DoesNotProduceMalformedLinks()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) =
            BuildService(today, frontendBaseUrl: "https://app.example.com/ ");

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);
        var job = MakeJob(owner, property.Id, "Boiler service", today.AddDays(-1));
        await jobs.AddAsync(job, CancellationToken.None);

        await service.RunDigestPassAsync(CancellationToken.None);

        var mine = emailSender.Sent.Single(e => e.To == email);
        mine.Html.ShouldContain($"href=\"https://app.example.com/jobs/{job.Id}\"");
    }

    [Fact]
    public async Task MissingBaseUrl_SkipsPass_AndDoesNotThrow()
    {
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var email = UniqueEmail();
        var today = new DateOnly(2026, 6, 1);
        var (service, emailSender, jobs, profiles, properties) = BuildService(today, frontendBaseUrl: "");

        var property = Property.Create($"prop-{Guid.NewGuid():N}", owner, "Main House");
        await properties.AddAsync(property, CancellationToken.None);
        await profiles.UpsertEmailAsync(owner, email, CancellationToken.None);
        var job = MakeJob(owner, property.Id, "Boiler service", today.AddDays(-1));
        await jobs.AddAsync(job, CancellationToken.None);

        await Should.NotThrowAsync(() => service.RunDigestPassAsync(CancellationToken.None));

        emailSender.Sent.ShouldNotContain(e => e.To == email);
    }
}
