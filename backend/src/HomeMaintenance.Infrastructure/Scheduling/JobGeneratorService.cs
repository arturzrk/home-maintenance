using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeMaintenance.Infrastructure.Scheduling;

/// <summary>
/// Background service that periodically generates upcoming Job occurrences
/// for every active JobDefinition. Runs an initial pass at startup (so
/// downtime gaps are filled), then repeats every 24 hours.
///
/// Uses <see cref="IServiceScopeFactory"/> because the dependencies it needs
/// (<see cref="IJobDefinitionRepository"/>, <see cref="JobGenerationService"/>)
/// are scoped, while a BackgroundService is a singleton.
/// </summary>
public sealed class JobGeneratorService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobGeneratorService> _logger;

    public JobGeneratorService(IServiceScopeFactory scopeFactory, ILogger<JobGeneratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunGenerationPassAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunGenerationPassAsync(stoppingToken);
    }

    /// <summary>
    /// Runs a single generation pass across all active job definitions.
    /// Public so it can be triggered deterministically from tests without
    /// waiting on the periodic timer.
    /// </summary>
    public async Task RunGenerationPassAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var definitionRepository = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();
        var generationService = scope.ServiceProvider.GetRequiredService<JobGenerationService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var definitions = await definitionRepository.ListAllActiveAsync(ct);
        var today = dateTimeProvider.UtcToday;

        foreach (var definition in definitions)
        {
            try
            {
                await generationService.GenerateForDefinition(definition, today, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job generation failed for definition {DefinitionId}", definition.Id);
            }
        }
    }
}
