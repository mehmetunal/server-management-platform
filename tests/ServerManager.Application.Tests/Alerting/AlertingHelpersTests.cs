using ServerManager.Application.Alerting;
using ServerManager.Application.DTOs.Ssl;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Alerting;

public class AlertingHelpersTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, 200, true)]
    [InlineData("", 399, true)]
    [InlineData(null, 404, false)]
    [InlineData("200,204,301-302", 302, true)]
    [InlineData("200,204,301-302", 303, false)]
    [InlineData(" 200 - 299 ", 250, true)]
    public void Status_codes_are_matched_by_ranges(string? value, int code, bool expected)
    {
        Assert.Equal(expected, StatusCodeRanges.IsAccepted(value, code));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("300-200")]
    [InlineData("99")]
    [InlineData("600")]
    [InlineData("200-300-400")]
    [InlineData(",,,")]
    public void Invalid_status_code_ranges_are_rejected(string value)
    {
        Assert.False(StatusCodeRanges.TryParse(value, out _));
    }

    [Fact]
    public void Too_many_status_code_parts_are_rejected()
    {
        Assert.False(StatusCodeRanges.TryParse(string.Join(',', Enumerable.Range(200, 21)), out _));
    }

    [Theory]
    [InlineData("example.com", true)]
    [InlineData("sub.example.com.", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("[::1]", true)]
    [InlineData("-bad.com", false)]
    [InlineData("bad_host.com", false)]
    [InlineData("exa mple.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validates_host_names(string? host, bool expected)
    {
        Assert.Equal(expected, NetworkTargets.IsValidHost(host));
    }

    [Theory]
    [InlineData("https://example.com/health", true)]
    [InlineData("http://10.0.0.5:8080", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("https://user:pass@example.com", false)]
    [InlineData("example.com", false)]
    [InlineData("", false)]
    public void Validates_urls(string url, bool expected)
    {
        Assert.Equal(expected, NetworkTargets.TryValidateUrl(url, out var error));
        Assert.Equal(expected, error is null);
    }

    [Theory]
    [InlineData(31, 29, true)]
    [InlineData(16, 14, true)]
    [InlineData(14, 13, false)]
    [InlineData(8, 6.5, true)]
    [InlineData(2, 0.5, true)]
    [InlineData(1, -1, true)]
    [InlineData(-1, -2, false)]
    public void Ssl_reminds_once_per_expiry_step(double notified, double current, bool expected)
    {
        Assert.Equal(expected, SslExpirySteps.ShouldRemind(notified, current));
    }

    [Fact]
    public void Ssl_never_notified_does_not_remind()
    {
        Assert.False(SslExpirySteps.ShouldRemind(null, 2));
    }

    private static SslProbeResult Probe(DateTime? notAfter, string? validationError = null) =>
        new("example.com", "Test CA", Now.AddDays(-30), notAfter, "93.184.216.34", validationError, notAfter is null ? "Bağlantı kurulamadı." : null);

    [Fact]
    public void Ssl_classifier_maps_states()
    {
        Assert.Equal(SslCertificateStatus.Error, SslStatusClassifier.Classify(Probe(null), Now, 30));
        Assert.Equal(SslCertificateStatus.Expired, SslStatusClassifier.Classify(Probe(Now.AddMinutes(-1), "ad uyuşmuyor"), Now, 30));
        Assert.Equal(SslCertificateStatus.Invalid, SslStatusClassifier.Classify(Probe(Now.AddDays(90), "zincir güvenilmiyor"), Now, 30));
        Assert.Equal(SslCertificateStatus.Expiring, SslStatusClassifier.Classify(Probe(Now.AddDays(10)), Now, 30));
        Assert.Equal(SslCertificateStatus.Valid, SslStatusClassifier.Classify(Probe(Now.AddDays(90)), Now, 30));
    }

    [Fact]
    public void Ssl_days_remaining_rounds_down()
    {
        Assert.Equal(9, SslStatusClassifier.DaysRemaining(Now.AddDays(9.9), Now));
        Assert.Equal(-1, SslStatusClassifier.DaysRemaining(Now.AddHours(-1), Now));
    }
}
