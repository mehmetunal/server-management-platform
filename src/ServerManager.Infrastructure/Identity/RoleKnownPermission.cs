namespace ServerManager.Infrastructure.Identity;

/// <summary>
/// Seeder'ın bu role daha önce önerdiği (varsayılan olarak eklediği) izin. Yönetici izni rolden kaldırırsa kayıt kalır;
/// böylece sonraki açılışta izin geri eklenmez. Yalnızca hiç önerilmemiş yeni izinler eklenir.
/// </summary>
public class RoleKnownPermission
{
    public Guid RoleId { get; set; }

    public string Permission { get; set; } = string.Empty;
}
