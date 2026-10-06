using ServerManager.Application.Common;
using ServerManager.Application.DTOs.ManagedServices;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>Kurulumu bekleyen otomatik yedek isteği (işlemi başlatan kullanıcı adına işlenir).</summary>
public sealed record PendingAutoBackupOperation(Guid OperationId, Guid ServiceId, string? UserId, string? UserName, string? IpAddress);

/// <summary>
/// Servis kurulumundan sonra oluşturulacak otomatik yedek işi için kalıcı kuyruk. İstek, şifrelenmiş olarak (parola dahil)
/// kurulum işleminin kaydına yazılır; kurulum başarıyla bitince iş oluşturulur, başarısız olursa istek düşürülür. Uygulama
/// yeniden başlarsa bekleyen istekler açılışta işlenir.
/// </summary>
public interface IServiceAutoBackupQueue
{
    Task<ServiceResult> EnqueueAsync(Guid operationId, ServiceBackupOptionsDto options, CancellationToken cancellationToken = default);

    /// <summary>
    /// İşlemin bekleyen isteğini işler. İşlem sürüyorsa veya istek yoksa null döner; aksi halde canlı çıktıya yazılacak
    /// satırı (<see cref="ManagedServices.ServiceConsole"/> biçiminde) döner.
    /// </summary>
    Task<string?> ProcessAsync(Guid operationId, CancellationToken cancellationToken = default);

    /// <summary>Bitmiş ama isteği işlenmemiş işlemler (uygulama işlem sırasında kapandıysa).</summary>
    Task<IReadOnlyList<PendingAutoBackupOperation>> ListPendingAsync(CancellationToken cancellationToken = default);
}
