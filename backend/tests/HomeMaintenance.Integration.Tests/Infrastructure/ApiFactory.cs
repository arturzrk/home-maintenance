using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.MongoDb;

namespace HomeMaintenance.Integration.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory that spins up a real MongoDB container via Testcontainers
/// and wires the API against it. All integration tests inherit from this fixture.
///
/// Tests may override per-instance configuration (environment, in-memory keys)
/// via <see cref="WithEnvironment"/> and <see cref="WithSettings"/> before
/// calling <see cref="CreateClient(WebApplicationFactoryClientOptions?)"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017))
        .Build();

    private string _environment = "Development";
    private readonly Dictionary<string, string?> _overrides = new()
    {
        ["Auth:UseStub"] = "true",
        // Use the Mongo audit provider in all integration tests. This matches
        // the staging/production configuration and exercises the real sink.
        // MongoIndexInitializer creates the auditEvents collection on startup.
        ["AuditLog:Provider"] = "Mongo",
    };

    public ApiFactory WithEnvironment(string environment)
    {
        _environment = environment;
        return this;
    }

    public ApiFactory WithSettings(IDictionary<string, string?> settings)
    {
        foreach (var (key, value) in settings)
            _overrides[key] = value;
        return this;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // ConfigureHostConfiguration values are converted to command-line args
        // (--key=value) and passed to WebApplication.CreateBuilder(args), which
        // adds them as the highest-priority config source — beating appsettings.json.
        // This is essential for overrides that are read EAGERLY at service-registration
        // time (e.g. AuditLog:Provider in AddAuditLogging), because
        // ConfigureWebHost.ConfigureAppConfiguration runs AFTER service registration
        // in the WebApplication / DeferredHostBuilder flow.
        builder.UseEnvironment(_environment);
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = _mongoContainer.GetConnectionString(),
                ["MongoDB:DatabaseName"] = "home-maintenance-integration-tests",
            });
            config.AddInMemoryCollection(_overrides);
        });
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
    }

    public async Task InitializeAsync() => await _mongoContainer.StartAsync();

    public new async Task DisposeAsync() => await _mongoContainer.DisposeAsync();
}
