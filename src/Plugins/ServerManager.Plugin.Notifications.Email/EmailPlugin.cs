namespace ServerManager.Plugin.Notifications.Email;

public static class EmailPlugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır; kanal kaydında sağlayıcı adı olarak saklanır.</summary>
    public const string SystemName = "Notifications.Email";

    public const string DisplayName = "E-posta (SMTP)";

    public const string HostKey = "Host";
    public const string PortKey = "Port";
    public const string SecurityKey = "Security";
    public const string UsernameKey = "Username";
    public const string PasswordKey = "Password";
    public const string FromKey = "From";
    public const string ToKey = "To";

    public const string SecurityStartTls = "starttls";
    public const string SecuritySsl = "ssl";
    public const string SecurityNone = "none";

    public const int MaxRecipients = 10;
}
