namespace ServerManager.Application.DTOs.Cloud;

public sealed class CloudProvisionDto
{
    public Guid AccountId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Size { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public Guid? TemplateId { get; set; }

    public string? SshPublicKey { get; set; }
}
