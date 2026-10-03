using ServerManager.Application.DTOs.Ssl;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public static class SslStatusClassifier
{
    public static SslCertificateStatus Classify(SslProbeResult probe, DateTime now, int expiringDays)
    {
        if (!probe.HasCertificate)
            return SslCertificateStatus.Error;

        if (probe.NotAfter!.Value <= now)
            return SslCertificateStatus.Expired;

        if (probe.ValidationError is not null)
            return SslCertificateStatus.Invalid;

        return DaysRemaining(probe.NotAfter.Value, now) < expiringDays
            ? SslCertificateStatus.Expiring
            : SslCertificateStatus.Valid;
    }

    public static int DaysRemaining(DateTime notAfter, DateTime now) =>
        (int)Math.Floor((notAfter - now).TotalDays);
}
