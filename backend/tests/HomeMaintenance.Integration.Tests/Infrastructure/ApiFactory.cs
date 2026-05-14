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
        builder.UseEnvironment(_environment);
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = _mongoContainer.GetConnectionString(),
                ["MongoDB:DatabaseName"] = "home-maintenance-integration-tests",
            });
            config.AddInMemoryCollection(_overrides);
        });
    }

    public async Task InitializeAsync() => await _mongoContainer.StartAsync();

    public new async Task DisposeAsync() => await _mongoContainer.DisposeAsync();
}
