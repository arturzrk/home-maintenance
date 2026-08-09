using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Infrastructure.AuditLog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Infrastructure.AuditLog;

/// <summary>
/// Unit tests for <see cref="AuditLogExtensions"/> provider selection and
/// fail-fast guards. These run without Docker / WebApplicationFactory because
/// they only exercise DI registration logic.
/// </summary>
public sealed class AuditLogExtensionsTests
{
    private static IConfiguration BuildConfig(string provider) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLog:Provider"] = provider,
            })
            .Build();

    private static IHostEnvironment DevelopmentEnv()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        return env;
    }

    private static IHostEnvironment ProductionEnv()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Production);
        return env;
    }

    // ── Happy paths ───────────────────────────────────────────────────────

    [Fact]
    public void FileProvider_In_Development_RegistersFileAuditLog()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuditLogging(BuildConfig("File"), DevelopmentEnv());
        var sp = services.BuildServiceProvider();

        var auditLog = sp.GetRequiredService<IAuditLog>();
        auditLog.ShouldBeOfType<FileAuditLog>();
    }

    // ── Fail-fast: unknown provider ───────────────────────────────────────

    [Fact]
    public void UnknownProvider_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddAuditLogging(BuildConfig("InvalidSink"), DevelopmentEnv()));
        ex.Message.ShouldContain("InvalidSink");
    }

    [Fact]
    public void UnknownProvider_ErrorMentionsValidValues()
    {
        var services = new ServiceCollection();
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddAuditLogging(BuildConfig("Blob"), DevelopmentEnv()));
        ex.Message.ShouldContain("File");
        ex.Message.ShouldContain("Mongo");
    }

    // ── Fail-fast: File in Production ─────────────────────────────────────

    [Fact]
    public void FileProvider_In_Production_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddAuditLogging(BuildConfig("File"), ProductionEnv()));
        ex.Message.ShouldContain("Production");
    }

    [Fact]
    public void FileProvider_ErrorMentionsPersistentStore()
    {
        var services = new ServiceCollection();
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddAuditLogging(BuildConfig("File"), ProductionEnv()));
        // Error must guide the operator to the correct fix.
        ex.Message.ShouldContain("Mongo");
    }
}
