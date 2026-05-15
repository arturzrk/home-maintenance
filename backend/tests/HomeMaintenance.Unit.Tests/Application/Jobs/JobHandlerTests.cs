using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Commands;
using HomeMaintenance.Application.Jobs.Queries;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Domain.Properties;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Jobs;

internal static class TestFixtures
{
    public static readonly OwnerId Alice = new("alice");

    public static (IJobRepository jobs, IPropertyRepository properties, IIdentityProvider identity, IAuditLog audit, ICorrelationContext correlation)
        NewSubs()
    {
        var jobs = Substitute.For<IJobRepository>();
        var properties = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");
        return (jobs, properties, identity, audit, correlation);
    }

    public static Job ActiveJob(int stepCount = 3)
    {
        var steps = Enumerable.Range(0, stepCount).Select(i => $"Step {i}");
        return Job.Create(
            Guid.NewGuid().ToString("N"),
            Alice,
            "prop-1",
            "Service boiler",
            null,
            steps);
    }
}

public sealed class CreateJobHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_PersistsJob_AndEmitsAudit()
    {
        var (jobs, properties, identity, audit, correlation) = TestFixtures.NewSubs();
        properties.GetAsync("prop-1", TestFixtures.Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", TestFixtures.Alice, "Main House"));

        var handler = new CreateJobHandler(jobs, properties, identity, audit, correlation);
        var result = await handler.Handle(new CreateJobCommand(
            "prop-1", "Service boiler", null, new[] { "Shut off gas" }));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.PropertyId.ShouldBe("prop-1");
        result.Value.Steps.Count.ShouldBe(1);

        await jobs.Received(1).AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.JobCreated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PropertyNotOwned_ReturnsNotFound()
    {
        var (jobs, properties, identity, audit, correlation) = TestFixtures.NewSubs();
        properties.GetAsync("prop-1", TestFixtures.Alice, Arg.Any<CancellationToken>())
            .Returns((Property?)null);

        var handler = new CreateJobHandler(jobs, properties, identity, audit, correlation);
        var result = await handler.Handle(new CreateJobCommand(
            "prop-1", "Service boiler", null, new[] { "X" }));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
        await jobs.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidName_ReturnsValidationError()
    {
        var (jobs, properties, identity, audit, correlation) = TestFixtures.NewSubs();
        properties.GetAsync("prop-1", TestFixtures.Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", TestFixtures.Alice, "Main House"));

        var handler = new CreateJobHandler(jobs, properties, identity, audit, correlation);
        var result = await handler.Handle(new CreateJobCommand("prop-1", "", null, Array.Empty<string>()));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        await jobs.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }
}

public sealed class GetJobHandlerTests
{
    [Fact]
    public async Task Handle_OwnedJob_ReturnsDetail()
    {
        var (jobs, _, identity, _, _) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new GetJobHandler(jobs, identity);
        var result = await handler.Handle(new GetJobQuery(job.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Id.ShouldBe(job.Id);
        result.Value.Steps.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Handle_UnknownOrNotOwned_ReturnsNotFound()
    {
        var (jobs, _, identity, _, _) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new GetJobHandler(jobs, identity);
        var result = await handler.Handle(new GetJobQuery("missing"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }
}

public sealed class TickStepHandlerTests
{
    [Fact]
    public async Task Handle_OnActiveJob_TicksStep_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        var stepId = job.Steps[0].Id;
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new TickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new TickStepCommand(job.Id, stepId));

        result.IsSuccess.ShouldBeTrue();
        job.Steps[0].IsCompleted.ShouldBeTrue();
        await jobs.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.StepTicked),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new TickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new TickStepCommand("missing-job", "step"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Handle_UnknownStep_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new TickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new TickStepCommand(job.Id, "missing-step"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Handle_CompletedJob_ReturnsBusinessRule()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        foreach (var s in job.Steps) job.TickStep(s.Id, DateTime.UtcNow);
        job.Complete(DateTime.UtcNow);
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new TickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new TickStepCommand(job.Id, job.Steps[0].Id));

        result.IsFailure.ShouldBeTrue();
        var err = result.Error.ShouldBeOfType<BusinessRuleError>();
        err.Rule.ShouldBe("job_completed");
    }
}

public sealed class CompleteJobHandlerTests
{
    [Fact]
    public async Task Handle_AllStepsTicked_TransitionsToCompleted()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        foreach (var s in job.Steps) job.TickStep(s.Id, DateTime.UtcNow);
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new CompleteJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new CompleteJobCommand(job.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe(JobStatus.Completed);
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.JobCompleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithIncompleteStep_ReturnsStepsIncomplete()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new CompleteJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new CompleteJobCommand(job.Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<BusinessRuleError>().Rule.ShouldBe("steps_incomplete");
    }

    [Fact]
    public async Task Handle_WithZeroSteps_ReturnsJobHasNoSteps()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob(stepCount: 0);
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new CompleteJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new CompleteJobCommand(job.Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<BusinessRuleError>().Rule.ShouldBe("job_has_no_steps");
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ReturnsAlreadyCompleted()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        foreach (var s in job.Steps) job.TickStep(s.Id, DateTime.UtcNow);
        job.Complete(DateTime.UtcNow);
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new CompleteJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new CompleteJobCommand(job.Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<BusinessRuleError>().Rule.ShouldBe("job_already_completed");
    }
}
