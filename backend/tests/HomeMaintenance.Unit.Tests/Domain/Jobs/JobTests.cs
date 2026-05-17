using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.Jobs;

public sealed class JobTests
{
    private static readonly OwnerId Owner = new("alice");
    private const string PropertyId = "prop-1";

    private static Job NewJob(IEnumerable<string>? steps = null, DateOnly? due = null)
        => Job.Create(
            Guid.NewGuid().ToString("N"),
            Owner,
            PropertyId,
            "Service boiler",
            due,
            steps ?? new[] { "Shut off gas", "Drain system", "Replace filter" });

    [Fact]
    public void Create_WithValidInputs_StartsActive_OrdersStepsContiguously()
    {
        var job = NewJob();

        job.Owner.ShouldBe(Owner);
        job.PropertyId.ShouldBe(PropertyId);
        job.Status.ShouldBe(JobStatus.Active);
        job.CompletedAt.ShouldBeNull();
        job.Steps.Count.ShouldBe(3);
        job.Steps.Select(s => s.Order).ShouldBe(new[] { 0, 1, 2 });
        job.Steps.All(s => !s.IsCompleted).ShouldBeTrue();
    }

    [Fact]
    public void Create_WithEmptyStepList_Succeeds()
    {
        var job = NewJob(steps: Array.Empty<string>());
        job.Steps.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_Throws(string invalid)
    {
        Should.Throw<ArgumentException>(() =>
            Job.Create("id", Owner, PropertyId, invalid, null, Array.Empty<string>()));
    }

    [Fact]
    public void Create_WithName201Chars_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            Job.Create("id", Owner, PropertyId, new string('x', 201), null, Array.Empty<string>()));
    }

    [Fact]
    public void Create_WithStepDescription501Chars_Throws()
    {
        var bad = new string('x', 501);
        Should.Throw<ArgumentException>(() =>
            Job.Create("id", Owner, PropertyId, "Job", null, new[] { bad }));
    }

    [Fact]
    public void Create_WithEmptyPropertyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            Job.Create("id", Owner, "", "Job", null, Array.Empty<string>()));
    }

    [Fact]
    public void Create_WithNullOwner_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            Job.Create("id", null!, PropertyId, "Job", null, Array.Empty<string>()));
    }

    [Fact]
    public void Rename_OnActiveJob_TrimsAndUpdates()
    {
        var job = NewJob();
        job.Rename("  New Name  ");
        job.Name.ShouldBe("New Name");
    }

    [Fact]
    public void Rename_OnCompletedJob_ThrowsInvalidOperation()
    {
        var job = NewJob();
        TickAll(job);
        job.Complete(DateTime.UtcNow);
        Should.Throw<InvalidOperationException>(() => job.Rename("New Name"));
    }

    [Fact]
    public void SetDueDate_OnActiveJob_Updates()
    {
        var job = NewJob();
        var due = new DateOnly(2026, 6, 1);
        job.SetDueDate(due);
        job.DueDate.ShouldBe(due);
    }

    [Fact]
    public void SetDueDate_OnCompletedJob_Throws()
    {
        var job = NewJob();
        TickAll(job);
        job.Complete(DateTime.UtcNow);
        Should.Throw<InvalidOperationException>(() => job.SetDueDate(null));
    }

    [Fact]
    public void AddStep_AppendsWithNextOrder()
    {
        var job = NewJob();
        var step = job.AddStep("New step");
        step.Order.ShouldBe(3);
        job.Steps.Count.ShouldBe(4);
    }

    [Fact]
    public void AddStep_OnCompletedJob_Throws()
    {
        var job = NewJob();
        TickAll(job);
        job.Complete(DateTime.UtcNow);
        Should.Throw<InvalidOperationException>(() => job.AddStep("New"));
    }

    [Fact]
    public void RemoveStep_RenumbersContiguously()
    {
        var job = NewJob();
        var middleId = job.Steps[1].Id;

        var outcome = job.RemoveStep(middleId);

        outcome.ShouldBe(StepMutationOutcome.Success);
        job.Steps.Count.ShouldBe(2);
        job.Steps.Select(s => s.Order).ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public void RemoveStep_UnknownId_ReturnsStepNotFound()
    {
        var job = NewJob();
        job.RemoveStep("nope").ShouldBe(StepMutationOutcome.StepNotFound);
        job.Steps.Count.ShouldBe(3);
    }

    [Fact]
    public void ReorderSteps_FullList_RenumbersToNewOrder()
    {
        var job = NewJob();
        var ids = job.Steps.Select(s => s.Id).ToList();
        var reversed = ids.AsEnumerable().Reverse().ToList();

        var outcome = job.ReorderSteps(reversed);

        outcome.ShouldBe(ReorderStepsOutcome.Success);
        job.Steps.Select(s => s.Id).ShouldBe(reversed);
        job.Steps.Select(s => s.Order).ShouldBe(new[] { 0, 1, 2 });
    }

    [Fact]
    public void ReorderSteps_WrongCount_Returns_WrongCount()
    {
        var job = NewJob();
        var partial = job.Steps.Take(2).Select(s => s.Id).ToList();
        job.ReorderSteps(partial).ShouldBe(ReorderStepsOutcome.WrongCount);
    }

    [Fact]
    public void ReorderSteps_Duplicate_ReturnsDuplicateId()
    {
        var job = NewJob();
        var id = job.Steps[0].Id;
        job.ReorderSteps(new[] { id, id, job.Steps[1].Id }).ShouldBe(ReorderStepsOutcome.DuplicateId);
    }

    [Fact]
    public void ReorderSteps_UnknownId_ReturnsUnknownId()
    {
        var job = NewJob();
        var ids = job.Steps.Select(s => s.Id).ToList();
        ids[1] = "nope";
        job.ReorderSteps(ids).ShouldBe(ReorderStepsOutcome.UnknownId);
    }

    [Fact]
    public void EditStepDescription_OnActiveJob_TrimsAndUpdates()
    {
        var job = NewJob();
        var id = job.Steps[1].Id;
        job.EditStepDescription(id, "  Updated  ").ShouldBe(StepMutationOutcome.Success);
        job.Steps[1].Description.ShouldBe("Updated");
    }

    [Fact]
    public void EditStepDescription_Empty_Throws()
    {
        var job = NewJob();
        var id = job.Steps[1].Id;
        Should.Throw<ArgumentException>(() => job.EditStepDescription(id, ""));
    }

    [Fact]
    public void TickStep_KnownId_SetsCompletedAt()
    {
        var job = NewJob();
        var id = job.Steps[0].Id;
        var now = DateTime.UtcNow;

        job.TickStep(id, now).ShouldBe(StepMutationOutcome.Success);

        job.Steps[0].IsCompleted.ShouldBeTrue();
        job.Steps[0].CompletedAt.ShouldBe(now);
    }

    [Fact]
    public void TickStep_Idempotent()
    {
        var job = NewJob();
        var id = job.Steps[0].Id;
        var first = DateTime.UtcNow;

        job.TickStep(id, first);
        job.TickStep(id, first.AddMinutes(5));

        // CompletedAt should NOT be updated on second tick (idempotent).
        job.Steps[0].CompletedAt.ShouldBe(first);
    }

    [Fact]
    public void TickStep_UnknownId_ReturnsStepNotFound()
    {
        var job = NewJob();
        job.TickStep("nope", DateTime.UtcNow).ShouldBe(StepMutationOutcome.StepNotFound);
    }

    [Fact]
    public void UntickStep_ClearsCompletedAt()
    {
        var job = NewJob();
        var id = job.Steps[0].Id;
        job.TickStep(id, DateTime.UtcNow);

        job.UntickStep(id).ShouldBe(StepMutationOutcome.Success);

        job.Steps[0].IsCompleted.ShouldBeFalse();
        job.Steps[0].CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void Complete_AllStepsDone_TransitionsToCompleted()
    {
        var job = NewJob();
        TickAll(job);
        var when = DateTime.UtcNow;

        job.Complete(when).ShouldBe(CompleteJobOutcome.Success);

        job.Status.ShouldBe(JobStatus.Completed);
        job.CompletedAt.ShouldBe(when);
    }

    [Fact]
    public void Complete_AnyStepIncomplete_ReturnsStepsIncomplete()
    {
        var job = NewJob();
        job.TickStep(job.Steps[0].Id, DateTime.UtcNow);
        // Second and third steps still unticked.

        job.Complete(DateTime.UtcNow).ShouldBe(CompleteJobOutcome.StepsIncomplete);
        job.Status.ShouldBe(JobStatus.Active);
    }

    [Fact]
    public void Complete_NoSteps_ReturnsNoSteps()
    {
        var job = NewJob(steps: Array.Empty<string>());
        job.Complete(DateTime.UtcNow).ShouldBe(CompleteJobOutcome.NoSteps);
        job.Status.ShouldBe(JobStatus.Active);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ReturnsAlreadyCompleted()
    {
        var job = NewJob();
        TickAll(job);
        job.Complete(DateTime.UtcNow);

        job.Complete(DateTime.UtcNow).ShouldBe(CompleteJobOutcome.AlreadyCompleted);
    }

    [Fact]
    public void Mutation_OnCompletedJob_ThrowsEnsureActive()
    {
        var job = NewJob();
        TickAll(job);
        job.Complete(DateTime.UtcNow);

        Should.Throw<InvalidOperationException>(() => job.AddStep("x"));
        Should.Throw<InvalidOperationException>(() => job.RemoveStep("any"));
        Should.Throw<InvalidOperationException>(() => job.TickStep("any", DateTime.UtcNow));
        Should.Throw<InvalidOperationException>(() => job.UntickStep("any"));
        Should.Throw<InvalidOperationException>(() => job.EditStepDescription("any", "x"));
        Should.Throw<InvalidOperationException>(() => job.ReorderSteps(new[] { "any" }));
    }

    private static void TickAll(Job job)
    {
        foreach (var step in job.Steps.ToList())
            job.TickStep(step.Id, DateTime.UtcNow);
    }
}
