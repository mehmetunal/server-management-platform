namespace ServerManager.Web.Models;

/// <summary>Menüdeki genel bir bölümün (Docker, Terminal, Loglar…) sunucu seçildikten sonra açılacak sekmesi.</summary>
public sealed record ServerPickerSection(
    string Key,
    string Title,
    string Lead,
    string Icon,
    string TargetController,
    string TargetAction,
    string OpenLabel);
