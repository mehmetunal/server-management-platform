using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.Ssl;
using ServerManager.Application.DTOs.Uptime;
using ServerManager.Application.Validators.Alerting;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Validators;

public class AlertingValidatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<string>> InvalidAsync<T>(FluentValidation.IValidator<T> validator, T dto)
    {
        var result = await validator.ValidateAsync(dto, Ct);
        return result.Errors.Select(e => e.PropertyName).Distinct().ToList();
    }

    [Fact]
    public async Task Default_rule_form_is_valid()
    {
        Assert.Empty(await InvalidAsync(new AlertRuleFormDtoValidator(), new AlertRuleFormDto { Name = "CPU" }));
    }

    [Theory]
    [InlineData(AlertRuleKind.CpuUsage, 0, true)]
    [InlineData(AlertRuleKind.CpuUsage, 101, true)]
    [InlineData(AlertRuleKind.SslCertificateExpiry, 0, true)]
    [InlineData(AlertRuleKind.SslCertificateExpiry, 30, false)]
    [InlineData(AlertRuleKind.ServerOffline, 0, false)]
    [InlineData(AlertRuleKind.DeploymentFailed, -5, false)]
    public async Task Threshold_is_checked_only_for_kinds_that_use_it(AlertRuleKind kind, double threshold, bool invalid)
    {
        var errors = await InvalidAsync(new AlertRuleFormDtoValidator(), new AlertRuleFormDto { Name = "x", Kind = kind, Threshold = threshold });

        Assert.Equal(invalid, errors.Contains(nameof(AlertRuleFormDto.Threshold)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(5, false)]
    [InlineData(20000, true)]
    public async Task Repeat_interval_is_zero_or_at_least_five_minutes(int minutes, bool invalid)
    {
        var errors = await InvalidAsync(new AlertRuleFormDtoValidator(), new AlertRuleFormDto { Name = "x", RepeatIntervalMinutes = minutes });

        Assert.Equal(invalid, errors.Contains(nameof(AlertRuleFormDto.RepeatIntervalMinutes)));
    }

    [Fact]
    public async Task Rule_rejects_too_many_channels_and_empty_name()
    {
        var dto = new AlertRuleFormDto { Name = "", ChannelIds = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList() };

        var errors = await InvalidAsync(new AlertRuleFormDtoValidator(), dto);

        Assert.Contains(nameof(AlertRuleFormDto.Name), errors);
        Assert.Contains(nameof(AlertRuleFormDto.ChannelIds), errors);
    }

    [Fact]
    public async Task Http_check_requires_valid_url_and_codes()
    {
        var dto = new UptimeCheckFormDto { Name = "site", Url = "ftp://x", AcceptedStatusCodes = "abc" };

        var errors = await InvalidAsync(new UptimeCheckFormDtoValidator(), dto);

        Assert.Contains(nameof(UptimeCheckFormDto.Url), errors);
        Assert.Contains(nameof(UptimeCheckFormDto.AcceptedStatusCodes), errors);
        Assert.DoesNotContain(nameof(UptimeCheckFormDto.Host), errors);
    }

    [Fact]
    public async Task Tcp_check_requires_host_and_port_but_not_url()
    {
        var dto = new UptimeCheckFormDto { Name = "db", Type = UptimeCheckType.Tcp, Host = "bad host", Port = null };

        var errors = await InvalidAsync(new UptimeCheckFormDtoValidator(), dto);

        Assert.Contains(nameof(UptimeCheckFormDto.Host), errors);
        Assert.Contains(nameof(UptimeCheckFormDto.Port), errors);
        Assert.DoesNotContain(nameof(UptimeCheckFormDto.Url), errors);
    }

    [Fact]
    public async Task Valid_tcp_check_passes()
    {
        var dto = new UptimeCheckFormDto { Name = "db", Type = UptimeCheckType.Tcp, Host = "10.0.0.5", Port = 5432 };

        Assert.Empty(await InvalidAsync(new UptimeCheckFormDtoValidator(), dto));
    }

    [Theory]
    [InlineData(60, 60, true)]
    [InlineData(60, 0, true)]
    [InlineData(5, 2, true)]
    [InlineData(30, 10, false)]
    public async Task Timeout_must_be_shorter_than_interval(int interval, int timeout, bool invalid)
    {
        var dto = new UptimeCheckFormDto { Name = "site", Url = "https://example.com", IntervalSeconds = interval, TimeoutSeconds = timeout };

        var errors = await InvalidAsync(new UptimeCheckFormDtoValidator(), dto);

        Assert.Equal(invalid, errors.Contains(nameof(UptimeCheckFormDto.TimeoutSeconds)) || errors.Contains(nameof(UptimeCheckFormDto.IntervalSeconds)));
    }

    [Theory]
    [InlineData("example.com", 443, true)]
    [InlineData("", 443, false)]
    [InlineData("example.com", 0, false)]
    [InlineData("bad host", 443, false)]
    public async Task Ssl_form_checks_host_and_port(string host, int port, bool valid)
    {
        var errors = await InvalidAsync(new SslMonitorFormDtoValidator(), new SslMonitorFormDto { Host = host, Port = port });

        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public async Task Channel_form_requires_name_and_provider()
    {
        var errors = await InvalidAsync(new NotificationChannelFormDtoValidator(), new NotificationChannelFormDto());

        Assert.Contains(nameof(NotificationChannelFormDto.Name), errors);
        Assert.Contains(nameof(NotificationChannelFormDto.ProviderSystemName), errors);
    }
}
