using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.DTOs.Templates;

namespace ServerManager.Web.Models;

public sealed class CloudProvisionViewModel
{
    public required CloudProvisionDto Form { get; init; }

    public required IReadOnlyList<CloudAccountOptionDto> Accounts { get; init; }

    public required IReadOnlyList<ServerTemplateOptionDto> Templates { get; init; }
}
