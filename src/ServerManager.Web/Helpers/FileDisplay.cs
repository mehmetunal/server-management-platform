using ServerManager.Application.DTOs.Files;
using ServerManager.Application.Files;

namespace ServerManager.Web.Helpers;

public static class FileDisplay
{
    public static string Icon(FileEntryDto entry) => entry.Kind switch
    {
        RemoteFileKind.Directory => "folder",
        RemoteFileKind.SymbolicLink => "link",
        _ => "document"
    };

    public static string IconClass(FileEntryDto entry) => entry.Kind switch
    {
        RemoteFileKind.Directory => "file-icon is-directory",
        RemoteFileKind.SymbolicLink => "file-icon is-link",
        _ => "file-icon"
    };

    public static string KindText(RemoteFileKind kind) => kind switch
    {
        RemoteFileKind.Directory => "Klasör",
        RemoteFileKind.SymbolicLink => "Sembolik bağlantı",
        RemoteFileKind.File => "Dosya",
        _ => "Özel dosya"
    };

    public static string Size(FileEntryDto entry) =>
        entry.IsDirectory ? "—" : MetricDisplay.Bytes(entry.Size);

    public static string KindCode(RemoteFileKind kind) => kind switch
    {
        RemoteFileKind.Directory => "directory",
        RemoteFileKind.SymbolicLink => "link",
        RemoteFileKind.File => "file",
        _ => "other"
    };
}
