using HomeMaintenance.Application.Jobs.Commands;
using HomeMaintenance.Application.Jobs.Queries;
using HomeMaintenance.Application.Properties.Commands;
using HomeMaintenance.Application.Properties.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace HomeMaintenance.Application;

/// <summary>
/// Registers Application layer services with the DI container.
/// Called from the API project's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Properties (WP03)
        services.AddScoped<CreatePropertyHandler>();
        services.AddScoped<RenamePropertyHandler>();
        services.AddScoped<ListPropertiesHandler>();
        services.AddScoped<GetPropertyHandler>();

        // Jobs (WP05)
        services.AddScoped<CreateJobHandler>();
        services.AddScoped<GetJobHandler>();
        services.AddScoped<ListJobsHandler>();
        services.AddScoped<TickStepHandler>();
        services.AddScoped<UntickStepHandler>();
        services.AddScoped<CompleteJobHandler>();

        // Job mutation (WP07)
        services.AddScoped<AddStepHandler>();
        services.AddScoped<RemoveStepHandler>();
        services.AddScoped<EditStepDescriptionHandler>();
        services.AddScoped<ReorderStepsHandler>();
        services.AddScoped<UpdateJobHandler>();

        return services;
    }
}
