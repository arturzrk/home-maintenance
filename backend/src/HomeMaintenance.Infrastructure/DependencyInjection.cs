using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Infrastructure.AuditLog;
using HomeMaintenance.Infrastructure.Auth;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

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

        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.ConnectionString));

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return client.GetDatabase(settings.DatabaseName);
        });

        services.AddSingleton<MongoDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<IIdentityProvider, HttpContextIdentityProvider>();

        services.Configure<AuditLogOptions>(
            configuration.GetSection(AuditLogOptions.SectionName));
        services.AddSingleton<IAuditLog, FileAuditLog>();

        // Repository implementations will be registered here as features are added.
        // e.g.: services.AddScoped<IPropertyRepository, MongoPropertyRepository>();

        return services;
    }
}
