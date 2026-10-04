namespace ServerManager.Domain.Enums;

public enum SecurityCheckStatus
{
    Pass = 1,
    Info = 2,
    Warning = 3,
    Critical = 4,

    /// <summary>Yetki yetersizliği veya araç eksikliği nedeniyle tespit edilemedi.</summary>
    Unknown = 5
}
