using System.Net;
using System.Text;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeMaintenance.Infrastructure.Scheduling;

/// <summary>
/// Background service that sends one digest email per owner covering
/// every active job due within <see cref="HorizonDays"/> days or
/// already overdue. Runs an initial pass at startup, then repeats
/// every 24 hours. Structurally mirrors <see cref="JobGeneratorService"/>:
/// <see cref="IServiceScopeFactory"/> because the dependencies are
/// scoped while this service is a singleton, and a public
/// <see cref="RunDigestPassAsync"/> so tests can trigger a pass
/// deterministically instead of waiting on the timer.
/// </summary>
public sealed class ReminderDigestService : BackgroundService
{
    private const int HorizonDays = 3;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderDigestService> _logger;

    public ReminderDigestService(IServiceScopeFactory scopeFactory, ILogger<ReminderDigestService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunDigestPassAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunDigestPassAsync(stoppingToken);
    }

    /// <summary>
    /// Runs a single digest pass across all owners with a qualifying job.
    /// Public so it can be triggered deterministically from tests without
    /// waiting on the periodic timer.
    /// </summary>
    public async Task RunDigestPassAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var profileRepository = scope.ServiceProvider.GetRequiredService<IOwnerProfileRepository>();
        var propertyRepository = scope.ServiceProvider.GetRequiredService<IPropertyRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var frontendBaseUrl = scope.ServiceProvider.GetRequiredService<IOptions<FrontendOptions>>().Value.BaseUrl;

        var today = dateTimeProvider.UtcToday;
        var horizon = today.AddDays(HorizonDays);

        var dueJobs = await jobRepository.ListDueOrOverdueAsync(horizon, ct);

        foreach (var group in dueJobs.GroupBy(j => j.Owner))
        {
            try
            {
                await SendDigestForOwnerAsync(
                    group.Key, group.ToList(), profileRepository, propertyRepository, emailSender, today, frontendBaseUrl, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder digest failed for owner {OwnerId}", group.Key.Value);
            }
        }
    }

    private static async Task SendDigestForOwnerAsync(
        OwnerId owner,
        IReadOnlyList<Job> jobs,
        IOwnerProfileRepository profileRepository,
        IPropertyRepository propertyRepository,
        IEmailSender emailSender,
        DateOnly today,
        string frontendBaseUrl,
        CancellationToken ct)
    {
        var profile = await profileRepository.GetAsync(owner, ct);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Email) || !profile.RemindersEnabled)
            return;

        var sections = new List<(string PropertyName, IReadOnlyList<Job> Jobs)>();
        foreach (var group in jobs.GroupBy(j => j.PropertyId))
        {
            var property = await propertyRepository.GetAsync(group.Key, owner, ct);
            sections.Add((property?.Name ?? "Property", group.ToList()));
        }

        var subject = jobs.Count == 1
            ? "1 maintenance job needs your attention"
            : $"{jobs.Count} maintenance jobs need your attention";
        var html = BuildHtmlBody(sections, today, frontendBaseUrl);

        await emailSender.SendAsync(profile.Email, subject, html, ct);
    }

    private static string BuildHtmlBody(
        IReadOnlyList<(string PropertyName, IReadOnlyList<Job> Jobs)> sections,
        DateOnly today,
        string frontendBaseUrl)
    {
        var sb = new StringBuilder();
        sb.Append("<html><body>");
        sb.Append("<p>Here is what is due or overdue across your properties:</p>");

        foreach (var (propertyName, jobs) in sections)
        {
            sb.Append("<h3>").Append(WebUtility.HtmlEncode(propertyName)).Append("</h3><ul>");
            foreach (var job in jobs)
            {
                var status = job.DueDate!.Value < today
                    ? "Overdue"
                    : $"Due {job.DueDate.Value:yyyy-MM-dd}";
                sb.Append("<li><a href=\"").Append(frontendBaseUrl).Append("/jobs/").Append(job.Id).Append("\">")
                    .Append(WebUtility.HtmlEncode(job.Name)).Append("</a> - ").Append(status).Append("</li>");
            }
            sb.Append("</ul>");
        }

        sb.Append("<p><a href=\"").Append(frontendBaseUrl)
            .Append("/settings/notifications\">Turn reminder emails off</a></p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
