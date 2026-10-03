using System.Net;
using Microsoft.Extensions.Options;
using ServerManager.Plugin.Notifications.Telegram;
using ServerManager.Plugin.Notifications.Tests.Fakes;

namespace ServerManager.Plugin.Notifications.Tests;

public class TelegramNotificationProviderTests
{
    private const string FakeToken = "123456789:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TelegramNotificationProvider Create(RecordingHttpHandler handler, string apiUrl = "http://127.0.0.1:9/") =>
        new(new StubHttpClientFactory(handler), Options.Create(new TelegramOptions { ApiUrl = apiUrl }));

    private static Dictionary<string, string> Settings(string token = FakeToken, string chatId = "-1001234567890") => new()
    {
        [TelegramPlugin.BotTokenKey] = token,
        [TelegramPlugin.ChatIdKey] = chatId
    };

    [Theory]
    [InlineData(FakeToken, "-1001234567890", 0)]
    [InlineData(FakeToken, "@ops_alerts", 0)]
    [InlineData("not-a-token", "-100", 1)]
    [InlineData(FakeToken, "@ab", 1)]
    [InlineData("1:short", "chat id", 2)]
    public void Validates_token_and_chat_id(string token, string chatId, int errorCount)
    {
        Assert.Equal(errorCount, Create(new RecordingHttpHandler()).Validate(Settings(token, chatId)).Count);
    }

    [Fact]
    public void Format_escapes_html_and_adds_link()
    {
        var text = TelegramNotificationProvider.Format(TestMessages.Create(title: "<b>x</b> & y", body: "a < b"));

        Assert.StartsWith("<b>&lt;b&gt;x&lt;/b&gt; &amp; y</b>", text);
        Assert.Contains("a &lt; b", text);
        Assert.Contains("<a href=\"https://panel.example.com/Alerts\">Panelde aç</a>", text);
    }

    [Fact]
    public void Format_keeps_turkish_characters_readable()
    {
        var text = TelegramNotificationProvider.Format(TestMessages.Create(body: "Sunucuya erişilemiyor; gönderildi \"çğış\""));

        Assert.Contains("Sunucuya erişilemiyor; gönderildi &quot;çğış&quot;", text);
        Assert.DoesNotContain("&#", text);
    }

    [Fact]
    public void Format_truncates_long_messages()
    {
        var text = TelegramNotificationProvider.Format(TestMessages.Create(body: new string('x', 10_000)));

        Assert.True(text.Length <= TelegramNotificationProvider.MaxMessageLength);
    }

    [Fact]
    public async Task Sends_to_configured_api_with_html_mode()
    {
        var handler = new RecordingHttpHandler();

        var result = await Create(handler).SendAsync(Settings(), TestMessages.Create(), Ct);

        Assert.True(result.IsSuccess);
        var (uri, body) = Assert.Single(handler.Requests);
        Assert.Equal($"http://127.0.0.1:9/bot{FakeToken}/sendMessage", uri.ToString());
        Assert.Contains("\"parse_mode\":\"HTML\"", body);
        Assert.Contains("\"chat_id\":\"-1001234567890\"", body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "geçersiz")]
    [InlineData(HttpStatusCode.TooManyRequests, "hız sınırı")]
    public async Task Failure_messages_never_contain_token(HttpStatusCode status, string expected)
    {
        var handler = new RecordingHttpHandler(status, "{\"ok\":false,\"description\":\"Unauthorized\"}");

        var result = await Create(handler).SendAsync(Settings(), TestMessages.Create(), Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains(expected, result.Message);
        Assert.DoesNotContain(FakeToken, result.Message);
    }

    [Fact]
    public async Task Rejects_non_http_api_url()
    {
        var handler = new RecordingHttpHandler();

        var result = await Create(handler, "file:///etc").SendAsync(Settings(), TestMessages.Create(), Ct);

        Assert.False(result.IsSuccess);
        Assert.Empty(handler.Requests);
    }
}
