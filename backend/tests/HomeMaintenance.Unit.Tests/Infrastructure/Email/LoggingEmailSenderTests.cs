using HomeMaintenance.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Infrastructure.Email;

public sealed class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendAsync_DoesNotThrow_AndCompletesWithoutSending()
    {
        var sender = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);

        await Should.NotThrowAsync(
            () => sender.SendAsync("alice@example.com", "Reminder", "<p>hi</p>"));
    }
}
