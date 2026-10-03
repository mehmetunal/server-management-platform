namespace ServerManager.Domain.Enums;

public enum SslCertificateStatus
{
    Unknown = 0,
    Valid = 1,
    Expiring = 2,
    Expired = 3,
    Invalid = 4,
    Error = 5
}
