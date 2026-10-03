namespace ServerManager.Application.DTOs.Ssl;

/// <summary>
/// Sertifika okunabildiyse <see cref="NotAfter"/> doludur; <see cref="ValidationError"/> zincir veya ad uyuşmazlığını,
/// <see cref="Error"/> bağlantının kurulamadığını anlatır.
/// </summary>
public sealed record SslProbeResult(
    string? Subject,
    string? Issuer,
    DateTime? NotBefore,
    DateTime? NotAfter,
    string? ResolvedAddress,
    string? ValidationError,
    string? Error)
{
    public bool HasCertificate => NotAfter is not null;
}
