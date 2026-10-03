using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class FileEditorViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required string Path { get; init; }

    public required string FileName { get; init; }

    public required string DirectoryPath { get; init; }

    public int MaxEditKilobytes { get; init; }

    public Guid ServerId => Page.Server.Id;
}
