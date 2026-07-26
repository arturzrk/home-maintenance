using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HomeMaintenance.Integration.Tests.Jobs;

[Collection(nameof(ApiFactory))]
public sealed class JobRepositoryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public JobRepositoryTests(ApiFactory factory) => _factory = factory;

    private IJobRepository Repository()
        => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IJobRepository>();

    private static Job MakeJob(OwnerId owner, string name, DateOnly? dueDate)
        => Job.Create($"job-{Guid.NewGuid():N}", owner, "prop-1", name, dueDate, new[] { "Step" });

    [Fact]
    public async Task ListDueOrOverdue_ReturnsAcrossOwners_WithinHorizon_ExcludesLaterAndCompleted()
    {
        var repo = Repository();
        var ownerA = new OwnerId($"owner-{Guid.NewGuid():N}");
        var ownerB = new OwnerId($"owner-{Guid.NewGuid():N}");
        var horizon = new DateOnly(2026, 6, 4);

        var overdueA = MakeJob(ownerA, "Overdue A", new DateOnly(2026, 5, 30));
        var dueAtHorizonB = MakeJob(ownerB, "Due at horizon B", horizon);
        var laterA = MakeJob(ownerA, "Later A", horizon.AddDays(1));
        var noDueDateB = MakeJob(ownerB, "No due date B", null);

        var completed = Job.Create(
            $"job-{Guid.NewGuid():N}", ownerA, "prop-1", "Completed but overdue", new DateOnly(2026, 5, 30), new[] { "Step" });
        completed.TickStep(completed.Steps[0].Id, DateTime.UtcNow);
        completed.Complete(DateTime.UtcNow);

        foreach (var job in new[] { overdueA, dueAtHorizonB, laterA, noDueDateB, completed })
            await repo.AddAsync(job, CancellationToken.None);

        var due = await repo.ListDueOrOverdueAsync(horizon, CancellationToken.None);

        due.ShouldContain(j => j.Id == overdueA.Id);
        due.ShouldContain(j => j.Id == dueAtHorizonB.Id);
        due.ShouldNotContain(j => j.Id == laterA.Id);
        due.ShouldNotContain(j => j.Id == noDueDateB.Id);
        due.ShouldNotContain(j => j.Id == completed.Id);
    }
}
