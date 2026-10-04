namespace ServerManager.Application.Security;

public enum PortExposure
{
    /// <summary>Yalnızca sunucunun kendisi (127.0.0.0/8, ::1).</summary>
    Loopback = 1,

    /// <summary>0.0.0.0, :: veya * — tüm ağ arayüzleri.</summary>
    AllInterfaces = 2,

    PrivateAddress = 3,

    PublicAddress = 4
}
