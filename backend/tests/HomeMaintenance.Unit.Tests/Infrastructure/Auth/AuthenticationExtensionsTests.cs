using HomeMaintenance.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Infrastructure.Auth;

/// <summary>
/// Direct coverage of the production-stub assertion in
/// <see cref="AuthenticationExtensions.AddAppAuthentication"/>.
///
/// The behaviour is also reproducible end-to-end via `dotnet run` with
/// `ASPNETCORE_ENVIRONMENT=Production` and `Auth:UseStub=true`, but the
/// `WebApplicationFactory` test fixture locks the environment too early
/// to drive that path from xUnit.
/// </summary>
public sealed class AuthenticationExtensionsTests
{
    [Fact]
    public void UseStub_InProduction_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:UseStub"] = "true",
            })
            .Build();
        var env = StubEnvironment("Production");

        var ex = Should.Throw<InvalidOperationException>(
            () => services.AddAppAuthentication(configuration, env));

        ex.Message.ShouldContain("Auth:UseStub");
        ex.Message.ShouldContain("Development");
    }

    [Fact]
    public void UseStub_InDevelopment_DoesNotThrow_AndRegistersDevStub()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:UseStub"] = "true",
            })
            .Build();
        var env = StubEnvironment("Development");

        Should.NotThrow(() => services.AddAppAuthentication(configuration, env));
    }

    [Fact]
    public void UseStub_False_RequiresGoogleClientId()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:UseStub"] = "false",
            })
            .Build();
        var env = StubEnvironment("Production");

        var ex = Should.Throw<InvalidOperationException>(
            () => services.AddAppAuthentication(configuration, env));

        ex.Message.ShouldContain("Auth:Google:ClientId");
    }

    [Fact]
    public void UseStub_False_WithClientId_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:UseStub"] = "false",
                ["Auth:Google:ClientId"] = "test-client-id.apps.googleusercontent.com",
            })
            .Build();
        var env = StubEnvironment("Production");

        Should.NotThrow(() => services.AddAppAuthentication(configuration, env));
    }

    private static IHostEnvironment StubEnvironment(string name)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }
}
