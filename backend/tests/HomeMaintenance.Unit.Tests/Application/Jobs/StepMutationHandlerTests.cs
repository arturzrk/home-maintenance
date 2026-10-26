using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Commands;
using HomeMaintenance.Application.Jobs.Queries;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Jobs;

public sealed class AddStepHandlerTests
{
    [Fact]
    public async Task Handle_OnActiveJob_AddsStep_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new AddStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new AddStepCommand(job.Id, "New step"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Steps.Count.ShouldBe(4);
        await jobs.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.StepAdded),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new AddStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new AddStepCommand("missing", "desc"));

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

        var handler = new AddStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new AddStepCommand(job.Id, "desc"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<BusinessRuleError>();
    }

    [Fact]
    public async Task Handle_EmptyDescription_ReturnsValidationError()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new AddStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new AddStepCommand(job.Id, ""));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }
}

public sealed class RemoveStepHandlerTests
{
    [Fact]
    public async Task Handle_ExistingStep_RemovesStep_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        var stepId = job.Steps[0].Id;
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new RemoveStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new RemoveStepCommand(job.Id, stepId));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Steps.Count.ShouldBe(2);
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.StepRemoved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownStep_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new RemoveStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new RemoveStepCommand(job.Id, "missing-step"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new RemoveStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new RemoveStepCommand("missing", "step"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }
}

public sealed class EditStepDescriptionHandlerTests
{
    [Fact]
    public async Task Handle_ExistingStep_EditsDescription_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        var stepId = job.Steps[0].Id;
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new EditStepDescriptionHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new EditStepDescriptionCommand(job.Id, stepId, "Updated"));

        result.IsSuccess.ShouldBeTrue();
        job.Steps[0].Description.ShouldBe("Updated");
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.StepDescriptionEdited),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyDescription_ReturnsValidationError()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        var stepId = job.Steps[0].Id;
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new EditStepDescriptionHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new EditStepDescriptionCommand(job.Id, stepId, ""));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public async Task Handle_UnknownStep_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new EditStepDescriptionHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new EditStepDescriptionCommand(job.Id, "missing-step", "Updated"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new EditStepDescriptionHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new EditStepDescriptionCommand("missing", "step", "Updated"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }
}

public sealed class ReorderStepsHandlerTests
{
    [Fact]
    public async Task Handle_ValidOrder_ReordersSteps_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        var reversedIds = job.Steps.Reverse().Select(s => s.Id).ToList();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new ReorderStepsHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new ReorderStepsCommand(job.Id, reversedIds));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Steps.Select(s => s.Id).ShouldBe(reversedIds);
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.StepReordered),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrongCount_ReturnsValidationError()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new ReorderStepsHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new ReorderStepsCommand(job.Id, new[] { job.Steps[0].Id }));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public async Task Handle_DuplicateId_ReturnsValidationError()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);
        var duplicated = Enumerable.Repeat(job.Steps[0].Id, job.Steps.Count).ToList();

        var handler = new ReorderStepsHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new ReorderStepsCommand(job.Id, duplicated));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsValidationError()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);
        var withUnknown = job.Steps.Skip(1).Select(s => s.Id).Append("unknown-step").ToList();

        var handler = new ReorderStepsHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new ReorderStepsCommand(job.Id, withUnknown));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new ReorderStepsHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new ReorderStepsCommand("missing", Array.Empty<string>()));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }
}

public sealed class UntickStepHandlerTests
{
    [Fact]
    public async Task Handle_TickedStep_UnticksStep_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        var stepId = job.Steps[0].Id;
        job.TickStep(stepId, DateTime.UtcNow);
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new UntickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UntickStepCommand(job.Id, stepId));

        result.IsSuccess.ShouldBeTrue();
        job.Steps[0].IsCompleted.ShouldBeFalse();
        await jobs.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.StepUnticked),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new UntickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UntickStepCommand("missing", "step"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Handle_UnknownStep_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new UntickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UntickStepCommand(job.Id, "missing-step"));

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

        var handler = new UntickStepHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UntickStepCommand(job.Id, job.Steps[0].Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<BusinessRuleError>();
    }
}

public sealed class UpdateJobHandlerTests
{
    [Fact]
    public async Task Handle_RenameOnly_RenamesJob_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new UpdateJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UpdateJobCommand(job.Id, "New Name", DueDateProvided: false, DueDate: null));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("New Name");
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.JobRenamed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DueDateProvided_SetsDueDate_AndEmitsAudit()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var handler = new UpdateJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UpdateJobCommand(job.Id, null, DueDateProvided: true, DueDate: newDate));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.DueDate.ShouldBe(newDate);
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.JobDueDateChanged),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NeitherFieldProvided_ReturnsValidationError()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();

        var handler = new UpdateJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UpdateJobCommand("job-1", null, DueDateProvided: false, DueDate: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        await jobs.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        jobs.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((Job?)null);

        var handler = new UpdateJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UpdateJobCommand("missing", "New Name", DueDateProvided: false, DueDate: null));

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

        var handler = new UpdateJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UpdateJobCommand(job.Id, "New Name", DueDateProvided: false, DueDate: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<BusinessRuleError>();
    }

    [Fact]
    public async Task Handle_InvalidName_ReturnsValidationError()
    {
        var (jobs, _, identity, audit, correlation) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.GetAsync(job.Id, TestFixtures.Alice, Arg.Any<CancellationToken>()).Returns(job);

        var handler = new UpdateJobHandler(jobs, identity, audit, correlation);
        var result = await handler.Handle(new UpdateJobCommand(job.Id, "", DueDateProvided: false, DueDate: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }
}

public sealed class ListJobsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsJobSummaries_FilteredByOwner()
    {
        var (jobs, _, identity, _, _) = TestFixtures.NewSubs();
        var job = TestFixtures.ActiveJob();
        jobs.ListAsync(TestFixtures.Alice, "prop-1", JobStatus.Active, null, Arg.Any<CancellationToken>())
            .Returns(new List<Job> { job });

        var handler = new ListJobsHandler(jobs, identity);
        var result = await handler.Handle(new ListJobsQuery("prop-1", JobStatus.Active));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Jobs.Count.ShouldBe(1);
        result.Value.Jobs[0].Id.ShouldBe(job.Id);
    }

    [Fact]
    public async Task Handle_NoMatches_ReturnsEmptyList()
    {
        var (jobs, _, identity, _, _) = TestFixtures.NewSubs();
        jobs.ListAsync(Arg.Any<OwnerId>(), Arg.Any<string?>(), Arg.Any<JobStatus?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Job>());

        var handler = new ListJobsHandler(jobs, identity);
        var result = await handler.Handle(new ListJobsQuery(null, null));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Jobs.ShouldBeEmpty();
    }
}
