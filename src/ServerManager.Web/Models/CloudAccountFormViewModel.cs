using ServerManager.Application.DTOs.Cloud;

namespace ServerManager.Web.Models;

public sealed class CloudAccountFormViewModel
{
    public required CloudAccountFormDto Form { get; init; }

    public required IReadOnlyList<CloudProviderOptionDto> Providers { get; init; }

    public string? ProviderName { get; init; }
}
