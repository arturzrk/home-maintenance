using HomeMaintenance.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeMaintenance.Infrastructure.Email;

/// <summary>
/// Wires the <see cref="IEmailSender"/> implementation selected by
/// <c>Email:Provider</c>: "Log" (the Development/CI default) registers
/// <see cref="LoggingEmailSender"/>; "Resend" registers
/// <see cref="ResendEmailSender"/> as a typed HttpClient and fails fast
/// at startup if <c>Email:Resend:ApiKey</c> is missing, mirroring
/// <see cref="Auth.AuthenticationExtensions"/>'s misconfiguration check.
/// </summary>
public static class EmailExtensions
{
    public static IServiceCollection AddEmailSending(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var provider = configuration["Email:Provider"] ?? "Log";

        if (string.Equals(provider, "Resend", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = configuration["Email:Resend:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Email:Resend:ApiKey is required when Email:Provider is 'Resend'.");
            }

            services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.resend.com/");
            });
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }
}
