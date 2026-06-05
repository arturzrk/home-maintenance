using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.JobDefinitions;

internal static class JobDefinitionMappings
{
    public static JobDefinitionDto ToDto(this JobDefinition def)
        => new(
            def.Id,
            def.PropertyId,
            def.Name,
            def.Schedule.ToDto(),
            def.StepTemplates.Select(st => st.ToDto()).ToList());

    public static ScheduleDefinitionDto ToDto(this ScheduleDefinition sched)
        => new(sched.Unit.ToString(), sched.Multiplier, sched.StartDate, sched.EndDate);

    public static StepTemplateDto ToDto(this StepTemplate st)
        => new(st.Id, st.Order, st.Description);
}
