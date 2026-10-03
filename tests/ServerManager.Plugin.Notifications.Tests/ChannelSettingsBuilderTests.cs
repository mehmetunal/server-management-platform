using ServerManager.Application.Services;
using ServerManager.Plugin.Notifications.Email;

namespace ServerManager.Plugin.Notifications.Tests;

public class ChannelSettingsBuilderTests
{
    private static readonly EmailNotificationProvider Provider = new();

    private static Dictionary<string, string?> Input(Action<Dictionary<string, string?>>? change = null)
    {
        var input = new Dictionary<string, string?>
        {
            [EmailPlugin.HostKey] = " smtp.example.com ",
            [EmailPlugin.PortKey] = "587",
            [EmailPlugin.SecurityKey] = EmailPlugin.SecurityStartTls,
            [EmailPlugin.UsernameKey] = "mailer",
            [EmailPlugin.PasswordKey] = "",
            [EmailPlugin.FromKey] = "alarm@example.com",
            [EmailPlugin.ToKey] = "ops@example.com"
        };
        change?.Invoke(input);
        return input;
    }

    [Fact]
    public void Empty_secret_keeps_stored_value()
    {
        var stored = new Dictionary<string, string> { [EmailPlugin.PasswordKey] = "stored-secret" };

        var (settings, errors) = NotificationChannelService.BuildSettings(Provider, Input(), stored);

        Assert.Empty(errors);
        Assert.Equal("stored-secret", settings[EmailPlugin.PasswordKey]);
        Assert.Equal("smtp.example.com", settings[EmailPlugin.HostKey]);
    }

    [Fact]
    public void New_secret_replaces_stored_value()
    {
        var stored = new Dictionary<string, string> { [EmailPlugin.PasswordKey] = "old" };

        var (settings, _) = NotificationChannelService.BuildSettings(Provider, Input(i => i[EmailPlugin.PasswordKey] = "new"), stored);

        Assert.Equal("new", settings[EmailPlugin.PasswordKey]);
    }

    [Fact]
    public void Unknown_keys_are_dropped()
    {
        var (settings, _) = NotificationChannelService.BuildSettings(Provider, Input(i => i["Injected"] = "x"), null);

        Assert.DoesNotContain("Injected", settings.Keys);
    }

    [Fact]
    public void Missing_required_field_is_reported_with_form_key()
    {
        var (_, errors) = NotificationChannelService.BuildSettings(Provider, Input(i => i[EmailPlugin.HostKey] = " "), null);

        Assert.Contains(errors, e => e.PropertyName == $"Settings[{EmailPlugin.HostKey}]");
    }

    [Fact]
    public void Select_and_number_fields_are_checked()
    {
        var (_, errors) = NotificationChannelService.BuildSettings(Provider, Input(i =>
        {
            i[EmailPlugin.SecurityKey] = "plaintext";
            i[EmailPlugin.PortKey] = "abc";
        }), null);

        Assert.Contains(errors, e => e.PropertyName == $"Settings[{EmailPlugin.SecurityKey}]");
        Assert.Contains(errors, e => e.PropertyName == $"Settings[{EmailPlugin.PortKey}]");
    }

    [Fact]
    public void Provider_validation_errors_are_mapped_to_form_keys()
    {
        var (_, errors) = NotificationChannelService.BuildSettings(Provider, Input(i => i[EmailPlugin.FromKey] = "nobody"), null);

        Assert.Equal($"Settings[{EmailPlugin.FromKey}]", Assert.Single(errors).PropertyName);
    }

    [Fact]
    public void Defaults_fill_empty_optional_fields()
    {
        var (settings, errors) = NotificationChannelService.BuildSettings(Provider, Input(i => i[EmailPlugin.PortKey] = ""), null);

        Assert.Empty(errors);
        Assert.Equal("587", settings[EmailPlugin.PortKey]);
    }
}
