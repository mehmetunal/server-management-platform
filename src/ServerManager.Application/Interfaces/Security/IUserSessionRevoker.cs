namespace ServerManager.Application.Interfaces.Security;

/// <summary>
/// Kullanıcının yetkisi düştüğünde (pasifleştirme, kilitleme, rol değişikliği, çıkış) çerez dışındaki uzun ömürlü
/// oturumlarını (ör. SSH terminalleri) kapatır. Uygulaması Web katmanındadır.
/// </summary>
public interface IUserSessionRevoker
{
    Task RevokeAsync(string userId, string reason, CancellationToken cancellationToken = default);
}
