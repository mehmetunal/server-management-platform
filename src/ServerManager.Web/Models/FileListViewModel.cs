using ServerManager.Application.DTOs.Files;

namespace ServerManager.Web.Models;

public sealed class FileListViewModel
{
    public required Guid ServerId { get; init; }

    public FileListingDto? Listing { get; init; }

    public string? Error { get; init; }

    /// <summary>Liste alınamadığında kullanıcının açmak istediği yol.</summary>
    public string? RequestedPath { get; init; }

    public string CurrentPath => Listing?.Path ?? RequestedPath ?? string.Empty;
}
