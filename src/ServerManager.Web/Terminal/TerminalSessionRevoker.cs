using ServerManager.Application.Interfaces.Security;

namespace ServerManager.Web.Terminal;

/// <summary>Kullanıcının yetkisi düştüğünde açık SSH terminal oturumlarını kapatır.</summary>
public sealed class TerminalSessionRevoker : IUserSessionRevoker
{
    private readonly TerminalManager _manager;
    private readonly ILogger<TerminalSessionRevoker> _logger;

    public TerminalSessionRevoker(TerminalManager manager, ILogger<TerminalSessionRevoker> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public async Task RevokeAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            await _manager.CloseForUserAsync(userId, reason);
        }
        catch (Exception ex)
        {
            // Kullanıcı işlemi (pasifleştirme, çıkış) terminal kapatma hatası yüzünden başarısız sayılmamalı.
            _logger.LogError(ex, "Kullanıcının terminal oturumları kapatılamadı. UserId: {UserId}", userId);
        }
    }
}
