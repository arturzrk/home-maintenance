using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeMaintenance.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services with the DI container.
/// Called from the API project's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(
            configuration.GetSection(MongoDbSettings.SectionName));

        services.AddSingleton<MongoDbContext>();

        // Repository implementations will be registered here as features are added.
        // e.g.: services.AddScoped<IPropertyRepository, MongoPropertyRepository>();

        return services;
    }
}
