namespace ServerManager.Application.Security;

public sealed class FailedLoginFacts
{
    /// <summary>journal (son 24 saat), /var/log/auth.log veya /var/log/secure (son kayıtlar); okunamadıysa boş.</summary>
    public string? Source { get; init; }

    public int? Total { get; init; }

    public IReadOnlyList<LoginSourceCount> TopSources { get; init; } = [];

    /// <summary>fail2ban kurulu değilse boş.</summary>
    public bool? Fail2BanActive { get; init; }
}
