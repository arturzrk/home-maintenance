namespace HomeMaintenance.Application.Common.Interfaces;

/// <summary>
/// Sends a single transactional email. Concrete implementations live in
/// Infrastructure: <c>ResendEmailSender</c> for staging/prod,
/// <c>LoggingEmailSender</c> (no-op, logs instead) for Development/CI so
/// no real email provider account is ever required to build or test.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
