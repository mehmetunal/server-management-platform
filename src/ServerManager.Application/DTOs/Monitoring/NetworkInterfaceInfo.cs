namespace ServerManager.Application.DTOs.Monitoring;

public sealed class NetworkInterfaceInfo
{
    public string Name { get; set; } = string.Empty;

    public bool IsVirtual { get; set; }

    public long RxBytes { get; set; }

    public long TxBytes { get; set; }

    public long RxPackets { get; set; }

    public long TxPackets { get; set; }

    public double RxBytesPerSecond { get; set; }

    public double TxBytesPerSecond { get; set; }

    public List<string> Addresses { get; set; } = [];
}
