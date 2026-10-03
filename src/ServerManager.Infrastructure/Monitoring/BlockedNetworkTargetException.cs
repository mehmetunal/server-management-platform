namespace ServerManager.Infrastructure.Monitoring;

public sealed class BlockedNetworkTargetException : Exception
{
    public BlockedNetworkTargetException(string host)
        : base($"'{host}' izin verilmeyen bir ağ adresine çözümlendi (link-local, multicast veya belirsiz adres).")
    {
    }
}
