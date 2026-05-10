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
        // Use cases and handlers will be registered here as features are added.
        // e.g.: services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
