namespace ServerManager.Domain.Enums;

public enum ManagedServiceVolumeMode
{
    /// <summary>Panelin oluşturduğu Docker volume'u (<c>sm-svc-&lt;slug&gt;-data</c>).</summary>
    NamedVolume = 1,

    /// <summary>Sunucudaki mutlak bir klasör (bind mount).</summary>
    HostPath = 2
}
