using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Plugin.DevOps.Dokku.Services;

namespace ServerManager.Plugin.DevOps.Dokku.Integration;

public sealed class SshDokkuProvider : IDokkuProvider
{
    private readonly IRemoteCommandRunner _runner;

    public SshDokkuProvider(IRemoteCommandRunner runner)
    {
        _runner = runner;
    }

    public Task<ServiceResult<DokkuReport>> GetReportAsync(RemoteExecutionContext context, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        _runner.RunAsync<DokkuReport>(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(DokkuCommands.Probe, timeout, Elevate: true), ct);
            if (!output.IsSuccess)
                return ServiceResult<DokkuReport>.Failure(DescribeFailure(output));

            var report = DokkuReportParser.Parse(output.Stdout);
            return report is null
                ? ServiceResult<DokkuReport>.Failure("Dokku durumu okunamadı.")
                : ServiceResult<DokkuReport>.Success(report);
        }, cancellationToken);

    public async Task<ServiceResult> RestartAsync(RemoteExecutionContext context, string app, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync<bool>(context, async (executor, ct) =>
        {
            if (!DokkuCommands.IsAppName(app))
                return ServiceResult<bool>.Failure("Uygulama adı geçersiz.");

            var output = await executor.ExecuteAsync(new RemoteCommand(DokkuCommands.Restart(app), timeout, Elevate: true), ct);
            return output.IsSuccess
                ? ServiceResult<bool>.Success(true, $"{app} yeniden başlatıldı.")
                : ServiceResult<bool>.Failure(DescribeFailure(output));
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success(result.Message) : ServiceResult.Failure(result.Message ?? "Uygulama yeniden başlatılamadı.");
    }

    public async Task<ServiceResult> InstallAsync(
        RemoteExecutionContext context,
        string version,
        TimeSpan timeout,
        Func<string, CancellationToken, Task> onOutput,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync<bool>(context, async (executor, ct) =>
        {
            if (!DokkuCommands.IsVersion(version))
                return ServiceResult<bool>.Failure("Dokku sürümü geçersiz. Ayarlarda v0.38.31 biçiminde bir sürüm olmalı.");

            var output = await executor.ExecuteStreamingAsync(
                new RemoteCommand(DokkuCommands.Install(version), timeout, Elevate: true),
                onOutput,
                ct);

            return output.IsSuccess
                ? ServiceResult<bool>.Success(true, $"Dokku {version} kuruldu.")
                : ServiceResult<bool>.Failure(output.TimedOut ? "Dokku kurulumu zaman aşımına uğradı." : DescribeFailure(output));
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success(result.Message) : ServiceResult.Failure(result.Message ?? "Dokku kurulumu başarısız.");
    }

    private static string DescribeFailure(RemoteCommandOutput output)
    {
        if (output.TimedOut)
            return "Sunucu komutu zaman aşımına uğradı.";

        var detail = FirstLine(output.Stderr) ?? FirstLine(output.Stdout);
        if (detail is not null && detail.Contains("sudo", StringComparison.OrdinalIgnoreCase))
            return "Bu işlem için sudo yetkisi gerekli.";

        return detail is null ? "Sunucuda Dokku komutu başarısız oldu." : TextHelper.Truncate(detail, 300)!;
    }

    private static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }
}
