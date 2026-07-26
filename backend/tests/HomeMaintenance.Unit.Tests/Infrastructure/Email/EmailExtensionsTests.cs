using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Infrastructure.Email;

/// <summary>
/// Direct coverage of the provider-selection and misconfiguration
/// assertion in <see cref="EmailExtensions.AddEmailSending"/>, mirroring
/// <c>AuthenticationExtensionsTests</c>'s coverage of the analogous
/// Auth:UseStub check.
/// </summary>
public sealed class EmailExtensionsTests
{
    [Fact]
    public void NoProviderConfigured_DefaultsToLog_RegistersLoggingSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();

        Should.NotThrow(() => services.AddEmailSending(configuration));

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEmailSender>().GetType().Name.ShouldBe("LoggingEmailSender");
    }

    [Fact]
    public void ProviderResend_MissingApiKey_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Resend",
            }).Build();

        var ex = Should.Throw<InvalidOperationException>(
            () => services.AddEmailSending(configuration));

        ex.Message.ShouldContain("Email:Resend:ApiKey");
    }

    [Fact]
    public void ProviderResend_WithApiKey_DoesNotThrow_RegistersResendSender()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Resend",
                ["Email:Resend:ApiKey"] = "re_test_key",
                ["Email:FromAddress"] = "reminders@maintained.house",
            }).Build();

        Should.NotThrow(() => services.AddEmailSending(configuration));

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEmailSender>().GetType().Name.ShouldBe("ResendEmailSender");
    }
}
