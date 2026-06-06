using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Commands;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.JobDefinitions;

public sealed class UpdateJobDefinitionHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    private static JobDefinition MakeDefinition(string[]? steps = null)
        => JobDefinition.Create(
            "def-1",
            Alice,
            "prop-1",
            "Boiler service",
            new ScheduleDefinition(CadenceUnit.Month, 1, new DateOnly(2026, 1, 1)),
            steps ?? new[] { "Step A", "Step B" });

    private static (IJobDefinitionRepository repo, IAuditLog audit, UpdateJobDefinitionHandler handler) Build(JobDefinition? definition = null)
    {
        var repo = Substitute.For<IJobDefinitionRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");

        var def = definition ?? MakeDefinition();
        repo.GetAsync("def-1", Alice, Arg.Any<CancellationToken>()).Returns(def);

        var handler = new UpdateJobDefinitionHandler(repo, identity, audit, correlation);
        return (repo, audit, handler);
    }

    [Fact]
    public async Task Rename_ValidName_Succeeds()
    {
        var (repo, _, handler) = Build();
        var result = await handler.Handle(new UpdateJobDefinitionCommand("def-1", Name: "New Name"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("New Name");
        await repo.Received(1).UpdateAsync(Arg.Any<JobDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_EmptyName_ReturnsValidationError()
    {
        var (_, _, handler) = Build();
        var result = await handler.Handle(new UpdateJobDefinitionCommand("def-1", Name: ""));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("validation");
    }

    [Fact]
    public async Task UpdateSchedule_ValidSchedule_Persists()
    {
        var (repo, _, handler) = Build();
        var newSchedule = new ScheduleDefinitionDto("Year", 1, new DateOnly(2026, 6, 1), null);

        var result = await handler.Handle(new UpdateJobDefinitionCommand("def-1", Schedule: newSchedule));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Schedule.Unit.ShouldBe("Year");
        await repo.Received(1).UpdateAsync(Arg.Any<JobDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddStep_AppendsToStepTemplates()
    {
        var (_, _, handler) = Build(MakeDefinition(Array.Empty<string>()));
        var result = await handler.Handle(new UpdateJobDefinitionCommand(
            "def-1", AddStepDescriptions: new[] { "New Step" }));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StepTemplates.Count.ShouldBe(1);
        result.Value.StepTemplates[0].Description.ShouldBe("New Step");
    }

    [Fact]
    public async Task RemoveStep_KnownId_Removes()
    {
        var def = MakeDefinition(new[] { "A", "B" });
        var idToRemove = def.StepTemplates[0].Id;
        var (_, _, handler) = Build(def);

        var result = await handler.Handle(new UpdateJobDefinitionCommand(
            "def-1", RemoveStepTemplateIds: new[] { idToRemove }));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.StepTemplates.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveStep_UnknownId_ReturnsNotFound()
    {
        var (_, _, handler) = Build();
        var result = await handler.Handle(new UpdateJobDefinitionCommand(
            "def-1", RemoveStepTemplateIds: new[] { "ghost-id" }));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task NotFound_ReturnsNotFoundError()
    {
        var repo = Substitute.For<IJobDefinitionRepository>();
        repo.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((JobDefinition?)null);
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        var handler = new UpdateJobDefinitionHandler(repo, identity, audit, correlation);

        var result = await handler.Handle(new UpdateJobDefinitionCommand("missing", Name: "New Name"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task NoMutationFields_ReturnsValidationError()
    {
        var (_, _, handler) = Build();
        var result = await handler.Handle(new UpdateJobDefinitionCommand("def-1"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("validation");
    }
}
