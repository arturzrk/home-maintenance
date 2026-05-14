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
        services.AddScoped<CreatePropertyHandler>();
        services.AddScoped<RenamePropertyHandler>();
        services.AddScoped<ListPropertiesHandler>();
        services.AddScoped<GetPropertyHandler>();

        return services;
    }
}
