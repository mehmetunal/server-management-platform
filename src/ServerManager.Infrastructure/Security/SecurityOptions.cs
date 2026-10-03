namespace ServerManager.Infrastructure.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string MasterKey { get; set; } = string.Empty;

    public int KeyVersion { get; set; } = 1;
}
