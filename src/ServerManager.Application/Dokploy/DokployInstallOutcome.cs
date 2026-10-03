using ServerManager.Domain.Enums;

namespace ServerManager.Application.Dokploy;

/// <param name="Status">Boşsa <paramref name="Succeeded"/>'a göre başarılı veya başarısız sayılır.</param>
internal sealed record DokployInstallOutcome(
    bool Succeeded,
    string Message,
    string? Sha256,
    int? ExitCode,
    DokployInstallationStatus? Status = null);
