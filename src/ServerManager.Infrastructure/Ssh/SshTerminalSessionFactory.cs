using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet.Common;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Ssh;

public sealed class SshTerminalSessionFactory : ITerminalSessionFactory
{
    private readonly SshOptions _options;
    private readonly ILogger<SshTerminalSessionFactory> _logger;

    public SshTerminalSessionFactory(IOptions<SshOptions> options, ILogger<SshTerminalSessionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<ITerminalSession>> OpenAsync(TerminalOpenRequest request, ITerminalOutputSink sink, CancellationToken cancellationToken = default)
    {
        var connection = request.Context.Connection;
        if (string.IsNullOrEmpty(connection.ExpectedHostKeyFingerprint))
            return ServiceResult<ITerminalSession>.Failure("Host key henüz doğrulanmadı. Önce bağlantı testi yapın.");

        SshClientLease lease;
        try
        {
            lease = SshClientLease.Create(connection, _options);
        }
        catch (Exception ex) when (ex is SshException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning("SSH kimlik bilgisi hazırlanamadı. Target: {Target}, Error: {ErrorType}", connection, ex.GetType().Name);
            return ServiceResult<ITerminalSession>.Failure("Kimlik bilgisi okunamadı.");
        }

        try
        {
            await lease.ConnectAsync(_options, cancellationToken);
            var stream = lease.Client.CreateShellStream("xterm-256color", (uint)request.Columns, (uint)request.Rows, 0, 0, 16384);

            var sudoPassword = SudoCommandBuilder.RequiresPasswordInput(request.Context, request.Elevate)
                ? request.Context.SudoPassword
                : null;
            var session = new SshTerminalSession(lease, stream, sink, sudoPassword, _logger);

            // Baştaki boşluk, HISTCONTROL=ignorespace olan shell'lerde satırın geçmişe yazılmasını engeller.
            // \033c ekranı temizler; böylece login banner'ı ve yazılan komut kullanıcıya görünmez.
            var command = SudoCommandBuilder.BuildInteractive(request.Context, request.Command, request.Elevate);
            session.Start($" printf '\\033c'; exec {command}");

            return ServiceResult<ITerminalSession>.Success(session);
        }
        catch (Exception ex) when (lease.HostKeyVerifier.Mismatch)
        {
            lease.Dispose();
            _logger.LogWarning("Host key fingerprint uyuşmuyor. Target: {Target}, Error: {ErrorType}", connection, ex.GetType().Name);
            return ServiceResult<ITerminalSession>.Failure(HostKeyVerifier.MismatchMessage);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                   && SshErrorTranslator.TryTranslate(ex, connection, _logger, out var message))
        {
            lease.Dispose();
            return ServiceResult<ITerminalSession>.Failure(message);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
