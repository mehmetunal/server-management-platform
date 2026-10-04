using ServerManager.Application.DTOs.Commands;
using ServerManager.Application.DTOs.ServerGroups;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.DTOs.Templates;

namespace ServerManager.Web.Models;

public sealed class CommandRunFormViewModel
{
    public required CommandRunRequestDto Form { get; init; }

    public required IReadOnlyList<ServerOptionDto> Servers { get; init; }

    public required IReadOnlyList<ServerGroupOptionDto> Groups { get; init; }

    public required IReadOnlyList<ServerTemplateOptionDto> Templates { get; init; }
}
