using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Commands;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using HomeMaintenance.Domain.Jobs;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.JobDefinitions;

public sealed class GenerateNextOccurrenceHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    // Fixed schedule: monthly from 2026-01-01
    private static JobDefinition MakeDefinition()
        => JobDefinition.Create(
            "def-1",
            Alice,
            "prop-1",
            "Boiler service",
            new ScheduleDefinition(CadenceUnit.Month, 1, new DateOnly(2026, 1, 1)),
            new[] { "Step A" });

    private static (
        IJobDefinitionRepository defs,
        IJobRepository jobs,
        IAuditLog audit,
        ICorrelationContext correlation,
        GenerateNextOccurrenceHandler handler) Build(JobDefinition? definition = null)
    {
        var defs = Substitute.For<IJobDefinitionRepository>();
        var jobs = Substitute.For<IJobRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");

        var def = definition ?? MakeDefinition();
        defs.GetAsync("def-1", Alice, Arg.Any<CancellationToken>()).Returns(def);

        var handler = new GenerateNextOccurrenceHandler(defs, jobs, identity, audit, correlation);
        return (defs, jobs, audit, correlation, handler);
    }

    [Fact]
    public async Task NoExistingJobs_UsesStartDate()
    {
        var (_, jobs, _, _, handler) = Build();
        jobs.LatestGeneratedJobDueDateAsync("def-1", Arg.Any<CancellationToken>())
            .Returns((DateOnly?)null);
        jobs.HasGeneratedJobForOccurrenceAsync("def-1", new DateOnly(2026, 1, 1), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await handler.Handle(new GenerateNextOccurrenceCommand("def-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.DueDate.ShouldBe(new DateOnly(2026, 1, 1));
        await jobs.Received(1).AddAsync(Arg.Is<Job>(j => j.DueDate == new DateOnly(2026, 1, 1)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistingJobs_UsesNextOccurrenceAfterLatest()
    {
        var (_, jobs, _, _, handler) = Build();
        jobs.LatestGeneratedJobDueDateAsync("def-1", Arg.Any<CancellationToken>())
            .Returns(new DateOnly(2026, 1, 1));
        jobs.HasGeneratedJobForOccurrenceAsync("def-1", new DateOnly(2026, 2, 1), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await handler.Handle(new GenerateNextOccurrenceCommand("def-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.DueDate.ShouldBe(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public async Task NextOccurrenceAlreadyExists_ReturnsBusinessRuleError()
    {
        var (_, jobs, _, _, handler) = Build();
        jobs.LatestGeneratedJobDueDateAsync("def-1", Arg.Any<CancellationToken>())
            .Returns((DateOnly?)null);
        jobs.HasGeneratedJobForOccurrenceAsync("def-1", new DateOnly(2026, 1, 1), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await handler.Handle(new GenerateNextOccurrenceCommand("def-1"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("business_rule");
        ((BusinessRuleError)result.Error).Rule.ShouldBe("next_occurrence_already_exists");
    }

    [Fact]
    public async Task DefinitionNotFound_ReturnsNotFoundError()
    {
        var defs = Substitute.For<IJobDefinitionRepository>();
        defs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((JobDefinition?)null);
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var handler = new GenerateNextOccurrenceHandler(
            defs,
            Substitute.For<IJobRepository>(),
            identity,
            Substitute.For<IAuditLog>(),
            Substitute.For<ICorrelationContext>());

        var result = await handler.Handle(new GenerateNextOccurrenceCommand("missing"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("not_found");
    }
}
