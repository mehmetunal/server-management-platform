namespace ServerManager.Domain.Enums;

/// <summary>Domain'in TLS'i nerede biter.</summary>
public enum DeploymentTlsMode
{
    /// <summary>Kilit Cloudflare'de kalır; sunucu yalnızca 80 dinler.</summary>
    Cloudflare = 1,

    /// <summary>Traefik HTTP-01 ile Let's Encrypt sertifikası alır.</summary>
    LetsEncrypt = 2,

    /// <summary>Kullanıcının yapıştırdığı sertifika ve özel anahtar.</summary>
    Custom = 3
}
