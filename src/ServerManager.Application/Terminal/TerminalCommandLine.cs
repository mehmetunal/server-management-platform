namespace ServerManager.Application.Terminal;

/// <param name="Text">Kullanıcının yazdığı satır; düzenleme izlenemediyse eksik olabilir.</param>
/// <param name="IsApproximate">Satır ok tuşu, Tab, geçmiş araması vb. ile değiştiyse true.</param>
public sealed record TerminalCommandLine(string Text, bool IsApproximate);
