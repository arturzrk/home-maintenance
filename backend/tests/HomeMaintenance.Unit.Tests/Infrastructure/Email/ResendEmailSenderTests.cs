using System.Net;
using System.Text.Json;
using HomeMaintenance.Infrastructure.Email;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Infrastructure.Email;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task SendAsync_PostsExpectedRequestShape_ToResendApi()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        var http = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.resend.com/") };
        var options = Options.Create(new EmailOptions
        {
            FromAddress = "reminders@maintained.house",
            Resend = new ResendOptions { ApiKey = "re_test_key" },
        });
        var sender = new ResendEmailSender(http, options);

        await sender.SendAsync("alice@example.com", "Reminder", "<p>hi</p>");

        fakeHandler.LastRequest.ShouldNotBeNull();
        fakeHandler.LastRequest!.Method.ShouldBe(HttpMethod.Post);
        fakeHandler.LastRequest.RequestUri.ShouldBe(new Uri("https://api.resend.com/emails"));
        fakeHandler.LastRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        fakeHandler.LastRequest.Headers.Authorization.Parameter.ShouldBe("re_test_key");

        var body = JsonSerializer.Deserialize<JsonElement>(fakeHandler.LastRequestBody!);
        body.GetProperty("from").GetString().ShouldBe("reminders@maintained.house");
        body.GetProperty("to")[0].GetString().ShouldBe("alice@example.com");
        body.GetProperty("subject").GetString().ShouldBe("Reminder");
        body.GetProperty("html").GetString().ShouldBe("<p>hi</p>");
    }

    [Fact]
    public async Task SendAsync_NonSuccessResponse_Throws()
    {
        var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized);
        var http = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.resend.com/") };
        var options = Options.Create(new EmailOptions
        {
            FromAddress = "reminders@maintained.house",
            Resend = new ResendOptions { ApiKey = "bad_key" },
        });
        var sender = new ResendEmailSender(http, options);

        await Should.ThrowAsync<HttpRequestException>(
            () => sender.SendAsync("alice@example.com", "Reminder", "<p>hi</p>"));
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
            => _statusCode = statusCode;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statusCode);
        }
    }
}
