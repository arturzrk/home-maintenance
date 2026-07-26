using HomeMaintenance.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace HomeMaintenance.Infrastructure.Email;

/// <summary>
/// No-op sender selected whenever <c>Email:Provider</c> is "Log" (the
/// Development/CI default). Logs instead of sending so no real email
/// provider account or key is ever required to build, run, or test
/// locally or in CI.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Email not sent (Email:Provider=Log): To={ToEmail} Subject={Subject}",
            toEmail,
            subject);
        return Task.CompletedTask;
    }
}
