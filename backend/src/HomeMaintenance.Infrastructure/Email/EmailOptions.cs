namespace HomeMaintenance.Infrastructure.Email;

/// <summary>
/// Bound from configuration section <c>Email</c>. <see cref="Provider"/>
/// selects the <see cref="Application.Common.Interfaces.IEmailSender"/>
/// implementation registered in <see cref="EmailExtensions"/> - "Log"
/// (the Development/CI default) or "Resend".
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Log";
    public string FromAddress { get; set; } = string.Empty;
    public ResendOptions Resend { get; set; } = new();
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
}
