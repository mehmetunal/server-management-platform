using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class TerminalDisplay
{
    public static string KindText(TerminalSessionKind kind) => kind switch
    {
        TerminalSessionKind.Server => "Sunucu",
        TerminalSessionKind.Container => "Container",
        _ => kind.ToString()
    };

    public static string CommandStatusText(TerminalCommandStatus status) => status switch
    {
        TerminalCommandStatus.Executed => "Çalıştırıldı",
        TerminalCommandStatus.Confirmed => "Onaylandı",
        TerminalCommandStatus.Cancelled => "İptal edildi",
        TerminalCommandStatus.Blocked => "Engellendi",
        _ => status.ToString()
    };

    public static string CommandStatusBadgeClass(TerminalCommandStatus status) => status switch
    {
        TerminalCommandStatus.Confirmed => "badge-warning",
        TerminalCommandStatus.Cancelled => "badge-neutral",
        TerminalCommandStatus.Blocked => "badge-danger",
        _ => "badge-success"
    };
}
