using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.JobDefinitions;

public sealed class JobDefinitionTests
{
    private static readonly OwnerId Owner = new("alice");
    private const string PropertyId = "prop-1";

    private static ScheduleDefinition MonthlySchedule()
        => new(CadenceUnit.Month, 1, new DateOnly(2026, 1, 1));

    private static JobDefinition NewDef(string[]? steps = null)
        => JobDefinition.Create(
            Guid.NewGuid().ToString("N"),
            Owner,
            PropertyId,
            "Annual boiler service",
            MonthlySchedule(),
            steps ?? new[] { "Shut off gas", "Check pressure" });

    [Fact]
    public void Create_ValidParams_SetsAllProperties()
    {
        var def = NewDef();

        def.Owner.ShouldBe(Owner);
        def.PropertyId.ShouldBe(PropertyId);
        def.Name.ShouldBe("Annual boiler service");
        def.Schedule.Unit.ShouldBe(CadenceUnit.Month);
        def.StepTemplates.Count.ShouldBe(2);
        def.StepTemplates.Select(s => s.Order).ShouldBe(new[] { 0, 1 });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_Throws(string invalid)
    {
        Should.Throw<ArgumentException>(() =>
            JobDefinition.Create("id", Owner, PropertyId, invalid, MonthlySchedule(), Array.Empty<string>()));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var longName = new string('x', 201);
        Should.Throw<ArgumentException>(() =>
            JobDefinition.Create("id", Owner, PropertyId, longName, MonthlySchedule(), Array.Empty<string>()));
    }

    [Fact]
    public void Rename_ValidName_UpdatesName()
    {
        var def = NewDef();
        def.Rename("Quarterly service");
        def.Name.ShouldBe("Quarterly service");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyName_Throws(string invalid)
    {
        var def = NewDef();
        Should.Throw<ArgumentException>(() => def.Rename(invalid));
    }

    [Fact]
    public void UpdateSchedule_ReplacesSchedule()
    {
        var def = NewDef();
        var newSchedule = new ScheduleDefinition(CadenceUnit.Year, 1, new DateOnly(2026, 6, 1));
        def.UpdateSchedule(newSchedule);
        def.Schedule.ShouldBe(newSchedule);
    }

    [Fact]
    public void AddStepTemplate_AppendsWithOrder()
    {
        var def = NewDef(Array.Empty<string>());
        def.AddStepTemplate("Step A");
        def.AddStepTemplate("Step B");

        def.StepTemplates.Count.ShouldBe(2);
        def.StepTemplates[0].Order.ShouldBe(0);
        def.StepTemplates[0].Description.ShouldBe("Step A");
        def.StepTemplates[1].Order.ShouldBe(1);
    }

    [Fact]
    public void RemoveStepTemplate_KnownId_RemovesAndRenumbers()
    {
        var def = NewDef(new[] { "A", "B", "C" });
        var idToRemove = def.StepTemplates[0].Id;

        var outcome = def.RemoveStepTemplate(idToRemove);

        outcome.ShouldBe(StepTemplateMutationOutcome.Success);
        def.StepTemplates.Count.ShouldBe(2);
        def.StepTemplates.Select(s => s.Order).ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public void RemoveStepTemplate_UnknownId_ReturnsNotFound()
    {
        var def = NewDef();
        var outcome = def.RemoveStepTemplate("does-not-exist");
        outcome.ShouldBe(StepTemplateMutationOutcome.StepTemplateNotFound);
    }

    [Fact]
    public void ReorderStepTemplates_ValidIds_Reorders()
    {
        var def = NewDef(new[] { "A", "B", "C" });
        var ids = def.StepTemplates.Select(s => s.Id).ToList();
        var reversed = ids.AsEnumerable().Reverse().ToList();

        var outcome = def.ReorderStepTemplates(reversed);

        outcome.ShouldBe(ReorderStepTemplatesOutcome.Success);
        def.StepTemplates[0].Description.ShouldBe("C");
        def.StepTemplates[1].Description.ShouldBe("B");
        def.StepTemplates[2].Description.ShouldBe("A");
        def.StepTemplates.Select(s => s.Order).ShouldBe(new[] { 0, 1, 2 });
    }

    [Fact]
    public void ReorderStepTemplates_WrongCount_ReturnsError()
    {
        var def = NewDef(new[] { "A", "B" });
        var ids = def.StepTemplates.Take(1).Select(s => s.Id).ToList();

        var outcome = def.ReorderStepTemplates(ids);

        outcome.ShouldBe(ReorderStepTemplatesOutcome.WrongCount);
    }

    [Fact]
    public void EditStepTemplateDescription_ValidId_Updates()
    {
        var def = NewDef(new[] { "Old description" });
        var id = def.StepTemplates[0].Id;

        var outcome = def.EditStepTemplateDescription(id, "New description");

        outcome.ShouldBe(StepTemplateMutationOutcome.Success);
        def.StepTemplates[0].Description.ShouldBe("New description");
    }

    [Fact]
    public void EditStepTemplateDescription_UnknownId_ReturnsNotFound()
    {
        var def = NewDef();
        var outcome = def.EditStepTemplateDescription("ghost-id", "Anything");
        outcome.ShouldBe(StepTemplateMutationOutcome.StepTemplateNotFound);
    }
}
