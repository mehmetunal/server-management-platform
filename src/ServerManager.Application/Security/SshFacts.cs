namespace ServerManager.Application.Security;

public sealed class SshFacts
{
    public const string EffectiveSource = "sshd -T";
    public const string ConfigFileSource = "sshd_config";

    /// <summary><see cref="EffectiveSource"/> (geçerli ayarlar, root gerekir), <see cref="ConfigFileSource"/> (dosyadan yaklaşık) veya boş.</summary>
    public string? Source { get; init; }

    /// <summary>Anahtarlar küçük harflidir (permitrootlogin, passwordauthentication...).</summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
}
