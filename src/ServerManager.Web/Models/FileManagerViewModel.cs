using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class FileManagerViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required FileListViewModel List { get; init; }

    public int MaxUploadMegabytes { get; init; }

    public Guid ServerId => Page.Server.Id;
}
