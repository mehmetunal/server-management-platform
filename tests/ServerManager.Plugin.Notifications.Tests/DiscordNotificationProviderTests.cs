using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServerManager.Domain.Enums;
using ServerManager.Plugin.Notifications.Discord;
using ServerManager.Plugin.Notifications.Tests.Fakes;

namespace ServerManager.Plugin.Notifications.Tests;

public class DiscordNotificationProviderTests
{
    private const string ValidWebhook = "https://discord.com/api/webhooks/123/abc";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DiscordNotificationProvider Create(RecordingHttpHandler? handler = null, DiscordOptions? options = null) =>
        new(new StubHttpClientFactory(handler ?? new RecordingHttpHandler(HttpStatusCode.NoContent, "")), Options.Create(options ?? new DiscordOptions()));

    [Theory]
    [InlineData(ValidWebhook, true)]
    [InlineData("https://canary.discord.com/api/webhooks/1/x", true)]
    [InlineData("http://discord.com/api/webhooks/1/x", false)]
    [InlineData("https://evil.com/api/webhooks/1/x", false)]
    [InlineData("https://discord.com.evil.com/api/webhooks/1/x", false)]
    [InlineData("https://user:pw@discord.com/api/webhooks/1/x", false)]
    [InlineData("https://discord.com/api/users/1", false)]
    [InlineData("not a url", false)]
    public void Webhook_must_target_allowed_discord_host(string url, bool valid)
    {
        Assert.Equal(valid, Create().ValidateWebhook(url) is null);
    }

    [Fact]
    public void Local_test_host_requires_explicit_allow_list_and_http_flag()
    {
        var options = new DiscordOptions { AllowedHosts = ["127.0.0.1:5199"], AllowHttp = true };

        Assert.Null(Create(options: options).ValidateWebhook("http://127.0.0.1:5199/api/webhooks/1/x"));
        Assert.NotNull(Create(options: options).ValidateWebhook("http://127.0.0.1:6000/api/webhooks/1/x"));
        Assert.NotNull(Create().ValidateWebhook("http://127.0.0.1:5199/api/webhooks/1/x"));
    }

    [Fact]
    public void Username_cannot_contain_discord()
    {
        var errors = Create().Validate(new Dictionary<string, string>
        {
            [DiscordPlugin.WebhookUrlKey] = ValidWebhook,
            [DiscordPlugin.UsernameKey] = "My Discord Bot"
        });

        Assert.Equal(DiscordPlugin.UsernameKey, Assert.Single(errors).PropertyName);
    }

    [Theory]
    [InlineData(NotificationKind.Recovery, AlertSeverity.Critical, 0x2F9E44)]
    [InlineData(NotificationKind.Test, AlertSeverity.Warning, 0x1C7ED6)]
    [InlineData(NotificationKind.Firing, AlertSeverity.Critical, 0xE03131)]
    [InlineData(NotificationKind.Reminder, AlertSeverity.Warning, 0xF08C00)]
    public void Color_reflects_kind_and_severity(NotificationKind kind, AlertSeverity severity, int expected)
    {
        Assert.Equal(expected, DiscordNotificationProvider.Color(TestMessages.Create(kind, severity)));
    }

    [Fact]
    public void Payload_disables_mentions_and_truncates()
    {
        var json = JsonSerializer.Serialize(DiscordNotificationProvider.BuildPayload(TestMessages.Create(title: new string('t', 400), body: "@everyone"), "Ops"));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var embed = root.GetProperty("embeds")[0];

        Assert.Equal(0, root.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
        Assert.Equal("Ops", root.GetProperty("username").GetString());
        Assert.True(embed.GetProperty("title").GetString()!.Length <= DiscordNotificationProvider.MaxTitleLength);
        Assert.Equal("https://panel.example.com/Alerts", embed.GetProperty("url").GetString());
        Assert.Equal("Kritik • web-01", embed.GetProperty("footer").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Send_refuses_disallowed_webhook_without_request()
    {
        var handler = new RecordingHttpHandler();

        var result = await Create(handler).SendAsync(new Dictionary<string, string> { [DiscordPlugin.WebhookUrlKey] = "https://evil.com/api/webhooks/1/x" }, TestMessages.Create(), Ct);

        Assert.False(result.IsSuccess);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Send_posts_embed_and_maps_not_found()
    {
        var ok = new RecordingHttpHandler(HttpStatusCode.NoContent, "");
        var gone = new RecordingHttpHandler(HttpStatusCode.NotFound, "{}");
        var settings = new Dictionary<string, string> { [DiscordPlugin.WebhookUrlKey] = ValidWebhook };

        var sent = await Create(ok).SendAsync(settings, TestMessages.Create(), Ct);
        var failed = await Create(gone).SendAsync(settings, TestMessages.Create(), Ct);

        Assert.True(sent.IsSuccess);
        Assert.Contains("\"embeds\"", Assert.Single(ok.Requests).Body);
        Assert.False(failed.IsSuccess);
        Assert.DoesNotContain("abc", failed.Message);
    }
}
