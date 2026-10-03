using ServerManager.Plugin.DevOps.Dokploy.Domain;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Core;

public static class DokployMappings
{
    public static DokployInstanceDto ToDto(this DokployInstance instance) => new()
    {
        BaseUrl = instance.BaseUrl,
        Port = DokployUrls.GetPort(instance.BaseUrl),
        HasApiKey = !string.IsNullOrEmpty(instance.EncryptedApiKey),
        Version = instance.Version,
        Status = instance.Status,
        StatusMessage = instance.StatusMessage,
        LastHealthCheckAt = instance.LastHealthCheckAt,
        LastResponseTimeMs = instance.LastResponseTimeMs,
        InstalledAt = instance.InstalledAt,
        InstalledByPanel = instance.InstallationId is not null
    };

    public static DokployInstallationDto ToDto(this DokployInstallation installation, bool includeOutput = false) => new()
    {
        Id = installation.Id,
        UserName = installation.UserName,
        RequestedVersion = installation.RequestedVersion,
        ScriptUrl = installation.ScriptUrl,
        ScriptSha256 = installation.ScriptSha256,
        Status = installation.Status,
        ExitCode = installation.ExitCode,
        FailureReason = installation.FailureReason,
        StartedAt = installation.StartedAt,
        CompletedAt = installation.CompletedAt,
        Output = includeOutput ? installation.Output : null
    };
}
