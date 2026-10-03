using ServerManager.Application.Docker;

namespace ServerManager.Application.DTOs.Docker;

public sealed class ContainerActionRequest
{
    public string Container { get; set; } = string.Empty;

    public DockerContainerAction Action { get; set; }

    /// <summary>Yalnızca silmede: çalışan container'ı da zorla siler.</summary>
    public bool Force { get; set; }

    /// <summary>Silme onayı: container adının birebir yazılmış hali.</summary>
    public string? ConfirmationName { get; set; }
}
