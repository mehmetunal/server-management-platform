using ServerManager.Application.Terminal;

namespace ServerManager.Web.Terminal;

/// <param name="Enter">Komutu gönderen tuş; onaylanırsa kabuğa bu karakter yazılır.</param>
/// <param name="Remainder">Aynı girdide Enter'dan sonra gelen ve onaya kadar bekletilen kısım (çok satırlı yapıştırma).</param>
public sealed record PendingTerminalConfirmation(
    string Token,
    TerminalCommandLine Line,
    char Enter,
    string Remainder,
    string Description,
    DateTime CreatedAt);
