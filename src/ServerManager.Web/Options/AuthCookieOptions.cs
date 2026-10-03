namespace ServerManager.Web.Options;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Auth";

    public int SessionTimeoutMinutes { get; set; } = 60;
}
