namespace ServerManager.Application.Security;

public sealed class ListeningPort
{
    /// <summary>tcp veya udp.</summary>
    public string Protocol { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public int Port { get; init; }

    public string? Process { get; init; }

    public PortExposure Exposure { get; init; }

    public bool IsReachableFromNetwork => Exposure is PortExposure.AllInterfaces or PortExposure.PublicAddress or PortExposure.PrivateAddress;
}
