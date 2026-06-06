using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using HomeMaintenance.Infrastructure.Scheduling;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.JobDefinitions;

[Collection(nameof(ApiFactory))]
public sealed class JobGeneratorServiceTests : IClassFixture<ApiFactory>
{
    private static readonly OwnerId Alice = new("alice");

    private readonly ApiFactory _factory;

    public JobGeneratorServiceTests(ApiFactory factory) => _factory = factory;

    private sealed class StubDateTimeProvider : IDateTimeProvider
    {
        public DateOnly UtcToday { get; set; }
        public StubDateTimeProvider(DateOnly today) => UtcToday = today;
    }

    /// <summary>
    /// Builds a JobGeneratorService backed by the real Mongo-backed
    /// repositories from the ApiFactory host, but with a stubbed clock so
    /// "today" is deterministic. The service's own scope factory resolves
    /// against this small dedicated container rather than the host's.
    /// </summary>
    private (JobGeneratorService service, IJobDefinitionRepository definitions, IJobRepository jobs) BuildService(DateOnly today)
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

    private static JobDefinition MakeMonthlyDefinition(DateOnly startDate, DateOnly? endDate = null)
        => JobDefinition.Create(
            $"def-{Guid.NewGuid():N}",
            Alice,
            "prop-1",
            "Boiler service",
            new ScheduleDefinition(CadenceUnit.Month, 1, startDate, endDate),
            new[] { "Step A" });

    [Fact]
    public async Task Startup_GeneratesOccurrencesWithinHorizon()
    {
        var today = new DateOnly(2026, 6, 1);
        var (service, definitions, jobs) = BuildService(today);
        var definition = MakeMonthlyDefinition(today);
        await definitions.AddAsync(definition, CancellationToken.None);

        await service.RunGenerationPassAsync(CancellationToken.None);

        foreach (var expected in new[] { today, today.AddMonths(1), today.AddMonths(2), today.AddMonths(3) })
        {
            (await jobs.HasGeneratedJobForOccurrenceAsync(definition.Id, expected, CancellationToken.None))
                .ShouldBeTrue($"expected occurrence on {expected}");
        }
    }

    [Fact]
    public async Task SecondRun_ProducesNoDuplicates()
    {
        var today = new DateOnly(2026, 6, 1);
        var (service, definitions, _) = BuildService(today);
        var definition = MakeMonthlyDefinition(today);
        await definitions.AddAsync(definition, CancellationToken.None);

        await service.RunGenerationPassAsync(CancellationToken.None);
        var afterFirst = await CountGeneratedJobs(definition.Id, today);

        await service.RunGenerationPassAsync(CancellationToken.None);
        var afterSecond = await CountGeneratedJobs(definition.Id, today);

        afterSecond.ShouldBe(afterFirst);
    }

    [Fact]
    public async Task EndDate_InPast_GeneratesNoJobs()
    {
        var today = new DateOnly(2026, 6, 1);
        var (service, definitions, jobs) = BuildService(today);
        var definition = MakeMonthlyDefinition(new DateOnly(2026, 1, 1), endDate: today.AddDays(-1));
        await definitions.AddAsync(definition, CancellationToken.None);

        await service.RunGenerationPassAsync(CancellationToken.None);

        (await jobs.LatestGeneratedJobDueDateAsync(definition.Id, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task StartupRun_FillsGapFromDowntime()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var farAhead = startDate.AddDays(45);
        var (service, definitions, jobs) = BuildService(farAhead);
        var definition = MakeMonthlyDefinition(startDate);
        await definitions.AddAsync(definition, CancellationToken.None);

        await service.RunGenerationPassAsync(CancellationToken.None);

        var horizon = farAhead.AddMonths(3);
        foreach (var occurrence in definition.Schedule.OccurrencesInRange(farAhead, horizon))
        {
            (await jobs.HasGeneratedJobForOccurrenceAsync(definition.Id, occurrence, CancellationToken.None))
                .ShouldBeTrue($"expected occurrence on {occurrence}");
        }
    }

    private async Task<int> CountGeneratedJobs(string definitionId, DateOnly from)
    {
        var jobs = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IJobRepository>();
        var count = 0;
        for (var n = 0; n < 6; n++)
        {
            if (await jobs.HasGeneratedJobForOccurrenceAsync(definitionId, from.AddMonths(n), CancellationToken.None))
                count++;
        }
        return count;
    }
}
