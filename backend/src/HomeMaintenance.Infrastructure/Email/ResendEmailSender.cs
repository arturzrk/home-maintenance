using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeMaintenance.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace HomeMaintenance.Infrastructure.Email;

/// <summary>
/// Sends via Resend's email API. Registered as a typed HttpClient
/// (base address <c>https://api.resend.com/</c>) in
/// <see cref="EmailExtensions"/> when <c>Email:Provider</c> is "Resend".
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly EmailOptions _options;

    public ResendEmailSender(HttpClient http, IOptions<EmailOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = _options.FromAddress,
                to = new[] { toEmail },
                subject,
                html = htmlBody,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
