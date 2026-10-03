namespace ServerManager.Application.Authorization;

/// <summary>Eklentinin tanımladığı izinler. Eklenti kurulurken izinler rollere eklenir; mevcut atamalar kaldırılmaz.</summary>
public interface IPermissionProvider
{
    IReadOnlyList<PermissionDefinition> GetPermissions();

    /// <summary>Rol adı → izin listesi. SuperAdmin tanımlı her izni ayrıca otomatik alır.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> GetDefaultRolePermissions();
}
