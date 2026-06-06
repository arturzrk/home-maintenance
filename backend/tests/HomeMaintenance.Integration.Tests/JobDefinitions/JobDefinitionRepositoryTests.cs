using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.JobDefinitions;

[Collection(nameof(ApiFactory))]
public sealed class JobDefinitionRepositoryTests : IClassFixture<ApiFactory>
{
    private static readonly OwnerId Alice = new("alice");
    private static readonly OwnerId Bob = new("bob");

    private readonly ApiFactory _factory;

    public JobDefinitionRepositoryTests(ApiFactory factory) => _factory = factory;

    private IJobDefinitionRepository Repository()
        => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IJobDefinitionRepository>();

    private static JobDefinition MakeDefinition(OwnerId owner, string id, string propertyId = "prop-1", string name = "Boiler service")
        => JobDefinition.Create(
            id,
            owner,
            propertyId,
            name,
            new ScheduleDefinition(CadenceUnit.Month, 1, new DateOnly(2026, 1, 1)),
            new[] { "Step A", "Step B" });

    [Fact]
    public async Task AddAndGet_OwnedDefinition_ReturnsCorrectData()
    {
        var repo = Repository();
        var definition = MakeDefinition(Alice, $"def-{Guid.NewGuid():N}");

        await repo.AddAsync(definition, CancellationToken.None);
        var loaded = await repo.GetAsync(definition.Id, Alice, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(definition.Id);
        loaded.Name.ShouldBe("Boiler service");
        loaded.PropertyId.ShouldBe("prop-1");
        loaded.Schedule.Unit.ShouldBe(CadenceUnit.Month);
        loaded.Schedule.Multiplier.ShouldBe(1);
        loaded.Schedule.StartDate.ShouldBe(new DateOnly(2026, 1, 1));
        loaded.StepTemplates.Count.ShouldBe(2);
        loaded.StepTemplates[0].Description.ShouldBe("Step A");
        loaded.StepTemplates[1].Description.ShouldBe("Step B");
    }

    [Fact]
    public async Task Get_CrossOwner_ReturnsNull()
    {
        var repo = Repository();
        var definition = MakeDefinition(Alice, $"def-{Guid.NewGuid():N}");
        await repo.AddAsync(definition, CancellationToken.None);

        var loaded = await repo.GetAsync(definition.Id, Bob, CancellationToken.None);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task List_ByOwner_ReturnsOwnedOnly()
    {
        var repo = Repository();
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var other = new OwnerId($"owner-{Guid.NewGuid():N}");
        var mine = MakeDefinition(owner, $"def-{Guid.NewGuid():N}");
        var theirs = MakeDefinition(other, $"def-{Guid.NewGuid():N}");
        await repo.AddAsync(mine, CancellationToken.None);
        await repo.AddAsync(theirs, CancellationToken.None);

        var list = await repo.ListAsync(owner, null, CancellationToken.None);

        list.ShouldContain(d => d.Id == mine.Id);
        list.ShouldNotContain(d => d.Id == theirs.Id);
    }

    [Fact]
    public async Task List_FilteredByPropertyId_ReturnsMatchingOnly()
    {
        var repo = Repository();
        var owner = new OwnerId($"owner-{Guid.NewGuid():N}");
        var matching = MakeDefinition(owner, $"def-{Guid.NewGuid():N}", propertyId: "prop-match");
        var other = MakeDefinition(owner, $"def-{Guid.NewGuid():N}", propertyId: "prop-other");
        await repo.AddAsync(matching, CancellationToken.None);
        await repo.AddAsync(other, CancellationToken.None);

        var list = await repo.ListAsync(owner, "prop-match", CancellationToken.None);

        list.ShouldContain(d => d.Id == matching.Id);
        list.ShouldNotContain(d => d.Id == other.Id);
    }

    [Fact]
    public async Task Update_RoundTrips_ScheduleAndStepTemplates()
    {
        var repo = Repository();
        var definition = MakeDefinition(Alice, $"def-{Guid.NewGuid():N}");
        await repo.AddAsync(definition, CancellationToken.None);

        definition.UpdateSchedule(new ScheduleDefinition(CadenceUnit.Week, 2, new DateOnly(2026, 3, 1), new DateOnly(2027, 1, 1)));
        definition.AddStepTemplate("Step C");
        definition.RemoveStepTemplate(definition.StepTemplates[0].Id);
        await repo.UpdateAsync(definition, CancellationToken.None);

        var reloaded = await repo.GetAsync(definition.Id, Alice, CancellationToken.None);

        reloaded.ShouldNotBeNull();
        reloaded.Schedule.Unit.ShouldBe(CadenceUnit.Week);
        reloaded.Schedule.Multiplier.ShouldBe(2);
        reloaded.Schedule.StartDate.ShouldBe(new DateOnly(2026, 3, 1));
        reloaded.Schedule.EndDate.ShouldBe(new DateOnly(2027, 1, 1));
        reloaded.StepTemplates.Select(s => s.Description).ShouldBe(new[] { "Step B", "Step C" });
    }

    [Fact]
    public async Task ListAllActive_ReturnsAllDefinitionsAcrossOwners()
    {
        var repo = Repository();
        var ownerA = new OwnerId($"owner-{Guid.NewGuid():N}");
        var ownerB = new OwnerId($"owner-{Guid.NewGuid():N}");
        var defA = MakeDefinition(ownerA, $"def-{Guid.NewGuid():N}");
        var defB = MakeDefinition(ownerB, $"def-{Guid.NewGuid():N}");
        await repo.AddAsync(defA, CancellationToken.None);
        await repo.AddAsync(defB, CancellationToken.None);

        var all = await repo.ListAllActiveAsync(CancellationToken.None);

        all.ShouldContain(d => d.Id == defA.Id);
        all.ShouldContain(d => d.Id == defB.Id);
    }
}
