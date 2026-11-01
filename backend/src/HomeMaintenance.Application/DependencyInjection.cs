using HomeMaintenance.Application.Account.Handlers;
using HomeMaintenance.Application.Assets.Handlers;
using HomeMaintenance.Application.JobDefinitions.Handlers;
using HomeMaintenance.Application.Jobs.Handlers;
using HomeMaintenance.Application.Properties.Handlers;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace HomeMaintenance.Application;

/// <summary>
/// Registers Application layer services with the DI container.
/// Called from the API project's Program.cs.
/// </summary>
/// <remarks>
/// Excluded from code coverage: pure DI registration with no branches
/// or business logic to unit-test; its correctness is exercised
/// end-to-end by every integration test that boots the API.
/// </remarks>
[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Properties (WP03)
        services.AddScoped<CreatePropertyHandler>();
        services.AddScoped<RenamePropertyHandler>();
        services.AddScoped<ListPropertiesHandler>();
        services.AddScoped<GetPropertyHandler>();

        // Assets (008-assets WP01)
        services.AddScoped<CreateAssetHandler>();
        services.AddScoped<UpdateAssetHandler>();
        services.AddScoped<GetAssetHandler>();
        services.AddScoped<ListAssetsHandler>();

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

        // JobDefinitions (WP04 - 002-recurring-jobs)
        services.AddScoped<CreateJobDefinitionHandler>();
        services.AddScoped<ListJobDefinitionsHandler>();
        services.AddScoped<GetJobDefinitionHandler>();
        services.AddScoped<UpdateJobDefinitionHandler>();
        services.AddScoped<GenerateNextOccurrenceHandler>();

        // Account / notification preferences (WP01 - 011-reminders)
        services.AddScoped<GetNotificationPreferencesHandler>();
        services.AddScoped<UpdateNotificationPreferencesHandler>();

        return services;
    }
}
