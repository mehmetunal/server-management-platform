using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Security;

namespace ServerManager.Infrastructure.Security;

public sealed class SshSecurityScanner : ISecurityScanner
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(3);

    private readonly IRemoteCommandRunner _runner;
    private readonly ILogger<SshSecurityScanner> _logger;

    public SshSecurityScanner(IRemoteCommandRunner runner, ILogger<SshSecurityScanner> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<ServiceResult<SecurityFacts>> CollectAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(context, async (executor, ct) =>
        {
            RemoteCommandOutput? output = null;
            if (context.UseSudo)
            {
                output = await executor.ExecuteAsync(new RemoteCommand(SecurityScanScript.Command, ScanTimeout, Elevate: true), ct);
                if (!SecurityFactsParser.IsComplete(output.Stdout))
                {
                    // sudo yalnızca belirli komutlara izin veriyorsa betik normal kullanıcıyla yeniden çalıştırılır.
                    _logger.LogInformation("Güvenlik taraması sudo ile çalışmadı, yetkisiz tekrar deneniyor. Target: {Target}", context);
                    output = null;
                }
            }

            output ??= await executor.ExecuteAsync(new RemoteCommand(SecurityScanScript.Command, ScanTimeout), ct);
            if (output.TimedOut)
                return ServiceResult<SecurityFacts>.Failure("Güvenlik taraması zaman aşımına uğradı.");
            if (!SecurityFactsParser.IsComplete(output.Stdout))
                return ServiceResult<SecurityFacts>.Failure("Tarama betiği tamamlanamadı. Sunucunun Linux ve sh kabuğu olduğundan emin olun.");

            try
            {
                return ServiceResult<SecurityFacts>.Success(SecurityFactsParser.Parse(output.Stdout));
            }
            catch (FormatException ex)
            {
                _logger.LogWarning("Güvenlik taraması çıktısı okunamadı. Target: {Target}, Error: {Error}", context, ex.Message);
                return ServiceResult<SecurityFacts>.Failure("Tarama çıktısı okunamadı.");
            }
        }, cancellationToken);
}
