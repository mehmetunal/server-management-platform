using MimeKit;
using ServerManager.Plugin.Notifications.Email;
using ServerManager.Plugin.Notifications.Tests.Fakes;

namespace ServerManager.Plugin.Notifications.Tests;

public class EmailNotificationProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Settings(Action<Dictionary<string, string>>? change = null)
    {
        var settings = new Dictionary<string, string>
        {
            [EmailPlugin.HostKey] = "smtp.example.com",
            [EmailPlugin.PortKey] = "587",
            [EmailPlugin.SecurityKey] = EmailPlugin.SecurityStartTls,
            [EmailPlugin.FromKey] = "alarm@example.com",
            [EmailPlugin.ToKey] = "ops@example.com, admin@example.com"
        };
        change?.Invoke(settings);
        return settings;
    }

    [Fact]
    public void Valid_settings_pass()
    {
        Assert.Empty(new EmailNotificationProvider().Validate(Settings()));
    }

    [Theory]
    [InlineData(EmailPlugin.HostKey, "bad host")]
    [InlineData(EmailPlugin.PortKey, "70000")]
    [InlineData(EmailPlugin.FromKey, "not-an-address")]
    [InlineData(EmailPlugin.ToKey, "ops@example.com, broken")]
    [InlineData(EmailPlugin.ToKey, " , ")]
    public void Invalid_fields_are_reported(string key, string value)
    {
        var errors = new EmailNotificationProvider().Validate(Settings(s => s[key] = value));

        Assert.Equal(key, Assert.Single(errors).PropertyName);
    }

    [Fact]
    public void Too_many_recipients_are_rejected()
    {
        var many = string.Join(',', Enumerable.Range(0, EmailPlugin.MaxRecipients + 1).Select(i => $"u{i}@example.com"));

        Assert.Single(new EmailNotificationProvider().Validate(Settings(s => s[EmailPlugin.ToKey] = many)));
    }

    [Fact]
    public void Password_without_username_is_rejected()
    {
        var errors = new EmailNotificationProvider().Validate(Settings(s => s[EmailPlugin.PasswordKey] = "x"));

        Assert.Equal(EmailPlugin.UsernameKey, Assert.Single(errors).PropertyName);
    }

    [Fact]
    public void Recipients_are_split_trimmed_and_deduplicated()
    {
        Assert.Equal(["a@example.com", "b@example.com"], EmailNotificationProvider.SplitRecipients(" a@example.com; b@example.com ,A@example.com,, "));
    }

    [Fact]
    public void Message_is_plain_text_with_link()
    {
        var mail = EmailNotificationProvider.BuildMessage(TestMessages.Create(), "alarm@example.com", ["ops@example.com"]);

        Assert.Equal("[Kritik] web-01: Yüksek CPU", mail.Subject);
        Assert.Equal("ops@example.com", Assert.Single(mail.To.Mailboxes).Address);
        var text = Assert.IsType<TextPart>(mail.Body).Text;
        Assert.Contains("Panelde aç: https://panel.example.com/Alerts", text);
    }

    [Fact]
    public async Task Send_refuses_metadata_address()
    {
        var result = await new EmailNotificationProvider().SendAsync(Settings(s => s[EmailPlugin.HostKey] = "169.254.169.254"), TestMessages.Create(), Ct);

        Assert.False(result.IsSuccess);
    }
}
