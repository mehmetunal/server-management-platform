using ServerManager.Application.DTOs.ServerGroups;
using ServerManager.Application.DTOs.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerGroupFormViewModel
{
    public required ServerGroupFormDto Form { get; init; }

    public required IReadOnlyList<ServerOptionDto> Servers { get; init; }
}
