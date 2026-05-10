using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;

namespace HomeMaintenance.Integration.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory that spins up a real MongoDB container via Testcontainers
/// and wires the API against it. All integration tests inherit from this fixture.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = _mongoContainer.GetConnectionString(),
                ["MongoDB:DatabaseName"] = "home-maintenance-integration-tests"
            });
        });
    }

    public async Task InitializeAsync() => await _mongoContainer.StartAsync();

    public new async Task DisposeAsync() => await _mongoContainer.DisposeAsync();
}
