using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Queries;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.JobDefinitions;

public sealed class GetJobDefinitionHandlerTests
{
    private static readonly OwnerId Alice = new("alice");
    private static readonly OwnerId Bob = new("bob");

    private static JobDefinition MakeDefinition(OwnerId owner)
        => JobDefinition.Create(
            "def-1",
            owner,
            "prop-1",
            "Boiler service",
            new ScheduleDefinition(CadenceUnit.Month, 1, new DateOnly(2026, 1, 1)),
            Array.Empty<string>());

    private static IIdentityProvider IdentityFor(OwnerId owner)
    {
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(owner);
        return identity;
    }

    [Fact]
    public async Task GetById_Owned_ReturnsDtoSuccess()
    {
        var repo = Substitute.For<IJobDefinitionRepository>();
        repo.GetAsync("def-1", Alice, Arg.Any<CancellationToken>())
            .Returns(MakeDefinition(Alice));

        var handler = new GetJobDefinitionHandler(repo, IdentityFor(Alice));
        var result = await handler.Handle(new GetJobDefinitionQuery("def-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Id.ShouldBe("def-1");
        result.Value.Name.ShouldBe("Boiler service");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFoundError()
    {
        var repo = Substitute.For<IJobDefinitionRepository>();
        repo.GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>())
            .Returns((JobDefinition?)null);

        var handler = new GetJobDefinitionHandler(repo, IdentityFor(Alice));
        var result = await handler.Handle(new GetJobDefinitionQuery("missing"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task GetById_CrossOwner_ReturnsNotFoundError()
    {
        var repo = Substitute.For<IJobDefinitionRepository>();
        repo.GetAsync("def-1", Bob, Arg.Any<CancellationToken>())
            .Returns((JobDefinition?)null);

        var handler = new GetJobDefinitionHandler(repo, IdentityFor(Bob));
        var result = await handler.Handle(new GetJobDefinitionQuery("def-1"));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task List_ByOwner_ReturnsMappedDtos()
    {
        var repo = Substitute.For<IJobDefinitionRepository>();
        repo.ListAsync(Alice, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<JobDefinition> { MakeDefinition(Alice) });

        var handler = new ListJobDefinitionsHandler(repo, IdentityFor(Alice));
        var result = await handler.Handle(new ListJobDefinitionsQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        result.Value[0].Id.ShouldBe("def-1");
    }

    [Fact]
    public async Task List_FilteredByPropertyId_PassesFilterToRepository()
    {
        var repo = Substitute.For<IJobDefinitionRepository>();
        repo.ListAsync(Alice, "prop-1", null, Arg.Any<CancellationToken>())
            .Returns(new List<JobDefinition> { MakeDefinition(Alice) });

        var handler = new ListJobDefinitionsHandler(repo, IdentityFor(Alice));
        var result = await handler.Handle(new ListJobDefinitionsQuery("prop-1"));

        result.IsSuccess.ShouldBeTrue();
        await repo.Received(1).ListAsync(Alice, "prop-1", null, Arg.Any<CancellationToken>());
    }
}
